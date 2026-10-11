using System.Globalization;
using System.Text;
using System.Text.Json;
using Scs.SharedKernel.Notifications;
using Scs.SharedKernel.Wording;

namespace Scs.Notifications.Infrastructure;

/// <summary>
/// Turns a stored notification into words in the reader's language (spec 008, FR-019): the
/// title is the event's name, and the line is the event's template filled from the facts of the
/// moment. Nothing here is stored; the same notification reads in Arabic for one reader and in
/// English for another.
/// </summary>
internal static class NotificationWords
{
    /// <summary>The fact that holds a user agent, read as a device in words.</summary>
    public const string Device = "device";

    public static string Title(NotificationEvent? known, string eventKey, string language) =>
        known is null ? eventKey : language == Words.English ? known.NameEn : known.NameAr;

    public static string Line(NotificationEvent? known, string? label, string? factsJson, string language)
    {
        var template = language == Words.English ? known?.LineEn : known?.LineAr;
        if (string.IsNullOrEmpty(template))
        {
            return label ?? "";
        }

        var text = new StringBuilder(template);

        // An identifier keeps its Latin letters in both languages (أ-ق١٨).
        text.Replace("{label}", label ?? "");

        if (!string.IsNullOrEmpty(factsJson))
        {
            using var facts = JsonDocument.Parse(factsJson);
            foreach (var fact in facts.RootElement.EnumerateObject())
            {
                text.Replace($"{{{fact.Name}}}", Value(fact, language));
            }
        }

        return text.ToString();
    }

    private static string Value(JsonProperty fact, string language)
    {
        var english = language == Words.English;

        return fact.Value.ValueKind switch
        {
            JsonValueKind.String or JsonValueKind.Null when fact.Name == Device =>
                DeviceText.Describe(fact.Value.GetString(), language),
            JsonValueKind.String => fact.Value.GetString() ?? "",
            JsonValueKind.Number when fact.Value.TryGetInt32(out var whole) => Words.Number(whole, english),
            JsonValueKind.Number => fact.Value.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.True => english ? "yes" : "نعم",
            JsonValueKind.False => english ? "no" : "لا",
            _ => "",
        };
    }
}
