namespace Scs.Subscriptions.Contracts;

/// <summary>
/// The four monthly limits, metered per clinic and per month in Cairo (spec 014, SH-018): every
/// use takes from this month's allowance and a use it cannot cover is refused. Only this
/// contract writes the meter; consumers (061, the teleconsult, x-ray and dictation specs, 021)
/// decide what one use is.
/// </summary>
public interface IMonthlyMeter
{
    /// <summary>
    /// Takes from this month's allowance. Refused with <c>PLAN_LIMIT_REACHED</c> (the feature and
    /// the allowance named) when what is left cannot cover it — unless the use completes what was
    /// already promised, which is taken beyond the allowance and marked so (FR-020).
    /// </summary>
    /// <returns>The use's id, to give it back later.</returns>
    Task<Guid> UseAsync(MeterUse use, CancellationToken cancellation);

    /// <summary>Returns part or all of a use, in the use's month; beyond what is left of it, <c>METER_GIVE_BACK_TOO_MUCH</c>.</summary>
    Task GiveBackAsync(Guid useId, int amount, CancellationToken cancellation);

    /// <param name="month"><c>yyyy-MM</c>; null for this month.</param>
    Task<MeterReading> ReadAsync(string featureKey, string? month, CancellationToken cancellation);
}

/// <param name="Purpose">What it is for, as a code (<c>message.sent</c>); never personal text.</param>
/// <param name="Reference">The consumer's id of what was used for.</param>
/// <param name="CompletesPromise">Something already promised — a booked call running past the minutes — that must finish.</param>
public sealed record MeterUse(string FeatureKey, int Amount, string Purpose, string? Reference = null, bool CompletesPromise = false);

/// <param name="Allowance">This month's allowance; null for no limit.</param>
/// <param name="RenewsOn">The 1st of the month after <paramref name="Month"/>, in Cairo.</param>
public sealed record MeterReading(string FeatureKey, string Month, int? Allowance, int Used, int Left, int Overrun, DateOnly RenewsOn);
