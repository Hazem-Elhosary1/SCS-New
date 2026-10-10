using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel;

namespace Scs.UnitTests.Tenancy;

public class ClinicContextTests
{
    private static readonly Guid A = Guid.CreateVersion7();
    private static readonly Guid B = Guid.CreateVersion7();

    [Fact]
    public void No_clinic_until_one_is_established()
    {
        var context = new ClinicContext();

        Assert.Null(context.ClinicId);
        Assert.Null(context.Source);
    }

    [Fact]
    public void Establishing_fixes_the_clinic_and_where_it_came_from()
    {
        var context = new ClinicContext();

        context.Establish(A, ClinicSource.PatientAddress);

        Assert.Equal(A, context.ClinicId);
        Assert.Equal(ClinicSource.PatientAddress, context.Source);
        Assert.Equal(A, context.Require());
    }

    [Fact]
    public void The_same_clinic_again_is_accepted_and_the_first_source_is_kept()
    {
        var context = new ClinicContext();
        context.Establish(A, ClinicSource.PatientAddress);

        context.Establish(A, ClinicSource.Account);

        Assert.Equal(A, context.ClinicId);
        Assert.Equal(ClinicSource.PatientAddress, context.Source);
    }

    [Fact]
    public void A_different_clinic_is_refused_and_the_first_one_stays()
    {
        var context = new ClinicContext();
        context.Establish(A, ClinicSource.PatientAddress);

        var refusal = Assert.Throws<ScsProblemException>(() => context.Establish(B, ClinicSource.Account));

        Assert.Equal(ErrorCodes.ClinicMismatch, refusal.Code);
        Assert.Equal(400, refusal.Status);
        Assert.Equal(A, context.ClinicId);
    }

    [Fact]
    public void Requiring_a_clinic_with_none_established_is_refused()
    {
        var refusal = Assert.Throws<ScsProblemException>(() => new ClinicContext().Require());

        Assert.Equal(ErrorCodes.ClinicRequired, refusal.Code);
    }

    [Fact]
    public void An_empty_clinic_id_is_refused()
    {
        var context = new ClinicContext();

        var refusal = Assert.Throws<ScsProblemException>(() => context.Establish(Guid.Empty, ClinicSource.Job));

        Assert.Equal(ErrorCodes.ClinicRequired, refusal.Code);
        Assert.Null(context.ClinicId);
    }

    [Fact]
    public void A_source_is_required()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ClinicContext().Establish(A, " "));
    }
}

public class PlatformWideTablesTests
{
    [Theory]
    [InlineData("platform", "JobHeartbeat", true)]
    [InlineData("platform", "__EFMigrationsHistory", true)]
    [InlineData("tenancy", "__EFMigrationsHistory", true)]
    [InlineData("hangfire", "Job", true)]
    [InlineData("hangfire", "Anything", true)]
    [InlineData("tenancy", "ClinicDirectory", true)]
    [InlineData("crossing", "CrossClinicAccess", true)]
    [InlineData("tenancy", "Clinic", false)]
    [InlineData("platform", "Appointment", false)]
    [InlineData("bookings", "JobHeartbeat", false)]
    public void Only_declared_tables_are_platform_wide(string schema, string table, bool expected)
    {
        Assert.Equal(expected, PlatformWideTables.Contains(schema, table));
    }

    [Fact]
    public void Every_declaration_carries_a_reason()
    {
        Assert.All(PlatformWideTables.All, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Reason)));
    }
}

public class ClinicIsolationSqlTests
{
    [Fact]
    public void A_policy_has_a_filter_and_the_four_blocks_and_is_switched_on()
    {
        var sql = ClinicIsolationSql.CreatePolicy("bookings", "Appointment");

        Assert.Contains("CREATE SECURITY POLICY [bookings].[AppointmentIsolation]", sql, StringComparison.Ordinal);
        Assert.Contains("ADD FILTER PREDICATE [tenancy].[fn_ClinicAccess]([ClinicId]) ON [bookings].[Appointment]", sql, StringComparison.Ordinal);
        foreach (var operation in new[] { "AFTER INSERT", "AFTER UPDATE", "BEFORE UPDATE", "BEFORE DELETE" })
        {
            Assert.Contains($"ON [bookings].[Appointment] {operation}", sql, StringComparison.Ordinal);
        }

        Assert.Contains("STATE = ON", sql, StringComparison.Ordinal);
        Assert.Contains("SCHEMABINDING = ON", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bookings]; DROP TABLE x; --", "Appointment")]
    [InlineData("bookings", "Appoint ment")]
    [InlineData("", "Appointment")]
    public void Names_that_are_not_plain_identifiers_are_refused(string schema, string table)
    {
        Assert.Throws<ArgumentException>(() => ClinicIsolationSql.CreatePolicy(schema, table));
    }

    [Fact]
    public void With_no_clinic_set_the_function_can_match_nothing()
    {
        var sql = ClinicIsolationSql.CreateAccessFunction();

        // An unset or malformed value becomes NULL, and NULL equals nothing.
        Assert.Contains(
            "TRY_CONVERT(uniqueidentifier, CONVERT(nvarchar(36), SESSION_CONTEXT(N'ClinicId')))", sql, StringComparison.Ordinal);
        Assert.Contains("WITH SCHEMABINDING", sql, StringComparison.Ordinal);
        Assert.Contains("IS_ROLEMEMBER(N'scs_cross_clinic') = 1", sql, StringComparison.Ordinal);
    }
}
