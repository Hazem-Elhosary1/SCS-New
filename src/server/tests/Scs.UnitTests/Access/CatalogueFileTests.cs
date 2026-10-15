using System.Text.Json;
using Scs.SharedKernel.Access;

namespace Scs.UnitTests.Access;

/// <summary>
/// Spec 005, FR-004: the web sites read the catalogue from one generated file. It must be what
/// the server's catalogue would write today; regenerate it with <c>Scs.Migrator catalogue</c>.
/// </summary>
public class CatalogueFileTests
{
    private static string FindFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. PermissionCatalogueFile.RepositoryPath.Split('/')]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"{PermissionCatalogueFile.RepositoryPath} was not found above the test directory. " +
            "Create it with: dotnet run --project src/server/tools/Scs.Migrator -- catalogue " + PermissionCatalogueFile.RepositoryPath);
    }

    [Fact]
    public void The_checked_in_file_is_what_the_catalogue_writes()
    {
        var onDisk = File.ReadAllText(FindFile()).ReplaceLineEndings("\n");

        Assert.True(
            onDisk == PermissionCatalogueFile.Write(),
            "The client's catalogue differs from the server's. Regenerate it: "
            + "dotnet run --project src/server/tools/Scs.Migrator -- catalogue " + PermissionCatalogueFile.RepositoryPath);
    }

    /// <summary>Spec 011: the operators' console reads its own catalogue the same way.</summary>
    [Fact]
    public void The_operators_checked_in_file_is_what_their_catalogue_writes()
    {
        string? found = null;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null && found is null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. OperatorPermissionsFile.RepositoryPath.Split('/')]);
            found = File.Exists(candidate) ? candidate : null;
        }

        const string How = "Regenerate it: dotnet run --project src/server/tools/Scs.Migrator -- ops-catalogue " + OperatorPermissionsFile.RepositoryPath;
        Assert.True(found is not null, $"{OperatorPermissionsFile.RepositoryPath} was not found. {How}");
        Assert.True(
            File.ReadAllText(found!).ReplaceLineEndings("\n") == OperatorPermissionsFile.Write(),
            "The console's catalogue differs from the server's. " + How);

        using var document = JsonDocument.Parse(OperatorPermissionsFile.Write());
        Assert.Equal(9, document.RootElement.GetProperty("groups").GetArrayLength());
        Assert.Equal(28, document.RootElement.GetProperty("permissions").GetArrayLength());
        Assert.Equal(3, document.RootElement.GetProperty("systemRoles").GetArrayLength());
    }

    [Fact]
    public void The_file_carries_every_group_and_permission_with_both_names()
    {
        using var document = JsonDocument.Parse(PermissionCatalogueFile.Write());
        var root = document.RootElement;

        Assert.Equal("clinic", root.GetProperty("catalogue").GetString());
        Assert.Equal(11, root.GetProperty("groups").GetArrayLength());
        Assert.Equal(83, root.GetProperty("permissions").GetArrayLength());

        var roles = root.GetProperty("permissions").EnumerateArray().Single(p => p.GetProperty("code").GetString() == "admin.roles.manage");
        Assert.Equal(51, roles.GetProperty("number").GetInt32());
        Assert.Equal("إدارة الأدوار والصلاحيات", roles.GetProperty("nameAr").GetString());
        Assert.Equal("Manage roles & permissions", roles.GetProperty("nameEn").GetString());
        Assert.Equal("admin", roles.GetProperty("group").GetString());
        Assert.Equal("write", roles.GetProperty("kind").GetString());
        Assert.Equal("admin.roles.view", roles.GetProperty("requires").GetString());

        var view = root.GetProperty("permissions").EnumerateArray().First();
        Assert.Equal(JsonValueKind.Null, view.GetProperty("requires").ValueKind);
    }
}
