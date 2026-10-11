using Scs.SharedKernel.Wording;

namespace Scs.UnitTests.Wording;

/// <summary>Spec 007, FR-038: the device of an entry reads as the design's log writes it.</summary>
public class DeviceTextTests
{
    private const string ChromeWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private const string EdgeWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0";

    private const string FirefoxWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:127.0) Gecko/20100101 Firefox/127.0";

    private const string SafariMac =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15";

    private const string ChromeMac =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private const string ChromeAndroid =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Mobile Safari/537.36";

    private const string FirefoxAndroid = "Mozilla/5.0 (Android 14; Mobile; rv:127.0) Gecko/127.0 Firefox/127.0";

    private const string SafariIphone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";

    private const string ChromeIphone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/126.0.0.0 Mobile/15E148 Safari/604.1";

    private const string EdgeAndroid =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Mobile Safari/537.36 EdgA/126.0.0.0";

    [Theory]
    [InlineData(ChromeWindows, "متصفح Chrome على Windows", "Chrome on Windows")]
    [InlineData(EdgeWindows, "متصفح Edge على Windows", "Edge on Windows")]
    [InlineData(FirefoxWindows, "متصفح Firefox على Windows", "Firefox on Windows")]
    [InlineData(SafariMac, "متصفح Safari على macOS", "Safari on macOS")]
    [InlineData(ChromeMac, "متصفح Chrome على macOS", "Chrome on macOS")]
    [InlineData(ChromeAndroid, "متصفح Chrome على Android", "Chrome on Android")]
    [InlineData(FirefoxAndroid, "متصفح Firefox على Android", "Firefox on Android")]
    [InlineData(EdgeAndroid, "متصفح Edge على Android", "Edge on Android")]
    [InlineData(SafariIphone, "متصفح Safari على iOS", "Safari on iOS")]
    [InlineData(ChromeIphone, "متصفح Chrome على iOS", "Chrome on iOS")]
    public void A_known_browser_on_a_known_system_reads_as_the_design_writes_it(string agent, string arabic, string english)
    {
        Assert.Equal(arabic, DeviceText.Describe(agent, "ar"));
        Assert.Equal(english, DeviceText.Describe(agent, "en"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curl/8.7.1")]
    [InlineData("something nobody has heard of")]
    public void An_unrecognised_or_empty_one_is_an_unknown_device(string? agent)
    {
        Assert.Equal("جهاز غير معروف", DeviceText.Describe(agent, "ar"));
        Assert.Equal("Unknown device", DeviceText.Describe(agent, "en"));
    }

    [Fact]
    public void Half_of_it_is_still_said()
    {
        Assert.Equal("Chrome", DeviceText.Describe("Mozilla/5.0 (PlayStation) Chrome/100.0", "en"));
        Assert.Equal("متصفح Chrome", DeviceText.Describe("Mozilla/5.0 (PlayStation) Chrome/100.0", "ar"));
        Assert.Equal("A browser on Windows", DeviceText.Describe("SomeAgent (Windows NT 10.0)", "en"));
        Assert.Equal("متصفح على Windows", DeviceText.Describe("SomeAgent (Windows NT 10.0)", "ar"));
    }
}
