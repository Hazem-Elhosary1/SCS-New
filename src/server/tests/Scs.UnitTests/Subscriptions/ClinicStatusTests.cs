using Scs.Subscriptions.Domain;

namespace Scs.UnitTests.Subscriptions;

/// <summary>
/// Spec 015, FR-015 (data-model.md §3): «تجربة» until the trial's end and «نشطة» from it, with the
/// days left counted up.
/// </summary>
public class ClinicStatusTests
{
    private static readonly DateTimeOffset End = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_clinic_is_on_trial_until_the_second_its_trial_ends()
    {
        var before = ClinicStatus.Of("active", End, End.AddSeconds(-1));
        var at = ClinicStatus.Of("active", End, End);

        Assert.Equal(new ClinicStatusView("trial", "تجربة", "Trial", "info"), before);
        Assert.Equal(new ClinicStatusView("active", "نشطة", "Active", "success"), at);
    }

    [Fact]
    public void A_clinic_with_no_trial_is_active()
    {
        Assert.Equal("active", ClinicStatus.Of("active", null, End).Code);
    }

    [Fact]
    public void A_stored_state_of_spec_016_is_never_shown_as_active()
    {
        Assert.Equal("suspended", ClinicStatus.Of("suspended", End, End.AddDays(-1)).Code);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    [InlineData(14 * 24, 14)]
    public void The_days_left_are_counted_up(int hoursLeft, int days)
    {
        Assert.Equal(days, ClinicStatus.DaysLeft(End, End.AddHours(-hoursLeft)));
    }
}
