using Scs.SharedKernel.Audit;

namespace Scs.UnitTests.Audit;

/// <summary>
/// Spec 007, FR-006 to FR-008, FR-016, FR-039: the catalogue of audit actions is closed, its
/// codes are frozen, and no action can ever carry a secret or an email address as a fact.
/// </summary>
public class AuditActionsTests
{
    private static readonly (string Code, string Category)[] Expected =
    [
        ("role.created", "users"),
        ("role.updated", "users"),
        ("role.deleted", "users"),
        ("account.role-changed", "users"),
        ("account.access-reissued", "users"),
        ("invitation.accepted", "users"),
        ("signin.succeeded", "security"),
        ("signin.failed-run", "security"),
        ("session.signed-out", "security"),
        ("sessions.ended", "security"),
        ("password.reset-completed", "security"),
        ("password.temporary-replaced", "security"),

        // Spec 008: who turned the email of a notification on or off.
        ("settings.notification-email-changed", "changes"),

        // Spec 009: a file leaving the system, and a file the check refused.
        ("file.downloaded", "records"),
        ("file.scan-refused", "security"),

        // Spec 011: the operators console's own team and roles, in the platform's own trail.
        ("operator.invited", "users"),
        ("operator.edited", "users"),
        ("operator.role-changed", "users"),
        ("operator.suspended", "users"),
        ("operator.reactivated", "users"),
        ("operator.link-sent", "security"),
        ("operator.unlocked", "security"),
        ("opsrole.created", "users"),
        ("opsrole.edited", "users"),
        ("opsrole.deleted", "users"),
        ("feature.created", "changes"),
        ("feature.updated", "changes"),
        ("feature.retired", "changes"),
        ("feature.resumed", "changes"),
    ];

    [Fact]
    public void The_catalogue_holds_exactly_the_actions_the_specs_name()
    {
        Assert.Equal(
            Expected.OrderBy(e => e.Code, StringComparer.Ordinal),
            AuditActions.All.Select(a => (a.Code, a.Category)).OrderBy(e => e.Code, StringComparer.Ordinal));
    }

    [Fact]
    public void Codes_are_unique_and_well_formed()
    {
        var codes = AuditActions.All.Select(a => a.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches(@"^[a-z]+(\.[a-z]+(-[a-z]+)*)+$", code));
        Assert.All(codes, code => Assert.True(code.Length <= 64, code));
    }

    [Fact]
    public void Every_category_is_one_of_the_four()
    {
        Assert.Equal(["security", "changes", "records", "users"], AuditCategories.All);
        Assert.All(AuditActions.All, action => Assert.Contains(action.Category, AuditCategories.All));
    }

    [Fact]
    public void Every_code_of_the_catalogue_is_frozen_with_its_category()
    {
        var locked = AuditActions.Locked();

        var missing = AuditActions.All.Where(a => !locked.Contains((a.Code, a.Category))).Select(a => a.Code).ToList();
        Assert.True(
            missing.Count == 0,
            "Add these to audit-actions.lock, on purpose, with their category: " + string.Join(", ", missing));
    }

    [Fact]
    public void A_frozen_code_is_never_removed_or_moved_to_another_category()
    {
        var gone = AuditActions.Locked()
            .Where(entry => AuditActions.Find(entry.Code)?.Category != entry.Category)
            .Select(entry => entry.Code)
            .ToList();

        Assert.True(gone.Count == 0, "Frozen codes that are missing or changed category: " + string.Join(", ", gone));
    }

    [Fact]
    public void No_action_allows_a_fact_that_could_hold_a_secret_or_an_email_address()
    {
        Assert.Equal(
            ["password", "code", "token", "link", "secret", "key", "email", "proof", "cookie"], AuditActions.NeverKeys);

        var offenders = AuditActions.All
            .SelectMany(action => action.Facts.Select(fact => (action.Code, Fact: fact)))
            .Where(x => AuditActions.NeverKeys.Any(never => x.Fact.Contains(never, StringComparison.OrdinalIgnoreCase)))
            .Select(x => $"{x.Code}: {x.Fact}")
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
    }

    [Fact]
    public void Every_action_has_a_name_and_a_sentence_in_both_languages()
    {
        Assert.All(AuditActions.All, action =>
        {
            Assert.False(string.IsNullOrWhiteSpace(action.NameAr), action.Code);
            Assert.False(string.IsNullOrWhiteSpace(action.NameEn), action.Code);
            Assert.False(string.IsNullOrWhiteSpace(action.SentenceAr), action.Code);
            Assert.False(string.IsNullOrWhiteSpace(action.SentenceEn), action.Code);
        });
    }

    [Fact]
    public void The_actions_that_change_a_value_and_no_other_need_before_and_after()
    {
        Assert.Equal(
            [
                "account.role-changed", "feature.created", "feature.resumed", "feature.retired", "feature.updated", "operator.edited",
                "operator.role-changed", "opsrole.created", "opsrole.edited", "role.created", "role.deleted", "role.updated",
                "settings.notification-email-changed",
            ],
            AuditActions.All.Where(a => a.NeedsBeforeAfter).Select(a => a.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void An_action_is_found_by_its_code_and_a_stranger_is_not()
    {
        Assert.Equal("users", AuditActions.Find(AuditActions.RoleCreated)?.Category);
        Assert.Null(AuditActions.Find("refund.issued"));
        Assert.Null(new AuditCatalogue().Find("no.such-action"));
    }
}
