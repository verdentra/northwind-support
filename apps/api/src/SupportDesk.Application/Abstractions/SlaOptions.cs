namespace SupportDesk.Application.Abstractions;

public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    public Dictionary<string, int> BaseWindowHours { get; set; } = new();

    public double PremiumCustomerMultiplier { get; set; }

    public double MinimumWindowHours { get; set; }

    public double AtRiskThresholdPercent { get; set; }
}
