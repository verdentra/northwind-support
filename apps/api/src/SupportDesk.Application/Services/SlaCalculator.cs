using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

/// <summary>
/// Calculates SLA windows using configurable business rules.
/// </summary>
public sealed class SlaCalculator(IOptions<SlaOptions> options)
{
    public double CalculateWindowHours(
        TicketPriority priority,
        CustomerTier customerTier)
    {
        var settings = options.Value;

        if (!settings.BaseWindowHours.TryGetValue(priority, out var baseHours))
        {
            throw new InvalidOperationException(
                $"No SLA window configured for priority {priority}.");
        }

        var windowHours = customerTier == CustomerTier.Premium
            ? baseHours * settings.PremiumCustomerMultiplier
            : baseHours;

        return Math.Max(windowHours, settings.MinimumWindowHours);
    }

    public DateTime CalculateDueDate(
        TicketPriority priority,
        CustomerTier customerTier,
        DateTime fromUtc)
    {
        var windowHours = CalculateWindowHours(priority, customerTier);

        return fromUtc.AddHours(windowHours);
    }
}