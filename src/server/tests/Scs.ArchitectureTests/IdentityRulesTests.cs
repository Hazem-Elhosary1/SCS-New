using NetArchTest.Rules;

namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 003: sign-in reaches the clinic only through its contracts, mail goes through the one
/// shared sender, and the session cookies are read and written in one place.
/// </summary>
public class IdentityRulesTests
{
    [Fact]
    public void Identity_reaches_the_clinic_only_through_its_contracts()
    {
        var identity = ScsAssemblies.Modules.Single(a => a.GetName().Name == "Scs.Identity");

        Assert.DoesNotContain(identity.GetReferencedAssemblies(), reference => reference.Name == "Scs.Tenancy");
        Assert.Contains(identity.GetReferencedAssemblies(), reference => reference.Name == "Scs.Tenancy.Contracts");
    }

    [Fact]
    public void Only_the_shared_project_talks_to_a_mail_server()
    {
        var everythingElse = ScsAssemblies.All.Where(a => a.GetName().Name != "Scs.BuildingBlocks");

        var result = Types.InAssemblies(everythingElse)
            .ShouldNot()
            .HaveDependencyOnAny("MailKit", "MimeKit")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Session_cookies_are_protected_in_one_place()
    {
        var server = FindUp("src", "server");
        var offenders = Directory
            .EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file) && !file.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file =>
            {
                // The link token of spec 004 has its own purpose and its own class; it writes no cookie.
                var name = Path.GetFileName(file);
                var text = File.ReadAllText(file);
                if (name == "SessionCookies.cs")
                {
                    return false;
                }

                if (name == "PasswordLinkTokens.cs")
                {
                    return text.Contains("\"__Host-scs-", StringComparison.Ordinal)
                           || text.Contains("\"__Secure-scs-", StringComparison.Ordinal);
                }

                return text.Contains("CreateProtector(", StringComparison.Ordinal)
                       || text.Contains("\"__Host-scs-", StringComparison.Ordinal)
                       || text.Contains("\"__Secure-scs-", StringComparison.Ordinal);
            })
            .Select(file => Path.GetRelativePath(server, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Use SessionCookies instead, in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Nothing_in_a_module_logs_an_email_address_parameter()
    {
        // A logging method that takes something called "email" is a leak waiting to happen.
        var modules = FindUp("src", "server", "modules");
        var offenders = Directory
            .EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file))
            .Where(file => File.ReadAllLines(file).Any(line =>
                line.Contains("partial void Log", StringComparison.Ordinal)
                && line.Contains("email", StringComparison.OrdinalIgnoreCase)))
            .Select(file => Path.GetRelativePath(modules, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Log an accountId instead, in: " + string.Join(", ", offenders));
    }

    private static bool InBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

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
