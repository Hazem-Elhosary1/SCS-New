using Scs.SharedKernel;

namespace Scs.ClinicData.Domain;

/// <summary>
/// A branch of a clinic (spec 015, BRD Ops §3-4). Today only the main branch exists, made with the
/// clinic and carrying its names and address as they were then; spec 028 adds the others. The
/// database keeps one main branch per clinic.
/// </summary>
public sealed class Branch : IClinicOwned
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 300;

    private Branch()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public string NameAr { get; private set; } = "";

    public string NameEn { get; private set; } = "";

    public string Address { get; private set; } = "";

    public bool IsMain { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Branch Main(Guid clinicId, string nameAr, string nameEn, string address, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        ClinicId = clinicId,
        NameAr = nameAr.Trim(),
        NameEn = nameEn.Trim(),
        Address = address.Trim(),
        IsMain = true,
        CreatedAtUtc = now,
    };
}
