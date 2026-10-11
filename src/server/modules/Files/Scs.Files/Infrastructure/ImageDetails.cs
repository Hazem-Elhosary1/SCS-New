using System.Buffers.Binary;
using Scs.SharedKernel.Files;

namespace Scs.Files.Infrastructure;

/// <summary>
/// Removes from an image what it says about where, when and with what it was taken, without
/// touching the picture (spec 009, FR-028b; research R10): whole segments of a JPEG and whole
/// chunks of a PNG are left out, and nothing is decoded or encoded again. Used for public kinds
/// only; a private file is never changed.
/// </summary>
internal static class ImageDetails
{
    /// <returns>The image without its descriptive parts; the input itself when it cannot be read as its type.</returns>
    public static byte[] Strip(byte[] image, string type) => type switch
    {
        FileTypes.Jpeg => StripJpeg(image) ?? image,
        FileTypes.Png => StripPng(image) ?? image,
        _ => image,
    };

    /// <summary>
    /// Leaves out the application segments that carry Exif, XMP, IPTC and the like (APP1 to
    /// APP15, except APP2 colour profiles and APP14, which decoding needs) and comments.
    /// </summary>
    private static byte[]? StripJpeg(byte[] image)
    {
        if (image.Length < 4 || image[0] != 0xFF || image[1] != 0xD8)
        {
            return null;
        }

        using var kept = new MemoryStream(image.Length);
        kept.Write(image, 0, 2);
        var at = 2;

        while (at + 4 <= image.Length)
        {
            if (image[at] != 0xFF)
            {
                return null;
            }

            var marker = image[at + 1];

            // Start of scan: the picture itself follows to the end, untouched.
            if (marker == 0xDA)
            {
                kept.Write(image, at, image.Length - at);
                return kept.ToArray();
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(at + 2, 2));
            if (length < 2 || at + 2 + length > image.Length)
            {
                return null;
            }

            var descriptive = marker == 0xFE || (marker is >= 0xE1 and <= 0xEF && marker != 0xE2 && marker != 0xEE);
            if (!descriptive)
            {
                kept.Write(image, at, 2 + length);
            }

            at += 2 + length;
        }

        return null;
    }

    private static readonly HashSet<string> PngDescriptive = new(StringComparer.Ordinal) { "tEXt", "zTXt", "iTXt", "tIME", "eXIf" };

    private static byte[]? StripPng(byte[] image)
    {
        const int Signature = 8;
        if (image.Length < Signature + 12)
        {
            return null;
        }

        using var kept = new MemoryStream(image.Length);
        kept.Write(image, 0, Signature);
        var at = Signature;

        while (at + 12 <= image.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(at, 4));
            var whole = 12L + length;
            if (at + whole > image.Length)
            {
                return null;
            }

            var name = System.Text.Encoding.ASCII.GetString(image, at + 4, 4);
            if (!PngDescriptive.Contains(name))
            {
                kept.Write(image, at, (int)whole);
            }

            at += (int)whole;
            if (name == "IEND")
            {
                return kept.ToArray();
            }
        }

        return null;
    }
}
