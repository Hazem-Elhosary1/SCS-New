using Scs.BuildingBlocks.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace Scs.UnitTests.Logging;

public class ScrubbingJsonFormatterTests
{
    private const string Mask = ScrubbingJsonFormatter.Mask;

    [Theory]
    [InlineData("a.b-c_d+tag@mail.example.com.eg")]
    [InlineData("01012345678")]
    [InlineData("01112345678")]
    [InlineData("01212345678")]
    [InlineData("01512345678")]
    [InlineData("+201012345678")]
    [InlineData("00201012345678")]
    [InlineData("+20 1012345678")]
    [InlineData("29001011234567")]
    [InlineData("30112011234567")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ6enEifQ.c2lnbmF0dXJlenpx")]
    public void Pattern_shaped_value_is_masked_wherever_it_appears(string value)
    {
        foreach (var text in new[] { value, $"before {value} after", $"\"field\":\"{value}\"", $"({value})." })
        {
            var scrubbed = ScrubbingJsonFormatter.Scrub(text);

            Assert.DoesNotContain(value, scrubbed, StringComparison.Ordinal);
            Assert.Contains(Mask, scrubbed, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Password=Sup3rS3cret;", "Password=***;")]
    [InlineData("pwd = Sup3rS3cret", "pwd=***")]
    [InlineData("User Id=scs_app;Password=x1", "User Id=***;Password=***")]
    [InlineData("AccountKey=abc123==;", "AccountKey=***;")]
    [InlineData("Authorization: Bearer abcdefgh12345678", "Authorization: Bearer ***")]
    public void Credentials_keep_their_key_and_lose_their_value(string text, string expected)
    {
        Assert.Equal(expected, ScrubbingJsonFormatter.Scrub(text));
    }

    [Theory]
    [InlineData("0101234567")]
    [InlineData("010123456789")]
    [InlineData("01312345678")]
    [InlineData("1900101123456")]
    [InlineData("290010112345678")]
    [InlineData("49001011234567")]
    [InlineData("room 12, 3 items, 2026-10-10")]
    [InlineData("user at example dot org")]
    public void Values_that_only_look_similar_are_left_alone(string text)
    {
        Assert.Equal(text, ScrubbingJsonFormatter.Scrub(text));
    }

    [Fact]
    public void A_formatted_entry_is_one_json_line_with_its_values_scrubbed()
    {
        var template = new MessageTemplateParser().Parse("Could not notify {Contact}");
        var entry = new LogEvent(
            DateTimeOffset.UnixEpoch,
            LogEventLevel.Warning,
            new InvalidOperationException("mailbox mariam.zzq@example.org rejected 01012345678"),
            template,
            [new LogEventProperty("Contact", new ScalarValue("mariam.zzq@example.org"))]);

        using var output = new StringWriter();
        new ScrubbingJsonFormatter().Format(entry, output);
        var line = output.ToString();

        Assert.Single(line.TrimEnd().Split('\n'));
        Assert.Contains("\"@l\":\"Warning\"", line, StringComparison.Ordinal);
        Assert.Contains("\"Contact\":\"***\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain("mariam.zzq", line, StringComparison.Ordinal);
        Assert.DoesNotContain("01012345678", line, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", line, StringComparison.Ordinal);
    }
}
