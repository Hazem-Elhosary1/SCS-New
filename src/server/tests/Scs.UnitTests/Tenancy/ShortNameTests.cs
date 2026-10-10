using Scs.Tenancy.Domain;

namespace Scs.UnitTests.Tenancy;

public class ShortNameTests
{
    [Theory]
    [InlineData("noor", "noor")]
    [InlineData("al-noor", "al-noor")]
    [InlineData("clinic24", "clinic24")]
    [InlineData("a1b", "a1b")]
    [InlineData("NOOR", "noor")]
    [InlineData("  Al-Noor  ", "al-noor")]
    [InlineData("/noor/", "noor")]
    [InlineData(" /Noor/ ", "noor")]
    public void An_acceptable_name_is_returned_in_canonical_form(string raw, string expected)
    {
        Assert.True(ShortName.TryCreate(raw, out var canonical, out var code));
        Assert.Equal(expected, canonical);
        Assert.Null(code);
    }

    [Theory]
    [InlineData(null, ShortName.Required)]
    [InlineData("", ShortName.Required)]
    [InlineData("   ", ShortName.Required)]
    [InlineData("//", ShortName.Required)]
    [InlineData("ab", ShortName.TooShort)]
    [InlineData("1noor", ShortName.InvalidFormat)]
    [InlineData("-noor", ShortName.InvalidFormat)]
    [InlineData("noor-", ShortName.InvalidFormat)]
    [InlineData("al--noor", ShortName.InvalidFormat)]
    [InlineData("al noor", ShortName.InvalidFormat)]
    [InlineData("al_noor", ShortName.InvalidFormat)]
    [InlineData("al.noor", ShortName.InvalidFormat)]
    [InlineData("noor/extra", ShortName.InvalidFormat)]
    [InlineData("النور", ShortName.InvalidFormat)]
    [InlineData("nооr", ShortName.InvalidFormat)] // Cyrillic letters that look like Latin ones
    [InlineData("noor​", ShortName.InvalidFormat)] // zero-width space
    [InlineData("n%6for", ShortName.InvalidFormat)]
    public void A_name_that_breaks_a_rule_is_refused_with_the_rule(string? raw, string expectedCode)
    {
        Assert.False(ShortName.TryCreate(raw, out _, out var code));
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void The_length_limits_are_three_and_forty()
    {
        Assert.False(ShortName.TryCreate(new string('a', 2), out _, out var tooShort));
        Assert.Equal(ShortName.TooShort, tooShort);

        Assert.True(ShortName.TryCreate(new string('a', 3), out _, out _));
        Assert.True(ShortName.TryCreate(new string('a', 40), out _, out _));

        Assert.False(ShortName.TryCreate(new string('a', 41), out _, out var tooLong));
        Assert.Equal(ShortName.TooLong, tooLong);
    }

    [Fact]
    public void Every_reserved_word_is_refused_in_any_letter_case()
    {
        Assert.Contains("api", ReservedShortNames.All);
        Assert.Contains("app", ReservedShortNames.All);

        foreach (var word in ReservedShortNames.All.Where(w => w.Length >= ShortName.MinLength))
        {
            Assert.False(ShortName.TryCreate(word, out _, out var code), word);
            Assert.Equal(ShortName.Reserved, code);

            Assert.False(ShortName.TryCreate(word.ToUpperInvariant(), out _, out code), word);
            Assert.Equal(ShortName.Reserved, code);
        }
    }

    [Fact]
    public void Reserved_words_are_themselves_canonical()
    {
        Assert.All(ReservedShortNames.All, word => Assert.Equal(ShortName.Canonicalise(word), word));
    }

    [Theory]
    [InlineData("noor", true)]
    [InlineData("api", true)] // reserved, but it has the shape of a name; the directory simply has no such entry
    [InlineData("ab", false)]
    [InlineData("config.json", false)]
    [InlineData("main-abc123.js", false)]
    [InlineData("النور", false)]
    [InlineData("", false)]
    public void Only_text_shaped_like_a_name_is_looked_up(string canonical, bool expected)
    {
        Assert.Equal(expected, ShortName.CouldBeAName(canonical));
    }
}

public class ClinicTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 9, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void A_clinic_is_its_own_owner_and_starts_active()
    {
        var id = Guid.CreateVersion7();

        var (clinic, errors) = Clinic.Create(id, "  عيادة النور ", " Al Noor Clinic  ", Now);

        Assert.Empty(errors);
        Assert.NotNull(clinic);
        Assert.Equal(id, clinic.Id);
        Assert.Equal(id, clinic.ClinicId);
        Assert.Equal("عيادة النور", clinic.NameAr);
        Assert.Equal("Al Noor Clinic", clinic.NameEn);
        Assert.Equal("active", clinic.State);
        Assert.Equal(TimeSpan.Zero, clinic.CreatedAtUtc.Offset);
    }

    [Theory]
    [InlineData(null, "Al Noor", "nameAr", "REQUIRED")]
    [InlineData("   ", "Al Noor", "nameAr", "REQUIRED")]
    [InlineData("عيادة", "", "nameEn", "REQUIRED")]
    public void A_missing_display_name_is_refused(string? nameAr, string? nameEn, string field, string code)
    {
        var (clinic, errors) = Clinic.Create(Guid.CreateVersion7(), nameAr, nameEn, Now);

        Assert.Null(clinic);
        Assert.Equal([code], errors[field]);
    }

    [Fact]
    public void A_display_name_may_be_two_hundred_characters_and_no_more()
    {
        var (accepted, _) = Clinic.Create(Guid.CreateVersion7(), new string('ع', 200), new string('a', 200), Now);
        var (refused, errors) = Clinic.Create(Guid.CreateVersion7(), new string('ع', 201), new string('a', 201), Now);

        Assert.NotNull(accepted);
        Assert.Null(refused);
        Assert.Equal(["TOO_LONG"], errors["nameAr"]);
        Assert.Equal(["TOO_LONG"], errors["nameEn"]);
    }
}
