using System.Buffers.Binary;
using System.Text;
using Scs.BuildingBlocks.Files;
using Scs.Files.Infrastructure;
using Scs.SharedKernel.Files;

namespace Scs.UnitTests.Files;

/// <summary>
/// Spec 009, FR-001 to FR-004: the table of kinds of file is what the BRD settles, its keys are
/// frozen, and nothing about a patient is of a public kind.
/// </summary>
public class FileKindsTests
{
    private const long Mb = 1024 * 1024;

    private static readonly (string Key, string[] Types, long Max, int? PerRecord, bool Public, string Class)[] Expected =
    [
        ("ticket.attachment", ["pdf", "png", "jpeg"], 10 * Mb, 3, false, "ticket-attachments"),
        ("patient.file", ["pdf", "png", "jpeg"], 10 * Mb, null, false, "medical-record"),
        ("consent.signed-copy", ["pdf", "png", "jpeg"], 10 * Mb, 1, false, "medical-record"),
        ("order.result-report", ["pdf", "png", "jpeg"], 10 * Mb, 1, false, "medical-record"),
        ("doctor.photo", ["jpeg", "png"], 2 * Mb, 1, true, "clinic-data"),
        ("dev.sample", ["pdf", "png", "jpeg"], 10 * Mb, 3, false, "none"),
    ];

    [Fact]
    public void The_table_holds_exactly_the_kinds_the_specs_name_with_their_limits()
    {
        Assert.Equal(Expected.Length, FileKinds.All.Count);
        foreach (var expected in Expected)
        {
            var kind = FileKinds.Find(expected.Key);
            Assert.NotNull(kind);
            Assert.Equal(expected.Types, kind.Types);
            Assert.Equal((expected.Max, expected.PerRecord, expected.Public, expected.Class), (kind.MaxBytes, kind.MaxPerRecord, kind.Public, kind.RetentionClass));
        }
    }

    [Fact]
    public void Keys_are_unique_well_formed_and_frozen()
    {
        var keys = FileKinds.All.Select(k => k.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.Matches(@"^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*$", key));
        Assert.All(keys, key => Assert.True(key.Length <= FileKinds.KeyMaxLength, key));

        var locked = FileKinds.Locked();
        var missing = keys.Except(locked, StringComparer.Ordinal).ToList();
        var gone = locked.Except(keys, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Add these to file-kinds.lock, on purpose: " + string.Join(", ", missing));
        Assert.True(gone.Count == 0, "Frozen keys that are missing or renamed: " + string.Join(", ", gone));
    }

    [Fact]
    public void Every_kind_takes_known_types_a_size_and_a_known_class()
    {
        Assert.All(FileKinds.All, kind =>
        {
            Assert.NotEmpty(kind.Types);
            Assert.All(kind.Types, type => Assert.Contains(type, FileTypes.All));
            Assert.True(kind.MaxBytes > 0 && kind.MaxBytes <= FileKinds.LargestBytes, kind.Key);
            Assert.True(kind.MaxPerRecord is null or > 0, kind.Key);
            Assert.Contains(kind.RetentionClass, RetentionClasses.WithPeriod.Append(RetentionClasses.None));
        });
    }

    [Fact]
    public void Only_a_doctors_photo_is_public_and_nothing_of_the_medical_record_ever_is()
    {
        Assert.Equal(["doctor.photo"], FileKinds.All.Where(k => k.Public).Select(k => k.Key));
        Assert.DoesNotContain(FileKinds.All, k => k.Public && k.RetentionClass == RetentionClasses.MedicalRecord);

        // A public kind is an image: a PDF is never shown to visitors.
        Assert.All(FileKinds.All.Where(k => k.Public), k => Assert.DoesNotContain(FileTypes.Pdf, k.Types));
    }

    [Fact]
    public void Only_the_builders_kind_is_for_a_developers_machine_alone()
    {
        Assert.Equal(["dev.sample"], FileKinds.All.Where(k => k.LocalOnly).Select(k => k.Key));
        Assert.Null(FileKinds.Find("no.such-kind"));
        Assert.Null(new FileKindCatalogue().Find(""));
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("exe", "application/octet-stream")]
    public void A_type_is_given_out_as_its_media_type_and_a_stranger_as_plain_bytes(string type, string media)
    {
        Assert.Equal(media, FileTypes.MediaType(type));
    }
}

/// <summary>The file the web sites read is the server's table, written by the migrator's tool.</summary>
public class FileKindsFileTests
{
    private static string FindFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. FileKindsFile.RepositoryPath.Split('/')]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"{FileKindsFile.RepositoryPath} was not found above the test directory. "
            + "Create it with: dotnet run --project src/server/tools/Scs.Migrator -- file-kinds " + FileKindsFile.RepositoryPath);
    }

    [Fact]
    public void The_checked_in_file_is_what_the_table_writes()
    {
        var onDisk = File.ReadAllText(FindFile()).ReplaceLineEndings("\n");

        Assert.True(
            onDisk == FileKindsFile.Write(),
            "The console's kinds of file differ from the server's. Regenerate them: "
            + "dotnet run --project src/server/tools/Scs.Migrator -- file-kinds " + FileKindsFile.RepositoryPath);
    }
}

/// <summary>Spec 009, FR-013: what a file is, is read from its first bytes and nothing else.</summary>
public class FileSignaturesTests
{
    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static readonly byte[] PngStart =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 1];

    [Fact]
    public void A_pdf_a_png_and_a_jpeg_are_read_as_themselves()
    {
        Assert.Equal("pdf", FileSignatures.Read(Ascii("%PDF-1.7\n1 0 obj")));
        Assert.Equal("png", FileSignatures.Read(PngStart));
        Assert.Equal("jpeg", FileSignatures.Read([0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F']));
        Assert.Equal("jpeg", FileSignatures.Read([0xFF, 0xD8, 0xFF, 0xE1, 0, 16]));
        Assert.Equal("jpeg", FileSignatures.Read([0xFF, 0xD8, 0xFF, 0xDB, 0, 67]));
    }

    public static TheoryData<string, byte[]> Strangers => new()
    {
        { "a Windows program", [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00] },
        { "a Linux program", [0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1] },
        { "a shell script", Ascii("#!/bin/sh\nrm -rf /") },
        { "a ZIP or an office document", [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00] },
        { "an HTML page", Ascii("<!doctype html><script>") },
        { "an SVG", Ascii("<svg xmlns=\"http://www.w3.org/2000/svg\">") },
        { "a GIF", Ascii("GIF89a\u0001\u0000") },
        { "a WebP", [.. Ascii("RIFF"), 0x24, 0, 0, 0, .. Ascii("WEBPVP8 ")] },
        { "a PDF that does not start as one", Ascii(" %PDF-1.7") },
        { "a PNG whose first chunk is not its header", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'t', (byte)'E', (byte)'X', (byte)'t'] },
        { "the eight bytes of a PNG and nothing more", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
        { "a JPEG start with no segment after it", [0xFF, 0xD8, 0xFF, 0x00] },
        { "two bytes", [0xFF, 0xD8] },
        { "nothing", [] },
    };

    [Theory]
    [MemberData(nameof(Strangers))]
    public void Anything_else_is_none_of_the_three(string what, byte[] head)
    {
        Assert.True(FileSignatures.Read(head) is null, what);
    }
}

/// <summary>
/// Spec 009, FR-028b: a public image loses what it says of where, when and with what it was
/// taken, and nothing of the picture.
/// </summary>
public class ImageDetailsTests
{
    private static void Segment(Stream jpeg, byte marker, byte[] data)
    {
        Span<byte> head = stackalloc byte[4];
        head[0] = 0xFF;
        head[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(head[2..], (ushort)(data.Length + 2));
        jpeg.Write(head);
        jpeg.Write(data);
    }

    private static byte[] Jpeg(params (byte Marker, string Data)[] segments)
    {
        using var jpeg = new MemoryStream();
        jpeg.Write([0xFF, 0xD8]);
        foreach (var (marker, data) in segments)
        {
            Segment(jpeg, marker, Encoding.ASCII.GetBytes(data));
        }

        jpeg.Write([0xFF, 0xDA, 0, 2, 9, 8, 7, 0xFF, 0xE1, 6, 5, 0xFF, 0xD9]);
        return jpeg.ToArray();
    }

    private static void Chunk(Stream png, string name, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        png.Write(length);
        png.Write(Encoding.ASCII.GetBytes(name));
        png.Write(data);
        png.Write([1, 2, 3, 4]);
    }

    private static byte[] Png(params string[] chunks)
    {
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        foreach (var name in chunks)
        {
            Chunk(png, name, Encoding.ASCII.GetBytes("data of " + name));
        }

        return png.ToArray();
    }

    [Fact]
    public void A_jpeg_loses_exif_xmp_iptc_and_comments_and_keeps_what_decoding_needs()
    {
        var image = Jpeg(
            (0xE0, "JFIF"), (0xE1, "Exif GPS 30.04N"), (0xE1, "http://ns.adobe.com/xap/1.0/ where"), (0xE2, "ICC_PROFILE"),
            (0xED, "Photoshop IPTC caption"), (0xEE, "Adobe"), (0xFE, "shot by Sara"), (0xDB, "tables"));

        var stripped = ImageDetails.Strip(image, FileTypes.Jpeg);

        Assert.Equal(Jpeg((0xE0, "JFIF"), (0xE2, "ICC_PROFILE"), (0xEE, "Adobe"), (0xDB, "tables")), stripped);
        var text = Encoding.ASCII.GetString(stripped);
        Assert.DoesNotContain("GPS", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sara", text, StringComparison.Ordinal);

        // The picture's own bytes are untouched, even where they look like a marker.
        Assert.EndsWith(Encoding.Latin1.GetString([0xFF, 0xDA, 0, 2, 9, 8, 7, 0xFF, 0xE1, 6, 5, 0xFF, 0xD9]), Encoding.Latin1.GetString(stripped), StringComparison.Ordinal);
    }

    [Fact]
    public void A_png_loses_its_text_time_and_exif_chunks()
    {
        var image = Png("IHDR", "tEXt", "gAMA", "zTXt", "iTXt", "tIME", "eXIf", "IDAT", "IEND");

        Assert.Equal(Png("IHDR", "gAMA", "IDAT", "IEND"), ImageDetails.Strip(image, FileTypes.Png));
    }

    [Fact]
    public void An_image_with_nothing_to_remove_is_unchanged_and_one_that_cannot_be_read_is_left_alone()
    {
        var clean = Jpeg((0xE0, "JFIF"), (0xDB, "tables"));
        Assert.Equal(clean, ImageDetails.Strip(clean, FileTypes.Jpeg));

        byte[] cut = [0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF, 1, 2];
        Assert.Same(cut, ImageDetails.Strip(cut, FileTypes.Jpeg));

        byte[] notPng = [1, 2, 3];
        Assert.Same(notPng, ImageDetails.Strip(notPng, FileTypes.Png));

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7");
        Assert.Same(pdf, ImageDetails.Strip(pdf, FileTypes.Pdf));
    }
}

/// <summary>Spec 009, FR-014 and FR-014a: the daemon's protocol, and the scanner of a developer's machine.</summary>
public class ScannerTests
{
    /// <summary>Records what is written to it and answers one line, as the daemon does.</summary>
    private sealed class FakeDaemon(string answer) : Stream
    {
        private readonly MemoryStream _answer = new(Encoding.ASCII.GetBytes(answer + "\0"));

        public MemoryStream Received { get; } = new();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _answer.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Received.Write(buffer, offset, count);
    }

    [Fact]
    public async Task The_content_is_sent_in_chunks_with_their_lengths_and_ended_with_an_empty_one()
    {
        using var daemon = new FakeDaemon("stream: OK");
        var content = new byte[150_000];
        Random.Shared.NextBytes(content);

        var answer = await ClamdScanner.AskAsync(daemon, new MemoryStream(content), CancellationToken.None);

        Assert.Equal("stream: OK", answer);
        var sent = daemon.Received.ToArray();
        Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(sent, 0, 10));

        // Re-assemble what the daemon was sent.
        using var whole = new MemoryStream();
        var at = 10;
        while (true)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(sent.AsSpan(at, 4));
            at += 4;
            if (length == 0)
            {
                break;
            }

            whole.Write(sent, at, length);
            at += length;
        }

        Assert.Equal(sent.Length, at);
        Assert.Equal(content, whole.ToArray());
    }

    [Theory]
    [InlineData("stream: OK", ScanAnswer.Clean)]
    [InlineData("stream: Win.Test.EICAR_HDB-1 FOUND", ScanAnswer.Infected)]
    [InlineData("stream: Heuristics.Encrypted.PDF FOUND", ScanAnswer.Unreadable)]
    [InlineData("stream: Heuristics.Encrypted.Zip FOUND", ScanAnswer.Unreadable)]
    [InlineData("stream: Heuristics.Broken.Media.JPEG.JFIFdupAppMarker FOUND", ScanAnswer.Unreadable)]
    [InlineData("INSTREAM size limit exceeded. ERROR", ScanAnswer.NoAnswer)]
    [InlineData("stream: Can't allocate memory ERROR", ScanAnswer.NoAnswer)]
    [InlineData("", ScanAnswer.NoAnswer)]
    public void The_daemons_line_is_read_as_one_of_four_answers(string line, ScanAnswer expected)
    {
        Assert.Equal(expected, ClamdScanner.Read(line));
    }

    [Fact]
    public async Task The_developers_scanner_knows_the_test_signature_wherever_it_is_and_nothing_else()
    {
        var scanner = new TestSignatureScanner();
        var padding = new byte[200_000];

        byte[] atTheEnd = [.. padding, .. TestSignatureScanner.Signature];
        byte[] acrossTwoReads = [.. new byte[64 * 1024 - 10], .. TestSignatureScanner.Signature, .. padding];

        Assert.Equal(ScanAnswer.Infected, await scanner.ScanAsync(new MemoryStream(TestSignatureScanner.Signature), CancellationToken.None));
        Assert.Equal(ScanAnswer.Infected, await scanner.ScanAsync(new MemoryStream(atTheEnd), CancellationToken.None));
        Assert.Equal(ScanAnswer.Infected, await scanner.ScanAsync(new MemoryStream(acrossTwoReads), CancellationToken.None));
        Assert.Equal(ScanAnswer.Clean, await scanner.ScanAsync(new MemoryStream(padding), CancellationToken.None));
        Assert.Equal(ScanAnswer.Clean, await scanner.ScanAsync(new MemoryStream(), CancellationToken.None));
    }

    [Fact]
    public async Task A_pdf_that_says_it_is_encrypted_is_known_as_one()
    {
        var open = Encoding.ASCII.GetBytes("%PDF-1.7\ntrailer\n<< /Root 1 0 R >>\n%%EOF");
        var locked = Encoding.ASCII.GetBytes("%PDF-1.7\ntrailer\n<< /Root 1 0 R /Encrypt 5 0 R >>\n%%EOF");

        Assert.False(await PdfProtection.IsEncryptedAsync(new MemoryStream(open), CancellationToken.None));
        Assert.True(await PdfProtection.IsEncryptedAsync(new MemoryStream(locked), CancellationToken.None));
    }
}
