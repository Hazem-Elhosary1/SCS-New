using Microsoft.Extensions.Options;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Configuration;

public class StartupValidationTests
{
    [Theory]
    [InlineData("Scs:ENVIRONMENT", "SCS_ENVIRONMENT")]
    [InlineData("Scs:DB:CONNECTION", "SCS_DB__CONNECTION")]
    public void Missing_required_value_stops_startup_and_names_the_key(string setting, string variable)
    {
        using var factory = new ApiFactory().With(setting, null);

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(variable, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_message_reveals_no_configured_value()
    {
        const string secret = "Server=db;User Id=scs_app;Password=S3cret-Value!";
        using var factory = new ApiFactory()
            .With("Scs:DB:CONNECTION", secret)
            .With("Scs:ENVIRONMENT", "staging-typo");

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("SCS_ENVIRONMENT", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("S3cret-Value!", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("staging-typo", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("production")]
    public void Cross_origin_allowance_is_refused_outside_local(string environment)
    {
        using var factory = new ApiFactory()
            .With("Scs:ENVIRONMENT", environment)
            .With("Scs:CORS:ORIGINS", "https://example.org");

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("SCS_CORS__ORIGINS", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_role_stops_startup()
    {
        using var factory = new ApiFactory().With("Scs:ROLE", "everything");

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("SCS_ROLE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valid_configuration_starts()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.True(response.IsSuccessStatusCode);
    }
}
