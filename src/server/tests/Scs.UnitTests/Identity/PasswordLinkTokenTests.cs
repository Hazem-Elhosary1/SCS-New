using Microsoft.AspNetCore.DataProtection;
using Scs.BuildingBlocks.Security;

namespace Scs.UnitTests.Identity;

/// <summary>Spec 004, research R1 and R2: the token inside an emailed password link.</summary>
public class PasswordLinkTokenTests
{
    private static readonly EphemeralDataProtectionProvider Keys = new();

    private static PasswordLinkToken Sample(string kind = PasswordLinkTokens.Reset) =>
        new(kind, Guid.NewGuid(), Guid.NewGuid(), PasswordLinkTokens.NewSecret());

    [Theory]
    [InlineData(PasswordLinkTokens.Reset)]
    [InlineData(PasswordLinkTokens.NewAccount)]
    public void A_token_carries_its_kind_clinic_link_and_secret_and_nothing_readable(string kind)
    {
        var tokens = new PasswordLinkTokens(Keys);
        var original = Sample(kind);

        var text = tokens.Write(original);
        var read = tokens.Read(text);

        Assert.NotNull(read);
        Assert.Equal((original.Kind, original.ClinicId, original.LinkId), (read.Kind, read.ClinicId, read.LinkId));
        Assert.Equal(original.Secret, read.Secret);

        // Safe in an address, short enough for one, and opaque.
        Assert.Matches("^[A-Za-z0-9_-]+$", text);
        Assert.InRange(text.Length, 100, 400);
        Assert.DoesNotContain(original.ClinicId.ToString("N"), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(original.Secret)[..16], text, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_tokens_for_the_same_link_differ()
    {
        var tokens = new PasswordLinkTokens(Keys);
        var link = Sample();

        Assert.NotEqual(tokens.Write(link), tokens.Write(link));
    }

    [Fact]
    public void An_altered_shortened_empty_or_overlong_token_does_not_open()
    {
        var tokens = new PasswordLinkTokens(Keys);
        var text = tokens.Write(Sample());

        Assert.Null(tokens.Read(null));
        Assert.Null(tokens.Read(""));
        Assert.Null(tokens.Read("nonsense"));
        Assert.Null(tokens.Read("not base64 !!"));
        Assert.Null(tokens.Read(text[..^4]));
        Assert.Null(tokens.Read(text[..^1] + (text[^1] == 'A' ? 'B' : 'A')));
        Assert.Null(tokens.Read((text[0] == 'A' ? 'B' : 'A') + text[1..]));
        Assert.Null(tokens.Read(new string('A', PasswordLinkTokens.MaxTokenLength + 1)));
    }

    [Fact]
    public void A_token_made_with_another_key_ring_or_for_another_purpose_does_not_open()
    {
        var text = new PasswordLinkTokens(Keys).Write(Sample());

        Assert.Null(new PasswordLinkTokens(new EphemeralDataProtectionProvider()).Read(text));

        // Something else protected with the same key ring — a session cookie, say — is not a link.
        var other = Keys.CreateProtector("Scs.Auth.Access.v1").Protect(new byte[66]);
        Assert.Null(new PasswordLinkTokens(Keys).Read(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(other)));
    }

    [Fact]
    public void A_secret_is_matched_against_its_hash_only()
    {
        var secret = PasswordLinkTokens.NewSecret();
        var hash = PasswordLinkTokens.Hash(secret);

        Assert.Equal(32, secret.Length);
        Assert.Equal(32, hash.Length);
        Assert.NotEqual(secret, hash);
        Assert.True(PasswordLinkTokens.Matches(secret, hash));
        Assert.False(PasswordLinkTokens.Matches(PasswordLinkTokens.NewSecret(), hash));
        Assert.Equal(nameof(PasswordLinkToken), Sample().ToString());
    }

    [Fact]
    public void A_secret_of_the_wrong_length_or_an_unknown_kind_is_a_programming_error()
    {
        var tokens = new PasswordLinkTokens(Keys);

        Assert.Throws<ArgumentException>(() => tokens.Write(new PasswordLinkToken(PasswordLinkTokens.Reset, Guid.NewGuid(), Guid.NewGuid(), new byte[8])));
        Assert.Throws<ArgumentOutOfRangeException>(() => tokens.Write(new PasswordLinkToken("other", Guid.NewGuid(), Guid.NewGuid(), new byte[32])));
    }
}
