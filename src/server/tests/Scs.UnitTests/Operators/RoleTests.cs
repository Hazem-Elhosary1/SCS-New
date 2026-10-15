using Scs.Operators.Domain;
using Scs.SharedKernel.Access;

namespace Scs.UnitTests.Operators;

/// <summary>
/// Spec 011, FR-012 and the account's new rules (data-model.md §2 to §4): a custom role's name,
/// description and permissions, and what may be done to an operator's account in each state.
/// </summary>
public class RoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Maker = Guid.NewGuid();
    private static readonly Guid SuperAdmin = OperatorPermissions.SystemRoles.IdOf(OperatorPermissions.SystemRoles.SuperAdmin);
    private static readonly string[] Reply = ["tickets.reply"];
    private static readonly string[] Unknown = ["tickets.delete"];

    [Fact]
    public void A_custom_role_keeps_its_name_trimmed_and_holds_what_its_permissions_need()
    {
        var role = Role.Create("  Support Agent ", "  Answers tickets. ", Reply, Maker, Now);

        Assert.Equal(("Support Agent", "support agent", "Answers tickets."), (role.Name, role.NameKey, role.Description));
        Assert.Equal(["tickets.view", "tickets.reply"], role.Codes);
        Assert.False(role.IsSystem);
        Assert.False(role.IsSuperAdmin);
        Assert.Equal(Maker, role.CreatedById);

        role.Hold(["clinics.edit"]);
        Assert.Equal(["clinics.view", "clinics.edit"], role.Codes);

        role.Describe("وكيل دعم فني", " ");
        Assert.Equal(("وكيل دعم فني", "وكيل دعم فني"), (role.Name, role.NameKey));
        Assert.Null(role.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_role_without_a_name_is_refused(string name) =>
        Assert.Throws<ArgumentException>(() => Role.Create(name, null, Reply, Maker, Now));

    [Fact]
    public void A_role_s_limits_are_a_hundred_five_hundred_one_permission_and_the_catalogue()
    {
        Assert.Equal(100, Role.Create(new string('n', 100), new string('d', 500), Reply, Maker, Now).Name.Length);
        Assert.Throws<ArgumentException>(() => Role.Create(new string('n', 101), null, Reply, Maker, Now));
        Assert.Throws<ArgumentException>(() => Role.Create("Role", new string('d', 501), Reply, Maker, Now));
        Assert.Throws<ArgumentException>(() => Role.Create("Role", null, [], Maker, Now));
        Assert.Throws<ArgumentException>(() => Role.Create("Role", null, Unknown, Maker, Now));
    }

    [Fact]
    public void A_deleted_role_keeps_its_name_and_frees_it_for_another()
    {
        var role = Role.Create("Customer Success", null, Reply, Maker, Now);

        role.Delete(Now.AddDays(1));

        Assert.True(role.IsDeleted);
        Assert.Equal("Customer Success", role.Name);
        Assert.NotEqual(Role.KeyOf("Customer Success"), role.NameKey);
        Assert.Equal(Now.AddDays(1), role.DeletedAtUtc);

        // Deleting twice changes nothing more.
        role.Delete(Now.AddDays(5));
        Assert.Equal(Now.AddDays(1), role.DeletedAtUtc);
    }

    [Fact]
    public void The_departments_are_the_five_of_the_brd() =>
        Assert.Equal(["support", "billing", "success", "sales", "management"], OperatorDepartments.All);

    [Fact]
    public void An_operator_s_details_keep_their_limits_and_never_touch_the_email()
    {
        var account = Operator.Invite("Sara Ahmed", "sara@heta.example", SuperAdmin, Now);

        account.Describe("  Sara A. Ahmed ", " +20 100 111 2233 ", "support", "  Support Specialist ", "en");
        Assert.Equal(
            ("Sara A. Ahmed", "+20 100 111 2233", "support", "Support Specialist", "en", "sara@heta.example"),
            (account.FullName, account.Mobile, account.Department, account.JobTitle, account.Language, account.Email));

        account.Describe("Sara", null, null, " ", "ar");
        Assert.Equal((null, null, null), (account.Mobile, account.Department, account.JobTitle));

        Assert.Throws<ArgumentException>(() => account.Describe("", null, null, null, "ar"));
        Assert.Throws<ArgumentException>(() => account.Describe(new string('n', 201), null, null, null, "ar"));
        Assert.Throws<ArgumentException>(() => account.Describe("Sara", new string('1', 31), null, null, "ar"));
        Assert.Throws<ArgumentException>(() => account.Describe("Sara", null, "marketing", null, "ar"));
        Assert.Throws<ArgumentException>(() => account.Describe("Sara", null, null, new string('t', 151), "ar"));
        Assert.Throws<ArgumentException>(() => account.Describe("Sara", null, null, null, "fr"));
        Assert.Equal("Sara", account.FullName);
    }

    [Fact]
    public void An_operator_holds_one_role_from_the_start_and_knows_when_it_changes()
    {
        Assert.Throws<ArgumentException>(() => Operator.Invite("Sara", "sara@heta.example", Guid.Empty, Now));

        var account = Operator.Invite("Sara", "sara@heta.example", SuperAdmin, Now);
        var other = Guid.NewGuid();

        Assert.Equal((SuperAdmin, true), (account.RoleId, account.TwoStepRequired));
        Assert.False(account.ChangeRole(SuperAdmin));
        Assert.True(account.ChangeRole(other));
        Assert.Equal(other, account.RoleId);
        Assert.Throws<ArgumentException>(() => account.ChangeRole(Guid.Empty));

        account.InvitedBy(Maker, " Ahmed Sami ");
        Assert.Equal((Maker, "Ahmed Sami"), (account.InvitedById, account.InvitedByName));
    }

    [Fact]
    public void Only_an_active_account_is_suspended_and_only_a_suspended_one_reactivated()
    {
        var account = Operator.Invite("Sara", "sara@heta.example", SuperAdmin, Now);

        // Invited: neither.
        Assert.Throws<InvalidOperationException>(() => account.Suspend(Now));
        Assert.Throws<InvalidOperationException>(account.Reactivate);
        Assert.Null(account.ActivatedAtUtc);

        account.Activate("v1.hash", Now.AddHours(1));
        Assert.Equal(Now.AddHours(1), account.ActivatedAtUtc);
        Assert.Throws<InvalidOperationException>(account.Reactivate);

        account.Suspend(Now.AddDays(1));
        Assert.Equal((OperatorStates.Suspended, Now.AddDays(1)), (account.State, account.SuspendedAtUtc));
        Assert.Throws<InvalidOperationException>(() => account.Suspend(Now.AddDays(2)));

        account.Reactivate();
        Assert.Equal((OperatorStates.Active, (DateTimeOffset?)null), (account.State, account.SuspendedAtUtc));
        Assert.Equal(((byte)0, (byte)0, (DateTimeOffset?)null), (account.FailedAttempts, account.SpentCodes, account.LockedUntilUtc));
    }

    [Fact]
    public void Only_an_active_account_that_is_locked_now_is_unlocked()
    {
        var account = Operator.Invite("Sara", "sara@heta.example", SuperAdmin, Now);
        Assert.Throws<InvalidOperationException>(() => account.Unlock(Now));

        account.Activate("v1.hash", Now);
        Assert.Throws<InvalidOperationException>(() => account.Unlock(Now));
    }
}
