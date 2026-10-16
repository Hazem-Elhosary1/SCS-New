using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Files;

namespace Scs.Files.Infrastructure;

/// <summary>
/// Asks a ClamAV daemon over the network, in its stream protocol (spec 009, research R5): the
/// content is sent in chunks and one line comes back. A daemon that cannot be reached, is slow
/// or answers with an error gives "no answer", and the file waits for the next try (FR-018).
/// The daemon is run with its alerts for encrypted documents and broken files on; those
/// findings mean the scanner could not look inside, not that malware was found (FR-014a).
/// </summary>
internal sealed class ClamdScanner(IOptions<ScsOptions> options) : IMalwareScanner
{
    private const int Chunk = 64 * 1024;

    private static readonly string[] CannotLookInside = ["Heuristics.Encrypted.", "Heuristics.Broken."];

    public async Task<ScanAnswer> ScanAsync(Stream content, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(content);
        var scan = options.Value.Scan;

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(scan.TimeoutSeconds));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(scan.Host, scan.Port, limit.Token).ConfigureAwait(false);
            var stream = client.GetStream();
            await using (stream.ConfigureAwait(false))
            {
                return Read(await AskAsync(stream, content, limit.Token).ConfigureAwait(false));
            }
        }
        catch (Exception failed) when (failed is SocketException or IOException or OperationCanceledException)
        {
            if (cancellation.IsCancellationRequested)
            {
                throw;
            }

            return ScanAnswer.NoAnswer;
        }
    }

    /// <summary>Speaks the protocol on any stream, so it can be tested without a daemon.</summary>
    internal static async Task<string> AskAsync(Stream daemon, Stream content, CancellationToken cancellation)
    {
        await daemon.WriteAsync("zINSTREAM\0"u8.ToArray(), cancellation).ConfigureAwait(false);

        var buffer = new byte[Chunk];
        var size = new byte[4];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(size, (uint)read);
            await daemon.WriteAsync(size, cancellation).ConfigureAwait(false);
            await daemon.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
        }

        // A chunk of no length ends the stream.
        await daemon.WriteAsync(new byte[4], cancellation).ConfigureAwait(false);
        await daemon.FlushAsync(cancellation).ConfigureAwait(false);

        using var answer = new MemoryStream();
        var one = new byte[256];
        while ((read = await daemon.ReadAsync(one, cancellation).ConfigureAwait(false)) > 0)
        {
            var end = Array.IndexOf(one, (byte)0, 0, read);
            answer.Write(one, 0, end < 0 ? read : end);
            if (end >= 0 || answer.Length > 4096)
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(answer.ToArray()).Trim();
    }

    /// <summary><c>stream: OK</c>, <c>stream: Some.Name FOUND</c>, or anything else.</summary>
    internal static ScanAnswer Read(string answer)
    {
        if (answer.EndsWith(": OK", StringComparison.Ordinal) || answer == "OK")
        {
            return ScanAnswer.Clean;
        }

        if (!answer.EndsWith(" FOUND", StringComparison.Ordinal))
        {
            return ScanAnswer.NoAnswer;
        }

        var name = answer[(answer.IndexOf(':', StringComparison.Ordinal) + 1)..^" FOUND".Length].Trim();
        return CannotLookInside.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))
            ? ScanAnswer.Unreadable
            : ScanAnswer.Infected;
    }
}

/// <summary>
/// The scanner of a developer's machine and of the tests when no daemon is configured (research
/// R5): it knows the standard malware test signature and nothing else, so a refusal can be
/// walked end to end without a daemon. The options validator refuses to start any other
/// environment without a real scanner.
/// </summary>
internal sealed class TestSignatureScanner : IMalwareScanner
{
    /// <summary>The EICAR test string, assembled so that this source file is not itself flagged.</summary>
    internal static readonly byte[] Signature = Encoding.ASCII.GetBytes(
        string.Concat("X5O!P%@AP[4\\PZX54(P^)7CC)7}$", "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"));

    public async Task<ScanAnswer> ScanAsync(Stream content, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(content);
        return await Bytes.ContainsAsync(content, Signature, cancellation).ConfigureAwait(false)
            ? ScanAnswer.Infected
            : ScanAnswer.Clean;
    }
}

/// <summary>
/// Whether a PDF says of itself that it is encrypted: its trailer then names an encryption
/// dictionary. Such a file cannot be looked inside by any scanner (FR-014a).
/// </summary>
internal static class PdfProtection
{
    private static readonly byte[] Encrypt = "/Encrypt"u8.ToArray();

    public static Task<bool> IsEncryptedAsync(Stream pdf, CancellationToken cancellation) =>
        Bytes.ContainsAsync(pdf, Encrypt, cancellation);
}

internal static class Bytes
{
    /// <summary>Whether the stream holds the pattern anywhere, reading it once from where it stands.</summary>
    public static async Task<bool> ContainsAsync(Stream content, byte[] pattern, CancellationToken cancellation)
    {
        var buffer = new byte[64 * 1024];
        var kept = 0;
        int read;

        while ((read = await content.ReadAsync(buffer.AsMemory(kept), cancellation).ConfigureAwait(false)) > 0)
        {
            var filled = kept + read;
            if (buffer.AsSpan(0, filled).IndexOf(pattern) >= 0)
            {
                return true;
            }

            // Keep the tail, so a pattern split across two reads is still found.
            kept = Math.Min(pattern.Length - 1, filled);
            buffer.AsSpan(filled - kept, kept).CopyTo(buffer);
        }

        return false;
    }
}
