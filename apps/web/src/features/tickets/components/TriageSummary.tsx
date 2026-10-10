import { formatDateTime } from '../../../lib/format';
import type { RaisedTicket } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';
import { PriorityBadge } from './TicketBadges';

interface TriageSummaryProps {
  ticket: RaisedTicket;
}

/** What automatic triage decided for a new ticket, and why. */
export function TriageSummary({ ticket }: TriageSummaryProps) {
  const { triage } = ticket;

  return (
    <dl className="summary triage">
      <div>
        <dt>Priority</dt>
        <dd>
          <PriorityBadge priority={ticket.priority} />
          <span className="summary__note">{triage.priorityReason}</span>
        </dd>
      </div>

      <div>
        <dt>Due</dt>
        <dd>
          {formatDateTime(ticket.dueAtUtc)} <SlaIndicator status={ticket.slaStatus} />
          <span className="summary__note">{triage.slaReason}</span>
        </dd>
      </div>

      <div>
        <dt>Assigned agent</dt>
        <dd>
          {ticket.assignedAgent ? ticket.assignedAgent.fullName : <strong>Unassigned</strong>}
          <span className="summary__note">{triage.assignmentReason}</span>
        </dd>
      </div>
    </dl>
  );
}
