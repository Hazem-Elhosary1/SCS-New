using Scs.SharedKernel.Access;

namespace Scs.UnitTests.Operators;

/// <summary>
/// Spec 011, FR-001 and FR-002: the operators' catalogue is the 28 permissions of the role
/// editor in its nine groups, closed, each with what it needs (data-model.md §1).
/// </summary>
public class OperatorPermissionsTests
{
    private static readonly string[] SecurityAlone = [OperatorPermissions.SecurityManage];
    private static readonly string[] ViewAllTwice = [OperatorPermissions.TicketsViewAll, OperatorPermissions.TicketsViewAll];
    private static readonly string[] NoticesAndPlans = [OperatorPermissions.NoticesSend, OperatorPermissions.BillingManagePlans];
    private static readonly string[] Unknown = ["operators.delete"];

    [Fact]
    public void The_catalogue_is_twenty_eight_permissions_in_nine_groups()
    {
        Assert.Equal(28, OperatorPermissions.All.Count);
        Assert.Equal(28, OperatorPermissions.All.Select(p => p.Code).Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(
            [
                ("dashboard", 1), ("clinics", 7), ("tickets", 6), ("notices", 1), ("billing", 2),
                ("operators", 3), ("reports", 5), ("security", 1), ("settings", 2),
            ],
            OperatorPermissions.Groups.All.Select(group => (group, OperatorPermissions.All.Count(p => p.Group == group))));

        // The groups are drawn in order, each in one run.
        Assert.Equal(
            OperatorPermissions.Groups.All,
            OperatorPermissions.All.Select(p => p.Group).Distinct());
    }

    [Fact]
    public void Every_permission_has_both_names_and_a_code_of_its_group()
    {
        Assert.All(OperatorPermissions.All, permission =>
        {
            Assert.Matches(@"\p{IsArabic}", permission.NameAr);
            Assert.DoesNotMatch(@"\p{IsArabic}", permission.NameEn);
            Assert.StartsWith(permission.Group + ".", permission.Code, StringComparison.Ordinal);
            Assert.True(OperatorPermissions.IsKnown(permission.Code));
        });

        Assert.Equal(
            ("إرسال إشعارات للعيادات", "Send Notices to Clinics", "notices"),
            (OperatorPermissions.Require("notices.send").NameAr, OperatorPermissions.Require("notices.send").NameEn,
             OperatorPermissions.Require("notices.send").Group));
    }

    [Fact]
    public void What_a_permission_needs_is_in_the_catalogue_and_needs_nothing_itself()
    {
        var needing = OperatorPermissions.All.Where(p => p.Needs is not null).ToList();

        Assert.Equal(16, needing.Count);
        Assert.All(needing, permission =>
        {
            var needed = OperatorPermissions.Require(permission.Needs!);
            Assert.Null(needed.Needs);
        });

        Assert.All(
            OperatorPermissions.All.Where(p => p.Group == "clinics" && p.Code != "clinics.view"),
            p => Assert.Equal("clinics.view", p.Needs));
        Assert.All(
            OperatorPermissions.All.Where(p => p.Group == "tickets" && p.Code != "tickets.view"),
            p => Assert.Equal("tickets.view", p.Needs));
        Assert.Equal("clinics.view", OperatorPermissions.Require("notices.send").Needs);
        Assert.Equal("billing.view", OperatorPermissions.Require("billing.managePlans").Needs);
        Assert.Equal("operators.view", OperatorPermissions.Require("operators.manage").Needs);
        Assert.Equal("operators.view", OperatorPermissions.Require("operators.manageRoles").Needs);
        Assert.Equal("settings.view", OperatorPermissions.Require("settings.edit").Needs);

        // The BRD ties this one to the security report on some routes only, not in the role.
        Assert.Null(OperatorPermissions.Require("security.manage").Needs);
        Assert.All(OperatorPermissions.All.Where(p => p.Group == "reports"), p => Assert.Null(p.Needs));
    }

    [Fact]
    public void A_role_is_saved_with_what_its_permissions_need_and_nothing_else()
    {
        Assert.Equal(["tickets.view", "tickets.viewAll"], OperatorPermissions.WithRequired(ViewAllTwice));
        Assert.Equal(
            ["clinics.view", "notices.send", "billing.view", "billing.managePlans"],
            OperatorPermissions.WithRequired(NoticesAndPlans));
        Assert.Equal(["security.manage"], OperatorPermissions.WithRequired(SecurityAlone));
        Assert.Empty(OperatorPermissions.WithRequired([]));

        Assert.Throws<ArgumentException>(() => OperatorPermissions.WithRequired(Unknown));
        Assert.False(OperatorPermissions.IsKnown("bookings.view"));
        Assert.False(OperatorPermissions.IsKnown(null));

        Assert.Equal(
            ["tickets.viewAll", "tickets.reply", "tickets.close", "tickets.assign", "tickets.create"],
            OperatorPermissions.NeededBy("tickets.view"));
    }

    [Fact]
    public void The_three_system_roles_hold_everything_eleven_and_every_view()
    {
        var roles = OperatorPermissions.SystemRoles.Keys;
        Assert.Equal(["super-admin", "billing-admin", "viewer"], roles);
        Assert.Equal(3, roles.Select(OperatorPermissions.SystemRoles.IdOf).Distinct().Count());

        Assert.Equal(28, OperatorPermissions.SystemRoles.CodesOf("super-admin").Count);

        var billing = OperatorPermissions.SystemRoles.CodesOf("billing-admin");
        var viewer = OperatorPermissions.SystemRoles.CodesOf("viewer");
        Assert.Equal((11, 11), (billing.Count, viewer.Count));

        // Each is whole: it already holds what its permissions need.
        Assert.All(roles, key =>
        {
            var codes = OperatorPermissions.SystemRoles.CodesOf(key);
            Assert.Equal(OperatorPermissions.WithRequired(codes), OperatorPermissions.InOrder(codes));
        });

        // A viewer reads everything and changes nothing.
        Assert.Equal(
            [
                "dashboard.view", "clinics.view", "tickets.view", "tickets.viewAll", "billing.view", "operators.view",
                "reports.financial", "reports.security", "reports.operators", "reports.activity", "settings.view",
            ],
            viewer);
        Assert.DoesNotContain(viewer, code => code is "operators.manage" or "operators.manageRoles" or "settings.edit" or "reports.export");

        Assert.Equal(("مسؤول عام", "Super Admin"), OperatorPermissions.SystemRoles.NameOf("super-admin"));
        Assert.Equal(("مسؤول الفوترة", "Billing Manager"), OperatorPermissions.SystemRoles.NameOf("billing-admin"));
        Assert.Equal(("مشاهد فقط", "Viewer Only"), OperatorPermissions.SystemRoles.NameOf("viewer"));
    }
}
