import type { TicketEscalation } from '../../../types/api';

interface TicketEscalationHistoryProps {
  escalations: TicketEscalation[];
  isLoading: boolean;
  error?: string;
}

function formatAgent(agentId: number | null) {
  return agentId === null ? 'Unassigned' : `Agent #${agentId}`;
}

function formatDate(value: string | null) {
  return value === null ? 'No due date' : new Date(value).toLocaleString();
}

export function TicketEscalationHistory({
  escalations,
  isLoading,
  error,
}: TicketEscalationHistoryProps) {
  return (
    <section className="panel" aria-labelledby="escalation-history-heading">
      <h2 id="escalation-history-heading">Escalation history</h2>

      {isLoading && <p role="status">Loading escalation history...</p>}

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}

      {!isLoading && !error && escalations.length === 0 && (
        <p className="field__hint">No escalations have been recorded.</p>
      )}

      {!isLoading && !error && escalations.length > 0 && (
        <ol className="escalation-history">
          {escalations.map((escalation) => (
            <li key={escalation.id} className="escalation-history__item">
              <p>
                <strong>
                  {escalation.fromPriority} → {escalation.toPriority}
                </strong>
              </p>

              <p>
                Agent: {formatAgent(escalation.fromAgentId)} →{' '}
                {formatAgent(escalation.toAgentId)}
              </p>

              <p>
                Due: {formatDate(escalation.fromDueAtUtc)} →{' '}
                {formatDate(escalation.toDueAtUtc)}
              </p>

              <p>Reason: {escalation.reason}</p>

              <p>
                Escalated by {escalation.escalatedBy} on{' '}
                {new Date(escalation.escalatedAtUtc).toLocaleString()}
              </p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
