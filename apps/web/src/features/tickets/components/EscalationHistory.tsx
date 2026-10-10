import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  escalations?: TicketEscalation[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

/** Every escalation of the ticket, newest first, with what changed, who did it and why. */
export function EscalationHistory({ escalations, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <section className="card" aria-labelledby="escalation-history-heading">
      <h2 id="escalation-history-heading">Escalation history</h2>

      {isLoading && !escalations && <Spinner label="Loading escalation history" />}

      {error && <ErrorMessage message={error} onRetry={onRetry} />}

      {escalations && escalations.length === 0 && <EmptyState title="Never escalated" />}

      {escalations && escalations.length > 0 && (
        <ol className="history" aria-busy={isLoading}>
          {escalations.map((escalation) => (
            <li key={escalation.id} className="history__item">
              <p className="history__headline">
                <PriorityBadge priority={escalation.fromPriority} /> <span aria-label="to">→</span>{' '}
                <PriorityBadge priority={escalation.toPriority} />
                <span className="history__meta">
                  by <strong>{escalation.escalatedBy}</strong> on{' '}
                  <time dateTime={escalation.escalatedAtUtc}>{formatDateTime(escalation.escalatedAtUtc)}</time>
                </span>
              </p>

              <dl className="history__changes">
                <div>
                  <dt>Agent</dt>
                  <dd>
                    {agentName(escalation.fromAgent)} → {agentName(escalation.toAgent)}
                    {escalation.fromAgent?.id === escalation.toAgent?.id && ' (unchanged)'}
                  </dd>
                </div>
                <div>
                  <dt>Due</dt>
                  <dd>
                    {formatDateTime(escalation.fromDueAtUtc)} → {formatDateTime(escalation.toDueAtUtc)}
                  </dd>
                </div>
              </dl>

              <blockquote className="history__reason">{escalation.reason}</blockquote>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function agentName(agent: AgentSummary | null): string {
  return agent?.fullName ?? 'Unassigned';
}
