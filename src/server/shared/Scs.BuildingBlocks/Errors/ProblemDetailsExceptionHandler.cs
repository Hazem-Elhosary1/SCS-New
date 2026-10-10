using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Errors;

/// <summary>
/// Turns every unhandled exception into the uniform failure shape. Nothing about the exception
/// itself is returned to the caller (FR-018).
/// </summary>
public sealed partial class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ScsProblemException known:
                await ProblemWriter.WriteAsync(httpContext, known.Status, known.Code, known.Errors);
                break;

            case BadHttpRequestException bad:
                await ProblemWriter.WriteAsync(
                    httpContext, bad.StatusCode, ProblemWriter.CodeForStatus(bad.StatusCode));
                break;

            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                // The caller went away; there is nobody to answer.
                break;

            default:
                LogUnhandled(logger, exception);
                await ProblemWriter.WriteAsync(
                    httpContext, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError);
                break;
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled failure while processing the request")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
