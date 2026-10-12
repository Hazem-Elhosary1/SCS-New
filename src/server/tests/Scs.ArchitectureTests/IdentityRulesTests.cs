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
                // The link token of spec 004, and the way in to a file of spec 009, each have
                // their own purpose and their own class; neither writes a cookie.
                var name = Path.GetFileName(file);
                var text = File.ReadAllText(file);
                // The clinic's cookies, and the operators' own (spec 010): each in its one file.
                if (name is "SessionCookies.cs" or "OperatorCookies.cs")
                {
                    return false;
                }

                if (name is "PasswordLinkTokens.cs" or "FileDownloads.cs")
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

    [Fact]
    public void Only_the_shared_gate_refuses_for_a_missing_permission()
    {
        // Spec 005: the permission is checked in one place, before the handler. A module that
        // answers PERMISSION_REQUIRED itself has put the decision where it can be forgotten.
        var modules = FindUp("src", "server", "modules");
        var offenders = Directory
            .EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file))
            .Where(file => File.ReadAllText(file).Contains("ErrorCodes.PermissionRequired", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(modules, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Declare the route with Requires(...) instead, in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Roles_are_reached_through_the_identity_contracts_only()
    {
        var others = ScsAssemblies.Modules.Where(a => a.GetName().Name != "Scs.Identity");

        var result = Types.InAssemblies(others)
            .ShouldNot()
            .HaveDependencyOnAny("Scs.Identity.Domain", "Scs.Identity.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Use IClinicRoles and IStaffAccess instead, in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Open_to_any_signed_in_person_is_declared_only_where_accounts_live()
    {
        // OwnAccountOnly() is for what a person does to their own account (spec 005, FR-021):
        // a module that is not Identity has no such route. The one exception is a person's own
        // bell (spec 008, FR-020): the four routes of access-engine.md §1, in one file. And the
        // files engine's addresses (spec 009): who may is decided by the kind's owner, the
        // uploader's identity and the token, which no single permission can say.
        var modules = FindUp("src", "server", "modules");
        var offenders = Directory
            .EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Identity{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.EndsWith(
                Path.Combine("Notifications", "Scs.Notifications", "Api", "NotificationEndpoints.cs"), StringComparison.Ordinal))
            .Where(file => !file.EndsWith(Path.Combine("Files", "Scs.Files", "Api", "FileEndpoints.cs"), StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains(".OwnAccountOnly()", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(modules, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Name the permission the route needs instead, in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Nothing_decides_by_the_name_of_a_role()
    {
        // A role is a set of permissions and nothing more (SH-050, ق-30): code asks for a
        // permission, never for «مالك العيادة» or "owner". The roles code itself is the exception.
        var modules = FindUp("src", "server", "modules");
        var offenders = Directory
            .EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file) && !file.Contains("Migrations", StringComparison.Ordinal))
            .Where(file => Path.GetFileName(file) is not ("ClinicRoles.cs" or "Role.cs" or "IdentityModule.cs" or "IdentityDbContext.cs"))
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("ReadyRoleKeys.", StringComparison.Ordinal)
                       || text.Contains("RoleName ==", StringComparison.Ordinal)
                       || text.Contains("HoldsEverything", StringComparison.Ordinal);
            })
            .Select(file => Path.GetRelativePath(modules, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Ask IStaffAccess for a permission instead, in: " + string.Join(", ", offenders));
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
