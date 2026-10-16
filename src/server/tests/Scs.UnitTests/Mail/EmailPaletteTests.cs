using Scs.BuildingBlocks.Mail;

namespace Scs.UnitTests.Mail;

/// <summary>
/// Spec 094, FR-006: every word of an email has a contrast of at least 4.5 to 1 with its
/// ground — in the light theme it is designed in and in the dark one it adds.
/// </summary>
public class EmailPaletteTests
{
    public static TheoryData<string, string, string> Pairs()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (what, text, ground) in EmailPalette.TextOnGround)
        {
            data.Add(what, text, ground);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Every_word_can_be_read_on_its_ground(string what, string text, string ground)
    {
        var contrast = EmailPalette.Contrast(text, ground);
        Assert.True(contrast >= 4.5, $"{what}: {text} on {ground} is {contrast:F2} to 1.");
    }

    [Fact]
    public void The_measure_is_the_standard_one()
    {
        Assert.Equal(21, EmailPalette.Contrast("#000000", "#ffffff"), 2);
        Assert.Equal(1, EmailPalette.Contrast("#7c3aed", "#7c3aed"), 2);
        // The two greys the design draws, and why they are not used.
        Assert.True(EmailPalette.Contrast("#74838e", "#ffffff") < 4.5);
        Assert.True(EmailPalette.Contrast("#9ca7b0", "#ffffff") < 4.5);
    }
}
