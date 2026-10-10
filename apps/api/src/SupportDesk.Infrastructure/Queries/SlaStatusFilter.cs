using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// <see cref="SlaEvaluator"/> rewritten as SQL-translatable predicates, so the ticket list can
/// filter by a status that is derived, never stored, without loading tickets into memory.
/// </summary>
/// <remarks>
/// <para>Each predicate is the exact branch of the evaluator that yields its status:</para>
/// <list type="bullet">
///   <item>NotApplicable: no due date.</item>
///   <item>Met: resolved on or before the due date.</item>
///   <item>Breached: resolved after the due date, or unresolved and strictly past it.</item>
///   <item>AtRisk / WithinSla: unresolved and not past due, split by the at-risk threshold.</item>
/// </list>
/// <para>
/// The threshold compares <c>DATEDIFF(second, now, due) * 100</c> with
/// <c>threshold * DATEDIFF(second, windowStart, due)</c>, in floating point so it cannot
/// overflow. The evaluator counts whole seconds the same way
/// (<see cref="SlaEvaluator.WholeSecondsBetween"/>), so the two agree at every boundary.
/// </para>
/// </remarks>
public static class SlaStatusFilter
{
    /// <param name="status">The status to keep.</param>
    /// <param name="nowUtc">The instant to evaluate at; the same one used for the projected status.</param>
    /// <param name="atRiskThresholdPercent">From the SLA policy.</param>
    public static Expression<Func<Ticket, bool>> Matching(SlaStatus status, DateTime nowUtc, double atRiskThresholdPercent) =>
        status switch
        {
            SlaStatus.NotApplicable => t => t.DueAtUtc == null,

            SlaStatus.Met => t =>
                t.DueAtUtc != null &&
                t.ResolvedAtUtc != null &&
                t.ResolvedAtUtc <= t.DueAtUtc,

            SlaStatus.Breached => t =>
                t.DueAtUtc != null &&
                ((t.ResolvedAtUtc != null && t.ResolvedAtUtc > t.DueAtUtc) ||
                 (t.ResolvedAtUtc == null && t.DueAtUtc < nowUtc)),

            SlaStatus.AtRisk => t =>
                t.DueAtUtc != null &&
                t.ResolvedAtUtc == null &&
                t.DueAtUtc >= nowUtc &&
                (double)EF.Functions.DateDiffSecond(nowUtc, t.DueAtUtc.Value) * 100.0 <=
                atRiskThresholdPercent * (double)EF.Functions.DateDiffSecond(t.SlaStartedAtUtc ?? t.CreatedAtUtc, t.DueAtUtc.Value),

            SlaStatus.WithinSla => t =>
                t.DueAtUtc != null &&
                t.ResolvedAtUtc == null &&
                t.DueAtUtc >= nowUtc &&
                (double)EF.Functions.DateDiffSecond(nowUtc, t.DueAtUtc.Value) * 100.0 >
                atRiskThresholdPercent * (double)EF.Functions.DateDiffSecond(t.SlaStartedAtUtc ?? t.CreatedAtUtc, t.DueAtUtc.Value),

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown SLA status.")
        };
}
