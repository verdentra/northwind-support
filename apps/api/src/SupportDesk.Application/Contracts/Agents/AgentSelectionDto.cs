namespace SupportDesk.Application.Contracts.Agents;

/// <summary>
/// An eligible agent selected for automatic ticket assignment.
/// </summary>
public sealed record AgentSelectionDto(
    int Id,
    string FullName,
    int OpenTicketCount);