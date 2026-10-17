using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets;

/// <summary>
/// Applies the shared priority, SLA, and assignment rules.
/// Ticket creation and escalation will both use this service.
/// </summary>
public sealed class TicketTriageService(
    IAgentRepository agents,
    ITicketRepository tickets,
    SlaOptions slaOptions)
{
    public async Task<TriageDecision> DecideAsync(
        Customer customer,
        Category category,
        TicketPriority? requestedPriority,
        DateTime calculationTimeUtc,
        int? currentAgentId,
        CancellationToken ct)
    {
        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : requestedPriority ?? TicketPriority.Medium;

        if (!slaOptions.BaseWindowHours.TryGetValue(
                priority.ToString(),
                out var baseWindowHours))
        {
            throw new InvalidOperationException(
                $"No SLA window is configured for priority '{priority}'.");
        }

        var multiplier = customer.Tier == CustomerTier.Premium
            ? slaOptions.PremiumCustomerMultiplier
            : 1.0;

        var configuredWindowHours = baseWindowHours * multiplier;

        var slaWindowHours = Math.Max(
            configuredWindowHours,
            slaOptions.MinimumWindowHours);

        var priorityReason = category.ForcesCriticalPriority
            ? $"Category '{category.Name}' is always treated as Critical"
            : requestedPriority.HasValue
                ? $"Requested priority '{requestedPriority.Value}' was applied"
                : "No priority was requested, so Medium was applied";

        var slaReason = customer.Tier == CustomerTier.Premium
            ? $"{priority} base window {baseWindowHours:0.##}h, halved for a Premium customer"
            : $"{priority} base window {baseWindowHours:0.##}h";

        if (configuredWindowHours < slaOptions.MinimumWindowHours)
        {
            slaReason +=
                $", raised to the {slaOptions.MinimumWindowHours:0.##}h minimum";
        }

        var candidates = new List<(Agent Agent, int OpenCount)>();

        var allAgents = await agents.GetAllAsync(ct);

        foreach (var agent in allAgents)
        {
            if (!agent.IsActive)
            {
                continue;
            }

            if (category.RequiresSpecialist &&
                !agent.Specializations.Any(
                    specialization => specialization.CategoryId == category.Id))
            {
                continue;
            }

            var openCount = await tickets.CountOpenByAgentAsync(agent.Id, ct);

            if (openCount < agent.MaxOpenTickets)
            {
                candidates.Add((agent, openCount));
            }
        }

        Agent? selectedAgent = null;

        // During escalation, keep the current agent if they are still eligible.
        if (currentAgentId.HasValue)
        {
            selectedAgent = candidates
                .FirstOrDefault(candidate =>
                    candidate.Agent.Id == currentAgentId.Value)
                .Agent;
        }

        // For new tickets, or when the current agent is no longer eligible,
        // choose the eligible agent with the fewest open tickets.
        selectedAgent ??= candidates
            .OrderBy(candidate => candidate.OpenCount)
            .ThenBy(candidate => candidate.Agent.Id)
            .Select(candidate => candidate.Agent)
            .FirstOrDefault();

        var assignmentReason = selectedAgent is not null
            ? currentAgentId.HasValue &&
              selectedAgent.Id == currentAgentId.Value
                ? "Current agent remains eligible"
                : category.RequiresSpecialist
                    ? $"Fewest open tickets among {category.Name} specialists"
                    : "Fewest open tickets among eligible active agents"
            : category.RequiresSpecialist
                ? $"No available agent specialised in {category.Name}"
                : "No active agent is below the open-ticket limit";

        return new TriageDecision
        {
            Priority = priority,
            DueAtUtc = calculationTimeUtc.AddHours(slaWindowHours),
            SlaWindowStartedAtUtc = calculationTimeUtc,
            SlaWindowHours = slaWindowHours,
            PriorityReason = priorityReason,
            SlaReason = slaReason,
            AssignmentReason = assignmentReason,
            AssignedAgent = selectedAgent
        };
    }
}
