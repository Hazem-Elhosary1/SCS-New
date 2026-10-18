using NetArchTest.Rules;
using Scs.BuildingBlocks.Tenancy;

namespace Scs.ArchitectureTests;

/// <summary>
/// The clinic boundary is crossed only by the operations declared in one place
/// (spec 002, FR-025, SC-008). Anything else that reaches for the operator connection, or for
/// the context built on it, fails here by name.
/// </summary>
public class DeclaredCrossingsTests
{
    private const string CrossingContext = "Scs.Tenancy.Infrastructure.Persistence.CrossingDbContext";
    private const string Factory = "Scs.BuildingBlocks.Tenancy.ICrossClinicConnectionFactory";

    /// <summary>Not operations: the context itself, and the module that registers the services.</summary>
    private static readonly string[] Plumbing =
    [
        CrossingContext,
        "Scs.BuildingBlocks.Tenancy.CrossClinicConnectionFactory",
        "Scs.Tenancy.TenancyModule",
    ];

    [Fact]
    public void Only_declared_types_use_the_operator_connection()
    {
        var users = Types.InAssemblies(ScsAssemblies.All)
            .That()
            .HaveDependencyOnAny(CrossingContext, Factory)
            .GetTypes()
            .Select(type => Outermost(type).FullName!)
            .Distinct()
            .Where(name => !Plumbing.Contains(name))
            .Order()
            .ToList();

        var declared = DeclaredCrossings.Types.Order().ToList();

        Assert.Equal(declared, users);
    }

    [Fact]
    public void Every_declared_type_exists()
    {
        var all = ScsAssemblies.All.SelectMany(a => a.GetTypes()).Select(t => t.FullName).ToHashSet();

        Assert.All(DeclaredCrossings.Types, type => Assert.Contains(type, all));
    }

    [Fact]
    public void There_are_twelve_crossings_each_with_a_reason_and_distinct_names()
    {
        // Spec 015 added three: the operators' reads of every clinic's plan and owner, and the
        // writes to a clinic's own record (research R13).
        Assert.Equal(12, DeclaredCrossings.All.Count);
        Assert.Equal(12, DeclaredCrossings.All.Select(c => c.Operation).Distinct().Count());
        Assert.All(DeclaredCrossings.All, c => Assert.False(string.IsNullOrWhiteSpace(c.Reason)));
        Assert.All(DeclaredCrossings.All, c => Assert.Matches("^[a-z]+(-[a-z]+)*$", c.Operation));
    }

    [Fact]
    public void Operations_a_person_performs_are_the_recorded_ones()
    {
        var recorded = DeclaredCrossings.All.Where(c => c.Recorded).Select(c => c.Operation).Order();

        Assert.Equal(["list-clinics", "register-clinic", "rename-clinic", "update-clinic"], recorded);
    }

    // Compiler-generated helpers (lambdas, async state machines) are nested in the type that wrote them.
    private static Type Outermost(Type type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }
}
