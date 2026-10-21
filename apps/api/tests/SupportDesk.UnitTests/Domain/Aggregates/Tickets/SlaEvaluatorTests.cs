using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private static readonly DateTime StartUtc =
        new(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime DueUtc =
        StartUtc.AddHours(4);
        
    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
        public void Evaluate_NoDueDate_ReturnsNotApplicable()
        {
            var result = SlaEvaluator.Evaluate(
                null, null, StartUtc, StartUtc, 25);

            Assert.Equal(SlaStatus.NotApplicable, result);
        }

    [Fact]
    public void Evaluate_PlentyOfTimeRemaining_ReturnsWithinSla()
    {
        var result = SlaEvaluator.Evaluate(
            DueUtc, null, StartUtc.AddHours(2), StartUtc, 25);

        Assert.Equal(SlaStatus.WithinSla, result);
    }

    [Fact]
    public void Evaluate_Exactly25PercentRemaining_ReturnsAtRisk()
    {
        // A four-hour window has one hour remaining at 1 PM.
        var result = SlaEvaluator.Evaluate(
            DueUtc, null, StartUtc.AddHours(3), StartUtc, 25);

        Assert.Equal(SlaStatus.AtRisk, result);
    }

    [Fact]
    public void Evaluate_JustAbove25PercentRemaining_ReturnsWithinSla()
    {
        var result = SlaEvaluator.Evaluate(
            DueUtc, null,
            StartUtc.AddHours(3).AddSeconds(-1),
            StartUtc, 25);

        Assert.Equal(SlaStatus.WithinSla, result);
    }

    [Fact]
    public void Evaluate_PastDueDate_ReturnsBreached()
    {
        var result = SlaEvaluator.Evaluate(
            DueUtc, null, DueUtc.AddSeconds(1), StartUtc, 25);

        Assert.Equal(SlaStatus.Breached, result);
    }

    [Fact]
    public void Evaluate_ResolvedBeforeDeadline_ReturnsMet()
    {
        var result = SlaEvaluator.Evaluate(
            DueUtc, StartUtc.AddHours(2),
            DueUtc.AddHours(5), StartUtc, 25);

        Assert.Equal(SlaStatus.Met, result);
    }

    [Fact]
    public void Evaluate_ResolvedAfterDeadline_ReturnsBreached()
    {
        var result = SlaEvaluator.Evaluate(
            DueUtc, DueUtc.AddMinutes(1),
            DueUtc.AddHours(2), StartUtc, 25);

        Assert.Equal(SlaStatus.Breached, result);
    }

    [Fact]
    public void Evaluate_ResolvedExactlyAtDeadline_ReturnsMet()
    {
        var result = SlaEvaluator.Evaluate(
            DueUtc, DueUtc, DueUtc.AddHours(1), StartUtc, 25);

        Assert.Equal(SlaStatus.Met, result);
    }

    [Fact]
    public void Evaluate_UsesConfiguredThreshold()
    {
        // Two hours remain out of four: 50%.
        // A configured threshold of 60% should mark it AtRisk.
        var result = SlaEvaluator.Evaluate(
            DueUtc, null, StartUtc.AddHours(2), StartUtc, 60);

        Assert.Equal(SlaStatus.AtRisk, result);
    }

    [Fact]
    public void Evaluate_InvalidThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlaEvaluator.Evaluate(
                DueUtc, null, StartUtc, StartUtc, 101));
    }
}
