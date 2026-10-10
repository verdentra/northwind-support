using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>The priority a new ticket gets, and why.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);

/// <summary>
/// How a ticket's priority is decided when it is raised, and how it moves when escalated.
/// </summary>
public static class TicketPriorityRules
{
    /// <summary>The priority used when the reporter does not ask for one.</summary>
    public const TicketPriority Default = TicketPriority.Medium;

    /// <summary>
    /// The requested priority, or <see cref="Default"/> when none was requested - unless the
    /// category forces Critical, which always wins.
    /// </summary>
    public static PriorityDecision Decide(TicketPriority? requested, Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (category.ForcesCriticalPriority)
        {
            var requestedText = requested is null ? "no priority was requested" : $"{requested} was requested";

            return new PriorityDecision(
                TicketPriority.Critical,
                $"{category.Name} tickets are always {TicketPriority.Critical} ({requestedText}).");
        }

        return requested is { } priority
            ? new PriorityDecision(priority, $"{priority} priority, as requested.")
            : new PriorityDecision(Default, $"No priority was requested, so it defaults to {Default}.");
    }

    /// <summary>True when there is a higher priority to escalate to.</summary>
    public static bool CanRaise(TicketPriority priority) => priority < TicketPriority.Critical;

    /// <summary>The next priority up: Low to Medium, Medium to High, High to Critical.</summary>
    /// <exception cref="InvalidOperationException">The priority is already Critical.</exception>
    public static TicketPriority Raise(TicketPriority priority) =>
        CanRaise(priority)
            ? priority + 1
            : throw new InvalidOperationException($"{priority} is the highest priority and cannot be raised.");
}
