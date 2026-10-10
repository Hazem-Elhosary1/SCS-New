using NetArchTest.Rules;

namespace Scs.ArchitectureTests;

/// <summary>
/// Everything is logged through <c>ILogger</c>, so everything passes the redaction layers
/// (FR-021). Writing to the console or to a logging sink directly would go around them.
/// </summary>
public class LoggingRulesTests
{
    [Fact]
    public void Nothing_writes_to_the_console_directly()
    {
        var result = Types.InAssemblies(ScsAssemblies.All)
            .ShouldNot()
            .HaveDependencyOn("System.Console")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Modules_do_not_use_the_logging_library_directly()
    {
        var modulesAndContracts = ScsAssemblies.All
            .Where(a => a.GetName().Name is not ("Scs.Api" or "Scs.BuildingBlocks" or "Scs.SharedKernel"));

        var result = Types.InAssemblies(modulesAndContracts)
            .ShouldNot()
            .HaveDependencyOnAny("Serilog", "Hangfire")
            .GetResult();

        Assert.True(result.IsSuccessful, "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
