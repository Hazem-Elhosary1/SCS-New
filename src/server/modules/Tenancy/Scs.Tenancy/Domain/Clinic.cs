using Scs.SharedKernel;
using Scs.SharedKernel.Clinics;

namespace Scs.Tenancy.Domain;

/// <summary>
/// The owner of data: everything recorded for a clinic refers to it. Its own row is isolated
/// like any other clinic-owned row, which is why it carries its id twice — <see cref="Id"/> as
/// the key and <see cref="ClinicId"/> as the column isolation is applied on; the two are always
/// equal. The application can read a clinic's row but never write it: clinics are registered
/// and changed only through the operator operations.
/// </summary>
public sealed class Clinic : IClinicOwned
{
    public const int NameMaxLength = ClinicFields.NameMaxLength;
    public const int StateMaxLength = 20;
    public const int SpecialtyMaxLength = 40;
    public const int AddressMaxLength = ClinicFields.AddressMaxLength;
    public const int TaxNumberLength = 11;
    public const int CreatedByMaxLength = 100;

    /// <summary>Registered by spec 015's add and not yet complete: seen by nobody (research R2).</summary>
    public const string SettingUp = "setting-up";

    /// <summary>An add that failed part-way and was undone: seen by nobody, its name never reused.</summary>
    public const string Discarded = "discarded";

    public const string Active = "active";

    private Clinic()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    /// <summary>Display name in Arabic.</summary>
    public string NameAr { get; private set; } = "";

    /// <summary>Display name in English; the Arabic when none was given at creation (spec 015).</summary>
    public string NameEn { get; private set; } = "";

    /// <summary>Stored value; the states and what each permits belong to spec 016.</summary>
    public string State { get; private set; } = Active;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// The clinic's specialty, one of the fifteen of SH-013, set at creation (spec 015); null for a
    /// clinic that predates it, or «غير مسجّل» chosen in editing. A feature that follows a specialty
    /// is off for a clinic without that one (spec 014, ق-٣٧).
    /// </summary>
    public string? Specialty { get; private set; }

    /// <summary>Required at creation; null for the clinics seeded before spec 015.</summary>
    public string? Address { get; private set; }

    /// <summary><c>NNN-NNN-NNN</c>, or none (BRD Ops §3-1).</summary>
    public string? TaxNumber { get; private set; }

    /// <summary><c>ar</c> or <c>en</c>; Arabic at creation, changed in editing.</summary>
    public string DefaultLanguage { get; private set; } = "ar";

    /// <summary>The operator who looks after the clinic («المسؤول من SCS»), or none.</summary>
    public Guid? AccountManagerId { get; private set; }

    /// <summary>The operator who added it; <c>tool:seed</c> for the seed.</summary>
    public string? CreatedBy { get; private set; }

    /// <summary>A save made from an older read is refused rather than lost.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Builds a clinic to be registered without a profile (the seed and spec 002's tests). Returns
    /// the field codes of what is wrong (<c>REQUIRED</c>, <c>TOO_LONG</c>) instead of a clinic.
    /// </summary>
    public static (Clinic? Clinic, IReadOnlyDictionary<string, string[]> Errors) Create(
        Guid id, string? nameAr, string? nameEn, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var arabic = Name(nameAr, "nameAr", required: true, errors);
        var english = Name(nameEn, "nameEn", required: true, errors);

        if (id == Guid.Empty)
        {
            errors["id"] = [ClinicFields.Required];
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new Clinic
        {
            Id = id,
            ClinicId = id,
            NameAr = arabic,
            NameEn = english,
            State = Active,
            CreatedAtUtc = now.ToUniversalTime(),
        }, errors);
    }

    /// <summary>
    /// Builds a clinic as an operator adds it (spec 015, FR-004): in <see cref="SettingUp"/> until
    /// everything else of the add is done, Arabic as its language, the English name falling back to
    /// the Arabic. Returns the field codes of what is wrong instead of a clinic.
    /// </summary>
    public static (Clinic? Clinic, IReadOnlyDictionary<string, string[]> Errors) Register(
        Guid id, string? nameAr, string? nameEn, string? specialty, string? address, string? taxNumber, string createdBy,
        DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var arabic = Name(nameAr, "nameAr", required: true, errors);
        var english = Name(nameEn, "nameEn", required: false, errors);
        var place = Text(address, "address", AddressMaxLength, errors);

        if (!Specialties.IsKnown(specialty))
        {
            errors["specialty"] = [string.IsNullOrWhiteSpace(specialty) ? ClinicFields.Required : ClinicFields.Invalid];
        }

        if (!ClinicFields.TryTaxNumber(taxNumber, out var tax))
        {
            errors["taxNumber"] = [ClinicFields.TaxNumberCode];
        }

        if (id == Guid.Empty)
        {
            errors["id"] = [ClinicFields.Required];
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new Clinic
        {
            Id = id,
            ClinicId = id,
            NameAr = arabic,
            NameEn = english.Length == 0 ? arabic : english,
            State = SettingUp,
            CreatedAtUtc = now.ToUniversalTime(),
            Specialty = specialty,
            Address = place,
            TaxNumber = tax,
            DefaultLanguage = "ar",
            CreatedBy = createdBy.Trim(),
        }, errors);
    }

    /// <summary>
    /// Applies an operator's edit (spec 015, FR-020 – FR-022) and returns each field that changed,
    /// with its value before and after; nothing changed returns none. A field of the edit that is
    /// not acceptable comes back in <paramref name="errors"/> and nothing is applied.
    /// </summary>
    public IReadOnlyList<(string Field, string? Before, string? After)> Edit(
        string? nameAr, string? nameEn, string? address, TaxNumberEdit taxNumber, string? specialty, string? defaultLanguage,
        Dictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var arabic = Name(nameAr, "nameAr", required: true, errors);
        var english = Name(nameEn, "nameEn", required: false, errors);
        var place = Text(address, "address", AddressMaxLength, errors);

        if (specialty is not null && !Specialties.IsKnown(specialty))
        {
            errors["specialty"] = [ClinicFields.Invalid];
        }

        if (defaultLanguage is null || !ClinicFields.Languages.Contains(defaultLanguage, StringComparer.Ordinal))
        {
            errors["defaultLanguage"] = [ClinicFields.Invalid];
        }

        var tax = TaxNumber;
        if (taxNumber.Sent)
        {
            // A stored number sent back as it is stays, even if it predates the rule (FR-022).
            if (string.Equals(taxNumber.Value?.Trim(), TaxNumber, StringComparison.Ordinal))
            {
                tax = TaxNumber;
            }
            else if (ClinicFields.TryTaxNumber(taxNumber.Value, out var kept))
            {
                tax = kept;
            }
            else
            {
                errors["taxNumber"] = [ClinicFields.TaxNumberCode];
            }
        }

        if (errors.Count > 0)
        {
            return [];
        }

        var changes = new List<(string Field, string? Before, string? After)>();
        void Set(string field, string? before, string? after, Action apply)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                changes.Add((field, before, after));
                apply();
            }
        }

        var englishOrArabic = english.Length == 0 ? arabic : english;
        Set("nameAr", NameAr, arabic, () => NameAr = arabic);
        Set("nameEn", NameEn, englishOrArabic, () => NameEn = englishOrArabic);
        Set("address", Address, place, () => Address = place);
        Set("taxNumber", TaxNumber, tax, () => TaxNumber = tax);
        Set("specialty", Specialty, specialty, () => Specialty = specialty);
        Set("defaultLanguage", DefaultLanguage, defaultLanguage, () => DefaultLanguage = defaultLanguage!);
        return changes;
    }

    /// <returns>Whether the manager changed.</returns>
    public bool AssignAccountManager(Guid? operatorId)
    {
        if (AccountManagerId == operatorId)
        {
            return false;
        }

        AccountManagerId = operatorId;
        return true;
    }

    /// <summary>The add is complete: the clinic becomes visible (research R2, stage 3).</summary>
    public void Activate()
    {
        if (State != SettingUp)
        {
            throw new InvalidOperationException($"Only a clinic being set up is activated, not one that is '{State}'.");
        }

        State = Active;
    }

    /// <summary>The add failed part-way: the clinic stays invisible for good (research R2).</summary>
    public void Discard()
    {
        if (State != SettingUp)
        {
            throw new InvalidOperationException($"Only a clinic being set up is discarded, not one that is '{State}'.");
        }

        State = Discarded;
    }

    private static string Name(string? value, string field, bool required, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0 && required)
        {
            errors[field] = [ClinicFields.Required];
        }
        else if (trimmed.Length > NameMaxLength)
        {
            errors[field] = [ClinicFields.TooLong];
        }

        return trimmed;
    }

    private static string Text(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            errors[field] = [ClinicFields.Required];
        }
        else if (trimmed.Length > maxLength)
        {
            errors[field] = [ClinicFields.TooLong];
        }

        return trimmed;
    }
}

/// <summary>The tax number of an edit: not sent keeps it; sent empty removes it; sent changes it.</summary>
public readonly record struct TaxNumberEdit(bool Sent, string? Value)
{
    public static TaxNumberEdit Keep => new(false, null);
}
