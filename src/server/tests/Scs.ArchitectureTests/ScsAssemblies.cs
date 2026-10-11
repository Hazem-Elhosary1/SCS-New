using System.Reflection;

namespace Scs.ArchitectureTests;

/// <summary>Finds the platform's assemblies, starting from the host.</summary>
internal static class ScsAssemblies
{
    private static readonly string[] NotModules = ["Scs.Api", "Scs.SharedKernel", "Scs.BuildingBlocks", "Scs.Migrator"];

    public static IReadOnlyList<Assembly> All { get; } = Load();

    /// <summary>Internal module assemblies (<c>Scs.Platform</c>), without their contracts.</summary>
    public static IReadOnlyList<Assembly> Modules { get; } = All
        .Where(a => !NotModules.Contains(a.GetName().Name) && !IsContracts(a))
        .ToList();

    public static bool IsContracts(Assembly assembly) =>
        assembly.GetName().Name!.EndsWith(".Contracts", StringComparison.Ordinal);

    public static string ModuleName(Assembly module) => module.GetName().Name!["Scs.".Length..];

    private static List<Assembly> Load()
    {
        var found = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>([typeof(Program).Assembly]);

        while (queue.TryDequeue(out var assembly))
        {
            if (!found.TryAdd(assembly.GetName().Name!, assembly))
            {
                continue;
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name!.StartsWith("Scs.", StringComparison.Ordinal))
                {
                    queue.Enqueue(Assembly.Load(reference));
                }
            }
        }

        return [.. found.Values];
    }
}
