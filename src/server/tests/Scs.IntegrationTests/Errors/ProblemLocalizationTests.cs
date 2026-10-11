using System.Text.Json;
using Scs.BuildingBlocks.Errors;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel;

namespace Scs.IntegrationTests.Errors;

public sealed class ProblemLocalizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("en", "en", "Sign-in is required")]
    [InlineData("en-GB", "en", "Sign-in is required")]
    [InlineData("en;q=0.9, ar;q=0.5", "en", "Sign-in is required")]
    [InlineData("ar", "ar", "يلزم تسجيل الدخول")]
    [InlineData("ar-EG", "ar", "يلزم تسجيل الدخول")]
    [InlineData("ar;q=0.9, en;q=0.5", "ar", "يلزم تسجيل الدخول")]
    [InlineData("fr", "ar", "يلزم تسجيل الدخول")]
    [InlineData(null, "ar", "يلزم تسجيل الدخول")]
    public async Task Title_follows_the_callers_language_and_defaults_to_Arabic(
        string? acceptLanguage, string expectedLanguage, string expectedTitle)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/protected");
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        using var response = await _client.SendAsync(request);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedTitle, body.RootElement.GetProperty("title").GetString());
        Assert.Equal(expectedLanguage, response.Content.Headers.ContentLanguage.Single());
    }

    [Fact]
    public async Task Code_is_the_same_in_both_languages()
    {
        async Task<string?> CodeIn(string language)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/throws");
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
            using var response = await _client.SendAsync(request);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("code").GetString();
        }

        Assert.Equal("INTERNAL_ERROR", await CodeIn("ar"));
        Assert.Equal("INTERNAL_ERROR", await CodeIn("en"));
    }

    [Fact]
    public void Every_code_has_a_title_in_both_languages()
    {
        var internalAr = ErrorMessages.Title(ErrorCodes.InternalError, ErrorMessages.Arabic);
        var internalEn = ErrorMessages.Title(ErrorCodes.InternalError, ErrorMessages.English);

        foreach (var code in ErrorCodes.All)
        {
            var arabic = ErrorMessages.Title(code, ErrorMessages.Arabic);
            var english = ErrorMessages.Title(code, ErrorMessages.English);

            Assert.False(string.IsNullOrWhiteSpace(arabic));
            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.NotEqual(arabic, english);

            // A code with no entry would silently borrow the internal-error text.
            if (code != ErrorCodes.InternalError)
            {
                Assert.NotEqual(internalAr, arabic);
                Assert.NotEqual(internalEn, english);
            }
        }
    }
}
