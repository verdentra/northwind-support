import { useState, type FormEvent } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail, TicketEscalation } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
  onEscalated: (escalation: TicketEscalation) => void;
}

export function EscalationPanel({
  ticket,
  onUpdated,
  onEscalated,
}: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [validationError, setValidationError] = useState<string>();
  const [requestError, setRequestError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const stateDisablesEscalation =
    ticket.priority === 'Critical' ||
    ticket.status === 'Resolved' ||
    ticket.status === 'Closed';

  const validate = () => {
    const trimmedReason = reason.trim();

    if (trimmedReason.length < 5) {
      return 'Escalation reason must be at least 5 characters.';
    }

    if (trimmedReason.length > 500) {
      return 'Escalation reason cannot exceed 500 characters.';
    }

    return undefined;
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const error = validate();
    setValidationError(error);
    setRequestError(undefined);

    if (error) {
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await ticketsApi.escalateTicket(ticket.id, {
        reason: reason.trim(),
      });

      onUpdated(response.ticket);
      onEscalated(response.escalation);
      setReason('');
    } catch (caughtError) {
      const message = toErrorMessage(
        caughtError,
        'Could not escalate the ticket.',
      );

      if (caughtError instanceof ApiError && caughtError.status === 409) {
        setRequestError(`Escalation conflict: ${message}`);
      } else {
        setRequestError(message);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (stateDisablesEscalation) {
    return (
      <section className="panel" aria-labelledby="escalation-heading">
        <h2 id="escalation-heading">Escalate ticket</h2>
        <p className="field__hint">
          This ticket cannot be escalated because it is already Critical,
          Resolved, or Closed.
        </p>
      </section>
    );
  }

  return (
    <section className="panel" aria-labelledby="escalation-heading">
      <h2 id="escalation-heading">Escalate ticket</h2>

      <form onSubmit={handleSubmit} noValidate>
        <div className="field">
          <label className="field__label" htmlFor="escalation-reason">
            Reason
          </label>
          <textarea
            id="escalation-reason"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            minLength={5}
            maxLength={500}
            aria-invalid={validationError !== undefined}
            aria-describedby="escalation-reason-hint"
            disabled={isSubmitting}
            rows={4}
          />
          <span id="escalation-reason-hint" className="field__hint">
            Enter 5–500 characters.
          </span>
        </div>

        {validationError && (
          <p className="field__error" role="alert">
            {validationError}
          </p>
        )}

        {requestError && (
          <p className="field__error" role="alert">
            {requestError}
          </p>
        )}

        <div className="button-row">
          <button
            type="submit"
            className="button button--primary"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Escalating...' : 'Escalate ticket'}
          </button>
        </div>
      </form>
    </section>
  );
}
