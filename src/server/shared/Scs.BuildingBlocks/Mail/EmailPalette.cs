namespace Scs.BuildingBlocks.Mail;

/// <summary>
/// The colours and measures of the design's "Email Template" page (spec 094). Two greys are
/// darker than the frames draw them, by the owner's decision: every word of an email has a
/// contrast of at least 4.5 to 1 with its ground (FR-006). <c>EmailPaletteTests</c> computes
/// every pair listed in <see cref="TextOnGround"/> and fails under that.
/// </summary>
public static class EmailPalette
{
    public const int CardWidth = 600;
    public const int CardRadius = 8;

    public const string Ground = "#f0f2f4";
    public const string Card = "#ffffff";
    public const string Rule = "#d9dce0";

    public const string Purple = "#7c3aed";
    public const string OnPurple = "#ffffff";
    public const string Title = "#1a2024";

    /// <summary>The frames draw <c>#74838e</c> (3.9 to 1 on white); this is 5.6.</summary>
    public const string Body = "#5c6975";

    /// <summary>The frames draw <c>#9ca7b0</c> (2.5 to 1 on white); this is 4.7.</summary>
    public const string Note = "#68757f";

    public const string CodeGround = "#f1edfe";
    public const string CodeBorder = "#d9dce0";

    public const string FontStack = "'Cairo','Segoe UI',Tahoma,Arial,sans-serif";

    // The dark theme a mail program may ask for (research R7).
    public const string DarkGround = "#111418";
    public const string DarkCard = "#1b2026";
    public const string DarkRule = "#333b44";
    public const string DarkTitle = "#f2f4f6";
    public const string DarkBody = "#c3ccd4";
    public const string DarkNote = "#a3aeb8";
    public const string DarkPurpleText = "#b79bff";
    public const string DarkCodeGround = "#2a2440";
    public const string DarkCodeText = "#cdbbff";
    public const string DarkBox = "#232931";

    /// <summary>The ground and the word colour of a status mark, by tone.</summary>
    public static (string Ground, string Text) Mark(EmailTone tone) => tone switch
    {
        EmailTone.Good => ("#e5f2ec", "#116932"),
        EmailTone.Warning => ("#ffffff", "#b45309"),
        EmailTone.Bad => ("#fbe9e9", "#b42318"),
        _ => ("#eef0f2", "#46525c"),
    };

    /// <summary>The ground of the attention box, by tone.</summary>
    public static string Attention(EmailTone tone) => tone switch
    {
        EmailTone.Good => "#e5f2ec",
        EmailTone.Bad => "#fbe9e9",
        EmailTone.Neutral => "#eef0f2",
        _ => "#fef1e1",
    };

    /// <summary>The ground of the attention box in the dark theme, by tone.</summary>
    public static string DarkAttention(EmailTone tone) => tone switch
    {
        EmailTone.Good => "#1d2b24",
        EmailTone.Bad => "#33201f",
        EmailTone.Neutral => DarkBox,
        _ => "#33291b",
    };

    /// <summary>Every pair of word colour and ground the template draws, with what it is for.</summary>
    public static IReadOnlyList<(string What, string Text, string Ground)> TextOnGround { get; } = BuildPairs();

    private static List<(string What, string Text, string Ground)> BuildPairs()
    {
        var pairs = new List<(string, string, string)>
        {
            ("title on the card", Title, Card),
            ("body on the card", Body, Card),
            ("note on the card", Note, Card),
            ("short line on the card", Purple, Card),
            ("button", OnPurple, Purple),
            ("code", Purple, CodeGround),
            ("dark: title on the card", DarkTitle, DarkCard),
            ("dark: body on the card", DarkBody, DarkCard),
            ("dark: note on the card", DarkNote, DarkCard),
            ("dark: short line on the card", DarkPurpleText, DarkCard),
            ("dark: code", DarkCodeText, DarkCodeGround),
            ("dark: fact's name in its box", DarkBody, DarkBox),
            ("dark: fact's value in its box", DarkTitle, DarkBox),
        };

        foreach (var tone in Enum.GetValues<EmailTone>())
        {
            var (ground, text) = Mark(tone);
            pairs.Add(($"status mark, {tone}", text, ground));
            pairs.Add(($"attention line, {tone}", Title, Attention(tone)));
            pairs.Add(($"attention second line, {tone}", Body, Attention(tone)));
            pairs.Add(($"dark: attention line, {tone}", DarkTitle, DarkAttention(tone)));
            pairs.Add(($"dark: attention second line, {tone}", DarkBody, DarkAttention(tone)));
        }

        return pairs;
    }

    /// <summary>The contrast of two colours written as <c>#rrggbb</c>, from 1 to 21.</summary>
    public static double Contrast(string first, string second)
    {
        var (a, b) = (Luminance(first), Luminance(second));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string colour)
    {
        double Channel(int at)
        {
            var value = Convert.ToInt32(colour.Substring(at, 2), 16) / 255d;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(1)) + (0.7152 * Channel(3)) + (0.0722 * Channel(5));
    }
}
