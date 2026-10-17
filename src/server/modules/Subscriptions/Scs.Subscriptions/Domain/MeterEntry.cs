using Scs.SharedKernel;

namespace Scs.Subscriptions.Domain;

/// <summary>
/// One use of a monthly limit, or one give-back of a use (spec 014, FR-019 to FR-021): the
/// meter is this ledger and nothing else. Never changed or removed; a give-back is a row of its
/// own, negative, in the month of the use it returns.
/// </summary>
public sealed class MeterEntry : IClinicOwned
{
    public const int KeyMaxLength = 60;
    public const int PurposeMaxLength = 100;
    public const int ReferenceMaxLength = 200;
    public const int ByMaxLength = 100;

    private MeterEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public string FeatureKey { get; private set; } = "";

    /// <summary><c>yyyy-MM</c> of the moment, in Cairo.</summary>
    public string Month { get; private set; } = "";

    /// <summary>Positive for a use, negative for a give-back; never 0.</summary>
    public int Amount { get; private set; }

    public DateTimeOffset AtUtc { get; private set; }

    /// <summary>What it was for, as a code: <c>message.sent</c>, <c>teleconsult.call</c>. Never personal text.</summary>
    public string Purpose { get; private set; } = "";

    /// <summary>The consumer's id of the thing used for: a message, a call.</summary>
    public string? Reference { get; private set; }

    /// <summary><c>account:{id}</c>, <c>operator:{id}</c> or <c>job:{name}</c>.</summary>
    public string By { get; private set; } = "";

    /// <summary>A promise completed past the allowance (FR-020).</summary>
    public bool BeyondAllowance { get; private set; }

    /// <summary>For a give-back: the use it returns.</summary>
    public Guid? GivesBack { get; private set; }

    public static MeterEntry Use(
        string featureKey, string month, int amount, DateTimeOffset at, string purpose, string? reference, string by,
        bool beyondAllowance)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A use takes at least one unit.");
        }

        return new MeterEntry
        {
            Id = Guid.NewGuid(),
            FeatureKey = featureKey,
            Month = month,
            Amount = amount,
            AtUtc = at,
            Purpose = Clip(purpose, PurposeMaxLength),
            Reference = reference is null ? null : Clip(reference, ReferenceMaxLength),
            By = Clip(by, ByMaxLength),
            BeyondAllowance = beyondAllowance,
        };
    }

    public static MeterEntry GiveBack(MeterEntry use, int amount, DateTimeOffset at, string by)
    {
        ArgumentNullException.ThrowIfNull(use);
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A give-back returns at least one unit.");
        }

        return new MeterEntry
        {
            Id = Guid.NewGuid(),
            FeatureKey = use.FeatureKey,
            Month = use.Month,
            Amount = -amount,
            AtUtc = at,
            Purpose = use.Purpose,
            Reference = use.Reference,
            By = Clip(by, ByMaxLength),
            GivesBack = use.Id,
        };
    }

    private static string Clip(string value, int length) => value.Length <= length ? value : value[..length];
}
