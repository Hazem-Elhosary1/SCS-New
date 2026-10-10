using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Security;

/// <summary>
/// Behind the CDN and the load balancer the caller's scheme and address arrive in forwarded
/// headers. They are believed only when the request comes from inside the private network;
/// otherwise anyone could claim to be on HTTPS, or to be a different caller each time.
/// </summary>
public class ForwardedHeadersTests
{
    private const string Proxy = "10.0.1.5";
    private const string Outsider = "203.0.113.9";
    private const string RemoteHeader = "X-Test-Remote-Address";
    private const string Report = """{"app":"clinic","version":"1","message":"m"}""";

    private static HttpClient Client(ApiFactory factory) =>
        factory
            .With("Scs:ENVIRONMENT", "test")
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new RemoteAddressFilter())))
            .CreateClient(new() { BaseAddress = new Uri("http://app.test.scs-heta.com"), AllowAutoRedirect = false });

    private static HttpRequestMessage Request(
        HttpMethod method, string path, string remote, string? forwardedProto = null, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(RemoteHeader, remote);
        if (forwardedProto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        }

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent(Report, Encoding.UTF8, "application/json");
        }

        return request;
    }

    [Fact]
    public async Task Scheme_forwarded_by_the_private_network_is_believed()
    {
        using var factory = new ApiFactory();
        using var client = Client(factory);

        using var response = await client.SendAsync(
            Request(HttpMethod.Get, "/v1/meta/version", Proxy, forwardedProto: "https"));

        // Already HTTPS as far as the caller is concerned: served, not redirected in a loop.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Scheme_claimed_from_outside_is_ignored()
    {
        using var factory = new ApiFactory();
        using var client = Client(factory);

        using var response = await client.SendAsync(
            Request(HttpMethod.Get, "/v1/meta/version", Outsider, forwardedProto: "https"));

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }

    [Fact]
    public async Task Plain_request_through_the_proxy_is_redirected()
    {
        using var factory = new ApiFactory();
        using var client = Client(factory);

        using var response = await client.SendAsync(
            Request(HttpMethod.Get, "/v1/meta/version", Proxy, forwardedProto: "http"));

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_counts_the_forwarded_caller_not_the_proxy()
    {
        using var factory = new ApiFactory();
        using var client = Client(factory);

        for (var i = 0; i < 30; i++)
        {
            using var accepted = await client.SendAsync(
                Request(HttpMethod.Post, "/v1/client-errors", Proxy, "https", forwardedFor: "198.51.100.1"));
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var sameCaller = await client.SendAsync(
            Request(HttpMethod.Post, "/v1/client-errors", Proxy, "https", forwardedFor: "198.51.100.1"));
        using var otherCaller = await client.SendAsync(
            Request(HttpMethod.Post, "/v1/client-errors", Proxy, "https", forwardedFor: "198.51.100.2"));

        Assert.Equal(HttpStatusCode.TooManyRequests, sameCaller.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, otherCaller.StatusCode);
    }

    [Fact]
    public async Task An_outsider_cannot_escape_the_rate_limit_by_inventing_addresses()
    {
        using var factory = new ApiFactory().With("Scs:ENVIRONMENT", "test");
        using var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new RemoteAddressFilter())))
            .CreateClient(new() { BaseAddress = new Uri("https://app.test.scs-heta.com"), AllowAutoRedirect = false });

        HttpStatusCode last = default;
        for (var i = 0; i < 31; i++)
        {
            using var response = await client.SendAsync(
                Request(HttpMethod.Post, "/v1/client-errors", Outsider, forwardedFor: $"198.51.100.{i + 1}"));
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    /// <summary>Lets a test say which address a request arrives from; the test server has none.</summary>
    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                if (context.Request.Headers.TryGetValue(RemoteHeader, out var value))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(value.ToString());
                }

                return following(context);
            });
            next(app);
        };
    }
}
