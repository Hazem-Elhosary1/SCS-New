using Scs.BuildingBlocks.Mail;
using Scs.Operators.Domain;

namespace Scs.Operators.Infrastructure;

/// <summary>
/// The operators' emails with made-up values, in both languages, for the catalogue (spec 094,
/// FR-037, FR-039). Read by the tests that export and check every email; nothing that runs
/// sends them.
/// </summary>
internal sealed class OperatorEmailSamples : IEmailSamples
{
    private const string To = "sara@heta.example";
    private const string Ops = "https://ops.scs.test";
    private const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";

    private static readonly DateTimeOffset When = new(2026, 10, 12, 9, 4, 0, TimeSpan.Zero);

    public IReadOnlyList<EmailSample> All() =>
    [
        .. Both("Code", language => OperatorEmails.Code(To, language, "742906", 10, ChallengePurposes.SignIn)),
        .. Both("Code", language => OperatorEmails.Code(To, language, "742906", 10, ChallengePurposes.PasswordChange), "change"),
        .. Both("ResetLink", language => OperatorEmails.ResetLink(To, language, $"{Ops}/password/reset#t=v1.sample-token-of-a-reset-link", 30)),
        .. Both("FirstPassword", language => OperatorEmails.FirstPassword(To, language, $"{Ops}/password/first#t=v1.sample-token-of-an-invitation", 72)),
        .. Both(
            "FirstPassword",
            language => OperatorEmails.FirstPassword(
                To, language, $"{Ops}/password/first#t=v1.sample-token-of-an-invitation", 72,
                language == "en" ? "Hazem Elhosary" : "حازم الحصري",
                language == "en" ? "Billing Manager" : "مسؤول الفوترة"),
            "invited"),
        .. Both("NewBrowser", language => OperatorEmails.NewBrowser(To, language, Chrome, "41.233.14.89", When)),
    ];

    private static EmailSample[] Both(string kind, Func<string, EmailContent> build, string variant = "") =>
        [new(kind, EmailSample.Operators, build("ar"), variant), new(kind, EmailSample.Operators, build("en"), variant)];
}
