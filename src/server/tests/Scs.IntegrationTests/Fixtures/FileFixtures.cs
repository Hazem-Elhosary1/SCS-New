using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Files;
using Scs.BuildingBlocks.Jobs;
using Scs.Files.Infrastructure;
using Scs.SharedKernel.Files;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>One row of <c>files.StoredFile</c>, read with the operator login.</summary>
public sealed record StoredFileRow(
    Guid Id,
    Guid ClinicId,
    string Kind,
    string Name,
    long SizeBytes,
    string Type,
    Guid UploadedBy,
    string Scan,
    string? RefusedFor,
    int ScanTries,
    string? RecordKind,
    Guid? RecordId,
    DateTimeOffset? RemovedAtUtc,
    Guid? RemovedBy,
    DateTimeOffset? RestoredAtUtc,
    DateTimeOffset? RetainFromUtc,
    DateTimeOffset? HeldAtUtc,
    DateTimeOffset? ErasedAtUtc,
    string? ErasedFor);

/// <summary>
/// The table of kinds as the tests see it: the real kinds, and kinds made for the tests the way
/// a later spec adds one (spec 009, FR-004, SC-009).
/// </summary>
public sealed class TestKinds : IFileKindCatalogue
{
    /// <summary>PDF, PNG and JPEG up to 10 MB; three for one record.</summary>
    public const string Three = "test.three";

    /// <summary>PDF only, up to 1 MB; one for a record.</summary>
    public const string OnePdf = "test.one-pdf";

    /// <summary>Any number for a record.</summary>
    public const string Many = "test.many";

    public const string PublicImage = "test.public-image";

    /// <summary>In the table, and nobody owns it.</summary>
    public const string NoOwner = "test.no-owner";

    /// <summary>Of the retention class <c>ticket-attachments</c>.</summary>
    public const string Ticket = "test.ticket";

    public static readonly string[] Owned = [Three, OnePdf, Many, PublicImage, Ticket];

    private static readonly string[] All = [FileTypes.Pdf, FileTypes.Png, FileTypes.Jpeg];

    private static readonly Dictionary<string, FileKind> Added = new FileKind[]
    {
        new(Three, All, 10 * FileKinds.Megabyte, 3, Public: false, RetentionClasses.MedicalRecord),
        new(OnePdf, [FileTypes.Pdf], FileKinds.Megabyte, 1, Public: false, RetentionClasses.MedicalRecord),
        new(Many, All, 10 * FileKinds.Megabyte, null, Public: false, RetentionClasses.MedicalRecord),
        new(PublicImage, [FileTypes.Jpeg, FileTypes.Png], 2 * FileKinds.Megabyte, 1, Public: true, RetentionClasses.ClinicData),
        new(NoOwner, All, FileKinds.Megabyte, 1, Public: false, RetentionClasses.MedicalRecord),
        new(Ticket, All, 10 * FileKinds.Megabyte, 3, Public: false, RetentionClasses.TicketAttachments),
    }.ToDictionary(k => k.Key, StringComparer.Ordinal);

    public FileKind? Find(string key) => Added.GetValueOrDefault(key) ?? FileKinds.Find(key);
}

/// <summary>What the owner of the test kinds answers; a test changes it as it goes.</summary>
public sealed class OwnerAnswers
{
    public bool MayUpload { get; set; } = true;

    public bool MaySee { get; set; } = true;

    /// <summary>Every record the owner was asked about, in order.</summary>
    public ConcurrentQueue<FileRecord> AskedToSee { get; } = new();
}

internal sealed class TestOwner(string kind, OwnerAnswers answers) : IFileKindOwner
{
    public string Kind => kind;

    public Task<bool> MayUploadAsync(CancellationToken cancellation) => Task.FromResult(answers.MayUpload);

    public Task<bool> MaySeeAsync(FileRecord record, CancellationToken cancellation)
    {
        answers.AskedToSee.Enqueue(record);
        return Task.FromResult(answers.MaySee);
    }
}

/// <summary>A scanner that answers what the test says; clean when the test says nothing.</summary>
public sealed class FakeScanner : IMalwareScanner
{
    /// <summary>What every scan answers until changed.</summary>
    public ScanAnswer Answer { get; set; } = ScanAnswer.Clean;

    public int Scans { get; private set; }

    public async Task<ScanAnswer> ScanAsync(Stream content, CancellationToken cancellation)
    {
        Scans++;
        if (Answer != ScanAnswer.Clean)
        {
            return Answer;
        }

        // Like a real one, it finds the standard test signature by itself.
        return await new TestSignatureScanner().ScanAsync(content, cancellation);
    }
}

/// <summary>A store that can be told to fail, in front of the real folder store.</summary>
public sealed class FailingStore(IFileStore inner) : IFileStore
{
    public bool FailPuts { get; set; }

    public Task PutAsync(Guid clinicId, Guid fileId, Stream content, CancellationToken cancellation) =>
        FailPuts ? throw new FileStoreException("The store is down.") : inner.PutAsync(clinicId, fileId, content, cancellation);

    public Task<Stream?> OpenAsync(Guid clinicId, Guid fileId, CancellationToken cancellation) =>
        inner.OpenAsync(clinicId, fileId, cancellation);

    public Task DeleteAsync(Guid clinicId, Guid fileId, CancellationToken cancellation) =>
        inner.DeleteAsync(clinicId, fileId, cancellation);

    public Task DeleteClinicAsync(Guid clinicId, CancellationToken cancellation) =>
        inner.DeleteClinicAsync(clinicId, cancellation);
}

/// <summary>Sample content, made here and never fetched.</summary>
public static class Samples
{
    public static byte[] Pdf(int size = 600, string? marker = null)
    {
        var text = $"%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n% {marker ?? Guid.NewGuid().ToString("N")}\n";
        return Padded(Encoding.ASCII.GetBytes(text), size, "\n%%EOF\n");
    }

    /// <summary>A PDF whose trailer says it is encrypted: no scanner can look inside.</summary>
    public static byte[] ProtectedPdf() =>
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R /Encrypt 2 0 R >>\n%%EOF\n");

    /// <summary>A PDF header in front of the standard malware test signature.</summary>
    public static byte[] Eicar() =>
        [.. Encoding.ASCII.GetBytes("%PDF-1.7\n"), .. TestSignatureScanner.Signature, .. Encoding.ASCII.GetBytes("\n%%EOF\n")];

    public static byte[] Png(bool withText = false)
    {
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0]);
        if (withText)
        {
            Chunk(png, "tEXt", Encoding.ASCII.GetBytes("Comment\0taken in Cairo"));
            Chunk(png, "tIME", [7, 234, 10, 11, 12, 0, 0]);
            Chunk(png, "eXIf", Encoding.ASCII.GetBytes("II*\0gps-here"));
        }

        Chunk(png, "IDAT", [0x78, 0x9C, 0x63, 0x60, 0x60, 0x60, 0, 0, 0, 4, 0, 1]);
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    public static byte[] Jpeg(bool withExif = false)
    {
        using var jpeg = new MemoryStream();
        jpeg.Write([0xFF, 0xD8]);
        Segment(jpeg, 0xE0, [.. Encoding.ASCII.GetBytes("JFIF\0"), 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        if (withExif)
        {
            Segment(jpeg, 0xE1, Encoding.ASCII.GetBytes("Exif\0\0GPS 30.0444N 31.2357E; Phone Model X"));
            Segment(jpeg, 0xE1, Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0<x:xmpmeta>where</x:xmpmeta>"));
            Segment(jpeg, 0xFE, Encoding.ASCII.GetBytes("a comment with a name"));
        }

        Segment(jpeg, 0xDB, new byte[65]);
        jpeg.Write([0xFF, 0xDA, 0, 2]);
        jpeg.Write([1, 2, 3, 4, 5, 6, 7, 8]);
        jpeg.Write([0xFF, 0xD9]);
        return jpeg.ToArray();
    }

    /// <summary>The start of a Windows program.</summary>
    public static byte[] Program() => Padded([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00], 400, "");

    public static byte[] Zip() => Padded([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00], 400, "");

    public static byte[] Script() => Encoding.ASCII.GetBytes("#!/bin/sh\nrm -rf /\n");

    public static byte[] Html() => Encoding.ASCII.GetBytes("<!doctype html><script>alert(1)</script>");

    public static byte[] Svg() => Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

    public static byte[] Gif() => Padded(Encoding.ASCII.GetBytes("GIF89a"), 100, "");

    public static byte[] WebP() => [.. Encoding.ASCII.GetBytes("RIFF"), 0x24, 0, 0, 0, .. Encoding.ASCII.GetBytes("WEBPVP8 "), .. new byte[24]];

    private static byte[] Padded(byte[] start, int size, string end)
    {
        var tail = Encoding.ASCII.GetBytes(end);
        var whole = new byte[Math.Max(size, start.Length + tail.Length)];
        Array.Fill(whole, (byte)' ');
        start.CopyTo(whole, 0);
        tail.CopyTo(whole, whole.Length - tail.Length);
        return whole;
    }

    private static void Chunk(Stream png, string name, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        png.Write(length);
        png.Write(Encoding.ASCII.GetBytes(name));
        png.Write(data);
        png.Write([0, 0, 0, 0]);
    }

    private static void Segment(Stream jpeg, byte marker, byte[] data)
    {
        Span<byte> head = stackalloc byte[4];
        head[0] = 0xFF;
        head[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(head[2..], (ushort)(data.Length + 2));
        jpeg.Write(head);
        jpeg.Write(data);
    }
}

/// <summary>Helpers for the files engine (spec 009).</summary>
public static class FilesHelp
{
    /// <summary>A factory with the test kinds, their owner, a scanner the test steers and a store that can fail.</summary>
    public static ApiFactory WithTestFiles(this ApiFactory factory, OwnerAnswers answers, FakeScanner scanner) =>
        factory.WithServices(services =>
        {
            services.AddSingleton<IFileKindCatalogue, TestKinds>();
            services.AddSingleton(answers);
            services.AddSingleton<IMalwareScanner>(scanner);
            foreach (var kind in TestKinds.Owned)
            {
                services.AddScoped<IFileKindOwner>(provider => new TestOwner(kind, provider.GetRequiredService<OwnerAnswers>()));
            }

            services.AddSingleton<FailingStore>(provider => new FailingStore(ActivatorUtilities.CreateInstance<FolderFileStore>(provider)));
            services.AddSingleton<IFileStore>(provider => provider.GetRequiredService<FailingStore>());
        });

    public static async Task<HttpResponseMessage> UploadAsync(
        this HttpClient browser, string kind, string name, byte[] content, string browserSays = "application/octet-stream")
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(browserSays);
        form.Add(part, "file", name);
        return await browser.PostAsync(new Uri($"/v1/files?kind={Uri.EscapeDataString(kind)}", UriKind.Relative), form);
    }

    /// <summary>Uploads and returns the new file's id; fails the test when the upload is refused.</summary>
    public static async Task<Guid> UploadedAsync(this HttpClient browser, string kind, string name, byte[] content)
    {
        using var response = await browser.UploadAsync(kind, name, content);
        var (status, code, body) = await response.ReadAsync();
        Assert.True(status == 201, $"The upload answered {status} {code}.");
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>Runs the clinic's scan job exactly as the job server does.</summary>
    public static async Task ScanFilesAsync(this ApiFactory factory, Guid clinicId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClinicJobRunner<FileScanJob>>().RunAsync(clinicId, CancellationToken.None);
    }

    public static async Task CleanFilesAsync(this ApiFactory factory, Guid clinicId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClinicJobRunner<FileCleanupJob>>().RunAsync(clinicId, CancellationToken.None);
    }

    public static async Task<bool> StoreHasAsync(this ApiFactory factory, Guid clinicId, Guid fileId)
    {
        var content = await factory.Services.GetRequiredService<IFileStore>().OpenAsync(clinicId, fileId, CancellationToken.None);
        if (content is null)
        {
            return false;
        }

        await content.DisposeAsync();
        return true;
    }

    public static async Task<byte[]?> StoredBytesAsync(this ApiFactory factory, Guid clinicId, Guid fileId)
    {
        var content = await factory.Services.GetRequiredService<IFileStore>().OpenAsync(clinicId, fileId, CancellationToken.None);
        if (content is null)
        {
            return null;
        }

        await using (content)
        {
            using var whole = new MemoryStream();
            await content.CopyToAsync(whole);
            return whole.ToArray();
        }
    }

    /// <summary>The clinic's files as stored, oldest first, read with the operator login.</summary>
    public static async Task<List<StoredFileRow>> FilesAsync(this TestDatabase database, Guid clinicId)
    {
        ArgumentNullException.ThrowIfNull(database);

        await using var connection = await TestDatabase.OpenAsync(database.Crossing);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ClinicId, Kind, Name, SizeBytes, Type, UploadedByAccountId, Scan, RefusedFor, ScanTries,
                   RecordKind, RecordId, RemovedAtUtc, RemovedByAccountId, RestoredAtUtc, RetainFromUtc, HeldAtUtc,
                   ErasedAtUtc, ErasedFor
            FROM [files].StoredFile
            WHERE ClinicId = @clinic
            ORDER BY UploadedAtUtc, Id
            """;
        command.Parameters.Add(new SqlParameter("@clinic", clinicId));

        var rows = new List<StoredFileRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string? Text(int column) => reader.IsDBNull(column) ? null : reader.GetString(column);
            Guid? Id(int column) => reader.IsDBNull(column) ? null : reader.GetGuid(column);
            DateTimeOffset? Moment(int column) => reader.IsDBNull(column) ? null : reader.GetDateTimeOffset(column);
            rows.Add(new StoredFileRow(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetString(5),
                reader.GetGuid(6), reader.GetString(7), Text(8), reader.GetInt32(9), Text(10), Id(11),
                Moment(12), Id(13), Moment(14), Moment(15), Moment(16), Moment(17), Text(18)));
        }

        return rows;
    }

    /// <summary>Sets the retention period of a class, as the operators' settings will (spec 024).</summary>
    public static Task SetRetentionAsync(this TestDatabase database, string retentionClass, int? days)
    {
        ArgumentNullException.ThrowIfNull(database);
        return TestDatabase.ExecuteAsync(
            database.Crossing,
            $"UPDATE [files].RetentionPeriod SET Days = {(days is { } d ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NULL")} WHERE Class = '{retentionClass}'");
    }
}
