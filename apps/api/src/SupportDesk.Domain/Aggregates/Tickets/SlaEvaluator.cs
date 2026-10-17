namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is derived, never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc) =>
        Evaluate(dueAtUtc, resolvedAtUtc, nowUtc, null, 25);

    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? windowStartedAtUtc,
        double atRiskThresholdPercent)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc
                ? SlaStatus.Met
                : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc.Value)
        {
            return SlaStatus.Breached;
        }

        if (windowStartedAtUtc is null)
        {
            return SlaStatus.WithinSla;
        }

        var totalWindow = dueAtUtc.Value - windowStartedAtUtc.Value;

        if (totalWindow <= TimeSpan.Zero)
        {
            return SlaStatus.WithinSla;
        }

        var remainingWindow = dueAtUtc.Value - nowUtc;
        var remainingPercent = remainingWindow.TotalSeconds / totalWindow.TotalSeconds * 100;

        return remainingPercent <= atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
