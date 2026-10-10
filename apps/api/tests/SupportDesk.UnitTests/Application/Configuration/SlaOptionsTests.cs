using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Configuration;

public class SlaOptionsTests
{
    [Fact]
    public void Validate_TheDevelopmentValues_HaveNoErrors() =>
        Assert.Empty(TestSla.Options().Validate());

    [Fact]
    public void Validate_ReportsEveryProblem()
    {
        var options = TestSla.Options();
        options.BaseWindowHours.Remove(TicketPriority.High);
        options.BaseWindowHours[TicketPriority.Low] = 0;
        options.PremiumCustomerMultiplier = 2;
        options.MinimumWindowHours = 0;
        options.AtRiskThresholdPercent = 101;

        var errors = options.Validate();

        Assert.Equal(5, errors.Count);
        Assert.Contains(errors, e => e.Contains("High"));
        Assert.Contains(errors, e => e.Contains("Low"));
        Assert.Contains(errors, e => e.Contains("PremiumCustomerMultiplier"));
        Assert.Contains(errors, e => e.Contains("MinimumWindowHours"));
        Assert.Contains(errors, e => e.Contains("AtRiskThresholdPercent"));
    }

    [Fact]
    public void ToPolicy_CarriesTheThreshold() =>
        Assert.Equal(25, TestSla.Options().ToPolicy().AtRiskThresholdPercent);
}
