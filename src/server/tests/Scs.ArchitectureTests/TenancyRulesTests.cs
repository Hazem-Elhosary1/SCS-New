using NetArchTest.Rules;

namespace Scs.ArchitectureTests;

/// <summary>
/// A module never opens its own database connection and never tells the database which clinic
/// it works for; both are done once, in the shared project
/// (specs/002-clinic-tenancy-isolation/contracts/isolation.md §2 and §6).
/// </summary>
public class TenancyRulesTests
{
    [Fact]
    public void Only_the_shared_project_opens_database_connections()
    {
        var everythingElse = ScsAssemblies.All.Where(a => a.GetName().Name != "Scs.BuildingBlocks");

        var result = Types.InAssemblies(everythingElse)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Data.SqlClient")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void No_module_sets_the_clinic_on_a_connection_itself()
    {
        var modules = FindUp("src", "server", "modules");
        var offenders = Directory
            .EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("sp_set_session_context", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("SESSION_CONTEXT", StringComparison.Ordinal)
                       || text.Contains("CREATE SECURITY POLICY", StringComparison.OrdinalIgnoreCase);
            })
            .Select(file => Path.GetRelativePath(modules, file))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Use migrationBuilder.CreateClinicIsolation and IClinicContext instead, in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Clinic_owned_entities_live_in_a_domain_namespace()
    {
        var result = Types.InAssemblies(ScsAssemblies.Modules)
            .That()
            .ImplementInterface(typeof(SharedKernel.IClinicOwned))
            .Should()
            .ResideInNamespaceMatching(@"^Scs\.\w+\.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

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

        throw new DirectoryNotFoundException(string.Join('/', parts) + " was not found above the test directory.");
    }
}
