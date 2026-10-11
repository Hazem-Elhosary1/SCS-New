using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scs.SharedKernel.Access;

/// <summary>
/// The catalogue as the web sites read it: one generated file, so the client and the server
/// cannot disagree about a name, a group or a requirement (spec 005, FR-004). Written by
/// <c>Scs.Migrator catalogue</c>; a test fails when the checked-in file differs from this.
/// </summary>
public static class PermissionCatalogueFile
{
    /// <summary>Where the file lives, from the repository root.</summary>
    public const string RepositoryPath = "src/web/projects/core/src/lib/access/permission-catalogue.json";

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
            Catalogue = PermissionCatalogue.Name,
            Groups = PermissionCatalogue.Groups.Select(g => new { g.Key, g.NameAr, g.NameEn }),
            Permissions = PermissionCatalogue.All.Select(p => new
            {
                p.Number,
                p.Code,
                p.NameAr,
                p.NameEn,
                Group = p.Group.Key,
                Kind = p.Kind.ToString().ToLowerInvariant(),
                p.Requires,
            }),
        };

        return JsonSerializer.Serialize(content, Json).ReplaceLineEndings("\n") + "\n";
    }
}
