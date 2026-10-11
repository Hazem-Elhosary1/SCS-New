using NetArchTest.Rules;

namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 008: notifications are written in one place and read by one module, and who people are
/// is asked of Identity through its contracts.
/// </summary>
public class NotificationRulesTests
{
    [Fact]
    public void The_engine_reaches_accounts_only_through_identitys_contracts()
    {
        var notifications = ScsAssemblies.Modules.Single(a => a.GetName().Name == "Scs.Notifications");

        Assert.DoesNotContain(notifications.GetReferencedAssemblies(), reference => reference.Name == "Scs.Identity");
        Assert.Contains(notifications.GetReferencedAssemblies(), reference => reference.Name == "Scs.Identity.Contracts");
    }

    [Fact]
    public void No_module_but_the_engine_uses_its_own_context()
    {
        var others = ScsAssemblies.All.Where(a => a.GetName().Name is not ("Scs.Notifications" or "Scs.Api" or "Scs.Migrator"));

        var result = Types.InAssemblies(others)
            .ShouldNot()
            .HaveDependencyOn("Scs.Notifications.Infrastructure.Persistence.NotificationsDbContext")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Notifications_are_inserted_by_the_shared_writer_alone_and_emails_are_sent_by_the_engines_job_alone()
    {
        var server = FindUp("src", "server");
        var sources = Directory
            .EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(file)
                           && !file.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(file => (File: Path.GetRelativePath(server, file), Text: File.ReadAllText(file)))
            .ToList();

        var writers = sources
            .Where(s => Path.GetFileName(s.File) != "NotificationWriter.cs")
            .Where(s => s.Text.Contains("INSERT INTO [notify]", StringComparison.OrdinalIgnoreCase)
                        || s.Text.Contains("INSERT INTO notify.", StringComparison.OrdinalIgnoreCase)
                        || s.Text.Contains("Notifications.Add(", StringComparison.Ordinal)
                        || s.Text.Contains("Emails.Add(", StringComparison.Ordinal))
            .Select(s => s.File)
            .ToList();

        Assert.True(writers.Count == 0, "Raise through the context's Notify(...) instead: " + string.Join(", ", writers));

        // A module tells people by raising an event; it does not read the notification tables.
        var readers = sources
            .Where(s => !s.File.Contains("Notifications", StringComparison.Ordinal))
            .Where(s => s.Text.Contains("[notify].", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.File)
            .ToList();

        Assert.True(readers.Count == 0, "Only the notifications engine reads its tables: " + string.Join(", ", readers));
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
