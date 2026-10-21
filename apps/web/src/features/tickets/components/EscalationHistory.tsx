
import { useCallback, useEffect, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { toErrorMessage } from '../../../api/client';
import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';

interface Props {
  ticketId: number;
  refreshKey: number;
}

export function EscalationHistory({ ticketId, refreshKey }: Props) {
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const load = useCallback(async () => {
    setLoading(true);
    setError(undefined);

    try {
      const records = await ticketsApi.getEscalations(ticketId);
      setHistory(records);
    } catch (caught) {
      setError(toErrorMessage(caught, 'Could not load escalation history.'));
    } finally {
      setLoading(false);
    }
  }, [ticketId]);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  return (
    <section className="card" aria-label="Escalation history">
      <h2>Escalation history</h2>

      {loading && <p>Loading history...</p>}

      {error && (
        <p role="alert">
          {error}{' '}
          <button type="button" onClick={() => void load()}>
            Retry
          </button>
        </p>
      )}

      {!loading && !error && history.length === 0 && (
        <p>No escalations recorded yet.</p>
      )}

      {!loading && !error && history.length > 0 && (
        <ol>
          {history.map((item) => (
            <li key={item.id}>
              <strong>
                {item.fromPriority} → {item.toPriority}
              </strong>
              <p>{item.reason}</p>
              <p>
                By {item.escalatedBy} on{' '}
                {formatDateTime(item.escalatedAtUtc)}
              </p>
              <p>
                Agent: {item.fromAgentId ?? 'Unassigned'} →{' '}
                {item.toAgentId ?? 'Unassigned'}
              </p>
              <p>
                SLA deadline:{' '}
                {formatDateTime(item.fromDueAtUtc)} →{' '}
                {formatDateTime(item.toDueAtUtc)}
              </p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
