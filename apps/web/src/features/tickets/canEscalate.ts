import type { TicketDetail, TicketPriority, TicketStatus } from '../../types/api';

/** Critical, Resolved and Closed tickets cannot be escalated (BR-7). */
export function canEscalate(ticket: Pick<TicketDetail, 'priority' | 'status'>): boolean {
  return canEscalatePriority(ticket.priority) && canEscalateStatus(ticket.status);
}

export function canEscalatePriority(priority: TicketPriority): boolean {
  return priority !== 'Critical';
}

export function canEscalateStatus(status: TicketStatus): boolean {
  return status !== 'Resolved' && status !== 'Closed';
}
