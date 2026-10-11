using NetArchTest.Rules;
using Scs.BuildingBlocks.Files;
using Scs.SharedKernel.Files;

namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 009: files are stored, checked and given out in one place. A module that owns a record
/// attaches its files through its own save and answers who may see them; it does not reach the
/// table, the store or the scanner.
/// </summary>
public class FileRulesTests
{
    /// <summary>
    /// The kinds of the table that no module owns yet, each with the spec that will. A kind with
    /// no owner is refused for everyone, so this list is what is knowingly not usable yet.
    /// </summary>
    private static readonly Dictionary<string, string> WaitingForItsSpec = new(StringComparer.Ordinal)
    {
        [FileKinds.TicketAttachment] = "082 support tickets",
        [FileKinds.PatientFile] = "032 patient files",
        [FileKinds.ConsentSignedCopy] = "the consents spec",
        [FileKinds.OrderResultReport] = "the orders and results spec",
        [FileKinds.DoctorPhoto] = "029 doctors",
    };

    [Fact]
    public void No_module_but_the_engine_uses_its_context_its_store_or_its_scanner()
    {
        var others = ScsAssemblies.All.Where(a => a.GetName().Name is not ("Scs.Files" or "Scs.BuildingBlocks" or "Scs.Api" or "Scs.Migrator"));

        var result = Types.InAssemblies(others)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Scs.Files.Infrastructure",
                "Scs.Files.Domain",
                typeof(IFileStore).FullName!,
                typeof(IMalwareScanner).FullName!)
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_table_of_files_is_written_from_outside_the_engine_by_the_shared_writer_alone()
    {
        var server = FindUp("src", "server");
        var sources = Directory
            .EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file)
                           && !file.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(file => (File: Path.GetRelativePath(server, file), Text: File.ReadAllText(file)))
            .ToList();

        var touching = sources
            .Where(s => !s.File.Contains($"{Path.DirectorySeparatorChar}Files{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(s => s.Text.Contains("[files].", StringComparison.OrdinalIgnoreCase)
                        || s.Text.Contains("FilesSql.Table", StringComparison.Ordinal)
                        || s.Text.Contains("FilesDbContext", StringComparison.Ordinal))
            .Select(s => s.File)
            .ToList();

        Assert.True(touching.Count == 0, "Use AttachFiles, RemoveFile and IRecordFiles instead, in: " + string.Join(", ", touching));
    }

    [Fact]
    public void Every_kind_has_an_owner_or_is_knowingly_waiting_for_its_spec()
    {
        var owned = ScsAssemblies.Modules
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IFileKindOwner).IsAssignableFrom(t))
            .ToList();

        // The one owner that exists today is the builders' own.
        Assert.Equal(["Scs.Files.DevSampleOwner"], owned.Select(t => t.FullName));

        var waiting = FileKinds.All.Where(k => !k.LocalOnly).Select(k => k.Key).Order(StringComparer.Ordinal);
        Assert.Equal(WaitingForItsSpec.Keys.Order(StringComparer.Ordinal), waiting);
        Assert.All(WaitingForItsSpec.Values, spec => Assert.False(string.IsNullOrWhiteSpace(spec)));
    }

    [Fact]
    public void A_private_file_is_given_out_by_the_engines_two_addresses_and_nowhere_else()
    {
        var modules = FindUp("src", "server", "modules");
        var opening = Directory
            .EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Files{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("IFileStore", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(modules, file))
            .ToList();

        Assert.True(opening.Count == 0, "Only the files engine opens file content: " + string.Join(", ", opening));
    }

    private static bool InBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindUp(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(string.Join('/', parts));
    }
}
