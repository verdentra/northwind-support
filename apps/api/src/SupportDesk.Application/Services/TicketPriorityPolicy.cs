using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

/// <summary>
/// Determines the initial priority of a ticket using
/// the requested priority and the category's rules.
/// </summary>
public static class TicketPriorityPolicy
{
    public static TicketPriority Determine(
        TicketPriority? requestedPriority,
        Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (category.ForcesCriticalPriority)
        {
            return TicketPriority.Critical;
        }

        return requestedPriority ?? TicketPriority.Medium;
    }
}