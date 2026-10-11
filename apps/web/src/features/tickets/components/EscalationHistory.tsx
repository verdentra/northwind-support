import { formatDateTime } from '../../../lib/format';
import type { Agent, TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  escalations: TicketEscalation[];
  agents: Agent[];
  isLoading?: boolean;
  error?: string;
  onRetry?: () => void;
}

/** Newest-first history of priority, agent and due-date changes from escalations. */
export function EscalationHistory({
  escalations,
  agents,
  isLoading,
  error,
  onRetry,
}: EscalationHistoryProps) {
  const agentName = (id: number | null): string => {
    if (id === null) {
      return 'Unassigned';
    }

    return agents.find((agent) => agent.id === id)?.fullName ?? `Agent #${id}`;
  };

  const orderedEscalations = [...escalations].sort(
    (left, right) =>
      new Date(right.escalatedAtUtc).getTime() - new Date(left.escalatedAtUtc).getTime(),
  );

  return (
    <div className="card">
      <h2>Escalation history</h2>

      {isLoading && <p className="field__hint">Loading history...</p>}

      {error && (
        <p className="field__error" role="alert">
          {error}{' '}
          {onRetry && (
            <button type="button" className="button button--ghost" onClick={onRetry}>
              Retry
            </button>
          )}
        </p>
      )}

      {!isLoading && !error && orderedEscalations.length === 0 && (
        <p className="field__hint">No escalations yet.</p>
      )}

      {orderedEscalations.length > 0 && (
        <ol className="escalation-history">
          {orderedEscalations.map((item) => (
            <li key={item.id} className="escalation-history__item">
              <header className="escalation-history__header">
                <strong>
                  {item.fromPriority} → {item.toPriority}
                </strong>
                <span className="escalation-history__when">
                  {formatDateTime(item.escalatedAtUtc)}
                </span>
              </header>

              <dl className="escalation-history__details">
                <div>
                  <dt>Agent</dt>
                  <dd>
                    {agentName(item.fromAgentId)} → {agentName(item.toAgentId)}
                  </dd>
                </div>
                <div>
                  <dt>Due</dt>
                  <dd>
                    {formatDateTime(item.fromDueAtUtc)} → {formatDateTime(item.toDueAtUtc)}
                  </dd>
                </div>
                <div>
                  <dt>By</dt>
                  <dd>{item.escalatedBy}</dd>
                </div>
                <div>
                  <dt>Reason</dt>
                  <dd>{item.reason}</dd>
                </div>
              </dl>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
