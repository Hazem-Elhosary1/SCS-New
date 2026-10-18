using Scs.Tenancy.Domain;

namespace Scs.UnitTests.Tenancy;

/// <summary>
/// Spec 015, FR-004 (research R9; BRD Ops §3-1): a new clinic's slug comes from the Latin letters
/// and digits of its English name, else its Arabic one, else <c>clinic-</c> and six characters.
/// </summary>
public class ShortNameDeriveTests
{
    private static int Zero(int _) => 0;

    [Theory]
    [InlineData("Al Noor Clinic", "al-noor-clinic")]
    [InlineData("  Dr. Ahmed's  Dental--Care ", "dr-ahmed-s-dental-care")]
    [InlineData("24 Hours Care", "hours-care")]
    [InlineData("admin clinic", "admin-clinic")]
    [InlineData("Clinic 2026", "clinic-2026")]
    public void It_comes_from_the_english_name(string english, string expected)
    {
        Assert.Equal(expected, ShortName.Derive(english, "عيادة", Zero));
    }

    [Fact]
    public void Without_an_english_name_it_comes_from_the_arabic_one()
    {
        Assert.Equal("noor-2", ShortName.Derive(null, "عيادة Noor 2", Zero));
        Assert.Equal("noor-2", ShortName.Derive("   ", "عيادة Noor 2", Zero));
    }

    [Theory]
    [InlineData("عيادة النور")]
    [InlineData("Admin")]
    [InlineData("ab")]
    [InlineData("123")]
    [InlineData("!!!")]
    public void A_name_with_nothing_usable_takes_clinic_and_six_characters(string english)
    {
        Assert.Equal("clinic-aaaaaa", ShortName.Derive(english, "عيادة النور", Zero));
        Assert.Matches("^clinic-[a-z0-9]{6}$", ShortName.Derive(english, "عيادة النور"));
    }

    [Fact]
    public void A_long_name_is_cut_so_a_number_still_fits()
    {
        var derived = ShortName.Derive(new string('a', 30) + " " + new string('b', 30), "عيادة", Zero);

        Assert.True(derived.Length <= ShortName.DerivedMaxLength);
        Assert.False(derived.EndsWith('-'));
        Assert.True(ShortName.TryCreate(ShortName.Candidate(derived, 20), out _, out _));
    }

    [Theory]
    [InlineData("Al Noor")]
    [InlineData("عيادة")]
    [InlineData("A b c d e f g h i j k l m n o p q r s t u v w x y z")]
    [InlineData("99 Bottles & Co.")]
    public void Every_result_and_its_numbered_candidates_are_acceptable_names(string english)
    {
        var derived = ShortName.Derive(english, "عيادة");

        Assert.True(ShortName.TryCreate(derived, out _, out _), derived);
        Assert.True(ShortName.TryCreate(ShortName.Candidate(derived, 2), out _, out _));
        Assert.Equal(derived, ShortName.Candidate(derived, 1));
        Assert.Equal($"{derived}-3", ShortName.Candidate(derived, 3));
    }
}
