using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Configuration;

/// <summary>
/// Configurable SLA rules shared by ticket creation,
/// escalation, and SLA status evaluation.
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];

    public double PremiumCustomerMultiplier { get; set; }

    public double MinimumWindowHours { get; set; }

    public double AtRiskThresholdPercent { get; set; }
}