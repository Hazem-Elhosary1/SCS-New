using Scs.SharedKernel;

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
    public const int NameMaxLength = 200;
    public const int StateMaxLength = 20;
    public const int SpecialtyMaxLength = 40;

    private Clinic()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    /// <summary>Display name in Arabic.</summary>
    public string NameAr { get; private set; } = "";

    /// <summary>Display name in English.</summary>
    public string NameEn { get; private set; } = "";

    /// <summary>Stored value; the states and what each permits belong to spec 016.</summary>
    public string State { get; private set; } = "active";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// The clinic's specialty, one of the fifteen of SH-013; null until clinic creation (015) sets it.
    /// A feature that follows a specialty is off for a clinic without that one (spec 014, ق-٣٧).
    /// </summary>
    public string? Specialty { get; private set; }

    /// <summary>
    /// Builds a clinic to be registered. Returns the field codes of what is wrong
    /// (<c>REQUIRED</c>, <c>TOO_LONG</c>) instead of a clinic when a name is not acceptable.
    /// </summary>
    public static (Clinic? Clinic, IReadOnlyDictionary<string, string[]> Errors) Create(
        Guid id, string? nameAr, string? nameEn, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var arabic = Check(nameAr, "nameAr", errors);
        var english = Check(nameEn, "nameEn", errors);

        if (id == Guid.Empty)
        {
            errors["id"] = ["REQUIRED"];
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
            State = "active",
            CreatedAtUtc = now.ToUniversalTime(),
        }, errors);
    }

    private static string Check(string? value, string field, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            errors[field] = ["REQUIRED"];
        }
        else if (trimmed.Length > NameMaxLength)
        {
            errors[field] = ["TOO_LONG"];
        }

        return trimmed;
    }
}
