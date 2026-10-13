namespace Scs.BuildingBlocks.Mail;

/// <summary>
/// One entry of the catalogue of emails (spec 094, FR-037): an email the platform can send, in
/// one language, with made-up sample values. The catalogue is exported as pages for a person
/// to look at and for the browser tests to open; nothing of the platform serves it.
/// </summary>
/// <param name="Kind">The email's name: the name of the method that builds it.</param>
/// <param name="Audience"><c>clinic</c>, <c>operators</c> or <c>sample</c>.</param>
/// <param name="Variant">Empty, or what tells two samples of one kind apart.</param>
public sealed record EmailSample(string Kind, string Audience, EmailContent Content, string Variant = "")
{
    public const string Clinic = "clinic";
    public const string Operators = "operators";
    public const string Sample = "sample";

    public string Language => Content.Language;

    /// <summary>The name of its page: audience, kind, variant and language.</summary>
    public string Slug =>
        string.Join('-', new[] { Audience, Kind, Variant, Language }.Where(part => part.Length > 0)).ToLowerInvariant();
}

/// <summary>
/// What a module provides to the catalogue: every email it can send, in Arabic and in English.
/// An email without its samples is caught by the architecture tests (FR-039).
/// </summary>
public interface IEmailSamples
{
    IReadOnlyList<EmailSample> All();
}
