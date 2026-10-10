namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// An agent as the assignment rules see them: whether they take work, how much they hold now,
/// and what they are qualified for. Read in one query for every agent, so choosing an owner
/// never costs a query per agent.
/// </summary>
/// <param name="OpenTicketCount">Tickets assigned to the agent that are neither Resolved nor Closed.</param>
public sealed record AgentWorkload(
    int AgentId,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyCollection<int> SpecializationCategoryIds);
