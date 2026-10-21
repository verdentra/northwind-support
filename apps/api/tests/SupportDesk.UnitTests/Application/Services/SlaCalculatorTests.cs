using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Services;

public class SlaCalculatorTests
{
    private static SlaCalculator CreateCalculator(
        double criticalHours = 4)
    {
        var settings = new SlaOptions
        {
            BaseWindowHours = new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Low] = 72,
                [TicketPriority.Medium] = 24,
                [TicketPriority.High] = 8,
                [TicketPriority.Critical] = criticalHours
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1,
            AtRiskThresholdPercent = 25
        };

        return new SlaCalculator(Options.Create(settings));
    }

    [Theory]
    [InlineData(TicketPriority.Low, 72, 36)]
    [InlineData(TicketPriority.Medium, 24, 12)]
    [InlineData(TicketPriority.High, 8, 4)]
    [InlineData(TicketPriority.Critical, 4, 2)]
    public void CalculateWindowHours_UsesPriorityAndCustomerTier(
        TicketPriority priority,
        double expectedStandard,
        double expectedPremium)
    {
        var calculator = CreateCalculator();

        Assert.Equal(
            expectedStandard,
            calculator.CalculateWindowHours(
                priority, CustomerTier.Standard));

        Assert.Equal(
            expectedPremium,
            calculator.CalculateWindowHours(
                priority, CustomerTier.Premium));
    }

    [Fact]
    public void CalculateWindowHours_PremiumNeverFallsBelowOneHour()
    {
        var calculator = CreateCalculator(criticalHours: 1);

        var result = calculator.CalculateWindowHours(
            TicketPriority.Critical,
            CustomerTier.Premium);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CalculateDueDate_AddsWindowToProvidedTime()
    {
        var calculator = CreateCalculator();
        var now = FixedClock.DefaultNow;

        var result = calculator.CalculateDueDate(
            TicketPriority.Critical,
            CustomerTier.Premium,
            now);

        Assert.Equal(now.AddHours(2), result);
    }
}