using Scs.BuildingBlocks.Security;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure;

namespace Scs.UnitTests.Identity;

/// <summary>Spec 004, research R10: the password the system chooses when asked to (SH-044).</summary>
public class PasswordGeneratorTests
{
    [Theory]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(64)]
    public void A_generated_password_has_the_length_asked_for_and_meets_the_rules(int length)
    {
        for (var i = 0; i < 500; i++)
        {
            var password = PasswordGenerator.Generate(length);

            Assert.Equal(length, password.Length);
            Assert.Empty(PasswordRules.Check(password));
        }
    }

    [Fact]
    public void It_leaves_out_the_characters_people_confuse()
    {
        var seen = new HashSet<char>();
        for (var i = 0; i < 2000; i++)
        {
            seen.UnionWith(PasswordGenerator.Generate(14));
        }

        Assert.DoesNotContain(seen, c => "0Oo1lI|".Contains(c, StringComparison.Ordinal));
        Assert.DoesNotContain(seen, char.IsWhiteSpace);
        Assert.All(seen, c => Assert.True(c < 128, "Only characters every keyboard has."));

        // And it does use its whole alphabet, each kind of character.
        Assert.Contains(seen, char.IsUpper);
        Assert.Contains(seen, char.IsLower);
        Assert.Contains(seen, char.IsAsciiDigit);
        Assert.Contains(seen, c => !char.IsLetterOrDigit(c));
        Assert.True(seen.Count > 60, $"Only {seen.Count} different characters appeared.");
    }

    [Fact]
    public void No_two_are_alike_and_no_position_is_fixed()
    {
        var passwords = Enumerable.Range(0, 10_000).Select(_ => PasswordGenerator.Generate(14)).ToList();

        Assert.Equal(passwords.Count, passwords.Distinct(StringComparer.Ordinal).Count());

        // The four guaranteed characters are shuffled in, not left at the front.
        Assert.Contains(passwords, p => !char.IsUpper(p[0]));
        Assert.Contains(passwords, p => !char.IsLower(p[1]));
        Assert.Contains(passwords, p => !char.IsAsciiDigit(p[2]));
    }

    [Fact]
    public void A_length_below_the_rules_is_a_programming_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(9));
    }
}
