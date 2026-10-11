using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scs.SharedKernel.Files;

/// <summary>
/// The table of kinds of file as the web sites read it (spec 009): what the upload control says
/// it takes before anything is chosen. Written by the migrator's <c>file-kinds</c> tool; a test
/// fails while the checked-in file differs from the table. The platform decides whatever the
/// file says.
/// </summary>
public static class FileKindsFile
{
    /// <summary>Where the file lives, from the repository root.</summary>
    public const string RepositoryPath = "src/web/projects/core/src/lib/files/file-kinds.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write()
    {
        var content = new
        {
            Kinds = FileKinds.All.Select(k => new { k.Key, k.Types, k.MaxBytes, k.MaxPerRecord, k.Public }),
        };

        return JsonSerializer.Serialize(content, Json).ReplaceLineEndings("\n") + "\n";
    }
}
