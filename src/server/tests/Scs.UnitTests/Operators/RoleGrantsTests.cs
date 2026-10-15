using Scs.Operators.Domain;
using Scs.SharedKernel.Access;

namespace Scs.UnitTests.Operators;

/// <summary>
/// Spec 011, SC-006: nobody ends up with a permission the person who granted it did not hold,
/// by any path. The one rule, over every combination that matters.
/// </summary>
public class RoleGrantsTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid MyRole = Guid.NewGuid();
    private static readonly string[] All = [.. OperatorPermissions.All.Select(p => p.Code)];

    private static Granter Holding(params string[] codes) =>
        new(Me, MyRole, IsSuperAdmin: false, OperatorPermissions.WithRequired(codes).ToHashSet(StringComparer.Ordinal));

    private static Granter SuperAdmin { get; } = new(Me, MyRole, IsSuperAdmin: true, All.ToHashSet(StringComparer.Ordinal));

    [Fact]
    public void A_role_is_given_only_by_someone_who_holds_everything_in_it()
    {
        var manager = Holding("operators.manage", "tickets.reply");

        Assert.Null(RoleGrants.WhyNotGrantable(manager, null, ["operators.view", "tickets.view"]));
        Assert.Null(RoleGrants.WhyNotGrantable(manager, null, []));
        Assert.Equal("holds-more", RoleGrants.WhyNotGrantable(manager, null, ["operators.view", "settings.edit"]));
        Assert.Equal("holds-more", RoleGrants.WhyNotGrantable(manager, "viewer", OperatorPermissions.SystemRoles.CodesOf("viewer")));
    }

    [Fact]
    public void The_super_admin_role_is_given_only_by_a_super_admin_even_to_someone_who_holds_everything()
    {
        // Holding all 28 through a custom role is not being «مسؤول عام».
        var everything = new Granter(Me, MyRole, IsSuperAdmin: false, All.ToHashSet(StringComparer.Ordinal));

        Assert.Equal("super-admin-only", RoleGrants.WhyNotGrantable(everything, "super-admin", All));
        Assert.Null(RoleGrants.WhyNotGrantable(everything, null, All));
        Assert.Null(RoleGrants.WhyNotGrantable(SuperAdmin, "super-admin", All));
        Assert.Null(RoleGrants.WhyNotGrantable(SuperAdmin, null, ["settings.edit"]));
    }

    [Fact]
    public void Nobody_changes_their_own_role_and_only_a_super_admin_edits_the_role_they_hold()
    {
        var manager = Holding("operators.manage", "operators.manageRoles");

        Assert.True(RoleGrants.IsOwnAccount(manager, Me));
        Assert.False(RoleGrants.IsOwnAccount(manager, Guid.NewGuid()));
        Assert.True(RoleGrants.IsOwnAccount(SuperAdmin, Me));

        Assert.False(RoleGrants.MayEdit(manager, MyRole));
        Assert.True(RoleGrants.MayEdit(manager, Guid.NewGuid()));
        Assert.True(RoleGrants.MayEdit(SuperAdmin, MyRole));
    }

    [Fact]
    public void A_permission_is_added_to_a_role_only_by_a_holder_and_what_the_role_had_stays()
    {
        var editor = Holding("operators.manageRoles", "tickets.reply");

        // Adding what they hold, with what it needs.
        Assert.Empty(RoleGrants.NotAddable(editor, [], ["tickets.reply"]));

        // Adding what they do not hold is named, with the view it would drag in.
        Assert.Equal(["settings.view", "settings.edit"], RoleGrants.NotAddable(editor, [], ["settings.edit"]));
        Assert.Equal(["reports.export"], RoleGrants.NotAddable(editor, ["tickets.view"], ["tickets.view", "reports.export"]));

        // What the role already held is not theirs to refuse: it stays.
        Assert.Empty(RoleGrants.NotAddable(editor, ["settings.view", "settings.edit"], ["settings.view", "settings.edit", "tickets.reply"]));

        // Removing is always allowed.
        Assert.Empty(RoleGrants.NotAddable(editor, ["settings.view", "settings.edit"], ["tickets.view"]));

        Assert.Empty(RoleGrants.NotAddable(SuperAdmin, [], All));
        Assert.True(RoleGrants.MayAdd(SuperAdmin, "settings.edit"));
        Assert.False(RoleGrants.MayAdd(editor, "settings.edit"));
    }

    [Fact]
    public void Copying_a_role_takes_what_the_creator_holds_and_says_what_was_left()
    {
        var creator = Holding("operators.manageRoles", "tickets.reply", "clinics.view");
        var source = new[] { "tickets.view", "tickets.reply", "tickets.close", "clinics.view", "clinics.edit", "settings.view" };

        var (taken, left) = RoleGrants.Copy(creator, source);

        Assert.Equal(["clinics.view", "tickets.view", "tickets.reply"], taken);
        Assert.Equal(["clinics.edit", "tickets.close", "settings.view"], left);
        Assert.All(taken, code => Assert.Contains(code, creator.Codes));

        var (everything, nothing) = RoleGrants.Copy(SuperAdmin, All);
        Assert.Equal((28, 0), (everything.Count, nothing.Count));
    }

    [Fact]
    public void Whatever_a_non_super_admin_holds_nothing_they_may_give_exceeds_it()
    {
        // Every pair of permissions as "what I hold", against every single-permission role.
        foreach (var first in All)
        {
            foreach (var second in All.Where(code => string.CompareOrdinal(code, first) > 0).Take(6))
            {
                var granter = Holding(first, second);
                foreach (var code in All)
                {
                    var role = OperatorPermissions.WithRequired([code]);
                    var grantable = RoleGrants.WhyNotGrantable(granter, null, role) is null;
                    Assert.Equal(role.All(granter.Codes.Contains), grantable);

                    var refused = RoleGrants.NotAddable(granter, [], [code]);
                    Assert.Equal(role.Where(c => !granter.Codes.Contains(c)), refused);
                }
            }
        }
    }
}
