using Scs.BuildingBlocks.Notifications;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Notifications;

/// <summary>
/// Spec 008, User Story 2 (FR-010 to FR-014; SC-002): each event tells exactly the people its
/// rule names — by a permission or by ownership, never by a role's name — and nobody else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RecipientsTests(SqlServerFixture sql)
{
    /// <summary>
    /// A clinic with an owner, a holder of the events' permission, a person without it, a
    /// disabled holder, an invited account and a person with no role; and a second clinic with
    /// an owner of its own.
    /// </summary>
    private sealed record Clinic(
        RefusalWorld World, Guid Owner, Guid Holder, Guid Other, Guid DisabledHolder, Guid Invited, Guid NoRole, Guid Elsewhere, Guid OtherClinic)
        : IDisposable
    {
        public void Dispose() => World.Dispose();
    }

    private async Task<Clinic> ClinicAsync()
    {
        var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());
        var factory = world.Factory;

        var ownerRole = await factory.ReadyRoleAsync(world.Clinic, "owner");
        var holderRole = await factory.CreateRoleAsync(world.Clinic, Permissions.TicketsView, TestEvents.Permission);
        var otherRole = await factory.CreateRoleAsync(world.Clinic, Permissions.BookingsView);

        async Task<Guid> WithRoleAsync(Guid role, string state = StaffAccountStates.Active)
        {
            var (id, _) = await factory.CreateAccountAsync(world.Clinic, state);
            await factory.RolesAsync(world.Clinic, r => r.AssignAsync(id, role, default));
            return id;
        }

        var owner = await WithRoleAsync(ownerRole);
        var holder = await WithRoleAsync(holderRole);
        var other = await WithRoleAsync(otherRole);
        var disabled = await WithRoleAsync(holderRole, StaffAccountStates.Disabled);
        var invited = await WithRoleAsync(holderRole, StaffAccountStates.Invited);
        var (noRole, _) = await factory.CreateAccountAsync(world.Clinic);

        var otherClinic = await sql.Main.SeedClinicAsync();
        var theirOwnerRole = await factory.ReadyRoleAsync(otherClinic, "owner");
        var (elsewhere, _) = await factory.CreateAccountAsync(otherClinic);
        await factory.RolesAsync(otherClinic, r => r.AssignAsync(elsewhere, theirOwnerRole, default));

        return new Clinic(world, owner, holder, other, disabled, invited, noRole, elsewhere, otherClinic);
    }

    private async Task<List<Guid>> ToldAsync(Clinic clinic, string eventKey) =>
        [.. (await sql.Main.NotificationsAsync(clinic.World.Clinic)).Where(n => n.EventKey == eventKey).Select(n => n.RecipientAccountId).Order()];

    [Fact]
    public async Task The_holders_of_a_permission_are_told_and_nobody_else()
    {
        using var clinic = await ClinicAsync();

        Assert.Null(await clinic.World.Factory.RaiseAsync(clinic.World.Clinic, new RaisedEvent(TestEvents.ToHolders, Notify.Ticket())));

        // The owner role holds whatever the catalogue holds, so its holder is told too.
        Assert.Equal(new[] { clinic.Owner, clinic.Holder }.Order(), await ToldAsync(clinic, TestEvents.ToHolders));
    }

    [Fact]
    public async Task The_owner_of_the_record_is_told_whatever_their_role()
    {
        using var clinic = await ClinicAsync();

        Assert.Null(await clinic.World.Factory.RaiseAsync(
            clinic.World.Clinic, new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [clinic.Other, clinic.NoRole])));

        Assert.Equal(new[] { clinic.Other, clinic.NoRole }.Order(), await ToldAsync(clinic, TestEvents.ToOwner));
    }

    [Fact]
    public async Task An_owner_who_also_holds_the_permission_is_told_once()
    {
        using var clinic = await ClinicAsync();

        Assert.Null(await clinic.World.Factory.RaiseAsync(
            clinic.World.Clinic,
            new RaisedEvent(TestEvents.ToOwnerAndHolders, Notify.Ticket(), Owners: [clinic.Holder, clinic.Other, clinic.Holder])));

        Assert.Equal(
            new[] { clinic.Owner, clinic.Holder, clinic.Other }.Order(), await ToldAsync(clinic, TestEvents.ToOwnerAndHolders));
    }

    [Fact]
    public async Task Everyone_means_everyone_who_can_sign_in()
    {
        using var clinic = await ClinicAsync();

        Assert.Null(await clinic.World.Factory.RaiseAsync(clinic.World.Clinic, new RaisedEvent(TestEvents.ToEveryone)));

        Assert.Equal(
            new[] { clinic.Owner, clinic.Holder, clinic.Other, clinic.NoRole }.Order(), await ToldAsync(clinic, TestEvents.ToEveryone));
    }

    [Fact]
    public async Task A_disabled_account_and_one_never_activated_are_told_nothing_even_by_name()
    {
        using var clinic = await ClinicAsync();

        Assert.Null(await clinic.World.Factory.RaiseAsync(
            clinic.World.Clinic,
            new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [clinic.DisabledHolder, clinic.Invited, clinic.Other])));

        Assert.Equal([clinic.Other], await ToldAsync(clinic, TestEvents.ToOwner));
    }

    [Fact]
    public async Task Nobody_of_another_clinic_is_told_even_when_named()
    {
        using var clinic = await ClinicAsync();

        Assert.Null(await clinic.World.Factory.RaiseAsync(clinic.World.Clinic, new RaisedEvent(TestEvents.ToEveryone)));
        Assert.Null(await clinic.World.Factory.RaiseAsync(clinic.World.Clinic, new RaisedEvent(TestEvents.ToHolders, Notify.Ticket())));
        Assert.Null(await clinic.World.Factory.RaiseAsync(
            clinic.World.Clinic, new RaisedEvent(TestEvents.ToOwner, Notify.Ticket(), Owners: [clinic.Elsewhere, clinic.Other])));

        Assert.Empty(await sql.Main.NotificationsAsync(clinic.OtherClinic));
        Assert.DoesNotContain(await sql.Main.NotificationsAsync(clinic.World.Clinic), n => n.RecipientAccountId == clinic.Elsewhere);
        Assert.Equal([clinic.Other], await ToldAsync(clinic, TestEvents.ToOwner));
    }

    [Fact]
    public async Task A_rule_that_finds_nobody_tells_nobody_and_fails_nothing()
    {
        using var world = await RefusalWorld.CreateAsync(sql, factory => factory.WithTestEvents());

        // A clinic with no account at all.
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.ToHolders, Notify.Ticket())));
        Assert.Null(await world.Factory.RaiseAsync(world.Clinic, new RaisedEvent(TestEvents.ToEveryone)));

        Assert.Empty(await sql.Main.NotificationsAsync(world.Clinic));
    }

    [Fact]
    public async Task What_was_delivered_stays_and_what_was_not_does_not_arrive_when_roles_change_later()
    {
        using var clinic = await ClinicAsync();
        var factory = clinic.World.Factory;
        var holderRole = (await factory.RolesAsync(clinic.World.Clinic, r => r.ListAsync(default)))
            .Single(r => r.Category == "custom" && r.PermissionCount == 2).Id;
        var otherRole = (await factory.RolesAsync(clinic.World.Clinic, r => r.ListAsync(default)))
            .Single(r => r.Category == "custom" && r.PermissionCount == 1).Id;

        Assert.Null(await factory.RaiseAsync(clinic.World.Clinic, new RaisedEvent(TestEvents.ToHolders, Notify.Ticket())));

        // The holder loses the permission and the other person gains it.
        await factory.RolesAsync(clinic.World.Clinic, r => r.AssignAsync(clinic.Holder, otherRole, default));
        await factory.RolesAsync(clinic.World.Clinic, r => r.AssignAsync(clinic.Other, holderRole, default));

        Assert.Equal(new[] { clinic.Owner, clinic.Holder }.Order(), await ToldAsync(clinic, TestEvents.ToHolders));

        // From the next event on, the new holder is the one told.
        Assert.Null(await factory.RaiseAsync(clinic.World.Clinic, new RaisedEvent(TestEvents.ToHolders, Notify.Ticket())));
        var told = (await sql.Main.NotificationsAsync(clinic.World.Clinic)).Where(n => n.EventKey == TestEvents.ToHolders).ToList();
        Assert.Equal(1, told.Count(n => n.RecipientAccountId == clinic.Holder));
        Assert.Equal(1, told.Count(n => n.RecipientAccountId == clinic.Other));
        Assert.Equal(2, told.Count(n => n.RecipientAccountId == clinic.Owner));
    }
}
