import type { TicketDetail, TicketPriority } from '../../types/api';

/** Mirrors the server's EscalateTicketRequestValidator, so invalid input never reaches the API. */
export const escalationRules = {
  reasonMinLength: 5,
  reasonMaxLength: 500,
} as const;

/** Who escalated is not entered: the API takes it from the signed-in agent's token. */
export interface EscalationForm {
  reason: string;
}

export type EscalationErrors = Partial<Record<keyof EscalationForm, string>>;

/** Lengths are counted after trimming, as the server counts them. */
export function validateEscalation(form: EscalationForm): EscalationErrors {
  const errors: EscalationErrors = {};
  const reason = form.reason.trim();
  const { reasonMinLength, reasonMaxLength } = escalationRules;

  if (reason.length < reasonMinLength || reason.length > reasonMaxLength) {
    errors.reason = `Give a reason of ${reasonMinLength} to ${reasonMaxLength} characters.`;
  }

  return errors;
}
const nextPriority: Record<TicketPriority, TicketPriority | null> = {
  Low: 'Medium',
  Medium: 'High',
  High: 'Critical',
  Critical: null,
};

/** The priority an escalation would move the ticket to, or null at the top. */
export function priorityAfterEscalation(priority: TicketPriority): TicketPriority | null {
  return nextPriority[priority];
}

/**
 * Why the ticket cannot be escalated, or null when it can. Used to disable the control; the
 * server makes the real decision and its 409 is still shown.
 */
export function escalationBlockedReason(ticket: TicketDetail): string | null {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return `This ticket is ${ticket.status.toLowerCase()}, so it cannot be escalated. Reopen it first.`;
  }

  if (priorityAfterEscalation(ticket.priority) === null) {
    return 'This ticket is already Critical, the highest priority.';
  }

  return ticket.canBeEscalated ? null : 'This ticket cannot be escalated.';
}
