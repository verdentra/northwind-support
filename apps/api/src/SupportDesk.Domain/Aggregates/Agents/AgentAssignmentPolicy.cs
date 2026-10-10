namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>Who should own a ticket, and why. A null agent is a normal outcome, not an error.</summary>
/// <param name="AgentId">The chosen agent, or null when nobody is eligible.</param>
/// <param name="AgentName">The chosen agent's name, or null.</param>
/// <param name="Reason">A sentence a team lead can read.</param>
/// <param name="KeptCurrentAgent">True when the ticket's existing owner was kept.</param>
public sealed record AssignmentDecision(int? AgentId, string? AgentName, string Reason, bool KeptCurrentAgent);

/// <summary>
/// Chooses the owner of a ticket. Used both when a ticket is raised and when it is escalated, so
/// the two can never disagree about who may take a ticket.
/// </summary>
/// <remarks>
/// <para>An agent is eligible when they are active, have fewer open tickets than their
/// <see cref="Agent.MaxOpenTickets"/>, and - for a category that requires a specialist - list
/// that category as a specialization.</para>
/// <para>Among eligible agents the one with the fewest open tickets wins; ties go to the lowest
/// agent id, so the same data always produces the same owner.</para>
/// <para>When re-evaluating a ticket's existing owner, that ticket is already part of their open
/// count. It is left out when judging whether they still have room, because keeping a ticket they
/// already hold does not add to their load.</para>
/// </remarks>
public static class AgentAssignmentPolicy
{
    /// <param name="agents">Every agent with their current workload.</param>
    /// <param name="categoryId">The ticket's category.</param>
    /// <param name="categoryName">The category's name, for the reason text.</param>
    /// <param name="requiresSpecialist">Whether only specialists in the category may take it.</param>
    /// <param name="currentAgentId">
    /// The ticket's current owner when re-evaluating an existing ticket; null for a new one.
    /// </param>
    public static AssignmentDecision Choose(
        IReadOnlyCollection<AgentWorkload> agents,
        int categoryId,
        string categoryName,
        bool requiresSpecialist,
        int? currentAgentId = null)
    {
        ArgumentNullException.ThrowIfNull(agents);

        var rules = new Eligibility(categoryId, categoryName, requiresSpecialist, currentAgentId);
        var current = currentAgentId is { } id ? agents.FirstOrDefault(a => a.AgentId == id) : null;
        var currentProblem = current is null ? null : rules.Problem(current);

        if (current is not null && currentProblem is null)
        {
            return new AssignmentDecision(
                current.AgentId,
                current.FullName,
                $"Kept {current.FullName}: still eligible, with {rules.EffectiveOpenCount(current)} other " +
                $"open ticket(s) of {current.MaxOpenTickets}.",
                KeptCurrentAgent: true);
        }

        var prefix = current is null
            ? currentAgentId is null ? string.Empty : $"The previous owner (agent {currentAgentId}) no longer exists. "
            : $"{current.FullName} is no longer eligible ({currentProblem}). ";

        var eligible = agents
            .Where(a => a.AgentId != currentAgentId && rules.Problem(a) is null)
            .OrderBy(rules.EffectiveOpenCount)
            .ThenBy(a => a.AgentId)
            .ToList();

        if (eligible.Count == 0)
        {
            return new AssignmentDecision(
                null, null, prefix + "Left unassigned: " + rules.WhyNobody(agents), KeptCurrentAgent: false);
        }

        var chosen = eligible[0];
        var tie = eligible.Count > 1 && rules.EffectiveOpenCount(eligible[1]) == rules.EffectiveOpenCount(chosen)
            ? " (tie broken by lowest agent id)"
            : string.Empty;

        return new AssignmentDecision(
            chosen.AgentId,
            chosen.FullName,
            prefix + $"Assigned to {chosen.FullName}: fewest open tickets ({chosen.OpenTicketCount} of " +
            $"{chosen.MaxOpenTickets}) among {eligible.Count} eligible agent(s){tie}.",
            KeptCurrentAgent: false);
    }

    private sealed record Eligibility(int CategoryId, string CategoryName, bool RequiresSpecialist, int? CurrentAgentId)
    {
        /// <summary>Open tickets, leaving out the ticket being re-evaluated when the agent holds it.</summary>
        public int EffectiveOpenCount(AgentWorkload agent) =>
            agent.AgentId == CurrentAgentId ? Math.Max(0, agent.OpenTicketCount - 1) : agent.OpenTicketCount;

        /// <summary>Why the agent may not take the ticket, or null when they may.</summary>
        public string? Problem(AgentWorkload agent)
        {
            if (!agent.IsActive)
            {
                return "inactive";
            }

            if (RequiresSpecialist && !agent.SpecializationCategoryIds.Contains(CategoryId))
            {
                return $"not a {CategoryName} specialist";
            }

            return EffectiveOpenCount(agent) >= agent.MaxOpenTickets
                ? $"at their limit of {agent.MaxOpenTickets} open tickets"
                : null;
        }

        public string WhyNobody(IReadOnlyCollection<AgentWorkload> agents)
        {
            var active = agents.Where(a => a.IsActive).ToList();

            if (active.Count == 0)
            {
                return "there are no active agents.";
            }

            if (!RequiresSpecialist)
            {
                return $"all {active.Count} active agent(s) are at their open-ticket limit.";
            }

            var specialists = active.Where(a => a.SpecializationCategoryIds.Contains(CategoryId)).ToList();

            return specialists.Count == 0
                ? $"{CategoryName} requires a specialist and no active agent specialises in it."
                : $"{CategoryName} requires a specialist and all {specialists.Count} active " +
                  $"{CategoryName} specialist(s) are at their open-ticket limit.";
        }
    }
}
