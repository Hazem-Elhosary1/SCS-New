using Microsoft.AspNetCore.Http;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;

namespace Scs.UnitTests.Errors;

public class ErrorCatalogueTests
{
    [Fact]
    public void There_are_twelve_codes_all_upper_snake_case_and_distinct()
    {
        Assert.Equal(43, ErrorCodes.All.Count);
        Assert.Equal(ErrorCodes.All.Count, ErrorCodes.All.Distinct().Count());
        Assert.All(ErrorCodes.All, code => Assert.Matches("^[A-Z][A-Z0-9_]+$", code));
    }

    [Fact]
    public void An_unknown_code_borrows_the_internal_error_text_rather_than_failing()
    {
        Assert.Equal(
            ErrorMessages.Title(ErrorCodes.InternalError, ErrorMessages.English),
            ErrorMessages.Title("NOT_A_REAL_CODE", ErrorMessages.English));
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ErrorCodes.MalformedRequest)]
    [InlineData(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated)]
    [InlineData(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden)]
    [InlineData(StatusCodes.Status404NotFound, ErrorCodes.NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed, ErrorCodes.MalformedRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, ErrorCodes.MalformedRequest)]
    [InlineData(StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited)]
    [InlineData(StatusCodes.Status500InternalServerError, ErrorCodes.InternalError)]
    [InlineData(StatusCodes.Status502BadGateway, ErrorCodes.InternalError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, ErrorCodes.ServiceUnavailable)]
    public void A_status_with_no_body_of_its_own_maps_to_a_code(int status, string code)
    {
        Assert.Equal(code, ProblemWriter.CodeForStatus(status));
    }

    [Fact]
    public void A_validation_refusal_carries_its_field_codes()
    {
        var refusal = ScsProblemException.Validation(new Dictionary<string, string[]> { ["email"] = ["REQUIRED"] });

        Assert.Equal(StatusCodes.Status400BadRequest, refusal.Status);
        Assert.Equal(ErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["REQUIRED"], refusal.Errors!["email"]);
    }
}
