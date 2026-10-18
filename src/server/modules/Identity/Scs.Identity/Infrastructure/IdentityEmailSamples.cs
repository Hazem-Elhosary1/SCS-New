using Scs.BuildingBlocks.Mail;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The clinic team's account emails with made-up values, in both languages, for the catalogue
/// (spec 094, FR-037, FR-039). Read by the tests that export and check every email; nothing
/// that runs sends them.
/// </summary>
internal sealed class IdentityEmailSamples : IEmailSamples
{
    private const string To = "sara@noor.example";
    private const string App = "https://app.scs.test";
    private const string ClinicAr = "عيادة النور";
    private const string ClinicEn = "Al Noor Clinic";

    private static readonly DateTimeOffset When = new(2026, 10, 12, 9, 4, 0, TimeSpan.Zero);

    public IReadOnlyList<EmailSample> All() =>
    [
        .. Both("SignInCode", language => AccountEmails.SignInCode(To, language, "742906", 10)),
        .. Both("PasswordReset", language => AccountEmails.PasswordReset(To, language, $"{App}/set-password#t=v1.sample-token-of-a-reset-link", 30)),
        .. Both("PasswordChanged", language => AccountEmails.PasswordChanged(To, language, When)),
        .. Both("Invitation", language => AccountEmails.Invitation(To, language, $"{App}/set-password#t=v1.sample-token-of-an-invitation", ClinicAr, ClinicEn, 72, App)),
        .. Both("AccountCreated", language => AccountEmails.AccountCreated(To, language, ClinicAr, ClinicEn, $"{App}/sign-in")),
        .. Both("AccessReset", language => AccountEmails.AccessReset(To, language, $"{App}/sign-in")),
        .. Both("EmailChanged", language => AccountEmails.EmailChanged(To, language, "haitham@alnoorclinic.example", ClinicAr, ClinicEn)),
    ];

    private static EmailSample[] Both(string kind, Func<string, EmailContent> build) =>
        [new(kind, EmailSample.Clinic, build("ar")), new(kind, EmailSample.Clinic, build("en"))];
}
