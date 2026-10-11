using Scs.SharedKernel;

namespace Scs.Identity.Domain;

/// <summary>
/// A browser an account has completed a sign-in from (spec 008). It holds a hash of the mark
/// the browser carries, never the mark; and nothing that describes the device.
/// </summary>
public sealed class KnownDevice : IClinicOwned
{
    private KnownDevice()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid AccountId { get; private set; }

#pragma warning disable CA1819 // A hash is a byte array.
    [Secret]
    public byte[] MarkHash { get; private set; } = [];
#pragma warning restore CA1819

    public DateTimeOffset FirstSeenAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public static KnownDevice Seen(Guid accountId, byte[] markHash, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        AccountId = accountId,
        MarkHash = markHash,
        FirstSeenAtUtc = now,
        LastSeenAtUtc = now,
    };

    public void SeenAgain(DateTimeOffset now) => LastSeenAtUtc = now;

    public override string ToString() => $"{nameof(KnownDevice)}#{Id}";
}
