using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Mail;

/// <summary>
/// A piece of an email's body that the design draws once (spec 094, page "Email Template").
/// The set is closed: a module chooses and orders parts and cannot add a kind.
/// </summary>
public abstract record EmailPart
{
    private protected EmailPart()
    {
    }

    /// <summary>
    /// True for an optional part that was given nothing: the template leaves it out of both
    /// forms (FR-016). What an email cannot be without is refused when built instead.
    /// </summary>
    public virtual bool IsEmpty => false;

    private protected static string Required(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EmailCompositionException($"{what} has nothing in it.");
        }

        return value.Trim();
    }
}

/// <summary>The four tones of a status mark and of the attention box.</summary>
public enum EmailTone
{
    Neutral,
    Good,
    Warning,
    Bad,
}

/// <summary>A paragraph of the body. One with nothing in it is not drawn and leaves no gap.</summary>
public sealed record EmailParagraph : EmailPart
{
    public EmailParagraph(string? text) => Text = (text ?? "").Trim();

    public string Text { get; }

    public override bool IsEmpty => Text.Length == 0;
}

/// <summary>A small line under what it follows. One with nothing in it is not drawn and leaves no gap.</summary>
public sealed record EmailNote : EmailPart
{
    public EmailNote(string? text) => Text = (text ?? "").Trim();

    public string Text { get; }

    public override bool IsEmpty => Text.Length == 0;
}

/// <summary>The six digits of a code, in their tinted box, left to right in both languages.</summary>
public sealed record EmailCode : EmailPart
{
    public const int Length = 6;

    public EmailCode(string digits)
    {
        if (digits is null || digits.Length != Length || !digits.All(char.IsAsciiDigit))
        {
            throw new EmailCompositionException("A code is six digits.");
        }

        Digits = digits;
    }

    public string Digits { get; }
}

/// <summary>
/// The one thing to do: a button, with the same address written out under it so that it can be
/// copied where the button cannot be pressed.
/// </summary>
public sealed record EmailButton : EmailPart
{
    public EmailButton(string label, string address)
    {
        Label = Required(label, "A button's label");
        Address = Required(address, "A button's address");

        // The same rule the configuration is held to at start, so a console's address that
        // was accepted there always makes a button.
        if (!Uri.TryCreate(Address, UriKind.Absolute, out var parsed) || !ConsoleAddresses.MayBeLinkedTo(parsed))
        {
            throw new EmailCompositionException("A button leads to an https address of the platform.");
        }
    }

    public string Label { get; }

    public string Address { get; }
}

/// <summary>One row of the box of facts.</summary>
public sealed record EmailFact
{
    public EmailFact(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
        {
            throw new EmailCompositionException("A fact has a name and a value.");
        }

        Name = name.Trim();
        Value = value.Trim();
    }

    public string Name { get; }

    public string Value { get; }
}

/// <summary>A short word on a tinted ground, with an optional quieter line beside it.</summary>
public sealed record EmailStatus
{
    public EmailStatus(string text, EmailTone tone, string? beside = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new EmailCompositionException("A status mark has a word.");
        }

        Text = text.Trim();
        Tone = tone;
        Beside = string.IsNullOrWhiteSpace(beside) ? null : beside.Trim();
    }

    public string Text { get; }

    public EmailTone Tone { get; }

    public string? Beside { get; }
}

/// <summary>
/// The box of facts: rows of a name and a value, the value at the far edge; optionally a rule
/// and a row that stands out as the total, and a status mark.
/// </summary>
public sealed record EmailFacts : EmailPart
{
    public EmailFacts(IReadOnlyList<EmailFact> rows, EmailFact? total = null, EmailStatus? status = null, bool ruled = true)
    {
        if (rows is null || rows.Count == 0)
        {
            throw new EmailCompositionException("A box of facts has at least one row.");
        }

        Rows = rows;
        Total = total;
        Status = status;
        Ruled = ruled;
    }

    public IReadOnlyList<EmailFact> Rows { get; }

    public EmailFact? Total { get; }

    public EmailStatus? Status { get; }

    /// <summary>Whether a rule is drawn above the total; without it only the total's value stands out.</summary>
    public bool Ruled { get; }
}

/// <summary>The tinted box for one thing that needs attention: a mark, a line, a quieter line.</summary>
public sealed record EmailAttention : EmailPart
{
    public EmailAttention(string line, string? second = null, EmailStatus? mark = null, EmailTone tone = EmailTone.Warning)
    {
        Line = Required(line, "An attention box");
        Second = string.IsNullOrWhiteSpace(second) ? null : second.Trim();
        Mark = mark;
        Tone = tone;
    }

    public string Line { get; }

    public string? Second { get; }

    public EmailStatus? Mark { get; }

    public EmailTone Tone { get; }
}
