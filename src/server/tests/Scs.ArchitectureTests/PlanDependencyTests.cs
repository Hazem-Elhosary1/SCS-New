namespace Scs.ArchitectureTests;

/// <summary>
/// Spec 014 (plan: «Plans never reads a clinic»): the clinic depends on the plan, never the other
/// way — the plans' module references neither the clinics' contracts nor the subscriptions', and
/// what a clinic has is worked out in the subscriptions' module alone.
/// </summary>
public class PlanDependencyTests
{
    [Fact]
    public void The_plans_module_reads_no_clinic_and_no_subscription()
    {
        var plans = ScsAssemblies.All.Where(a => a.GetName().Name is "Scs.Plans" or "Scs.Plans.Contracts").ToList();
        Assert.Equal(2, plans.Count);

        var violations =
            from assembly in plans
            from reference in assembly.GetReferencedAssemblies()
            where reference.Name!.StartsWith("Scs.Tenancy", StringComparison.Ordinal)
                  || reference.Name.StartsWith("Scs.Subscriptions", StringComparison.Ordinal)
            select $"{assembly.GetName().Name} -> {reference.Name}";

        Assert.Empty(violations);
    }
}
