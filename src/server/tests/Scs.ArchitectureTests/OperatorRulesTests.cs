using NetArchTest.Rules;
using Scs.BuildingBlocks.Tenancy;

namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 010: the people who run the platform are kept apart from every clinic. The Operators
/// module is reached through its contracts alone, reaches into no other module, and each of
/// its tables is a reviewed exception to "every table belongs to a clinic".
/// </summary>
public class OperatorRulesTests
{
    private static readonly string[] Tables =
        ["Operator", "SignInChallenge", "Session", "PasswordLink", "KnownBrowser", "SignInRecord"];

    [Fact]
    public void No_module_reaches_into_the_operators_module()
    {
        var others = ScsAssemblies.All.Where(a => a.GetName().Name is not ("Scs.Operators" or "Scs.Api" or "Scs.Migrator"));

        var result = Types.InAssemblies(others)
            .ShouldNot()
            .HaveDependencyOnAny("Scs.Operators.Infrastructure", "Scs.Operators.Domain", "Scs.Operators.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_operators_module_reaches_into_no_other_module()
    {
        var result = Types.InAssembly(typeof(Scs.Operators.OperatorsModule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Scs.Identity", "Scs.Tenancy", "Scs.Audit", "Scs.Notifications", "Scs.Files", "Scs.Platform")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Each_table_of_the_operators_is_listed_as_belonging_to_no_clinic_with_its_reason()
    {
        foreach (var table in Tables)
        {
            Assert.True(PlatformWideTables.Contains("ops", table), $"ops.{table} is not in the platform-wide list.");
            var (_, reason) = PlatformWideTables.All.Single(entry => entry.Pattern == $"ops.{table}");
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }

        // Named one by one: a table added to the schema later is not let through by a wildcard.
        Assert.DoesNotContain(PlatformWideTables.All, entry => entry.Pattern == "ops.*");
    }

    [Fact]
    public void The_operators_cookies_are_read_in_one_place()
    {
        var server = FindUp("src", "server");
        var offenders = Directory
            .EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && Path.GetFileName(file) != "OperatorCookies.cs")
            .Where(file => File.ReadAllText(file).Contains("scs-ops-", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(server, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Use OperatorCookies instead, in: " + string.Join(", ", offenders));
    }

    private static string FindUp(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(string.Join('/', parts));
    }
}
