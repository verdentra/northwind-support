
namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Calculates SLA status from the ticket's dates and the current time.
/// SLA status is calculated when needed, not stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? slaStartedAtUtc,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
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

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        // Without a start time, we cannot calculate the remaining percentage.
        if (slaStartedAtUtc is null)
        {
            return SlaStatus.WithinSla;
        }

        var totalWindow = (dueAtUtc.Value - slaStartedAtUtc.Value).TotalSeconds;

        if (totalWindow <= 0)
        {
            return SlaStatus.Breached;
        }

        var remainingWindow = (dueAtUtc.Value - nowUtc).TotalSeconds;
        var remainingPercent = remainingWindow / totalWindow * 100;

        return remainingPercent <= atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
