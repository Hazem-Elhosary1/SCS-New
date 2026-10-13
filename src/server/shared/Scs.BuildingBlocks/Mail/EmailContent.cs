namespace Scs.BuildingBlocks.Mail;

/// <summary>
/// What one email says (spec 094): a language, a subject, the short line above the title, the
/// title, the parts of its body in order, and the line that says why it was sent. A module
/// fills this and nothing else; the template lays it out, writes the footer and makes the
/// text-only form. An email that cannot be whole is refused here, when it is built.
/// </summary>
public sealed record EmailContent
{
    public const int SubjectMaxLength = 120;
    public const int EyebrowMaxLength = 60;
    public const int TitleMaxLength = 200;

    /// <summary>
    /// Stands for the platform's name inside any text of an email; the template writes the name
    /// of the email's language in its place, so that no email writes it as words of its own.
    /// </summary>
    public const string Platform = "";

    public EmailContent(
        string to, string language, string subject, string eyebrow, string title, IReadOnlyList<EmailPart>? parts = null,
        string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        To = to;
        Language = language == "en" ? "en" : "ar";
        Subject = CutAtAWord(Required(subject, "subject"), SubjectMaxLength);
        Eyebrow = Required(eyebrow, "short line");
        // Cut, not refused: a title may be what a person wrote (a notification's), and an email
        // that is too long in its title is still better sent than not.
        Title = CutAtAWord(Required(title, "title"), TitleMaxLength);
        Parts = parts ?? [];
        Reason = reason is null ? null : Required(reason, "reason");

        if (Subject.Contains(Platform, StringComparison.Ordinal))
        {
            throw new EmailCompositionException("A subject does not name the platform: the sender's name does.");
        }

        if (Eyebrow.Length > EyebrowMaxLength)
        {
            throw new EmailCompositionException("The short line is longer than an email holds.");
        }

        if (Parts.OfType<EmailButton>().Count() > 1)
        {
            throw new EmailCompositionException("An email has one button at most.");
        }

        if (Parts.OfType<EmailCode>().Count() > 1)
        {
            throw new EmailCompositionException("An email has one code at most.");
        }
    }

    /// <summary>The recipient. Personal data: never logged.</summary>
    public string To { get; }

    /// <summary><c>ar</c> or <c>en</c>; anything else is Arabic.</summary>
    public string Language { get; }

    public string Subject { get; }

    /// <summary>The short line above the title: what kind of message this is.</summary>
    public string Eyebrow { get; }

    public string Title { get; }

    public IReadOnlyList<EmailPart> Parts { get; }

    /// <summary>Why the recipient received it; the template's own line for account activity when null.</summary>
    public string? Reason { get; }

    /// <summary>Nothing of the email, so that one logged by mistake shows nothing.</summary>
    public override string ToString() => nameof(EmailContent);

    /// <summary>
    /// Marks a value that is written left to right whatever the language around it — an email
    /// address, a link, a number, a Latin name — so that it keeps its order in an Arabic line.
    /// </summary>
    public static string LeftToRight(string value) => $"{EmailMarks.Open}{value}{EmailMarks.Close}";

    private static string Required(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EmailCompositionException($"An email needs its {what}.");
        }

        return value.Trim();
    }

    private static string CutAtAWord(string words, int limit)
    {
        if (words.Length <= limit)
        {
            return words;
        }

        var head = words[..(limit - 1)];
        var space = head.LastIndexOf(' ');
        return (space > limit / 2 ? head[..space] : head).TrimEnd() + "…";
    }
}

/// <summary>The two marks <see cref="EmailContent.LeftToRight"/> puts around a value.</summary>
public static class EmailMarks
{
    public const char Open = '⁦';
    public const char Close = '⁩';
}

/// <summary>An email was asked for that cannot be whole. Carries no recipient and no content.</summary>
public sealed class EmailCompositionException : Exception
{
    public EmailCompositionException()
        : base("The email cannot be composed.")
    {
    }

    public EmailCompositionException(string message)
        : base(message)
    {
    }

    public EmailCompositionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
