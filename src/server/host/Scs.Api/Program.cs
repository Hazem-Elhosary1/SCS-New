using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Scs.Api;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Health;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Logging;
using Scs.BuildingBlocks.Modules;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Telemetry;
using Scs.BuildingBlocks.Tenancy;
using Scs.BuildingBlocks.Tracing;
using Scs.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddScsEnvironmentVariables();
var scs = builder.Services.AddScsOptions(builder.Configuration);

builder.Services.AddScsLogging(scs);
builder.Services.AddScsTelemetry(scs);
builder.Services.AddRateLimiter(options => options.OnRejected = async (rejected, _) =>
{
    // Tell the caller when to try again, so a form can say so instead of guessing.
    if (rejected.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
    {
        rejected.HttpContext.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    await ProblemWriter.WriteAsync(rejected.HttpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited);
});
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddHealthChecks().AddScsReadiness();
builder.Services.AddScsJobs(scs);
builder.Services.AddScsTenancy();
builder.Services.AddScsDenyByDefault();
builder.Services.AddScsApiDocs(scs);

// A request that cannot be bound answers 400 in the uniform shape instead of throwing.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var corsOrigins = scs.Cors.OriginList;
if (scs.IsLocal && corsOrigins.Length > 0)
{
    // Local only: in test and production each front end calls /api on its own origin.
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders(TraceIdMiddleware.HeaderName, "Retry-After")));
}

if (!scs.IsLocal)
{
    // Behind the CDN and the load balancer: take the caller's address and scheme from the
    // forwarded headers, but only when they come from inside the private network.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 2;
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
    });
    builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
}

foreach (var module in ModuleRegistration.Modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

if (!scs.IsLocal)
{
    app.UseForwardedHeaders();
}

// Every response: never guess a content type, never render inside a frame, send no referrer.
// Set as the response starts, because error handling clears headers set earlier.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return Task.CompletedTask;
    });
    return next(context);
});

app.UseMiddleware<TraceIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler(_ => { });
app.UseStatusCodePages(context => ProblemWriter.WriteAsync(
    context.HttpContext,
    context.HttpContext.Response.StatusCode,
    ProblemWriter.CodeForStatus(context.HttpContext.Response.StatusCode)));

if (!scs.IsLocal)
{
    app.UseHsts();

    // Health probes arrive from the load balancer over plain HTTP inside the network.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/health"),
        branch => branch.UseHttpsRedirection());
}

if (scs.IsLocal && corsOrigins.Length > 0)
{
    app.UseCors();
}

app.UseScsRequestOriginCheck();
app.UseAuthentication();
app.UseScsTemporaryPasswordGate();
app.UseScsStaffLogScope();
app.UseAuthorization();
app.UseScsBlockedNetworks();
app.UseRateLimiter();
app.UseScsPatientSurface();

app.MapScsHealth();
app.MapScsApiDocs(scs);

if (scs.IsLocal)
{
    // The job dashboard has no sign-in of its own, so it exists on a developer machine only
    // until operator sign-in arrives (spec 010). Its built-in filter also refuses non-local callers.
    app.MapHangfireDashboard("/jobs").AllowAnonymous();
}

var v1 = app.MapGroup("/v1");
foreach (var module in ModuleRegistration.Modules)
{
    module.MapEndpoints(v1);
}

foreach (var contributor in app.Services.GetServices<IEndpointContributor>())
{
    contributor.Map(app);
}

await app.RunAsync();

/// <summary>Entry point; public so integration tests can host the application.</summary>
public partial class Program;
