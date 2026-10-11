
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private const double AtRiskThresholdPercent = 25;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(
            SlaStatus.NotApplicable,
            SlaEvaluator.Evaluate(null, null, null, Now, AtRiskThresholdPercent));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(
            SlaStatus.Met,
            SlaEvaluator.Evaluate(
                Now.AddHours(-1), Now, Now, Now.AddDays(1), AtRiskThresholdPercent));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(
            SlaStatus.Breached,
            SlaEvaluator.Evaluate(
                Now.AddHours(-2), Now, Now.AddMinutes(1), Now, AtRiskThresholdPercent));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(
            SlaStatus.Breached,
            SlaEvaluator.Evaluate(
                Now.AddHours(-2), Now.AddMinutes(-1), null, Now, AtRiskThresholdPercent));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(
                Now, Now.AddHours(4), null, Now.AddHours(1), AtRiskThresholdPercent));

    [Fact]
    public void Evaluate_OpenWith25PercentRemaining_IsAtRisk() =>
        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(
                Now.AddHours(-3), Now.AddHours(1), null, Now, AtRiskThresholdPercent));

    [Fact]
    public void Evaluate_OpenAtDueTime_IsAtRisk()
    {
        var dueAt = Now.AddHours(4);

        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(
                Now, dueAt, null, dueAt, AtRiskThresholdPercent));
    }

    [Fact]
    public void Evaluate_OpenWithMoreThan25PercentRemaining_IsWithinSla() =>
        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(
                Now, Now.AddHours(4), null, Now.AddHours(2), AtRiskThresholdPercent));
}
