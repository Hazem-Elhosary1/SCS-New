using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Plans;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, User Story 3 (FR-011 to FR-017; research R8): the plan's «المستخدمون», refused at the
/// limit wherever an account comes to take a seat — created three ways, re-enabled, invited again,
/// handed over again — and by whoever asks: these tests call the operations with the clinic
/// established and no one signed in, as clinic creation and the operators' actions (015, 017)
/// will. A lapsed invitation takes no seat; a lowered limit takes nothing away. Test-first.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SeatTests(SqlServerFixture sql)
{
    private sealed record World(ApiFactory Factory, Guid Clinic, Guid Plan) : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private async Task<World> WorldAsync(int? users, int filled)
    {
        await sql.EnsureMigratedAsync();
        var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var plan = await factory.GivePlanAsync(clinic, (FeatureKeys.MaxUsers, users));
        for (var i = 0; i < filled; i++)
        {
            await factory.CreateAccountAsync(clinic);
        }

        return new World(factory, clinic, plan);
    }

    private static string NewEmail() => $"seat-{Guid.NewGuid():N}@clinic.example";

    private static async Task RefusedAtAsync(int limit, Func<Task> work)
    {
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(work);
        Assert.Equal("PLAN_LIMIT_REACHED", refusal.Code);
        Assert.Equal(409, refusal.Status);
        Assert.Equal("max_users", refusal.Extensions!["feature"]);
        Assert.Equal(limit, refusal.Extensions!["limit"]);
    }

    private Task<int> AccountsWithAsync(string email) =>
        TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM [identity].[StaffAccount] WHERE Email = '{email}'");

    private Task LapseInvitationAsync(Guid accountId) =>
        sql.ExecuteAsync(
            sql.Main.Crossing,
            $"""
             UPDATE [identity].[PasswordLink] SET ExpiresAtUtc = DATEADD(HOUR, -1, SYSDATETIMEOFFSET()) WHERE AccountId = '{accountId}';
             UPDATE [identity].[StaffAccount] SET CreatedAtUtc = DATEADD(DAY, -10, SYSDATETIMEOFFSET()) WHERE Id = '{accountId}';
             """);

    private static Task<T> AsClinicAsync<T>(World world, Func<IServiceProvider, Task<T>> work) =>
        world.Factory.ForClinicAsync(world.Clinic, work);

    private static Task AsClinicAsync(World world, Func<IServiceProvider, Task> work) =>
        world.Factory.ForClinicAsync(world.Clinic, work);

    [Fact]
    public async Task At_the_limit_each_of_the_three_ways_of_creating_an_account_is_refused_and_nothing_is_sent()
    {
        using var world = await WorldAsync(users: 3, filled: 3);
        var (link, generated, typed) = (NewEmail(), NewEmail(), NewEmail());

        await RefusedAtAsync(3, () => AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().CreateWithLinkAsync(link, CancellationToken.None)));
        await RefusedAtAsync(3, () => AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().CreateWithGeneratedPasswordAsync(generated, CancellationToken.None)));
        await RefusedAtAsync(3, () => AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().CreateWithTypedPasswordAsync(typed, SignIn.Password, CancellationToken.None)));

        foreach (var email in new[] { link, generated, typed })
        {
            Assert.Equal(0, await AccountsWithAsync(email));
            Assert.Empty(world.Factory.Mail.To(email));
        }
    }

    [Fact]
    public async Task Disabling_frees_a_seat_and_re_enabling_at_the_limit_is_refused()
    {
        using var world = await WorldAsync(users: 2, filled: 1);
        var (second, _) = await world.Factory.CreateAccountAsync(world.Clinic);
        await RefusedAtAsync(2, () => world.Factory.CreateAccountAsync(world.Clinic));

        await AsClinicAsync(world, s => s.GetRequiredService<IStaffAccounts>().SetStateAsync(second, StaffAccountStates.Disabled, CancellationToken.None));
        var (third, _) = await world.Factory.CreateAccountAsync(world.Clinic);

        await RefusedAtAsync(2, () => AsClinicAsync(world, s => s.GetRequiredService<IStaffAccounts>().SetStateAsync(second, StaffAccountStates.Active, CancellationToken.None)));
        Assert.Equal("disabled", await TestDatabase.ScalarAsync<string>(sql.Main.Crossing, $"SELECT State FROM [identity].[StaffAccount] WHERE Id = '{second}'"));
        Assert.NotEqual(Guid.Empty, third);
    }

    [Fact]
    public async Task A_live_invitation_takes_a_seat_and_a_lapsed_one_does_not_until_it_is_sent_again()
    {
        using var world = await WorldAsync(users: 2, filled: 1);
        var invited = await AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().CreateWithLinkAsync(NewEmail(), CancellationToken.None));
        await RefusedAtAsync(2, () => world.Factory.CreateAccountAsync(world.Clinic));

        await LapseInvitationAsync(invited.AccountId);
        await world.Factory.CreateAccountAsync(world.Clinic);

        await RefusedAtAsync(2, () => AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().ResendInvitationAsync(invited.AccountId, CancellationToken.None)));
        await RefusedAtAsync(2, () => AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().ReissueAsync(invited.AccountId, "link", null, CancellationToken.None)));
        await RefusedAtAsync(2, () => AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().ReissueAsync(invited.AccountId, "typed", SignIn.Password, CancellationToken.None)));
        Assert.Equal("invited", await TestDatabase.ScalarAsync<string>(sql.Main.Crossing, $"SELECT State FROM [identity].[StaffAccount] WHERE Id = '{invited.AccountId}'"));
    }

    [Fact]
    public async Task An_invitation_written_a_moment_ago_with_no_link_yet_already_takes_its_seat()
    {
        using var world = await WorldAsync(users: 1, filled: 0);

        await world.Factory.CreateAccountAsync(world.Clinic, StaffAccountStates.Invited);

        await RefusedAtAsync(1, () => world.Factory.CreateAccountAsync(world.Clinic));
    }

    [Fact]
    public async Task An_invitation_activated_by_its_link_at_the_limit_is_not_refused()
    {
        using var world = await WorldAsync(users: 2, filled: 1);
        var email = NewEmail();
        var invited = await AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().CreateWithLinkAsync(email, CancellationToken.None));
        await RefusedAtAsync(2, () => world.Factory.CreateAccountAsync(world.Clinic));

        // The invitation already holds its seat: activating it takes nothing more (FR-017).
        using var browser = world.Factory.Browser();
        using var set = await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), Passwords.New);

        Assert.True(set.IsSuccessStatusCode, $"Activation answered {(int)set.StatusCode}.");
        Assert.Equal("active", await TestDatabase.ScalarAsync<string>(sql.Main.Crossing, $"SELECT State FROM [identity].[StaffAccount] WHERE Id = '{invited.AccountId}'"));
        await RefusedAtAsync(2, () => world.Factory.CreateAccountAsync(world.Clinic));
    }

    [Fact]
    public async Task What_does_not_add_a_seat_is_never_refused_at_the_limit()
    {
        using var world = await WorldAsync(users: 1, filled: 0);
        var (account, _) = await world.Factory.CreateAccountAsync(world.Clinic);

        // An account that holds its seat: handing it over again, setting it active again.
        await AsClinicAsync(world, s => s.GetRequiredService<IAccountHandover>().ReissueAsync(account, "generated", null, CancellationToken.None));
        await AsClinicAsync(world, s => s.GetRequiredService<IStaffAccounts>().SetStateAsync(account, StaffAccountStates.Active, CancellationToken.None));
        var role = await world.Factory.CreateRoleAsync(world.Clinic, Scs.SharedKernel.Access.Permissions.PatientsView);
        await world.Factory.RolesAsync(world.Clinic, roles => roles.AssignAsync(account, role, CancellationToken.None));

        // A disabled account takes no seat at all.
        await world.Factory.CreateAccountAsync(world.Clinic, StaffAccountStates.Disabled);
    }

    [Fact]
    public async Task A_lowered_limit_takes_nothing_away_and_refuses_the_next()
    {
        using var world = await WorldAsync(users: 5, filled: 5);

        await sql.ExecuteAsync(sql.AdminConnection, $"UPDATE [plans].[PlanFeature] SET [Limit] = 3 WHERE PlanId = '{world.Plan}' AND FeatureKey = 'max_users'");

        Assert.Equal(5, await TestDatabase.ScalarAsync<int>(
            sql.Main.Crossing, $"SELECT COUNT(*) FROM [identity].[StaffAccount] WHERE ClinicId = '{world.Clinic}' AND State = 'active'"));
        await RefusedAtAsync(3, () => world.Factory.CreateAccountAsync(world.Clinic));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task No_limit_refuses_nothing(bool withPlan)
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        if (withPlan)
        {
            await factory.GivePlanAsync(clinic, (FeatureKeys.MaxUsers, null));
        }

        for (var i = 0; i < 4; i++)
        {
            await factory.CreateAccountAsync(clinic);
        }
    }

    [Fact]
    public async Task The_refusal_names_the_limit_in_the_requests_language()
    {
        using var world = await WorldAsync(users: 1, filled: 1);
        using var person = await Task.FromResult(world.Factory.Browser());

        // The development route of spec 004 re-sends an invitation; here a lapsed one, at the limit.
        var email = NewEmail();
        await sql.ExecuteAsync(sql.AdminConnection, $"UPDATE [plans].[PlanFeature] SET [Limit] = 2 WHERE PlanId = '{world.Plan}'");
        await world.Factory.CreateAccountAsync(world.Clinic, StaffAccountStates.Invited, email);
        var invitedId = await TestDatabase.ScalarAsync<Guid>(sql.Main.Crossing, $"SELECT Id FROM [identity].[StaffAccount] WHERE Email = '{email}'");
        await LapseInvitationAsync(invitedId);
        await world.Factory.CreateAccountAsync(world.Clinic);

        foreach (var (language, words) in new[]
                 {
                     ("ar", "بلغت العيادة حدّ باقتها من «المستخدمون»: 2. تُرفع الباقة من «الملف والاشتراك»."),
                     ("en", "The clinic has reached its plan's limit of Users: 2. The plan is raised in Profile & subscription."),
                 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/dev/staff-invitations")
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { email }),
            };
            request.Headers.AcceptLanguage.ParseAdd(language);
            using var response = await person.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();

            Assert.Equal(409, (int)response.StatusCode);
            using var json = System.Text.Json.JsonDocument.Parse(text);
            Assert.Equal(words, json.RootElement.GetProperty("title").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("limit").GetInt32());
        }
    }
}
