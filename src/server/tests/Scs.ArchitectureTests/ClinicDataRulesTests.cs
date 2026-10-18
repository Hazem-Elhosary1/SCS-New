namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 015 (research R3): the branches live in their own module, <c>ClinicData</c>. Other modules
/// reach them only through its contracts, and it reaches no other module's internals.
/// </summary>
public class ClinicDataRulesTests
{
    [Fact]
    public void The_module_is_part_of_the_platform()
    {
        Assert.Contains(ScsAssemblies.Modules, m => m.GetName().Name == "Scs.ClinicData");
    }

    [Fact]
    public void Nothing_but_the_host_references_the_module_s_internals()
    {
        var users = ScsAssemblies.All
            .Where(a => a.GetReferencedAssemblies().Any(r => r.Name == "Scs.ClinicData"))
            .Select(a => a.GetName().Name)
            .ToList();

        Assert.Equal(["Scs.Api"], users);
    }

    [Fact]
    public void The_module_references_only_shared_projects_and_contracts()
    {
        var module = ScsAssemblies.Modules.Single(m => m.GetName().Name == "Scs.ClinicData");
        var strangers = module.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(n => n.StartsWith("Scs.", StringComparison.Ordinal))
            .Where(n => n is not ("Scs.SharedKernel" or "Scs.BuildingBlocks") && !n.EndsWith(".Contracts", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(strangers);
    }
}
