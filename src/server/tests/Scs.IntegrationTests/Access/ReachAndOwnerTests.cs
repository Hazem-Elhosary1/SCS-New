using System.Diagnostics;
using System.Net;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 005, User Story 7 (SH-055, FR-025, FR-026): narrow by default. A scope permission widens
/// a view from "one's own" to "everyone's", and the owner of a record reaches that record.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReachAndOwnerTests(SqlServerFixture sql)
{
    private static async Task<string?> ReachAsync(HttpClient browser, string view)
    {
        using var response = await browser.GetAsync(new Uri($"/probe/reach/{view}", UriKind.Relative));
        var (_, _, body) = await response.ReadAsync();
        return body.GetProperty("reach").GetString();
    }

    [Theory]
    [InlineData(Permissions.BookingsView, Permissions.BookingsViewAll)]
    [InlineData(Permissions.TicketsView, Permissions.TicketsViewAll)]
    public async Task A_view_shows_ones_own_and_its_scope_permission_shows_everyones(string view, string scope)
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var without = await world.Factory.SignInWithAsync(world.Clinic, Permissions.PatientsView);
        using var narrow = await world.Factory.SignInWithAsync(world.Clinic, view);
        using var wide = await world.Factory.SignInWithAsync(world.Clinic, view, scope);

        Assert.Equal("None", await ReachAsync(without.Browser, view));
        Assert.Equal("Own", await ReachAsync(narrow.Browser, view));
        Assert.Equal("All", await ReachAsync(wide.Browser, view));
    }

    [Fact]
    public async Task A_view_that_has_no_scope_permission_shows_everything_to_its_holder()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var holder = await world.Factory.SignInWithAsync(world.Clinic, Permissions.PatientsView);

        Assert.Equal("All", await ReachAsync(holder.Browser, Permissions.PatientsView));
        Assert.Equal("None", await ReachAsync(holder.Browser, Permissions.MoneyView));
    }

    [Fact]
    public async Task The_owner_of_a_record_reaches_it_without_the_permission_and_reaches_no_other()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInWithAsync(world.Clinic, Permissions.BookingsView);
        using var holder = await world.Factory.SignInWithAsync(world.Clinic, Permissions.PatientsView, Permissions.PatientsRecordsView);

        static Uri Record(object account) => new($"/probe/own-record/{account}", UriKind.Relative);

        using (var own = await owner.Browser.GetAsync(Record(owner.AccountId)))
        {
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        }

        using var someoneElses = await owner.Browser.GetAsync(Record(holder.AccountId));
        using var noSuchRecord = await owner.Browser.GetAsync(Record("missing"));
        var other = await someoneElses.ReadAsync();
        var missing = await noSuchRecord.ReadAsync();

        Assert.Equal((403, "PERMISSION_REQUIRED"), (other.Status, other.Code));
        Assert.Equal((403, "PERMISSION_REQUIRED"), (missing.Status, missing.Code));
        Assert.Equal(Permissions.PatientsRecordsView, other.Body.GetProperty("permission").GetString());
        Assert.Equal(Permissions.PatientsRecordsView, missing.Body.GetProperty("permission").GetString());

        // The holder of the permission reaches anyone's.
        using var any = await holder.Browser.GetAsync(Record(owner.AccountId));
        Assert.Equal(HttpStatusCode.OK, any.StatusCode);
    }
}

/// <summary>
/// Spec 005, SC-006: checking a permission adds no delay a person can notice. Compares a route
/// that needs a permission with one that needs none, on the same session and machine.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class GateTimingTests(SqlServerFixture sql, ITestOutputHelper output)
{
    private const int Calls = 200;

    [Fact]
    public async Task The_check_adds_under_five_milliseconds_at_the_median()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var person = await world.Factory.SignInWithAsync(world.Clinic, Fixtures.Access.AllCodesExcept(Permissions.MoneyRefund));

        var guarded = new Uri($"/probe/needs/{Permissions.BookingsView}", UriKind.Relative);
        var plain = new Uri("/probe/reach-free", UriKind.Relative);

        async Task<double> OnceAsync(Uri address)
        {
            var started = Stopwatch.GetTimestamp();
            using var response = await person.Browser.GetAsync(address);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        // Warm both paths, then take the two in turns so anything else the machine is doing
        // falls on both alike.
        for (var i = 0; i < 20; i++)
        {
            await OnceAsync(guarded);
            await OnceAsync(plain);
        }

        var withCheck = new List<double>(Calls);
        var without = new List<double>(Calls);
        for (var i = 0; i < Calls; i++)
        {
            withCheck.Add(await OnceAsync(guarded));
            without.Add(await OnceAsync(plain));
        }

        static double Median(List<double> values) => values.Order().ElementAt(values.Count / 2);
        var added = Median(withCheck) - Median(without);

        output.WriteLine(
            $"median with the check {Median(withCheck):F2} ms, without {Median(without):F2} ms, added {added:F2} ms over {Calls} calls each");
        Assert.True(added < 5, $"The permission check added {added:F2} ms at the median.");
    }
}
