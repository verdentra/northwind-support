using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>
/// An 8-hour window that starts at <see cref="Start"/>: with a 25% threshold it becomes at risk
/// with 2 hours left, at <see cref="AtRiskFrom"/>.
/// </summary>
public class SlaEvaluatorTests
{
    private const double Threshold = 25;

    private static readonly DateTime Start = FixedClock.DefaultNow;
    private static readonly DateTime Due = Start.AddHours(8);
    private static readonly DateTime AtRiskFrom = Due.AddHours(-2);

    private static SlaStatus Open(DateTime now) => SlaEvaluator.Evaluate(Start, Due, null, now, Threshold);

    private static SlaStatus ResolvedAt(DateTime resolved) =>
        SlaEvaluator.Evaluate(Start, Due, resolved, Due.AddDays(3), Threshold);

    [Fact]
    public void WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(Start, null, null, Start, Threshold));

    [Fact]
    public void WithoutADueDate_IsNotApplicableEvenWhenResolved() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(Start, null, Start, Start, Threshold));

    [Fact]
    public void ResolvedBeforeTheDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, ResolvedAt(Due.AddMinutes(-1)));

    [Fact]
    public void ResolvedExactlyAtTheDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, ResolvedAt(Due));

    [Fact]
    public void ResolvedOneTickAfterTheDueDate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, ResolvedAt(Due.AddTicks(1)));

    [Fact]
    public void Resolved_IgnoresTheCurrentTime()
    {
        // Resolved on time; "now" being long past the due date does not matter.
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Start, Due, Start.AddHours(1), Due.AddYears(1), Threshold));
    }

    [Fact]
    public void OpenJustAfterTheWindowStarts_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, Open(Start));

    [Fact]
    public void OpenOneSecondBeforeTheThreshold_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, Open(AtRiskFrom.AddSeconds(-1)));

    [Fact]
    public void OpenWithExactlyTheThresholdRemaining_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Open(AtRiskFrom));

    [Fact]
    public void OpenWithLessThanTheThresholdRemaining_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Open(Due.AddMinutes(-1)));

    [Fact]
    public void OpenExactlyAtTheDueDate_IsAtRiskNotBreached() =>
        Assert.Equal(SlaStatus.AtRisk, Open(Due));

    [Fact]
    public void OpenOneTickPastTheDueDate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Open(Due.AddTicks(1)));

    [Fact]
    public void Threshold_IsMeasuredAgainstTheWindowStartNotTheCreationTime()
    {
        // Escalated two hours in: a fresh 4-hour window from then. One hour left is 25% of the new
        // window (at risk), but would only be ~17% of a window measured from creation.
        var escalatedAt = Start.AddHours(2);
        var due = escalatedAt.AddHours(4);

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(escalatedAt, due, null, due.AddHours(-1).AddSeconds(-1), Threshold));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(escalatedAt, due, null, due.AddHours(-1), Threshold));
    }

    [Fact]
    public void RemainingTime_IsCountedInWholeSecondsLikeSqlServer()
    {
        // Window 09:00:00.500 to 17:00:00.500; the threshold is 2 h (7200 s) remaining. At
        // 15:00:00.400 the exact remainder is 7200.1 s, but SQL Server's DATEDIFF(second, ...)
        // counts 7200 second boundaries - and so does the evaluator, so both say at risk.
        var start = Start.AddMilliseconds(500);
        var due = start.AddHours(8);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(start, due, null, due.AddHours(-2).AddMilliseconds(-100), Threshold));
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(start, due, null, due.AddHours(-2).AddMilliseconds(-600), Threshold));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 999, 0)]
    [InlineData(999, 1000, 1)]
    [InlineData(0, 1000, 1)]
    [InlineData(500, 2400, 2)]
    public void WholeSecondsBetween_CountsSecondBoundariesCrossed(int fromMs, int toMs, long expected)
    {
        var from = Start.AddMilliseconds(fromMs);
        var to = Start.AddMilliseconds(toMs);

        Assert.Equal(expected, SlaEvaluator.WholeSecondsBetween(from, to));
    }
}
