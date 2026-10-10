using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure;

namespace Scs.UnitTests.Identity;

public class EmailAddressTests
{
    [Theory]
    [InlineData("name@clinic.com", "name@clinic.com")]
    [InlineData("  Name@Clinic.COM  ", "name@clinic.com")]
    [InlineData("first.last+tag@sub.clinic.example", "first.last+tag@sub.clinic.example")]
    public void A_well_formed_address_is_kept_trimmed_and_in_lower_case(string typed, string stored)
    {
        var (email, error) = EmailAddress.TryCreate(typed);

        Assert.Null(error);
        Assert.Equal(stored, email!.Value);
        Assert.Equal(stored, EmailAddress.Normalize(typed));
    }

    [Theory]
    [InlineData(null, "REQUIRED")]
    [InlineData("", "REQUIRED")]
    [InlineData("   ", "REQUIRED")]
    [InlineData("name", "INVALID_FORMAT")]
    [InlineData("@clinic.com", "INVALID_FORMAT")]
    [InlineData("name@", "INVALID_FORMAT")]
    [InlineData("name@clinic", "INVALID_FORMAT")]
    [InlineData("name@@clinic.com", "INVALID_FORMAT")]
    [InlineData("name@.com", "INVALID_FORMAT")]
    [InlineData("name@clinic.", "INVALID_FORMAT")]
    [InlineData("name@clinic..com", "INVALID_FORMAT")]
    [InlineData("na me@clinic.com", "INVALID_FORMAT")]
    public void Anything_else_is_refused_with_a_field_code(string? typed, string code)
    {
        var (email, error) = EmailAddress.TryCreate(typed);

        Assert.Null(email);
        Assert.Equal(code, error);
    }

    [Fact]
    public void An_address_over_254_characters_is_too_long()
    {
        var typed = new string('a', 250) + "@b.co";

        Assert.Equal("TOO_LONG", EmailAddress.TryCreate(typed).Error);
        Assert.Null(EmailAddress.TryCreate(new string('a', 249) + "@b.co").Error);
    }

    [Fact]
    public void An_address_never_prints_itself()
    {
        var (email, _) = EmailAddress.TryCreate("name@clinic.com");

        Assert.DoesNotContain("name", email!.ToString(), StringComparison.Ordinal);
    }
}

public class PasswordRulesTests
{
    [Theory]
    [InlineData("Abcdefg1!x")]
    [InlineData("Correct-Horse-9!")]
    [InlineData("كلمةسرّ-Aa1!")]
    public void A_password_that_meets_every_rule_is_accepted(string password)
    {
        Assert.Empty(PasswordRules.Check(password));
    }

    [Theory]
    [InlineData("Abcdef1!x", "TOO_SHORT")]
    [InlineData("abcdefgh1!", "NO_UPPER_LOWER")]
    [InlineData("ABCDEFGH1!", "NO_UPPER_LOWER")]
    [InlineData("Abcdefghi!", "NO_DIGIT")]
    [InlineData("Abcdefghi1", "NO_SYMBOL")]
    public void Each_rule_has_its_own_code(string password, string code)
    {
        Assert.Equal([code], PasswordRules.Check(password));
    }

    [Fact]
    public void Every_unmet_rule_is_reported_at_once_and_nothing_is_too_long_to_refuse()
    {
        Assert.Equal(["TOO_SHORT", "NO_UPPER_LOWER", "NO_DIGIT", "NO_SYMBOL"], PasswordRules.Check(null));
        Assert.Equal(["TOO_LONG"], PasswordRules.Check("Aa1!" + new string('x', 200)));
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void A_hash_verifies_its_own_password_and_no_other()
    {
        var hash = PasswordHasher.Hash("Correct-Horse-9!");

        Assert.Equal(new PasswordVerification(true, false), PasswordHasher.Verify(hash, "Correct-Horse-9!"));
        Assert.False(PasswordHasher.Verify(hash, "correct-horse-9!").Matches);
        Assert.False(PasswordHasher.Verify(hash, "Correct-Horse-9! ").Matches);
    }

    [Fact]
    public void The_stored_form_is_versioned_and_two_hashes_of_one_password_differ()
    {
        var first = PasswordHasher.Hash("Correct-Horse-9!");
        var second = PasswordHasher.Hash("Correct-Horse-9!");

        Assert.Matches(@"^v1\.210000\.[A-Za-z0-9+/=]{24}\.[A-Za-z0-9+/=]{44}$", first);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain("Correct", first, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hash_made_with_older_settings_still_verifies_and_asks_to_be_replaced()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var old = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            "Correct-Horse-9!", new byte[16], 1000, System.Security.Cryptography.HashAlgorithmName.SHA512, 32);
        var stored = $"v1.1000.{salt}.{Convert.ToBase64String(old)}";

        Assert.Equal(new PasswordVerification(true, true), PasswordHasher.Verify(stored, "Correct-Horse-9!"));
        Assert.Equal(default, PasswordHasher.Verify(stored, "wrong"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a hash")]
    [InlineData("v2.210000.AAAA.AAAA")]
    [InlineData("v1.x.AAAA.AAAA")]
    [InlineData("v1.0.AAAA.AAAA")]
    [InlineData("v1.210000.!!!.AAAA")]
    [InlineData("v1.210000..")]
    public void A_malformed_value_verifies_as_no_without_failing(string? stored)
    {
        Assert.Equal(default, PasswordHasher.Verify(stored, "anything"));
    }

    [Fact]
    public void The_decoy_matches_nothing()
    {
        Assert.False(PasswordHasher.VerifyDecoy("Correct-Horse-9!").Matches);
        Assert.False(PasswordHasher.VerifyDecoy("").Matches);
    }
}

public class CodeHasherTests
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("a-test-key-of-thirty-two-bytes-!");

    [Fact]
    public void A_code_is_six_digits_and_codes_vary()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => CodeHasher.NewCode()).ToList();

        Assert.All(codes, code => Assert.Matches("^[0-9]{6}$", code));
        Assert.True(codes.Distinct().Count() > 190);
    }

    [Fact]
    public void A_code_hash_depends_on_the_code_the_sign_in_and_the_key()
    {
        var challenge = Guid.NewGuid();
        var hash = CodeHasher.HashCode(Key, challenge, "123456");

        Assert.Equal(32, hash.Length);
        Assert.Equal(hash, CodeHasher.HashCode(Key, challenge, "123456"));
        Assert.NotEqual(hash, CodeHasher.HashCode(Key, challenge, "123457"));
        Assert.NotEqual(hash, CodeHasher.HashCode(Key, Guid.NewGuid(), "123456"));
        Assert.NotEqual(hash, CodeHasher.HashCode(Encoding.UTF8.GetBytes("another-key-of-thirty-two-bytes!"), challenge, "123456"));
    }

    [Fact]
    public void An_email_key_is_comparable_and_does_not_contain_the_email()
    {
        var key = CodeHasher.EmailKey(Key, "name@clinic.com");

        Assert.Equal(key, CodeHasher.EmailKey(Key, "name@clinic.com"));
        Assert.NotEqual(key, CodeHasher.EmailKey(Key, "other@clinic.com"));
        Assert.DoesNotContain("name", Encoding.UTF8.GetString(key), StringComparison.Ordinal);
    }
}

public class SessionCookiesTests
{
    private static SessionCookies Create(string pathBase = "") => new(
        new EphemeralDataProtectionProvider(),
        Options.Create(new ScsOptions { Auth = new AuthOptions { CookiePathBase = pathBase } }));

    [Theory]
    [InlineData("", "/v1/auth")]
    [InlineData("/api", "/api/v1/auth")]
    public void The_renewal_cookie_is_limited_to_the_sign_in_addresses_under_the_api_base_path(string pathBase, string expected)
    {
        var cookies = Create(pathBase);
        var context = new DefaultHttpContext();

        cookies.WriteRenewal(
            context.Response,
            new RenewalTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new byte[32]),
            DateTimeOffset.UtcNow.AddHours(1));

        var header = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Equal(expected, cookies.RenewalPath);
        Assert.Contains($"path={expected};", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_ticket_survives_the_round_trip_and_one_kind_cannot_pass_for_another()
    {
        var cookies = Create();
        var written = new DefaultHttpContext();
        var access = new AccessTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        cookies.WriteAccess(written.Response, access, access.ExpiresAtUtc.AddHours(12));
        var value = written.Response.Headers.SetCookie.ToString().Split(';')[0].Split('=', 2)[1];

        var read = new DefaultHttpContext();
        read.Request.Headers.Cookie = $"{SessionCookies.Access}={value}";
        Assert.Equal(access, cookies.ReadAccess(read.Request));

        // The same value offered as a pending sign-in or as a renewal is refused.
        read.Request.Headers.Cookie = $"{SessionCookies.Challenge}={value}; {SessionCookies.Renewal}={value}";
        Assert.Null(cookies.ReadChallenge(read.Request));
        Assert.Null(cookies.ReadRenewal(read.Request));

        // A value made with another key ring is not ours.
        read.Request.Headers.Cookie = $"{SessionCookies.Access}={value}";
        Assert.Null(Create().ReadAccess(read.Request));
    }
}
