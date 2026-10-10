using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    private static readonly SlaPolicy Policy = TestSla.Policy;

    [Theory]
    [InlineData(TicketPriority.Low, 72d)]
    [InlineData(TicketPriority.Medium, 24d)]
    [InlineData(TicketPriority.High, 8d)]
    [InlineData(TicketPriority.Critical, 4d)]
    public void WindowFor_StandardCustomer_IsTheBaseWindow(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), Policy.WindowFor(priority, CustomerTier.Standard));

    [Theory]
    [InlineData(TicketPriority.Low, 36d)]
    [InlineData(TicketPriority.Medium, 12d)]
    [InlineData(TicketPriority.High, 4d)]
    [InlineData(TicketPriority.Critical, 2d)]
    public void WindowFor_PremiumCustomer_IsHalved(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), Policy.WindowFor(priority, CustomerTier.Premium));

    [Fact]
    public void WindowFor_NeverGoesBelowTheMinimum()
    {
        // A 1.5 h Critical window halves to 45 minutes for Premium, which the 1 h floor lifts.
        var options = TestSla.Options();
        options.BaseWindowHours[TicketPriority.Critical] = 1.5;
        var policy = options.ToPolicy();

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
        Assert.Equal(TimeSpan.FromHours(1.5), policy.WindowFor(TicketPriority.Critical, CustomerTier.Standard));
    }

    [Fact]
    public void WindowFor_ExactlyAtTheMinimum_IsKept()
    {
        var options = TestSla.Options();
        options.BaseWindowHours[TicketPriority.Critical] = 2;
        var policy = options.ToPolicy();

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void DueAt_AddsTheWindowToTheStart()
    {
        var start = FixedClock.DefaultNow;

        Assert.Equal(start.AddHours(4), Policy.DueAt(TicketPriority.High, CustomerTier.Premium, start));
    }

    [Fact]
    public void Describe_ExplainsThePremiumDiscount()
    {
        var text = Policy.Describe(TicketPriority.High, CustomerTier.Premium);

        Assert.Contains("8 h", text);
        Assert.Contains("4 h", text);
        Assert.Contains("Premium", text);
    }

    [Fact]
    public void Describe_ExplainsTheMinimum()
    {
        var options = TestSla.Options();
        options.BaseWindowHours[TicketPriority.Critical] = 1.5;

        var text = options.ToPolicy().Describe(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Contains("45 min", text);
        Assert.Contains("1 h minimum", text);
    }

    [Fact]
    public void Constructor_RejectsAMissingPriority()
    {
        var options = TestSla.Options();
        options.BaseWindowHours.Remove(TicketPriority.Low);

        Assert.Throws<ArgumentException>(() => options.ToPolicy());
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1.5d)]
    public void Constructor_RejectsAnUnusablePremiumMultiplier(double multiplier)
    {
        var options = TestSla.Options();
        options.PremiumCustomerMultiplier = multiplier;

        Assert.ThrowsAny<ArgumentException>(() => options.ToPolicy());
    }
}
