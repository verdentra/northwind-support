using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private readonly List<TicketEscalation> _escalations = [];

    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    /// <summary>
    /// When the current SLA window started: when the ticket was triaged, or when it was last
    /// escalated. Null for tickets whose window was never recorded; those are measured from
    /// <see cref="CreatedAtUtc"/> (see <see cref="SlaWindowStartUtc"/>).
    /// </summary>
    public DateTime? SlaStartedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>
    /// The escalations recorded on this ticket <em>in this unit of work</em>. History is read
    /// through the query side; the repository does not load it with the ticket.
    /// </summary>
    public IReadOnlyCollection<TicketEscalation> Escalations => _escalations;

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>The start of the window the at-risk threshold is measured against.</summary>
    public DateTime SlaWindowStartUtc => SlaStartedAtUtc ?? CreatedAtUtc;

    /// <summary>True when <see cref="Escalate"/> would accept this ticket.</summary>
    public bool CanBeEscalated => IsEscalatable(Status, Priority);

    /// <summary>
    /// The escalation rule on its own: only open tickets below Critical can be escalated. Static
    /// so the read side can report it without loading the aggregate.
    /// </summary>
    public static bool IsEscalatable(TicketStatus status, TicketPriority priority) =>
        status is not (TicketStatus.Resolved or TicketStatus.Closed) && TicketPriorityRules.CanRaise(priority);

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner and no due date; triage then calls <see cref="StartSlaWindow"/> and
    /// <see cref="AssignTo"/>.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Starts the ticket's SLA window now: the due date is worked out by the policy from the
    /// ticket's current priority and the customer's tier.
    /// </summary>
    public void StartSlaWindow(SlaPolicy policy, CustomerTier tier, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);

        SlaStartedAtUtc = nowUtc;
        DueAtUtc = policy.DueAt(Priority, tier, nowUtc);
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Throws unless the ticket can be escalated, so a caller can refuse early, before it does
    /// any other work.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is resolved, closed or already Critical.</exception>
    public void EnsureCanBeEscalated()
    {
        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is {Status} and cannot be escalated. Reopen it first if it still needs attention.");
        }

        if (!TicketPriorityRules.CanRaise(Priority))
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already {Priority}, the highest priority, and cannot be escalated further.");
        }
    }

    /// <summary>
    /// Raises the priority one level, restarts the SLA window from <paramref name="nowUtc"/> at
    /// the new priority, hands the ticket to <paramref name="agentId"/> (which may be the
    /// current owner, or null), and records the change.
    /// </summary>
    /// <param name="policy">The SLA rules used for the new due date.</param>
    /// <param name="tier">The customer's tier.</param>
    /// <param name="agentId">The owner chosen by the assignment rules.</param>
    /// <param name="reason">Why it is being escalated; trimmed.</param>
    /// <param name="escalatedBy">Who is escalating it; trimmed.</param>
    /// <param name="nowUtc">The moment of escalation.</param>
    /// <returns>The history record, also added to <see cref="Escalations"/>.</returns>
    /// <exception cref="BusinessRuleViolationException">The ticket is resolved, closed or already Critical.</exception>
    public TicketEscalation Escalate(
        SlaPolicy policy,
        CustomerTier tier,
        int? agentId,
        string reason,
        string escalatedBy,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);

        var trimmedReason = reason.Trim();
        var trimmedActor = escalatedBy.Trim();

        if (trimmedReason.Length is < TicketEscalation.ReasonMinLength or > TicketEscalation.ReasonMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                $"The reason must be {TicketEscalation.ReasonMinLength}-{TicketEscalation.ReasonMaxLength} characters.");
        }

        if (trimmedActor.Length > TicketEscalation.EscalatedByMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(escalatedBy), $"At most {TicketEscalation.EscalatedByMaxLength} characters.");
        }

        EnsureCanBeEscalated();

        var fromPriority = Priority;
        var fromAgentId = AssignedAgentId;
        var fromDueAtUtc = DueAtUtc;

        Priority = TicketPriorityRules.Raise(Priority);
        StartSlaWindow(policy, tier, nowUtc);
        AssignTo(agentId, nowUtc);

        var escalation = new TicketEscalation(
            Id,
            fromPriority,
            Priority,
            fromAgentId,
            AssignedAgentId,
            fromDueAtUtc,
            DueAtUtc!.Value,
            trimmedReason,
            trimmedActor,
            nowUtc);

        _escalations.Add(escalation);

        return escalation;
    }
}
