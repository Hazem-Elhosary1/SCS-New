using NetArchTest.Rules;

namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 007: the trail takes the actor itself, so nothing may hand it one — except sign-in,
/// where no one is signed in yet (FR-014, FR-014a) — and the trail's table is written in one place.
/// </summary>
public class AuditRulesTests
{
    private const string SignInAudit = "Scs.BuildingBlocks.Audit.SignInAudit";

    [Fact]
    public void Only_sign_in_records_for_someone_who_is_not_signed_in()
    {
        var everythingElse = ScsAssemblies.All.Where(a => a.GetName().Name is not ("Scs.Identity" or "Scs.BuildingBlocks"));

        var result = Types.InAssemblies(everythingElse)
            .ShouldNot()
            .HaveDependencyOn(SignInAudit)
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void No_module_says_who_the_actor_of_an_entry_is()
    {
        // The scope a job or a test names its person with. On a request it is not read at all;
        // off one, no module may set it either (FR-014).
        var result = Types.InAssemblies(ScsAssemblies.All.Where(a => a.GetName().Name != "Scs.BuildingBlocks"))
            .ShouldNot()
            .HaveDependencyOn("Scs.BuildingBlocks.Audit.AuditActorScope")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Sign_in_does_use_it()
    {
        var identity = ScsAssemblies.Modules.Single(a => a.GetName().Name == "Scs.Identity");

        // The rule above means something only while the type keeps its name.
        Assert.NotEmpty(Types.InAssembly(identity).That().HaveDependencyOn(SignInAudit).GetTypes());
    }

    [Fact]
    public void The_trail_reaches_accounts_only_through_identitys_contracts()
    {
        var audit = ScsAssemblies.Modules.Single(a => a.GetName().Name == "Scs.Audit");

        Assert.DoesNotContain(audit.GetReferencedAssemblies(), reference => reference.Name == "Scs.Identity");
        Assert.Contains(audit.GetReferencedAssemblies(), reference => reference.Name == "Scs.Identity.Contracts");
    }

    [Fact]
    public void Entries_are_inserted_by_the_shared_writer_alone()
    {
        var server = FindUp("src", "server");
        var offenders = Directory
            .EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file)
                           && !file.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && Path.GetFileName(file) != "AuditWriter.cs")
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("INSERT INTO [audit]", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("INSERT INTO audit.", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("Entries.Add(", StringComparison.Ordinal);
            })
            .Select(file => Path.GetRelativePath(server, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Record through the context's Record(...) instead: " + string.Join(", ", offenders));
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
