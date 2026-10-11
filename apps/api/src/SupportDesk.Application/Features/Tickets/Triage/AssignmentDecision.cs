using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>Result of applying BR-4 and BR-5 (or keeping the current owner on escalate).</summary>
public sealed record AssignmentDecision(Agent? Agent, string Reason);
