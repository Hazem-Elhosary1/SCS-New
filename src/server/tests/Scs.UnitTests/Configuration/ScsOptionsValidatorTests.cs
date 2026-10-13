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
            Auth = new AuthOptions { CodeKey = Convert.ToBase64String(new byte[32]), StaffAppUrl = "https://app.scs.test" },
            Mail = new MailOptions { Host = "mail", Port = 587, From = "no-reply@scs.test" },
            Files = new FilesOptions { Root = OperatingSystem.IsWindows() ? @"D:\scs\files" : "/var/scs/files" },
            Scan = new ScanOptions { Host = "scanner.internal" },
            Ops = new OpsOptions { AppUrl = "https://ops.scs.test" },
            Platform = new PlatformOptions { NameAr = "سمارت كلينك أو إس", NameEn = "Smart Clinic OS", SupportEmail = "support@scs.test" },
        };
        change?.Invoke(options);
        return options;
    }

    [Fact]
    public void The_sign_in_defaults_are_the_documented_ones()
    {
        var auth = new AuthOptions();

        Assert.Equal(
            (5, 15, 15, 10, 5, 3, 60, 3, 12, 5, 30, 10, 120, true, ""),
            (auth.MaxFailedAttempts, auth.AttemptWindowMinutes, auth.LockMinutes, auth.CodeMinutes, auth.CodeAttempts,
             auth.MaxSpentCodes, auth.ResendSeconds, auth.MaxResends, auth.SessionHours, auth.AccessMinutes,
             auth.RenewalGraceSeconds, auth.RequestsPerMinute, auth.RenewalsPerMinute, auth.EmailCodeEnabled,
             auth.CookiePathBase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !")]
    [InlineData("c2hvcnQ=")]
    public void A_missing_or_short_code_key_is_refused_without_echoing_it(string key)
    {
        var failures = Failures(Valid(o => o.Auth.CodeKey = key));

        var failure = Assert.Single(failures);
        Assert.Contains("SCS_AUTH__CODE_KEY", failure, StringComparison.Ordinal);
        if (key.Length > 0)
        {
            Assert.DoesNotContain(key, failure, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("SCS_AUTH__MAX_FAILED_ATTEMPTS", 2, 11)]
    [InlineData("SCS_AUTH__ATTEMPT_WINDOW_MINUTES", 0, 1441)]
    [InlineData("SCS_AUTH__LOCK_MINUTES", 0, 1441)]
    [InlineData("SCS_AUTH__CODE_MINUTES", 0, 31)]
    [InlineData("SCS_AUTH__CODE_ATTEMPTS", 2, 11)]
    [InlineData("SCS_AUTH__MAX_SPENT_CODES", 0, 11)]
    [InlineData("SCS_AUTH__RESEND_SECONDS", 14, 601)]
    [InlineData("SCS_AUTH__MAX_RESENDS", -1, 11)]
    [InlineData("SCS_AUTH__SESSION_HOURS", 0, 169)]
    [InlineData("SCS_AUTH__ACCESS_MINUTES", 0, 31)]
    [InlineData("SCS_AUTH__RENEWAL_GRACE_SECONDS", 4, 121)]
    [InlineData("SCS_AUTH__REQUESTS_PER_MINUTE", 4, 121)]
    [InlineData("SCS_AUTH__RENEWALS_PER_MINUTE", 29, 1201)]
    [InlineData("SCS_AUTH__RESET_LINK_MINUTES", 4, 1441)]
    [InlineData("SCS_AUTH__INVITE_LINK_HOURS", 0, 337)]
    [InlineData("SCS_AUTH__TEMP_PASSWORD_HOURS", 0, 337)]
    [InlineData("SCS_AUTH__RESET_EMAIL_GAP_SECONDS", 14, 3601)]
    [InlineData("SCS_AUTH__RESET_EMAILS_PER_HOUR", 0, 61)]
    [InlineData("SCS_AUTH__GENERATED_PASSWORD_LENGTH", 11, 65)]
    public void A_sign_in_setting_outside_its_limits_is_refused_by_name(string variable, int tooLow, int tooHigh)
    {
        foreach (var value in new[] { tooLow, tooHigh })
        {
            var failures = Failures(Valid(o => Set(o.Auth, variable, value)));
            Assert.Contains(variable, Assert.Single(failures), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("api", false)]
    [InlineData("/api/", false)]
    [InlineData("/api", true)]
    [InlineData("", true)]
    public void The_cookie_path_base_is_empty_or_a_rooted_path(string path, bool accepted)
    {
        var failures = Failures(Valid(o => o.Auth.CookiePathBase = path));
        Assert.Equal(accepted, failures.Count == 0);
    }

    [Fact]
    public void Mail_host_and_sender_are_required()
    {
        var failures = Failures(Valid(o => o.Mail = new MailOptions()));

        Assert.Contains(failures, f => f.Contains("SCS_MAIL__HOST", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("SCS_MAIL__FROM", StringComparison.Ordinal));
    }

    private static void Set(AuthOptions auth, string variable, int value)
    {
        switch (variable)
        {
            case "SCS_AUTH__MAX_FAILED_ATTEMPTS": auth.MaxFailedAttempts = value; break;
            case "SCS_AUTH__ATTEMPT_WINDOW_MINUTES": auth.AttemptWindowMinutes = value; break;
            case "SCS_AUTH__LOCK_MINUTES": auth.LockMinutes = value; break;
            case "SCS_AUTH__CODE_MINUTES": auth.CodeMinutes = value; break;
            case "SCS_AUTH__CODE_ATTEMPTS": auth.CodeAttempts = value; break;
            case "SCS_AUTH__MAX_SPENT_CODES": auth.MaxSpentCodes = value; break;
            case "SCS_AUTH__RESEND_SECONDS": auth.ResendSeconds = value; break;
            case "SCS_AUTH__MAX_RESENDS": auth.MaxResends = value; break;
            case "SCS_AUTH__SESSION_HOURS": auth.SessionHours = value; break;
            case "SCS_AUTH__ACCESS_MINUTES": auth.AccessMinutes = value; break;
            case "SCS_AUTH__RENEWAL_GRACE_SECONDS": auth.RenewalGraceSeconds = value; break;
            case "SCS_AUTH__REQUESTS_PER_MINUTE": auth.RequestsPerMinute = value; break;
            case "SCS_AUTH__RENEWALS_PER_MINUTE": auth.RenewalsPerMinute = value; break;
            case "SCS_AUTH__RESET_LINK_MINUTES": auth.ResetLinkMinutes = value; break;
            case "SCS_AUTH__INVITE_LINK_HOURS": auth.InviteLinkHours = value; break;
            case "SCS_AUTH__TEMP_PASSWORD_HOURS": auth.TempPasswordHours = value; break;
            case "SCS_AUTH__RESET_EMAIL_GAP_SECONDS": auth.ResetEmailGapSeconds = value; break;
            case "SCS_AUTH__RESET_EMAILS_PER_HOUR": auth.ResetEmailsPerHour = value; break;
            case "SCS_AUTH__GENERATED_PASSWORD_LENGTH": auth.GeneratedPasswordLength = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(variable));
        }
    }

    [Theory]
    [InlineData("local", "", true)]
    [InlineData("local", "http://localhost:4201", true)]
    [InlineData("test", "", false)]
    [InlineData("production", "", false)]
    [InlineData("production", "http://app.scs-heta.com", false)]
    [InlineData("production", "https://app.scs-heta.com", true)]
    [InlineData("production", "https://app.scs-heta.com/", true)]
    [InlineData("production", "https://app.scs-heta.com/sign-in", false)]
    [InlineData("production", "https://app.scs-heta.com?x=1", false)]
    [InlineData("production", "app.scs-heta.com", false)]
    [InlineData("production", "ftp://app.scs-heta.com", false)]
    public void The_staff_app_address_is_required_and_https_outside_local(string environment, string address, bool valid)
    {
        var failures = Failures(Valid(o =>
        {
            o.Environment = environment;
            o.Auth.StaffAppUrl = address;
        }));

        if (valid)
        {
            Assert.Empty(failures);
        }
        else
        {
            var failure = Assert.Single(failures);
            Assert.Contains("SCS_AUTH__STAFF_APP_URL", failure, StringComparison.Ordinal);
            Assert.DoesNotContain("scs-heta", failure, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("local", "http://localhost:4203", true)]
    [InlineData("local", "http://127.0.0.1:4203", true)]
    [InlineData("local", "https://ops.scs.test", true)]
    [InlineData("local", "http://192.168.1.20:4203", false)]
    [InlineData("local", "http://ops.scs.test", false)]
    [InlineData("production", "", false)]
    [InlineData("production", "http://ops.scs-heta.com", false)]
    [InlineData("production", "http://localhost:4203", false)]
    [InlineData("production", "https://ops.scs-heta.com/sign-in", false)]
    [InlineData("production", "ops.scs-heta.com", false)]
    [InlineData("production", "https://ops.scs-heta.com", true)]
    public void Both_consoles_addresses_are_held_to_what_an_email_s_link_may_lead_to(string environment, string address, bool valid)
    {
        // Spec 094, T050: the operators console's address had no rule but "set", and locally
        // either console could be given a plain http address elsewhere — which started, and
        // then could not send an email with a button.
        foreach (var (variable, set) in new (string, Action<ScsOptions>)[]
                 {
                     ("SCS_OPS__APP_URL", o => o.Ops.AppUrl = address),
                     ("SCS_AUTH__STAFF_APP_URL", o => o.Auth.StaffAppUrl = address),
                 })
        {
            var options = Valid(o =>
            {
                o.Environment = environment;
                set(o);
            });
            var failures = Failures(options);

            if (valid)
            {
                Assert.Empty(failures);
            }
            else
            {
                Assert.Contains(variable, Assert.Single(failures), StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData("local", "")]
    [InlineData("local", "http://localhost:4201")]
    [InlineData("local", "http://[::1]:4201")]
    [InlineData("local", "https://app.scs.test")]
    [InlineData("production", "https://app.scs-heta.com/")]
    public void An_address_the_configuration_accepts_always_makes_a_button(string environment, string address)
    {
        var options = Valid(o =>
        {
            o.Environment = environment;
            o.Auth.StaffAppUrl = address;
            o.Ops.AppUrl = address;
        });
        Assert.Empty(Failures(options));

        // The addresses the emails are really given: a console's base and a path under it.
        var links = new[] { $"{options.Auth.StaffAppBase()}/set-password#t=v1.token", $"{options.Ops.AppBase()}/password/reset#token=abc" };
        Assert.All(links, link => Assert.Equal(link, new Scs.BuildingBlocks.Mail.EmailButton("Open", link).Address));
    }

    [Fact]
    public void Locally_the_staff_app_address_falls_back_to_the_dev_server()
    {
        Assert.Equal("http://localhost:4201", new AuthOptions().StaffAppBase());
        Assert.Equal("https://app.scs.test", new AuthOptions { StaffAppUrl = "https://app.scs.test/" }.StaffAppBase());
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

    [Theory]
    [InlineData("local", "", "", true)]
    [InlineData("test", "", "scanner.internal", false)]
    [InlineData("production", "", "scanner.internal", false)]
    [InlineData("production", "files", "scanner.internal", false)]
    [InlineData("local", "relative/files", "", false)]
    [InlineData("test", null, "", false)]
    [InlineData("production", null, "", false)]
    [InlineData("production", null, "scanner.internal", true)]
    public void Files_need_a_folder_and_a_scanner_everywhere_but_on_a_developers_machine(
        string environment, string? root, string scanner, bool valid)
    {
        var failures = Failures(Valid(o =>
        {
            o.Environment = environment;
            o.Files.Root = root ?? o.Files.Root;
            o.Scan.Host = scanner;
        }));

        Assert.Equal(valid, failures.Count == 0);
        if (!valid)
        {
            Assert.All(failures, failure => Assert.Matches("SCS_(FILES__ROOT|SCAN__HOST)", failure));
        }
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void A_scan_may_take_five_seconds_to_two_minutes(int seconds, bool valid)
    {
        var failures = Failures(Valid(o => o.Scan.TimeoutSeconds = seconds));

        Assert.Equal(valid, failures.Count == 0);
    }

    [Fact]
    public void A_developer_with_no_folder_set_keeps_files_under_the_temporary_ones()
    {
        var options = Valid(o => o.Files.Root = "");

        Assert.StartsWith(Path.GetTempPath(), options.Files.RootOrLocal(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SCS_PLATFORM__NAME_AR")]
    [InlineData("SCS_PLATFORM__NAME_EN")]
    [InlineData("SCS_PLATFORM__SUPPORT_EMAIL")]
    public void Outside_local_the_platform_must_have_its_two_names_and_its_support_email(string missing)
    {
        var options = Valid(o =>
        {
            o.Environment = "production";
            if (missing.EndsWith("NAME_AR", StringComparison.Ordinal)) { o.Platform.NameAr = " "; }
            if (missing.EndsWith("NAME_EN", StringComparison.Ordinal)) { o.Platform.NameEn = ""; }
            if (missing.EndsWith("SUPPORT_EMAIL", StringComparison.Ordinal)) { o.Platform.SupportEmail = ""; }
        });

        var result = Failures(options);
        Assert.Contains(result, f => f.Contains(missing, StringComparison.Ordinal));
    }

    [Fact]
    public void Locally_the_platform_s_identity_falls_back_to_the_design_s_names_and_shows_no_phone_or_address()
    {
        var platform = new PlatformOptions();

        Assert.Equal(("سمارت كلينك أو إس", "Smart Clinic OS"), (platform.Name("ar"), platform.Name("en")));
        Assert.Equal(PlatformOptions.LocalSupportEmail, platform.Support());
        Assert.Equal(("", "", ""), (platform.Phone(), platform.Address("ar"), platform.Address("en")));

        // An address set in one language is used for both.
        platform.AddressEn = " 12 Tahrir St., Cairo ";
        Assert.Equal(("12 Tahrir St., Cairo", "12 Tahrir St., Cairo"), (platform.Address("ar"), platform.Address("en")));
    }

    [Fact]
    public void The_platform_s_identity_is_held_to_its_limits_everywhere()
    {
        var options = Valid(o =>
        {
            o.Environment = "production";
            o.Platform.NameEn = new string('n', 81);
            o.Platform.SupportPhone = new string('1', 41);
            o.Platform.AddressAr = new string('a', 201);
            o.Platform.SupportEmail = "not an address";
        });

        var failures = Failures(options);
        Assert.Contains(failures, f => f.Contains("SCS_PLATFORM__NAME_EN", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("SCS_PLATFORM__SUPPORT_PHONE", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("SCS_PLATFORM__ADDRESS_AR", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("SCS_PLATFORM__SUPPORT_EMAIL must be an email address", StringComparison.Ordinal));
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
