using System.Globalization;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The response-time rules: how long the team has for a ticket of a given priority and customer
/// tier, and when an open ticket counts as at risk. The numbers come from configuration; this
/// class only knows how to apply them.
/// </summary>
/// <remarks>
/// Window = base window for the priority, multiplied by the premium multiplier for Premium
/// customers, and never shorter than the minimum window. The minimum applies to every tier, so a
/// configuration with a very short base window cannot produce an impossible due date either.
/// </remarks>
public sealed class SlaPolicy
{
    private readonly IReadOnlyDictionary<TicketPriority, TimeSpan> _baseWindows;

    /// <exception cref="ArgumentException">A priority has no window, or a value is out of range.</exception>
    public SlaPolicy(
        IReadOnlyDictionary<TicketPriority, TimeSpan> baseWindows,
        double premiumCustomerMultiplier,
        TimeSpan minimumWindow,
        double atRiskThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(baseWindows);

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!baseWindows.TryGetValue(priority, out var window) || window <= TimeSpan.Zero)
            {
                throw new ArgumentException(
                    $"A positive base SLA window is required for {priority} priority.", nameof(baseWindows));
            }
        }

        if (premiumCustomerMultiplier is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(premiumCustomerMultiplier), premiumCustomerMultiplier,
                "The premium multiplier must be greater than 0 and at most 1.");
        }

        if (minimumWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumWindow), minimumWindow, "The minimum SLA window must be positive.");
        }

        if (atRiskThresholdPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atRiskThresholdPercent), atRiskThresholdPercent,
                "The at-risk threshold must be between 0 and 100 percent.");
        }

        _baseWindows = new Dictionary<TicketPriority, TimeSpan>(baseWindows);
        PremiumCustomerMultiplier = premiumCustomerMultiplier;
        MinimumWindow = minimumWindow;
        AtRiskThresholdPercent = atRiskThresholdPercent;
    }

    public double PremiumCustomerMultiplier { get; }

    public TimeSpan MinimumWindow { get; }

    /// <summary>At or below this percentage of its window remaining, an open ticket is at risk.</summary>
    public double AtRiskThresholdPercent { get; }

    public TimeSpan BaseWindowFor(TicketPriority priority) => _baseWindows[priority];

    /// <summary>How long the team has to resolve a ticket of this priority for this tier.</summary>
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier)
    {
        var window = BaseWindowFor(priority);

        if (tier == CustomerTier.Premium)
        {
            window *= PremiumCustomerMultiplier;
        }

        return window < MinimumWindow ? MinimumWindow : window;
    }

    /// <summary>The due date of a window that starts at <paramref name="startUtc"/>.</summary>
    public DateTime DueAt(TicketPriority priority, CustomerTier tier, DateTime startUtc) =>
        startUtc + WindowFor(priority, tier);

    /// <summary>Where a ticket stands against its due date. See <see cref="SlaEvaluator"/>.</summary>
    public SlaStatus Evaluate(DateTime windowStartUtc, DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc) =>
        SlaEvaluator.Evaluate(windowStartUtc, dueAtUtc, resolvedAtUtc, nowUtc, AtRiskThresholdPercent);

    /// <summary>
    /// A sentence explaining the window, e.g. "High priority has an SLA window of 8 h, reduced to
    /// 4 h (x0.5) for a Premium customer."
    /// </summary>
    public string Describe(TicketPriority priority, CustomerTier tier)
    {
        var baseWindow = BaseWindowFor(priority);
        var tierWindow = baseWindow;
        var sentence = $"{priority} priority has an SLA window of {Format(baseWindow)}";

        if (tier == CustomerTier.Premium)
        {
            tierWindow = baseWindow * PremiumCustomerMultiplier;
            sentence += $", reduced to {Format(tierWindow)} " +
                $"(x{PremiumCustomerMultiplier.ToString(CultureInfo.InvariantCulture)}) for a Premium customer";
        }

        if (WindowFor(priority, tier) > tierWindow)
        {
            sentence += $", raised to the {Format(MinimumWindow)} minimum";
        }

        return sentence + ".";
    }

    private static string Format(TimeSpan window) =>
        window.TotalHours >= 1
            ? $"{window.TotalHours.ToString("0.##", CultureInfo.InvariantCulture)} h"
            : $"{window.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture)} min";
}
