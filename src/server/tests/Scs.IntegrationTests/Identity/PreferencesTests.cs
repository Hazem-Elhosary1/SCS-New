using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Identity;

/// <summary>
/// Spec 006 (FR-017 to FR-019, FR-024; أ-ق25): what a person keeps between sessions — the
/// language of the console, whether the side bar is folded, and a subscription banner closed
/// for today — is theirs, follows them to another browser, and is nobody else's.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PreferencesTests(SqlServerFixture sql)
{
    private static readonly Uri Preferences = new("/v1/account/preferences", UriKind.Relative);
    private static readonly Uri Session = new("/v1/auth/session", UriKind.Relative);

    private static Task<HttpResponseMessage> SaveAsync(HttpClient browser, object body) =>
        browser.PutAsJsonAsync(Preferences, body);

    private static async Task<JsonElement> KeptAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync(Session);
        var (status, _, body) = await response.ReadAsync();
        Assert.Equal(200, status);
        return body.GetProperty("preferences");
    }

    [Fact]
    public async Task A_person_who_never_chose_keeps_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var browser = await world.Factory.SignInAsAsync(world.Clinic);

        var kept = await KeptAsync(browser);

        Assert.Equal(JsonValueKind.Null, kept.GetProperty("language").ValueKind);
        Assert.Equal(JsonValueKind.Null, kept.GetProperty("sidebarFolded").ValueKind);
        Assert.Equal(JsonValueKind.Null, kept.GetProperty("dismissedBanner").ValueKind);
    }

    [Fact]
    public async Task Each_choice_is_saved_alone_and_changes_nothing_else()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountAsync();
        using var browser = await world.Factory.SignInAsAsync(email);

        using (var saved = await SaveAsync(browser, new { language = "en" }))
        {
            var (status, _, body) = await saved.ReadAsync();
            Assert.Equal(200, status);
            Assert.Equal("en", body.GetProperty("language").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("sidebarFolded").ValueKind);
        }

        using (var saved = await SaveAsync(browser, new { sidebarFolded = true }))
        {
            Assert.Equal(200, (int)saved.StatusCode);
        }

        using (var saved = await SaveAsync(browser, new { dismissedBanner = "expiring" }))
        {
            Assert.Equal(200, (int)saved.StatusCode);
        }

        var kept = await KeptAsync(browser);
        Assert.Equal("en", kept.GetProperty("language").GetString());
        Assert.True(kept.GetProperty("sidebarFolded").GetBoolean());
        Assert.Equal("expiring", kept.GetProperty("dismissedBanner").GetString());

        // The person unfolds the bar and goes back to Arabic; the banner stays closed.
        using (var saved = await SaveAsync(browser, new { language = "ar", sidebarFolded = false }))
        {
            Assert.Equal(200, (int)saved.StatusCode);
        }

        kept = await KeptAsync(browser);
        Assert.Equal("ar", kept.GetProperty("language").GetString());
        Assert.False(kept.GetProperty("sidebarFolded").GetBoolean());
        Assert.Equal("expiring", kept.GetProperty("dismissedBanner").GetString());
    }

    [Fact]
    public async Task What_is_kept_follows_the_person_to_another_browser()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountAsync();
        using (var first = await world.Factory.SignInAsAsync(email))
        using (var saved = await SaveAsync(first, new { language = "en", sidebarFolded = true }))
        {
            Assert.Equal(200, (int)saved.StatusCode);
        }

        using var second = await world.Factory.SignInAsAsync(email);
        var kept = await KeptAsync(second);

        Assert.Equal("en", kept.GetProperty("language").GetString());
        Assert.True(kept.GetProperty("sidebarFolded").GetBoolean());
    }

    [Theory]
    [InlineData("""{"language":"fr"}""", "language")]
    [InlineData("""{"language":3}""", "language")]
    [InlineData("""{"sidebarFolded":"yes"}""", "sidebarFolded")]
    [InlineData("""{"dismissedBanner":"readonly"}""", "dismissedBanner")]
    [InlineData("""{"dismissedBanner":"anything"}""", "dismissedBanner")]
    public async Task A_value_that_is_not_one_of_the_choices_is_refused_and_nothing_is_saved(string body, string field)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var browser = await world.Factory.SignInAsAsync(world.Clinic);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var refused = await browser.PutAsync(Preferences, content);
        var (status, code, problem) = await refused.ReadAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal("INVALID", problem.GetProperty("errors").GetProperty(field)[0].GetString());
        Assert.Equal(JsonValueKind.Null, (await KeptAsync(browser)).GetProperty(field).ValueKind);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"somethingElse":true}""")]
    public async Task A_request_that_changes_nothing_is_refused(string body)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var browser = await world.Factory.SignInAsAsync(world.Clinic);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var refused = await browser.PutAsync(Preferences, content);
        var (status, code, problem) = await refused.ReadAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.Equal("REQUIRED", problem.GetProperty("errors").GetProperty("body")[0].GetString());
    }

    [Fact]
    public async Task A_banner_closed_today_is_back_the_next_day_in_Cairo()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (_, email) = await world.AccountAsync();
        using (var today = await world.Factory.SignInAsAsync(email))
        {
            using var closed = await SaveAsync(today, new { dismissedBanner = "trial" });
            Assert.Equal(200, (int)closed.StatusCode);
            Assert.Equal("trial", (await KeptAsync(today)).GetProperty("dismissedBanner").GetString());
        }

        world.Factory.Clock.Advance(TimeSpan.FromHours(25));

        using var tomorrow = await world.Factory.SignInAsAsync(email);
        Assert.Equal(JsonValueKind.Null, (await KeptAsync(tomorrow)).GetProperty("dismissedBanner").ValueKind);
    }

    [Fact]
    public async Task One_persons_choices_are_never_anothers()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        using var first = await world.Factory.SignInAsAsync(world.Clinic);
        using var colleague = await world.Factory.SignInAsAsync(world.Clinic);
        using var stranger = await world.Factory.SignInAsAsync(other);

        using (var saved = await SaveAsync(first, new { language = "en", sidebarFolded = true, dismissedBanner = "grace" }))
        {
            Assert.Equal(200, (int)saved.StatusCode);
        }

        foreach (var browser in new[] { colleague, stranger })
        {
            var kept = await KeptAsync(browser);
            Assert.Equal(JsonValueKind.Null, kept.GetProperty("language").ValueKind);
            Assert.Equal(JsonValueKind.Null, kept.GetProperty("sidebarFolded").ValueKind);
            Assert.Equal(JsonValueKind.Null, kept.GetProperty("dismissedBanner").ValueKind);
        }

        Assert.Equal(1, await world.ScalarAsync<int>("SELECT COUNT(*) FROM [identity].StaffPreference"
                                                     + $" WHERE ClinicId = '{world.Clinic}'"));
    }

    [Fact]
    public async Task Nobody_saves_without_a_session_and_a_temporary_password_comes_first()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();
        using (var unauthenticated = await SaveAsync(nobody, new { language = "en" }))
        {
            Assert.Equal(401, (int)unauthenticated.StatusCode);
        }

        var email = $"temp-{Guid.NewGuid():N}@clinic.example";
        var created = await world.Factory.HandOverAsync(world.Clinic, h => h.CreateWithGeneratedPasswordAsync(email, default));
        using var held = await world.Factory.SignInAsAsync(email, created.GeneratedPassword!);
        using var refused = await SaveAsync(held, new { language = "en" });
        var (status, code, _) = await refused.ReadAsync();

        Assert.Equal((403, "PASSWORD_CHANGE_REQUIRED"), (status, code));
    }
}
