import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import {
  escalationBlockedReason,
  escalationRules,
  priorityAfterEscalation,
  validateEscalation,
  type EscalationErrors,
  type EscalationForm,
} from '../escalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  /** Called only after the server has accepted the escalation. */
  onEscalated: (result: EscalationResult) => void;
}

interface SubmitError {
  message: string;
  /** The server refused because of the ticket's state (HTTP 409), not because of the input. */
  isConflict: boolean;
}

const emptyForm: EscalationForm = { reason: '' };

/**
 * Escalates the ticket: one priority level up, a new due date, and the owner re-checked. Checks
 * the same rules as the server first, so invalid input never reaches the API.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [form, setForm] = useState<EscalationForm>(emptyForm);
  const [errors, setErrors] = useState<EscalationErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<SubmitError>();
  const [outcome, setOutcome] = useState<string>();

  const blockedReason = escalationBlockedReason(ticket);
  const nextPriority = priorityAfterEscalation(ticket.priority);
  const disabled = blockedReason !== null || isSubmitting;
  const reasonLength = form.reason.trim().length;

  const update = (patch: Partial<EscalationForm>) => setForm((current) => ({ ...current, ...patch }));

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (blockedReason !== null || isSubmitting) {
      return;
    }

    const validationErrors = validateEscalation(form);
    setErrors(validationErrors);
    setSubmitError(undefined);
    setOutcome(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, { reason: form.reason.trim() });

      setForm(emptyForm);
      setOutcome(
        `Escalated from ${result.escalation.fromPriority} to ${result.escalation.toPriority}. ${result.assignmentReason}`,
      );
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 400) {
        setErrors(fromApiErrors(caught.fieldErrors));
      }

      setSubmitError({
        message: toErrorMessage(caught, 'Could not escalate the ticket.'),
        isConflict: caught instanceof ApiError && caught.status === 409,
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form className="card form escalation" onSubmit={submit} noValidate aria-labelledby="escalation-heading">
      <h2 id="escalation-heading">Escalate</h2>

      {blockedReason ? (
        <p className="field__hint" role="note">
          {blockedReason}
        </p>
      ) : (
        <p className="field__hint">
          Raises the priority from {ticket.priority} to {nextPriority}, restarts the SLA window from now and
          re-checks who owns the ticket. Recorded under your name.
        </p>
      )}

      <Field
        id="escalation-reason"
        label="Reason"
        error={errors.reason}
        hint={`${escalationRules.reasonMinLength}-${escalationRules.reasonMaxLength} characters (${reasonLength} so far).`}
      >
        {(fieldProps) => (
          <textarea
            {...fieldProps}
            rows={3}
            value={form.reason}
            maxLength={escalationRules.reasonMaxLength}
            disabled={disabled}
            onChange={(event) => update({ reason: event.target.value })}
          />
        )}
      </Field>

      {submitError && (
        <div className={submitError.isConflict ? 'error escalation__conflict' : 'error'} role="alert">
          <p>
            {submitError.isConflict && <strong>Not escalated - the ticket's state does not allow it: </strong>}
            {submitError.message}
          </p>
        </div>
      )}

      {outcome && (
        <p className="notice escalation__outcome" role="status">
          {outcome}
        </p>
      )}

      <div className="button-row">
        <button type="submit" className="button button--primary" disabled={disabled}>
          {isSubmitting ? 'Escalating...' : nextPriority ? `Escalate to ${nextPriority}` : 'Escalate'}
        </button>
      </div>
    </form>
  );
}

/** The API names fields in PascalCase ("Reason"); the form uses camelCase. */
function fromApiErrors(fieldErrors: Record<string, string[]>): EscalationErrors {
  const errors: EscalationErrors = {};

  for (const [field, messages] of Object.entries(fieldErrors)) {
    const key = field.charAt(0).toLowerCase() + field.slice(1);

    if (key === 'reason') {
      errors[key] = messages[0];
    }
  }

  return errors;
}
