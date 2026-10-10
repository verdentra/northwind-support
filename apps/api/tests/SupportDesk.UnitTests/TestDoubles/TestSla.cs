using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The SLA policy the tests run against: the same values as the <c>Sla</c> section of
/// appsettings.json, built through the same <see cref="SlaOptions"/> the API binds.
/// </summary>
public static class TestSla
{
    public static SlaOptions Options() => new()
    {
        BaseWindowHours = new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    public static SlaPolicy Policy { get; } = Options().ToPolicy();
}
