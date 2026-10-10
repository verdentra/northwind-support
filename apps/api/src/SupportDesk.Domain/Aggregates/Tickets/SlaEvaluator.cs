namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
/// <remarks>
/// <para>The rules, in the order they are applied:</para>
/// <list type="number">
///   <item>No due date: <see cref="SlaStatus.NotApplicable"/>.</item>
///   <item>Resolved on or before the due date: <see cref="SlaStatus.Met"/>; resolved after it:
///   <see cref="SlaStatus.Breached"/>.</item>
///   <item>Unresolved and strictly past the due date: <see cref="SlaStatus.Breached"/>.</item>
///   <item>Unresolved, not past due, with the threshold or less of its window remaining:
///   <see cref="SlaStatus.AtRisk"/>. Exactly at the due instant counts as at risk, not breached.</item>
///   <item>Otherwise <see cref="SlaStatus.WithinSla"/>.</item>
/// </list>
/// <para>
/// "Window remaining" is measured in whole seconds, counted the way SQL Server's
/// <c>DATEDIFF(second, a, b)</c> counts them (the number of second boundaries crossed). The
/// database-side filter uses exactly that function, so a ticket is never listed under one status
/// and shown with another.
/// </para>
/// </remarks>
public static class SlaEvaluator
{
    /// <param name="windowStartUtc">When the current SLA window started: creation, or the last escalation.</param>
    /// <param name="dueAtUtc">The due date, or null when no SLA applies.</param>
    /// <param name="resolvedAtUtc">When the ticket was resolved, or null while it is unresolved.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="atRiskThresholdPercent">At or below this share of the window remaining, the ticket is at risk.</param>
    public static SlaStatus Evaluate(
        DateTime windowStartUtc,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        double atRiskThresholdPercent)
    {
        if (dueAtUtc is not { } due)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is { } resolved)
        {
            return resolved <= due ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > due)
        {
            return SlaStatus.Breached;
        }

        var remainingSeconds = WholeSecondsBetween(nowUtc, due);
        var windowSeconds = WholeSecondsBetween(windowStartUtc, due);

        return remainingSeconds * 100.0 <= atRiskThresholdPercent * windowSeconds
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }

    /// <summary>
    /// The number of second boundaries between two instants - the same count SQL Server's
    /// <c>DATEDIFF(second, from, to)</c> returns.
    /// </summary>
    public static long WholeSecondsBetween(DateTime fromUtc, DateTime toUtc) =>
        (toUtc.Ticks / TimeSpan.TicksPerSecond) - (fromUtc.Ticks / TimeSpan.TicksPerSecond);
}
