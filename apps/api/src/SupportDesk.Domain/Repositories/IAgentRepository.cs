using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The agent with this email (case-insensitive), or null. Not tracked.</summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Every agent with their open-ticket count and specializations, in a single query, ordered
    /// by id. What <see cref="AgentAssignmentPolicy"/> chooses from.
    /// </summary>
    Task<IReadOnlyList<AgentWorkload>> GetWorkloadsAsync(CancellationToken ct);
}
