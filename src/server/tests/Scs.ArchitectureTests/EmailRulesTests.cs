using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using Scs.BuildingBlocks.Mail;

namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 094: an email is words given to the one template. Every email a module can build is in
/// the catalogue in both languages (FR-039); only the template writes markup; no module writes
/// the platform's name as words of its own (FR-025); and the catalogue is for looking at, not
/// for anything that runs (FR-037).
/// </summary>
public class EmailRulesTests
{
    private const string Samples = "Scs.BuildingBlocks.Mail.IEmailSamples";
    private const string Sample = "Scs.BuildingBlocks.Mail.EmailSample";

    private static readonly string[] Markup = ["<html", "<table", "<td", "<body", "<div", "<p>", "<p ", "<a href"];
    private static readonly string[] PlatformNames = ["Smart Clinic OS", "سمارت كلينك", "SmartClinicOS"];

    private static bool IsSamples(Type type) =>
        typeof(IEmailSamples).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false };

    private static bool IsGenerated(MemberInfo member) =>
        member.Name.Contains('<', StringComparison.Ordinal) || member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    /// <summary>Every method of a module that builds an email, by its type and name.</summary>
    private static List<(Assembly Module, string Type, string Kind)> Builders() =>
    [
        .. from module in ScsAssemblies.Modules
           from type in module.GetTypes()
           where !IsSamples(type) && !IsGenerated(type) && !(type.DeclaringType is { } outer && IsSamples(outer))
           from method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
           where method.ReturnType == typeof(EmailContent) && !IsGenerated(method)
           select (module, type.Name, method.Name),
    ];

    private static List<EmailSample> SamplesOf(Assembly module) =>
    [
        .. module.GetTypes().Where(IsSamples)
            .SelectMany(type => ((IEmailSamples)Activator.CreateInstance(type, nonPublic: true)!).All()),
    ];

    [Fact]
    public void Every_email_a_module_builds_is_in_the_catalogue_in_arabic_and_in_english()
    {
        var builders = Builders();

        // The rule means something only while it finds the emails that exist today.
        Assert.True(builders.Count >= 11, $"Only {builders.Count} emails were found.");
        Assert.Equal(3, builders.Select(b => b.Module).Distinct().Count());

        var missing = new List<string>();
        foreach (var group in builders.GroupBy(b => b.Module))
        {
            var samples = SamplesOf(group.Key);
            foreach (var (_, type, kind) in group)
            {
                foreach (var language in new[] { "ar", "en" })
                {
                    if (!samples.Any(s => s.Kind == kind && s.Language == language))
                    {
                        missing.Add($"{ScsAssemblies.ModuleName(group.Key)}: {type}.{kind} ({language})");
                    }
                }
            }
        }

        Assert.True(missing.Count == 0, "Emails with no sample in the catalogue: " + string.Join("; ", missing));
    }

    [Fact]
    public void A_sample_is_of_an_email_that_exists_and_no_two_share_a_page()
    {
        var kinds = Builders().Select(b => (b.Module, b.Kind)).ToHashSet();
        var all = ScsAssemblies.Modules.SelectMany(module => SamplesOf(module).Select(sample => (Module: module, Sample: sample))).ToList();

        Assert.All(all, entry => Assert.Contains((entry.Module, entry.Sample.Kind), kinds));
        Assert.All(all, entry => Assert.Contains(entry.Sample.Audience, new[] { EmailSample.Clinic, EmailSample.Operators }));
        Assert.Equal(all.Count, all.Select(entry => entry.Sample.Slug).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Only_the_template_writes_an_email_s_markup()
    {
        var offenders = Sources()
            .Where(file => Path.GetFileName(file) != "EmailRenderer.cs")
            .Where(file => Path.GetFileName(file).Contains("Email", StringComparison.Ordinal)
                           || file.Contains($"{Path.DirectorySeparatorChar}Mail{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Markup.Any(mark => File.ReadAllText(file).Contains(mark, StringComparison.OrdinalIgnoreCase)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "Email markup outside the template: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_module_writes_the_platform_s_name_as_its_own_words()
    {
        var modules = Path.Combine(FindUp("src", "server"), "modules");
        var offenders = Sources()
            .Where(file => file.StartsWith(modules, StringComparison.Ordinal))
            .Where(file => PlatformNames.Any(name => File.ReadAllText(file).Contains(name, StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "The platform's name as fixed words: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Nothing_that_runs_reads_the_catalogue()
    {
        // The hosts, the shared code and the contracts never touch it; in a module, only the
        // class that provides its samples does.
        var result = Types.InAssemblies(ScsAssemblies.All)
            .That()
            .DoNotImplementInterface(typeof(IEmailSamples))
            .And()
            .DoNotHaveNameMatching("^(EmailSample|IEmailSamples)$")
            .ShouldNot()
            .HaveDependencyOnAny(Samples, Sample)
            .GetResult();

        var offenders = (result.FailingTypes ?? [])
            .Where(type => !(type.DeclaringType is { } outer && IsSamples(outer)))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, "Offending types: " + string.Join(", ", offenders));
    }

    private static IEnumerable<string> Sources()
    {
        var server = FindUp("src", "server");
        var separator = Path.DirectorySeparatorChar;
        return Directory
            .EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                           && !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
                           && !file.Contains($"{separator}tests{separator}", StringComparison.Ordinal));
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

        throw new DirectoryNotFoundException(string.Join('/', parts));
    }
}
