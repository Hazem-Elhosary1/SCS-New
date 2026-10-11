using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Jobs;
using Scs.Identity.Infrastructure;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Notifications;
using Scs.SharedKernel.Wording;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, User Story 5 (FR-042 to FR-044; SC-011): a sign-in from a browser the account never
/// used tells its owner, in the bell and by email — the first real event, end to end.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class NewDeviceTests(SqlServerFixture sql)
{
    private const string Chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private const string Safari =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";

    private Task<RefusalWorld> WorldAsync() =>
        RefusalWorld.CreateAsync(sql, factory => factory.With("Scs:AUTH:REQUESTS_PER_MINUTE", "120"));

    private static HttpClient BrowserOf(RefusalWorld world, string agent)
    {
        var browser = world.Factory.Browser();
        browser.DefaultRequestHeaders.UserAgent.ParseAdd(agent);
        return browser;
    }

    /// <summary>Both steps; returns the cookies the last answer set.</summary>
    private static async Task<List<string>> SignInAsync(RefusalWorld world, HttpClient browser, string email)
    {
        using var first = await browser.StepOneAsync(email, SignIn.Password);
        Assert.True(first.IsSuccessStatusCode, $"Step one answered {(int)first.StatusCode}.");
        using var second = await browser.StepTwoAsync(world.Factory.Mail.LatestCodeFor(email));
        Assert.True(second.IsSuccessStatusCode, $"Step two answered {(int)second.StatusCode}.");
        return [.. second.Headers.GetValues("Set-Cookie")];
    }

    private Task<List<StoredNotification>> ToldAsync(RefusalWorld world, Guid account) =>
        sql.Main.NotificationsAsync(world.Clinic, account);

    [Fact]
    public async Task The_first_sign_in_an_account_ever_completes_marks_the_browser_and_tells_nobody()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world, Chrome);

        var cookies = await SignInAsync(world, browser, email);

        Assert.Empty(await ToldAsync(world, account));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].KnownDevice WHERE AccountId = '{account}'"));

        // The mark: kept for thirteen months, out of reach of scripts, sent over a safe connection only.
        var mark = Assert.Single(cookies, c => c.StartsWith("__Host-scs-device=", StringComparison.Ordinal));
        Assert.Contains("httponly", mark, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", mark, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", mark, StringComparison.OrdinalIgnoreCase);
        var expires = DateTimeOffset.Parse(
            mark.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))[8..],
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expires - world.Factory.Clock.GetUtcNow(), TimeSpan.FromDays(399), TimeSpan.FromDays(401));
    }

    [Fact]
    public async Task The_same_browser_again_tells_nobody()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world, Chrome);

        await SignInAsync(world, browser, email);
        using (await browser.SignOutAsync()) { }
        await SignInAsync(world, browser, email);
        await SignInAsync(world, browser, email);

        // Signing out does not take the mark away.
        Assert.Empty(await ToldAsync(world, account));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].KnownDevice WHERE AccountId = '{account}'"));
    }

    [Fact]
    public async Task A_browser_the_account_never_used_tells_its_owner_in_the_bell_and_by_email()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var usual = BrowserOf(world, Chrome);
        await SignInAsync(world, usual, email);
        var mailBefore = world.Factory.Mail.To(email).Count;

        using var fresh = BrowserOf(world, Safari);
        await SignInAsync(world, fresh, email);

        var told = Assert.Single(await ToldAsync(world, account));
        Assert.Equal((NotificationEvents.AccountNewDevice, "warning", "account", account), (told.EventKey, told.Mark, told.SubjectKind, told.SubjectId));
        Assert.Equal(Safari, told.FactsJson.GetProperty("device").GetString());
        Assert.Null(told.Link);
        Assert.Equal("waiting", told.EmailState);

        // Written with the session: one unit of work.
        Assert.Equal(2, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Session WHERE AccountId = '{account}'"));
        Assert.Equal(2, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].KnownDevice WHERE AccountId = '{account}'"));

        // The bell, in both languages.
        using var arabic = await usual.GetAsync(new Uri("/v1/notifications", UriKind.Relative));
        var item = Assert.Single((await arabic.ReadAsync()).Body.GetProperty("items").EnumerateArray());
        Assert.Equal(("الدخول من جهاز جديد", "متصفح Safari على iOS"), (item.GetProperty("title").GetString(), item.GetProperty("text").GetString()));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/v1/notifications", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Accept-Language", "en");
        using var english = await usual.SendAsync(request);
        var inEnglish = Assert.Single((await english.ReadAsync()).Body.GetProperty("items").EnumerateArray());
        Assert.Equal(("Sign-in from a new device", "Safari on iOS"), (inEnglish.GetProperty("title").GetString(), inEnglish.GetProperty("text").GetString()));

        // The email: what, from what device, and when the bell says — never a password or a code.
        await world.Factory.SendNotificationEmailsAsync(world.Clinic);
        var message = Assert.Single(world.Factory.Mail.To(email).Skip(mailBefore), m => m.Subject == "الدخول من جهاز جديد");
        Assert.Contains("متصفح Safari على iOS", message.TextBody, StringComparison.Ordinal);
        Assert.Contains(Words.Moment(told.RaisedAtUtc, english: false), message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(SignIn.Password, message.TextBody + message.HtmlBody, StringComparison.Ordinal);
        Assert.Equal("sent", Assert.Single(await ToldAsync(world, account)).EmailState);

        // The second browser is known from now on.
        using (await fresh.SignOutAsync()) { }
        await SignInAsync(world, fresh, email);
        Assert.Single(await ToldAsync(world, account));
    }

    [Fact]
    public async Task A_sign_in_that_fails_or_is_refused_marks_no_browser_and_tells_nobody()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var usual = BrowserOf(world, Chrome);
        await SignInAsync(world, usual, email);

        using var stranger = BrowserOf(world, Safari);
        using (var wrongPassword = await stranger.StepOneAsync(email, "Wrong-Password-1!"))
        {
            Assert.DoesNotContain(wrongPassword.Headers.TryGetValues("Set-Cookie", out var set) ? set : [], c => c.Contains("scs-device", StringComparison.Ordinal));
        }

        using (var rightPassword = await stranger.StepOneAsync(email, SignIn.Password))
        {
            // A correct password alone is not a sign-in.
            Assert.DoesNotContain(rightPassword.Headers.GetValues("Set-Cookie"), c => c.Contains("scs-device", StringComparison.Ordinal));
        }

        using (var wrongCode = await stranger.StepTwoAsync("000000" == world.Factory.Mail.LatestCodeFor(email) ? "000001" : "000000"))
        {
            Assert.Equal(401, (int)wrongCode.StatusCode);
        }

        Assert.Empty(await ToldAsync(world, account));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].KnownDevice WHERE AccountId = '{account}'"));
    }

    [Fact]
    public async Task An_account_with_a_long_history_and_no_known_browser_is_not_told_at_its_next_sign_in()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();

        // As every account is the day this ships: sessions and sign-ins behind it, and no mark.
        using (var before = BrowserOf(world, Chrome))
        {
            await SignInAsync(world, before, email);
        }

        await world.ExecuteAsync($"DELETE FROM [identity].KnownDevice WHERE AccountId = '{account}'");

        using var browser = BrowserOf(world, Chrome);
        await SignInAsync(world, browser, email);

        Assert.Empty(await ToldAsync(world, account));
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].KnownDevice WHERE AccountId = '{account}'"));
    }

    [Fact]
    public async Task Only_a_hash_of_the_mark_is_kept()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using var browser = BrowserOf(world, Chrome);

        var cookie = Assert.Single(await SignInAsync(world, browser, email), c => c.StartsWith("__Host-scs-device=", StringComparison.Ordinal));
        var value = cookie.Split(';')[0].Split('=', 2)[1];

        var stored = await TestDatabase.ReadColumnAsync(
            sql.Main.Crossing, $"SELECT CONVERT(varchar(64), MarkHash, 2) FROM [identity].KnownDevice WHERE AccountId = '{account}'");
        var hash = Assert.Single(stored);

        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(hash, value, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))), hash);
    }

    [Fact]
    public async Task Another_persons_mark_in_the_same_browser_does_not_make_it_known_to_this_account()
    {
        using var world = await WorldAsync();
        var (first, firstEmail) = await world.AccountAsync();
        var (second, secondEmail) = await world.AccountAsync();

        // A shared computer: one browser, two people.
        using var shared = BrowserOf(world, Chrome);
        using var own = BrowserOf(world, Safari);
        await SignInAsync(world, own, secondEmail);
        await SignInAsync(world, shared, firstEmail);
        using (await shared.SignOutAsync()) { }
        await SignInAsync(world, shared, secondEmail);

        Assert.Empty(await ToldAsync(world, first));
        Assert.Equal(NotificationEvents.AccountNewDevice, Assert.Single(await ToldAsync(world, second)).EventKey);
    }

    [Fact]
    public async Task A_browser_not_seen_for_thirteen_months_is_forgotten()
    {
        using var world = await WorldAsync();
        var (account, email) = await world.AccountAsync();
        using (var browser = BrowserOf(world, Chrome))
        {
            await SignInAsync(world, browser, email);
        }

        world.Factory.Clock.Advance(TimeSpan.FromDays(399));
        await RunCleanupAsync(world);
        Assert.Equal(1, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].KnownDevice WHERE AccountId = '{account}'"));

        world.Factory.Clock.Advance(TimeSpan.FromDays(2));
        await RunCleanupAsync(world);
        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].KnownDevice WHERE AccountId = '{account}'"));
    }

    private static async Task RunCleanupAsync(RefusalWorld world)
    {
        using var scope = world.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClinicJobRunner<CleanupJob>>().RunAsync(world.Clinic, CancellationToken.None);
    }
}
