using System.Reflection;
using Scs.SharedKernel.Access;

namespace Scs.UnitTests.Access;

/// <summary>
/// Spec 005, FR-001 to FR-006: the catalogue is exactly the table of BRD Chapter 10 §2-3, and
/// the build fails when it stops being consistent.
/// </summary>
public class PermissionCatalogueTests
{
    [Fact]
    public void There_are_83_permissions_numbered_without_gaps()
    {
        Assert.Equal(83, PermissionCatalogue.Count);
        Assert.Equal(Enumerable.Range(1, 83), PermissionCatalogue.All.Select(p => p.Number));
    }

    [Fact]
    public void Codes_are_unique_and_well_formed()
    {
        var codes = PermissionCatalogue.All.Select(p => p.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches(@"^[a-z]+(\.[a-zA-Z]+)+$", code));
        Assert.All(codes, code => Assert.True(code.Length <= 64, code));
    }

    [Fact]
    public void Every_permission_has_a_name_in_both_languages()
    {
        Assert.All(PermissionCatalogue.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.NameAr), p.Code);
            Assert.False(string.IsNullOrWhiteSpace(p.NameEn), p.Code);
        });
    }

    [Fact]
    public void There_are_eleven_groups_in_the_order_of_the_side_bar_and_home_holds_nothing()
    {
        Assert.Equal(
            ["home", "bookings", "patients", "money", "inbox", "tickets", "reports", "admin", "data", "settings", "subscription"],
            PermissionCatalogue.Groups.Select(g => g.Key));
        Assert.Equal(Enumerable.Range(1, 11), PermissionCatalogue.Groups.Select(g => g.Order));

        var counts = PermissionCatalogue.Groups.Select(g => PermissionCatalogue.All.Count(p => p.Group == g));
        Assert.Equal([0, 7, 20, 7, 4, 4, 7, 6, 8, 13, 7], counts);
    }

    [Fact]
    public void A_permission_code_begins_with_the_key_of_its_group()
    {
        // Two families sit under another group's heading: consent texts are data, lab cases
        // are patients — which is where their codes already begin.
        Assert.All(PermissionCatalogue.All, p => Assert.StartsWith(p.Group.Key + ".", p.Code, StringComparison.Ordinal));
    }

    [Fact]
    public void Kinds_are_counted_as_the_contract_says()
    {
        int Of(PermissionKind kind) => PermissionCatalogue.All.Count(p => p.Kind == kind);

        Assert.Equal(20, Of(PermissionKind.View));
        Assert.Equal(5, Of(PermissionKind.Read));
        Assert.Equal(56, Of(PermissionKind.Write));
        Assert.Equal(2, Of(PermissionKind.Scope));
    }

    [Fact]
    public void Every_requirement_exists_and_is_a_view_or_a_read()
    {
        var withRequirement = PermissionCatalogue.All.Where(p => p.Requires is not null).ToList();

        Assert.Equal(61, withRequirement.Count);
        Assert.All(withRequirement, p =>
        {
            var needed = PermissionCatalogue.Find(p.Requires);
            Assert.True(needed is not null, $"{p.Code} requires a code that does not exist");
            Assert.True(needed!.Kind is PermissionKind.View or PermissionKind.Read, $"{p.Code} requires a {needed.Kind}");
            Assert.NotEqual(p.Code, needed.Code);
        });
    }

    [Fact]
    public void A_view_requires_nothing_and_every_scope_requires_a_view()
    {
        Assert.All(
            PermissionCatalogue.All.Where(p => p.Kind == PermissionKind.View),
            p => Assert.Null(p.Requires));
        Assert.All(
            PermissionCatalogue.All.Where(p => p.Kind == PermissionKind.Scope),
            p => Assert.Equal(PermissionKind.View, PermissionCatalogue.Require(p.Requires!).Kind));
    }

    [Fact]
    public void Every_constant_is_in_the_table_and_every_row_has_its_constant()
    {
        var constants = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(
            PermissionCatalogue.All.Select(p => p.Code).Order(StringComparer.Ordinal),
            constants.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(1, "bookings.view", "عرض الحجوزات", "View bookings", "bookings", PermissionKind.View, null)]
    [InlineData(2, "bookings.viewAll", "عرض مواعيد كل الأطباء", "View all doctors' appointments", "bookings", PermissionKind.Scope, "bookings.view")]
    [InlineData(28, "money.collect", "تحصيل الدفعات", "Collect payments", "money", PermissionKind.Write, "money.view")]
    [InlineData(38, "tickets.viewAll", "عرض كل تذاكر العيادة", "View all clinic tickets", "tickets", PermissionKind.Scope, "tickets.view")]
    [InlineData(46, "reports.feedback.followup", "متابعة تقييمات المرضى", "Follow up on patient feedback", "reports", PermissionKind.Write, "reports.feedback")]
    [InlineData(47, "reports.export", "تصدير التقارير", "Export reports", "reports", PermissionKind.Write, null)]
    [InlineData(50, "admin.roles.view", "عرض الأدوار والصلاحيات", "View roles & permissions", "admin", PermissionKind.View, null)]
    [InlineData(51, "admin.roles.manage", "إدارة الأدوار والصلاحيات", "Manage roles & permissions", "admin", PermissionKind.Write, "admin.roles.view")]
    [InlineData(62, "settings.myHours", "عرض ساعاتي", "View my hours", "settings", PermissionKind.Read, null)]
    [InlineData(73, "subscription.view", "عرض الاشتراك", "View the subscription", "subscription", PermissionKind.View, null)]
    [InlineData(74, "subscription.invoices.view", "عرض فواتير الاشتراك", "View subscription invoices", "subscription", PermissionKind.Read, "subscription.view")]
    [InlineData(75, "subscription.pay", "دفع فواتير الاشتراك", "Pay subscription invoices", "subscription", PermissionKind.Write, "subscription.invoices.view")]
    [InlineData(76, "subscription.plan", "تغيير الباقة", "Change the plan", "subscription", PermissionKind.Write, "subscription.view")]
    [InlineData(80, "data.consents.view", "عرض نصوص الموافقة", "View consent texts", "data", PermissionKind.View, null)]
    [InlineData(81, "data.consents.manage", "إدارة نصوص الموافقة", "Manage consent texts", "data", PermissionKind.Write, "data.consents.view")]
    [InlineData(82, "patients.lab.view", "عرض حالات المعمل", "View lab cases", "patients", PermissionKind.View, null)]
    [InlineData(83, "patients.lab.manage", "إدارة حالات المعمل", "Manage lab cases", "patients", PermissionKind.Write, "patients.lab.view")]
    public void Rows_are_as_the_BRD_gives_them(
        int number, string code, string nameAr, string nameEn, string group, PermissionKind kind, string? requires)
    {
        var permission = PermissionCatalogue.All[number - 1];

        Assert.Equal(new Permission(number, code, nameAr, nameEn, permission.Group, kind, requires), permission);
        Assert.Equal(group, permission.Group.Key);
    }

    [Fact]
    public void The_two_scope_permissions_widen_bookings_and_tickets_and_nothing_else()
    {
        Assert.Equal(Permissions.BookingsViewAll, PermissionCatalogue.ScopeOf(Permissions.BookingsView)?.Code);
        Assert.Equal(Permissions.TicketsViewAll, PermissionCatalogue.ScopeOf(Permissions.TicketsView)?.Code);
        Assert.Null(PermissionCatalogue.ScopeOf(Permissions.PatientsView));
    }

    [Fact]
    public void A_code_that_is_not_in_the_catalogue_is_not_found_and_is_refused_by_name()
    {
        Assert.Null(PermissionCatalogue.Find("no.such.code"));
        Assert.Null(PermissionCatalogue.Find(null));
        Assert.False(PermissionCatalogue.Contains("Bookings.View"));

        var error = Assert.Throws<ArgumentException>(() => PermissionCatalogue.Require("no.such.code"));
        Assert.Contains("no.such.code", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Codes_are_put_in_catalogue_order_and_strangers_are_left_out()
    {
        Assert.Equal(
            [Permissions.BookingsView, Permissions.MoneyView, Permissions.AdminRolesView],
            PermissionCatalogue.InOrder([Permissions.AdminRolesView, "no.such.code", Permissions.BookingsView, Permissions.MoneyView]));
    }
}
