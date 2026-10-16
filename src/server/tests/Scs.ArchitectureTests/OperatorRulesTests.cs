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
            .HaveDependencyOnAny(
                "Scs.Identity", "Scs.Tenancy", "Scs.Notifications", "Scs.Files", "Scs.Platform",

                // The trail is read through its contracts (spec 011: an operator's last actions), and no deeper.
                "Scs.Audit.Domain", "Scs.Audit.Infrastructure", "Scs.Audit.Api", "Scs.Audit.AuditModule")
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

    /// <summary>
    /// Spec 011 (FR-011): «مسؤول عام» is the role with the system key, and nothing else. No code
    /// decides it by the role's name, or by counting what a role holds — a custom role given
    /// every permission is not one.
    /// </summary>
    [Fact]
    public void Who_is_a_super_admin_is_decided_by_the_system_key_alone()
    {
        var server = FindUp("src", "server");
        string[] forbidden =
        [
            "\"super-admin\"", "\"Super Admin\"", "\"مسؤول عام\"",
            "OperatorPermissions.All.Count", "Codes.Count == 28", "Codes.Count >= 28",
        ];

        var offenders = Directory
            .EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}Scs.SharedKernel{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}tools{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return forbidden.Any(piece => text.Contains(piece, StringComparison.Ordinal));
            })
            .Select(file => Path.GetRelativePath(server, file))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Ask the role's system key (OperatorPermissions.SystemRoles.SuperAdmin) instead, in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_roles_tables_and_the_platform_s_trail_belong_to_no_clinic_with_their_reasons()
    {
        foreach (var table in new[] { "ops.Role", "ops.RolePermission", "audit.PlatformEntry" })
        {
            var (_, reason) = PlatformWideTables.All.Single(entry => entry.Pattern == table);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
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
