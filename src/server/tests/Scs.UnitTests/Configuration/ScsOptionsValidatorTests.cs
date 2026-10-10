using Scs.BuildingBlocks.Configuration;

namespace Scs.UnitTests.Configuration;

public class ScsOptionsValidatorTests
{
    private static ScsOptions Valid(Action<ScsOptions>? change = null)
    {
        var options = new ScsOptions
        {
            Environment = "local",
            Role = "all",
            Db = new DatabaseOptions
            {
                Connection = "Server=db;Database=scs;User Id=scs_app;Password=Sup3rS3cret",
                ReportConnection = "Server=db;Database=scs;User Id=scs_report;Password=Sup3rS3cret",
                CrossingConnection = "Server=db;Database=scs;User Id=scs_ops;Password=Sup3rS3cret",
            },
        };
        change?.Invoke(options);
        return options;
    }

    private static IReadOnlyList<string> Failures(ScsOptions options) =>
        [.. new ScsOptionsValidator().Validate(null, options).Failures ?? []];

    [Fact]
    public void Complete_configuration_is_valid()
    {
        Assert.Empty(Failures(Valid()));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("test")]
    [InlineData("production")]
    public void Each_environment_name_is_accepted(string environment)
    {
        Assert.Empty(Failures(Valid(o => o.Environment = environment)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Production")]
    [InlineData("staging")]
    public void Any_other_environment_name_is_refused(string environment)
    {
        Assert.Contains(Failures(Valid(o => o.Environment = environment)), f => f.Contains("SCS_ENVIRONMENT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("worker")]
    [InlineData("all")]
    public void Each_role_is_accepted(string role)
    {
        Assert.Empty(Failures(Valid(o => o.Role = role)));
    }

    [Fact]
    public void Unknown_role_is_refused()
    {
        Assert.Contains(Failures(Valid(o => o.Role = "both")), f => f.Contains("SCS_ROLE", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_connection_is_refused_by_name()
    {
        Assert.Contains(Failures(Valid(o => o.Db.Connection = " ")), f => f.Contains("SCS_DB__CONNECTION", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_report_or_crossing_connection_is_refused_by_name()
    {
        Assert.Contains(Failures(Valid(o => o.Db.ReportConnection = "")), f => f.Contains("SCS_DB__REPORT_CONNECTION", StringComparison.Ordinal));
        Assert.Contains(Failures(Valid(o => o.Db.CrossingConnection = " ")), f => f.Contains("SCS_DB__CROSSING_CONNECTION", StringComparison.Ordinal));
    }

    [Fact]
    public void Cross_origin_allowance_is_for_local_only()
    {
        Assert.Empty(Failures(Valid(o => o.Cors.Origins = "http://localhost:4201, http://localhost:4202")));

        foreach (var environment in new[] { "test", "production" })
        {
            var failures = Failures(Valid(o =>
            {
                o.Environment = environment;
                o.Cors.Origins = "https://example.org";
            }));
            Assert.Contains(failures, f => f.Contains("SCS_CORS__ORIGINS", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Every_problem_is_reported_at_once_and_no_value_is_repeated()
    {
        var failures = Failures(Valid(o =>
        {
            o.Environment = "staging-typo";
            o.Role = "role-typo";
            o.Db.Connection = "";
        }));

        Assert.Equal(3, failures.Count);
        var text = string.Join(' ', failures);
        Assert.DoesNotContain("staging-typo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("role-typo", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("all", true, "api")]
    [InlineData("worker", true, "worker")]
    [InlineData("api", false, "api")]
    public void Role_decides_whether_a_worker_runs_and_how_the_service_is_named(string role, bool runsWorker, string service)
    {
        var options = Valid(o => o.Role = role);

        Assert.Equal(runsWorker, options.RunsWorker);
        Assert.Equal(service, options.ServiceName);
    }

    [Fact]
    public void Origins_are_split_and_trimmed()
    {
        var options = Valid(o => o.Cors.Origins = " http://a , http://b ,, ");

        Assert.Equal(["http://a", "http://b"], options.Cors.OriginList);
    }
}
