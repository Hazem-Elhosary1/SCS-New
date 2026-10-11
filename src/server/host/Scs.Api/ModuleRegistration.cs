using Scs.BuildingBlocks.Modules;

namespace Scs.Api;

/// <summary>
/// The modules that make up the platform. A new module is added here and nowhere else in the
/// host (see docs/adding-a-module.md).
/// </summary>
internal static class ModuleRegistration
{
    public static IReadOnlyList<IModule> Modules { get; } =
    [
        new Scs.Tenancy.TenancyModule(),
        new Scs.Audit.AuditModule(),
        new Scs.Identity.IdentityModule(),
        new Scs.Platform.PlatformModule(),
    ];
}
