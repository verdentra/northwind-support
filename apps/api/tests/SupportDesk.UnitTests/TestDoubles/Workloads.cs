using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>Agents as the assignment rules see them, with sensible defaults.</summary>
public static class Workloads
{
    public static AgentWorkload Workload(
        int id,
        int open = 0,
        int max = 10,
        bool active = true,
        int[]? specializations = null) =>
        new(id, $"Agent {id}", active, max, open, specializations ?? []);
}
