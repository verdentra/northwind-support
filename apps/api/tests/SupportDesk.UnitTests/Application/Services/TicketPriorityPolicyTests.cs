using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Services;

public class TicketPriorityPolicyTests
{
    [Fact]
    public void Determine_WhenCategoryForcesCritical_OverridesRequestedPriority()
    {
        var category = new Category(
            "Security",
            requiresSpecialist: true,
            forcesCriticalPriority: true);

        var result = TicketPriorityPolicy.Determine(
            TicketPriority.Low,
            category);

        Assert.Equal(TicketPriority.Critical, result);
    }

    [Fact]
    public void Determine_WhenCategoryDoesNotForceCritical_RespectsRequestedPriority()
    {
        var category = new Category(
            "Billing",
            requiresSpecialist: true,
            forcesCriticalPriority: false);

        var result = TicketPriorityPolicy.Determine(
            TicketPriority.High,
            category);

        Assert.Equal(TicketPriority.High, result);
    }

    [Fact]
    public void Determine_WhenNoPriorityRequested_DefaultsToMedium()
    {
        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        var result = TicketPriorityPolicy.Determine(
            null,
            category);

        Assert.Equal(TicketPriority.Medium, result);
    }

    [Fact]
    public void Determine_WhenCriticalIsForced_OverridesHighPriority()
    {
        var category = new Category(
            "Outage",
            requiresSpecialist: true,
            forcesCriticalPriority: true);

        var result = TicketPriorityPolicy.Determine(
            TicketPriority.High,
            category);

        Assert.Equal(TicketPriority.Critical, result);
    }
}