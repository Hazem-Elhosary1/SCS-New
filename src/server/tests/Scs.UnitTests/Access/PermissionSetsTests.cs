using Scs.SharedKernel.Access;

namespace Scs.UnitTests.Access;

/// <summary>
/// Spec 005, User Story 4 (SH-054, ق-31, FR-016 to FR-018): a permission cannot be held without
/// the one it requires, and a set that breaks this cannot be stored.
/// </summary>
public class PermissionSetsTests
{
    public static TheoryData<string, string> Requirements()
    {
        var data = new TheoryData<string, string>();
        foreach (var permission in PermissionCatalogue.All.Where(p => p.Requires is not null))
        {
            data.Add(permission.Code, permission.Requires!);
        }

        return data;
    }

    /// <summary>The permission with everything it needs, directly and further up.</summary>
    private static List<string> ChainOf(string code)
    {
        var chain = new List<string>();
        for (var next = code; next is not null; next = PermissionCatalogue.Require(next).Requires)
        {
            chain.Add(next);
        }

        return chain;
    }

    [Theory]
    [MemberData(nameof(Requirements))]
    public void A_permission_with_what_it_requires_is_accepted(string permission, string requires)
    {
        var chain = ChainOf(permission);

        Assert.Contains(requires, chain);
        Assert.True(PermissionSets.Check(chain).None);
    }

    [Theory]
    [MemberData(nameof(Requirements))]
    public void A_permission_without_what_it_requires_is_refused_naming_both(string permission, string requires)
    {
        var problems = PermissionSets.Check(ChainOf(permission).Where(code => code != requires));

        Assert.Equal([PermissionSets.MissingRequirement], problems.Codes);
        Assert.Equal([new MissingRequirement(permission, requires)], problems.Missing);
    }

    [Fact]
    public void Only_the_direct_requirement_is_demanded()
    {
        // Paying needs the invoices; the invoices need the subscription. With the first two
        // only, what is missing is named once, where it is missing.
        var problems = PermissionSets.Check([Permissions.SubscriptionPay, Permissions.SubscriptionInvoicesView]);

        Assert.Equal(
            [new MissingRequirement(Permissions.SubscriptionInvoicesView, Permissions.SubscriptionView)],
            problems.Missing);
    }

    [Fact]
    public void Paying_subscription_invoices_needs_all_three()
    {
        Assert.True(PermissionSets.Check(
            [Permissions.SubscriptionPay, Permissions.SubscriptionInvoicesView, Permissions.SubscriptionView]).None);
    }

    [Theory]
    [InlineData(Permissions.ReportsExport)]
    [InlineData(Permissions.SettingsMyHours)]
    [InlineData(Permissions.BookingsView)]
    public void A_permission_that_requires_nothing_stands_alone(string code)
    {
        Assert.True(PermissionSets.Check([code]).None);
    }

    [Fact]
    public void An_empty_set_is_refused()
    {
        Assert.Equal([PermissionSets.Required], PermissionSets.Check([]).Codes);
        Assert.Equal([PermissionSets.Required], PermissionSets.Check(null).Codes);
    }

    [Fact]
    public void A_code_that_is_not_in_the_catalogue_is_refused_by_name()
    {
        var problems = PermissionSets.Check(
            [Permissions.BookingsView, "clinic.own.permission", "another.stranger", "clinic.own.permission"]);

        Assert.Equal([PermissionSets.Unknown, PermissionSets.Duplicate], problems.Codes);
        Assert.Equal(["clinic.own.permission", "another.stranger"], problems.Unknown);
    }

    [Fact]
    public void A_code_given_twice_is_refused_by_name_once()
    {
        var problems = PermissionSets.Check(
            [Permissions.MoneyView, Permissions.BookingsView, Permissions.BookingsView, Permissions.BookingsView, Permissions.MoneyView]);

        Assert.Equal([PermissionSets.Duplicate], problems.Codes);
        Assert.Equal([Permissions.BookingsView, Permissions.MoneyView], problems.Repeated);
        Assert.Empty(problems.Unknown);
    }

    [Fact]
    public void A_set_that_may_be_stored_names_nothing()
    {
        var problems = PermissionSets.Check([Permissions.MoneyView, Permissions.MoneyCollect]);

        Assert.True(problems.None);
        Assert.Empty(problems.Unknown);
        Assert.Empty(problems.Repeated);
        Assert.Empty(problems.Missing);
    }

    [Fact]
    public void A_scope_permission_needs_its_view_like_any_other()
    {
        Assert.Equal(
            [new MissingRequirement(Permissions.BookingsViewAll, Permissions.BookingsView)],
            PermissionSets.Check([Permissions.BookingsViewAll]).Missing);
    }

    [Fact]
    public void The_whole_catalogue_is_a_valid_set()
    {
        Assert.True(PermissionSets.Check(PermissionCatalogue.All.Select(p => p.Code)).None);
    }

    [Fact]
    public void The_manager_holds_59_and_the_doctor_40_and_both_are_valid_sets()
    {
        Assert.Equal(59, ReadySets.Manager.Count);
        Assert.Equal(40, ReadySets.Doctor.Count);
        Assert.True(PermissionSets.Check(ReadySets.Manager).None);
        Assert.True(PermissionSets.Check(ReadySets.Doctor).None);
    }

    [Fact]
    public void The_ready_sets_hold_what_the_roles_page_needs_to_be_seen_in_each_state()
    {
        // The manager manages roles; the doctor sees them and may not change them.
        Assert.Contains(Permissions.AdminRolesManage, ReadySets.Manager);
        Assert.Contains(Permissions.AdminRolesView, ReadySets.Doctor);
        Assert.DoesNotContain(Permissions.AdminRolesManage, ReadySets.Doctor);
    }
}
