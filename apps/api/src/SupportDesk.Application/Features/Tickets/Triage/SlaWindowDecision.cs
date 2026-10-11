namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>Result of applying BR-2, BR-3 and BR-6 to a priority and customer tier.</summary>
public sealed record SlaWindowDecision(
    DateTime DueAtUtc,
    double WindowHours,
    string Reason);
