using NetArchTest.Rules;

namespace Scs.ArchitectureTests;

/// <summary>
/// Layer direction inside each module (constitution, Technology &amp; Platform Constraints):
/// dependencies point inward, and the domain references no framework, database or web type.
/// </summary>
public class LayeringTests
{
    public static TheoryData<string> ModuleNames =>
        new(ScsAssemblies.Modules.Select(ScsAssemblies.ModuleName));

    [Fact]
    public void Shared_kernel_depends_on_nothing_of_ours_and_no_framework()
    {
        var result = Types.InAssembly(typeof(SharedKernel.ErrorCodes).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Scs.BuildingBlocks", "Scs.Api", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_depends_only_on_the_shared_kernel(string module)
    {
        var result = InLayer(module, "Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                $"Scs.{module}.Application",
                $"Scs.{module}.Infrastructure",
                $"Scs.{module}.Api",
                "Scs.BuildingBlocks",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Hangfire",
                "Serilog")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_does_not_depend_on_infrastructure_or_api(string module)
    {
        var result = InLayer(module, "Application")
            .ShouldNot()
            .HaveDependencyOnAny($"Scs.{module}.Infrastructure", $"Scs.{module}.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Infrastructure_does_not_depend_on_api(string module)
    {
        var result = InLayer(module, "Infrastructure")
            .ShouldNot()
            .HaveDependencyOn($"Scs.{module}.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static PredicateList InLayer(string module, string layer) =>
        Types.InAssemblies(ScsAssemblies.Modules)
            .That()
            .ResideInNamespaceStartingWith($"Scs.{module}.{layer}");

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? ""
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
