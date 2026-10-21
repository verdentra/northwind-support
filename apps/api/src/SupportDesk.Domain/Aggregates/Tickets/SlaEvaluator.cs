
namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <summary>
    /// Evaluates SLA status without an AtRisk calculation.
    /// Preserved for existing callers.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc) =>
        dueAtUtc is null ? SlaStatus.NotApplicable
        : resolvedAtUtc is not null
            ? (resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
        : nowUtc > dueAtUtc ? SlaStatus.Breached
        : SlaStatus.WithinSla;

    /// <summary>
    /// Evaluates SLA status using the configured AtRisk threshold
    /// and the beginning of the current SLA window.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime windowStartUtc,
        double atRiskThresholdPercent)
    {
        if (atRiskThresholdPercent < 0 || atRiskThresholdPercent > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atRiskThresholdPercent));
        }

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

        var totalWindow = dueAtUtc.Value - windowStartUtc;

        if (totalWindow <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The SLA window must have a positive duration.",
                nameof(windowStartUtc));
        }

        var remainingWindow = dueAtUtc.Value - nowUtc;

        var remainingPercent =
            remainingWindow.TotalSeconds / totalWindow.TotalSeconds * 100;

        return remainingPercent <= atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
