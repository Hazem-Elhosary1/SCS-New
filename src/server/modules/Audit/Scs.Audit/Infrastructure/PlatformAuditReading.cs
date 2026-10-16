using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scs.Audit.Contracts;
using Scs.Audit.Infrastructure.Persistence;

namespace Scs.Audit.Infrastructure;

/// <summary>
/// Reads the platform's own trail (spec 011). It establishes no clinic and needs none: the
/// table has none. Only the operators' side asks.
/// </summary>
internal sealed class PlatformAuditReading(AuditDbContext db) : IPlatformAuditReading
{
    public const int MaxTake = 50;

    public async Task<IReadOnlyList<PlatformActivity>> LastByActorAsync(
        Guid operatorId, int take, CancellationToken cancellation)
    {
        var rows = await db.PlatformEntries.AsNoTracking()
            .Where(entry => entry.ActorAccountId == operatorId)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Take(Math.Clamp(take, 1, MaxTake))
            .Select(entry => new
            {
                entry.Id, entry.OccurredAtUtc, entry.ActionCode, entry.TargetKind, entry.TargetId, entry.TargetName,
                entry.Facts, entry.Changes,
            })
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row =>
            {
                var (after, afterKey) = AfterOf(row.Changes);
                return new PlatformActivity(
                    row.Id, row.OccurredAtUtc, row.ActionCode, row.TargetKind, row.TargetId, row.TargetName,
                    FactsOf(row.Facts), after, afterKey);
            }),
        ];
    }

    /// <summary>The facts that are short texts; a number or a yes/no is written as its word.</summary>
    private static Dictionary<string, string> FactsOf(string? json)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(json))
        {
            return facts;
        }

        using var document = JsonDocument.Parse(json);
        foreach (var fact in document.RootElement.EnumerateObject())
        {
            if (fact.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                facts[fact.Name] = fact.Value.ValueKind == JsonValueKind.String ? fact.Value.GetString() ?? "" : fact.Value.GetRawText();
            }
        }

        return facts;
    }

    /// <summary>What the first single-value change became: for a change of role, the new role's name.</summary>
    private static (string? After, string? AfterKey) AfterOf(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (null, null);
        }

        using var document = JsonDocument.Parse(json);
        foreach (var change in document.RootElement.EnumerateArray())
        {
            var after = change.TryGetProperty("after", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var key = change.TryGetProperty("afterKey", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
            if (after is not null || key is not null)
            {
                return (after, key);
            }
        }

        return (null, null);
    }
}
