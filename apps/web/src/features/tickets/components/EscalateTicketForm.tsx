
import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';

interface Props {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
  onEscalated: () => void;
}

export function EscalateTicketForm({
  ticket,
  onUpdated,
  onEscalated,
}: Props) {
  const [reason, setReason] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string>();

  const cannotEscalate =
    ticket.priority === 'Critical' ||
    ticket.status === 'Resolved' ||
    ticket.status === 'Closed';

  const submit = async (
    event: React.FormEvent<HTMLFormElement>
  ) => {
    event.preventDefault();
    setError(undefined);

    const cleanedReason = reason.trim();

    if (
      cleanedReason.length < 5 ||
      cleanedReason.length > 500
    ) {
      setError(
        'Reason must contain between 5 and 500 characters.'
      );
      return;
    }

    setIsSaving(true);

    try {
      const updated = await ticketsApi.escalateTicket(
        ticket.id,
        cleanedReason
      );

      onUpdated(updated);
      setReason('');
      onEscalated();
    } catch (caught) {
      if (
        caught instanceof ApiError &&
        caught.status === 409
      ) {
        setError(
          'This ticket can no longer be escalated. Refresh the ticket.'
        );
      } else {
        setError(
          toErrorMessage(
            caught,
            'Could not escalate the ticket.'
          )
        );
      }
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit}>
      <h2>Escalate ticket</h2>

      <p>
        Escalation increases priority by one level and recalculates
        the SLA deadline.
      </p>

      <div className="field">
        <label
          className="field__label"
          htmlFor="escalation-reason"
        >
          Escalation reason
        </label>

        <textarea
          id="escalation-reason"
          rows={3}
          maxLength={500}
          value={reason}
          disabled={cannotEscalate || isSaving}
          onChange={(event) =>
            setReason(event.target.value)
          }
          aria-invalid={Boolean(error)}
        />

        <small>5–500 characters required.</small>
      </div>

      {cannotEscalate && (
        <p role="status">
          Critical, resolved, and closed tickets cannot be escalated.
        </p>
      )}

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}

      <button
        type="submit"
        className="button button--primary"
        disabled={cannotEscalate || isSaving}
      >
        {isSaving ? 'Escalating...' : 'Escalate ticket'}
      </button>
    </form>
  );
}
