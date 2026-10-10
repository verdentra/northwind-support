using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Configuration;

/// <summary>
/// The <c>Sla</c> configuration section. Bound and validated at start-up, then turned into the
/// <see cref="SlaPolicy"/> the rest of the application uses, so the numbers live only in
/// configuration.
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    /// <summary>Base window per priority, in hours.</summary>
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];

    /// <summary>Premium customers' windows are multiplied by this, e.g. 0.5 for half.</summary>
    public double PremiumCustomerMultiplier { get; set; }

    /// <summary>No window is ever shorter than this, in hours.</summary>
    public double MinimumWindowHours { get; set; }

    /// <summary>An open ticket with this percentage or less of its window left is at risk.</summary>
    public double AtRiskThresholdPercent { get; set; }

    /// <summary>Everything wrong with these values; empty when they are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!BaseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                errors.Add($"{SectionName}:{nameof(BaseWindowHours)}:{priority} must be a positive number of hours.");
            }
        }

        if (PremiumCustomerMultiplier is <= 0 or > 1)
        {
            errors.Add($"{SectionName}:{nameof(PremiumCustomerMultiplier)} must be greater than 0 and at most 1.");
        }

        if (MinimumWindowHours <= 0)
        {
            errors.Add($"{SectionName}:{nameof(MinimumWindowHours)} must be a positive number of hours.");
        }

        if (AtRiskThresholdPercent is < 0 or > 100)
        {
            errors.Add($"{SectionName}:{nameof(AtRiskThresholdPercent)} must be between 0 and 100.");
        }

        return errors;
    }

    /// <exception cref="ArgumentException">The values are not usable; see <see cref="Validate"/>.</exception>
    public SlaPolicy ToPolicy() =>
        new(
            BaseWindowHours.ToDictionary(pair => pair.Key, pair => TimeSpan.FromHours(pair.Value)),
            PremiumCustomerMultiplier,
            TimeSpan.FromHours(MinimumWindowHours),
            AtRiskThresholdPercent);
}
