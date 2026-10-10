namespace Scs.ArchitectureTests;

/// <summary>
/// A module reaches another module only through that module's contracts project
/// (constitution, Technology &amp; Platform Constraints).
/// </summary>
public class ModuleBoundaryTests
{
    [Fact]
    public void No_module_references_another_modules_internals()
    {
        var internals = ScsAssemblies.Modules.Select(m => m.GetName().Name!).ToHashSet(StringComparer.Ordinal);

        var violations =
            from module in ScsAssemblies.All
            where module.GetName().Name != "Scs.Api"
            from reference in module.GetReferencedAssemblies()
            where internals.Contains(reference.Name!) && !BelongsTo(module.GetName().Name!, reference.Name!)
            select $"{module.GetName().Name} -> {reference.Name}";

        Assert.Empty(violations);
    }

    [Fact]
    public void Contracts_reference_no_module_internals_and_no_infrastructure()
    {
        var violations =
            from contracts in ScsAssemblies.All.Where(ScsAssemblies.IsContracts)
            from reference in contracts.GetReferencedAssemblies()
            where reference.Name!.StartsWith("Scs.", StringComparison.Ordinal)
                  && reference.Name != "Scs.SharedKernel"
                  && !reference.Name.EndsWith(".Contracts", StringComparison.Ordinal)
            select $"{contracts.GetName().Name} -> {reference.Name}";

        Assert.Empty(violations);
    }

    [Fact]
    public void Shared_projects_reference_no_module()
    {
        var violations =
            from shared in ScsAssemblies.All
            where shared.GetName().Name is "Scs.SharedKernel" or "Scs.BuildingBlocks"
            from reference in shared.GetReferencedAssemblies()
            where reference.Name!.StartsWith("Scs.", StringComparison.Ordinal)
                  && reference.Name is not ("Scs.SharedKernel" or "Scs.BuildingBlocks")
            select $"{shared.GetName().Name} -> {reference.Name}";

        Assert.Empty(violations);
    }

    // Scs.Platform.Contracts belongs to Scs.Platform; a module may reference itself.
    private static bool BelongsTo(string referencing, string module) =>
        referencing == module || referencing == module + ".Contracts";
}
