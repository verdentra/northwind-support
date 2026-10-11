using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>
/// Shared BR-2–BR-5 rules used when a ticket is raised and when it is escalated.
/// </summary>
public sealed class TicketTriageService(
    ITicketRepository tickets,
    IAgentRepository agents,
    IOptions<SlaOptions> slaOptions)
{
    /// <summary>
    /// Calculates the SLA deadline from a start time, priority and customer tier (BR-2, BR-3, BR-6).
    /// </summary>
    public SlaWindowDecision CalculateSlaWindow(
        TicketPriority priority,
        CustomerTier customerTier,
        DateTime startedAtUtc)
    {
        var options = slaOptions.Value;

        if (!options.BaseWindowHours.TryGetValue(
                priority.ToString(),
                out var baseWindowHours))
        {
            throw new InvalidOperationException(
                $"No SLA window is configured for priority '{priority}'.");
        }

        var windowHours = (double)baseWindowHours;

        var reason =
            $"The base SLA for {priority} priority is {baseWindowHours} hours.";

        if (customerTier == CustomerTier.Premium)
        {
            windowHours *= options.PremiumCustomerMultiplier;
            reason +=
                $" The Premium customer multiplier reduces it to {windowHours:0.##} hours.";
        }

        if (windowHours < options.MinimumWindowHours)
        {
            windowHours = options.MinimumWindowHours;
            reason +=
                $" The minimum SLA window is {options.MinimumWindowHours:0.##} hour(s).";
        }

        return new SlaWindowDecision(
            startedAtUtc.AddHours(windowHours),
            windowHours,
            reason);
    }

    /// <summary>
    /// Picks an owner per BR-4 and BR-5. When <paramref name="currentAssigneeId"/> is set,
    /// keeps that agent if they are still eligible (capacity counted without the ticket
    /// already assigned to them).
    /// </summary>
    public async Task<AssignmentDecision> DecideAssignmentAsync(
        Category category,
        int? currentAssigneeId,
        CancellationToken ct)
    {
        var allAgents = await agents.GetAllAsync(ct);

        if (currentAssigneeId is { } assigneeId)
        {
            var current = allAgents.FirstOrDefault(a => a.Id == assigneeId);

            if (current is not null &&
                await IsEligibleAsync(
                    current,
                    category,
                    excludeOpenTicketAlreadyHeld: true,
                    ct))
            {
                return new AssignmentDecision(
                    current,
                    $"Kept assigned to {current.FullName}, who remains eligible after escalation.");
            }
        }

        var eligible = new List<(Agent Agent, int OpenTicketCount)>();

        foreach (var agent in allAgents)
        {
            if (!IsQualifyingAgent(agent, category))
            {
                continue;
            }

            var openTicketCount = await tickets.CountOpenByAgentAsync(agent.Id, ct);

            if (openTicketCount < agent.MaxOpenTickets)
            {
                eligible.Add((agent, openTicketCount));
            }
        }

        var assigned = eligible
            .OrderBy(candidate => candidate.OpenTicketCount)
            .ThenBy(candidate => candidate.Agent.Id)
            .Select(candidate => candidate.Agent)
            .FirstOrDefault();

        if (assigned is not null)
        {
            var reason = category.RequiresSpecialist
                ? $"Assigned to {assigned.FullName}, who is qualified for category '{category.Name}' and has available capacity."
                : $"Assigned to {assigned.FullName}, the eligible agent with the fewest open tickets.";

            return new AssignmentDecision(assigned, reason);
        }

        var unassignedReason = category.RequiresSpecialist
            ? $"No active specialist for category '{category.Name}' has available capacity. The ticket remains unassigned."
            : "No active agent has available capacity. The ticket remains unassigned.";

        return new AssignmentDecision(null, unassignedReason);
    }

    private async Task<bool> IsEligibleAsync(
        Agent agent,
        Category category,
        bool excludeOpenTicketAlreadyHeld,
        CancellationToken ct)
    {
        if (!IsQualifyingAgent(agent, category))
        {
            return false;
        }

        var openTicketCount = await tickets.CountOpenByAgentAsync(agent.Id, ct);

        if (excludeOpenTicketAlreadyHeld && openTicketCount > 0)
        {
            openTicketCount--;
        }

        return openTicketCount < agent.MaxOpenTickets;
    }

    private static bool IsQualifyingAgent(Agent agent, Category category)
    {
        if (!agent.IsActive)
        {
            return false;
        }

        if (category.RequiresSpecialist &&
            !agent.Specializations.Any(
                specialization => specialization.CategoryId == category.Id))
        {
            return false;
        }

        return true;
    }
}
