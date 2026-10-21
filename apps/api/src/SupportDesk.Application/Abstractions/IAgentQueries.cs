using SupportDesk.Application.Contracts.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of agents.
/// </summary>
public interface IAgentQueries
{
    Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Finds the least-busy eligible agent for a ticket category.
    /// Returns null when no agent is eligible.
    /// </summary>
    Task<AgentSelectionDto?> FindBestAvailableAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct);

    Task<bool> IsCurrentAgentEligibleAsync(
    int agentId,
    int categoryId,
    bool requiresSpecialist,
    CancellationToken ct);
}