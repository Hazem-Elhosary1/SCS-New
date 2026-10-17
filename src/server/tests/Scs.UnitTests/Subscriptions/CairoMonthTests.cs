using Scs.Subscriptions.Domain;

namespace Scs.UnitTests.Subscriptions;

/// <summary>
/// Spec 014, FR-022: a month runs from 00:00 on its 1st to the end of its last day, in Cairo,
/// whatever the clock's zone — summer time included.
/// </summary>
public class CairoMonthTests
{
    [Fact]
    public void Midnight_in_Cairo_starts_the_month_whatever_the_clock_says()
    {
        // Cairo is two hours ahead of UTC in November (no summer time then).
        Assert.Equal("2026-11", CairoMonth.Of(new DateTimeOffset(2026, 11, 30, 21, 59, 59, TimeSpan.Zero)));
        Assert.Equal("2026-12", CairoMonth.Of(new DateTimeOffset(2026, 11, 30, 22, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Summer_time_moves_the_boundary_by_an_hour()
    {
        // In July Cairo keeps summer time, three hours ahead of UTC.
        Assert.Equal("2026-07", CairoMonth.Of(new DateTimeOffset(2026, 7, 31, 20, 59, 59, TimeSpan.Zero)));
        Assert.Equal("2026-08", CairoMonth.Of(new DateTimeOffset(2026, 7, 31, 21, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void The_allowance_renews_on_the_first_of_next_month()
    {
        Assert.Equal(new DateOnly(2027, 1, 1), CairoMonth.RenewsOn(new DateTimeOffset(2026, 12, 15, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2027, 1, 1), CairoMonth.RenewsOn(new DateTimeOffset(2026, 11, 30, 22, 30, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("2026-13", false)]
    [InlineData("26-10", false)]
    [InlineData(null, false)]
    public void Only_a_month_it_would_write_is_a_month(string? value, bool month) =>
        Assert.Equal(month, CairoMonth.IsMonth(value));
}
