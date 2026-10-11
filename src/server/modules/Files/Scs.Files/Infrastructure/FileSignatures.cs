using Scs.SharedKernel.Files;

namespace Scs.Files.Infrastructure;

/// <summary>
/// Reads what a file is from its first bytes (spec 009, FR-013; research R4). The name and what
/// the browser says are never asked. Three formats, each with a signature that cannot be
/// mistaken; a new format is one more case here and its tests.
/// </summary>
internal static class FileSignatures
{
    /// <summary>How many bytes from the start are enough to decide.</summary>
    public const int HeadLength = 32;

    private static ReadOnlySpan<byte> Pdf => "%PDF-"u8;

    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> Ihdr => "IHDR"u8;

    /// <returns>One of <see cref="FileTypes"/>, or null when the content is none of them.</returns>
    public static string? Read(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(Pdf))
        {
            return FileTypes.Pdf;
        }

        // The eight bytes of PNG, then a first chunk that is the header chunk.
        if (head.Length >= 16 && head.StartsWith(Png) && head.Slice(12, 4).SequenceEqual(Ihdr))
        {
            return FileTypes.Png;
        }

        // Start of image, then a marker that opens a segment: an application segment, a
        // quantisation or Huffman table, a comment or a frame.
        if (head.Length >= 4 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF && IsJpegSegment(head[3]))
        {
            return FileTypes.Jpeg;
        }

        return null;
    }

    private static bool IsJpegSegment(byte marker) =>
        marker is >= 0xE0 and <= 0xEF or 0xDB or 0xC4 or 0xFE or 0xC0 or 0xC1 or 0xC2 or 0xDD;
}
