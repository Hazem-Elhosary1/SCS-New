
namespace Scs.SharedKernel.Files;

/// <summary>The types a file may be, as read from its content (spec 009, FR-013).</summary>
public static class FileTypes
{
    public const string Pdf = "pdf";
    public const string Png = "png";
    public const string Jpeg = "jpeg";

    public static IReadOnlyList<string> All { get; } = [Pdf, Png, Jpeg];

    /// <summary>The media type a file of this type is given out as.</summary>
    public static string MediaType(string type) => type switch
    {
        Pdf => "application/pdf",
        Png => "image/png",
        Jpeg => "image/jpeg",
        _ => "application/octet-stream",
    };
}

/// <summary>The classes of data whose retention period is a platform setting (SH-095).</summary>
public static class RetentionClasses
{
    public const string MedicalRecord = "medical-record";
    public const string Conversations = "conversations";
    public const string TicketAttachments = "ticket-attachments";
    public const string ClinicData = "clinic-data";

    /// <summary>Not kept for a period: a kind that exists for builders only.</summary>
    public const string None = "none";

    /// <summary>The classes that have a period to set.</summary>
    public static IReadOnlyList<string> WithPeriod { get; } = [MedicalRecord, Conversations, TicketAttachments, ClinicData];
}

/// <summary>
/// One kind of file the product holds (spec 009, FR-001). Its limits are the product's, the
/// same for every clinic and every plan.
/// </summary>
/// <param name="Key"><c>area.name</c>, lower case, at most 64; never renamed or reused.</param>
/// <param name="Types">From <see cref="FileTypes"/>.</param>
/// <param name="MaxBytes">The largest one file.</param>
/// <param name="MaxPerRecord">How many one record holds; null for any number.</param>
/// <param name="Public">Shown without signing in. Never for anything about a patient.</param>
/// <param name="LocalOnly">Exists on a developer's machine and in tests only.</param>
/// <param name="NameAr">What a file of this kind is, for telling its uploader about it; the key when none is given.</param>
/// <param name="NameEn">The same in English.</param>
public sealed record FileKind(
    string Key,
    IReadOnlyList<string> Types,
    long MaxBytes,
    int? MaxPerRecord,
    bool Public,
    string RetentionClass,
    bool LocalOnly = false,
    string? NameAr = null,
    string? NameEn = null)
{
    /// <summary>What a file of this kind is, in Arabic.</summary>
    public string InArabic => NameAr ?? Key;

    /// <summary>What a file of this kind is, in English.</summary>
    public string InEnglish => NameEn ?? Key;
}

/// <summary>
/// The one table of kinds of file (spec 009, FR-001 to FR-004). The spec that builds a page
/// which holds files owns one of these kinds — it says who may upload and who may see — and
/// adds a kind here when the BRD settles a new one. Keys are frozen by <c>file-kinds.lock</c>.
/// </summary>
public static class FileKinds
{
    public const int KeyMaxLength = 64;

    public const long Megabyte = 1024 * 1024;

    public const string TicketAttachment = "ticket.attachment";
    public const string PatientFile = "patient.file";
    public const string ConsentSignedCopy = "consent.signed-copy";
    public const string OrderResultReport = "order.result-report";
    public const string DoctorPhoto = "doctor.photo";

    /// <summary>For builders: lets the engine be walked before any page owns a kind.</summary>
    public const string DevSample = "dev.sample";

    private static readonly string[] PdfAndImages = [FileTypes.Pdf, FileTypes.Png, FileTypes.Jpeg];

    public static IReadOnlyList<FileKind> All { get; } =
    [
        // Chapter 08, ق-٢٠: three for one message.
        new(TicketAttachment, PdfAndImages, 10 * Megabyte, 3, Public: false, RetentionClasses.TicketAttachments,
            NameAr: "مرفق تذكرة", NameEn: "ticket attachment"),

        // Chapter 03, ق-٤٤.
        new(PatientFile, PdfAndImages, 10 * Megabyte, null, Public: false, RetentionClasses.MedicalRecord,
            NameAr: "ملف مريض", NameEn: "patient file"),

        // Chapter 04, CL-PL-020.
        new(ConsentSignedCopy, PdfAndImages, 10 * Megabyte, 1, Public: false, RetentionClasses.MedicalRecord,
            NameAr: "نسخة موقّعة من إقرار", NameEn: "signed copy of a consent"),

        // Chapter 03 §6-14.
        new(OrderResultReport, PdfAndImages, 10 * Megabyte, 1, Public: false, RetentionClasses.MedicalRecord,
            NameAr: "تقرير نتيجة", NameEn: "result report"),

        // Chapter 11, CL-DM-034: shown on the booking page.
        new(DoctorPhoto, [FileTypes.Jpeg, FileTypes.Png], 2 * Megabyte, 1, Public: true, RetentionClasses.ClinicData,
            NameAr: "صورة طبيب", NameEn: "doctor’s photo"),

        new(DevSample, PdfAndImages, 10 * Megabyte, 3, Public: false, RetentionClasses.None, LocalOnly: true,
            NameAr: "ملف تجريبي", NameEn: "sample file"),
    ];

    private static readonly Dictionary<string, FileKind> ByKey = All.ToDictionary(k => k.Key, StringComparer.Ordinal);

    public static FileKind? Find(string key) => ByKey.GetValueOrDefault(key);

    /// <summary>The largest file any kind takes: the bound of the upload address.</summary>
    public static long LargestBytes { get; } = All.Max(k => k.MaxBytes);

    /// <summary>
    /// The keys that are frozen: <c>file-kinds.lock</c>, one key per line. A key in this file is
    /// never renamed, reused or removed.
    /// </summary>
    public static IReadOnlyList<string> Locked()
    {
        using var stream = typeof(FileKinds).Assembly.GetManifestResourceStream(
            typeof(FileKinds).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("file-kinds.lock", StringComparison.Ordinal)))!;
        using var reader = new StreamReader(stream);
        return
        [
            .. reader.ReadToEnd()
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith('#')),
        ];
    }
}

/// <summary>Finds a kind. Replaceable, so tests can add kinds as a later spec would.</summary>
public interface IFileKindCatalogue
{
    FileKind? Find(string key);
}

public sealed class FileKindCatalogue : IFileKindCatalogue
{
    public FileKind? Find(string key) => FileKinds.Find(key);
}
