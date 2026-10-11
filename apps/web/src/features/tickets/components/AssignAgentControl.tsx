import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { Agent, TicketDetail } from '../../../types/api';

interface AssignAgentControlProps {
  ticket: TicketDetail;
  agents: Agent[];
  onUpdated: (ticket: TicketDetail) => void;
}

/** Hands the ticket to an agent, or takes it back. */
export function AssignAgentControl({ ticket, agents, onUpdated }: AssignAgentControlProps) {
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string>();

  const assign = async (agentId: number | null) => {
    setIsSaving(true);
    setError(undefined);

    try {
      onUpdated(await ticketsApi.assignAgent(ticket.id, agentId));
    } catch (caught) {
      setError(toErrorMessage(caught, 'Could not assign the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="card">
      <h2>Assignment</h2>

      <Field id="assign-agent" label="Assigned agent" error={error}>
        {(fieldProps) => (
          <select
            {...fieldProps}
            value={ticket.assignedAgent?.id ?? ''}
            disabled={isSaving}
            onChange={(event) =>
              void assign(event.target.value === '' ? null : Number(event.target.value))
            }
          >
            <option value="">Unassigned</option>
            {agents.map((agent) => {
              const hasSpecialization = agent.specializations.some(
                (specialization) => specialization.id === ticket.category.id,
              );
              const isAlreadyAssigned = ticket.assignedAgent?.id === agent.id;
              const isAtCapacity = agent.openTicketCount >= agent.maxOpenTickets;
              const isSpecialistRequired = ticket.category.requiresSpecialist;
              const isDisabled =
                !agent.isActive ||
                (isSpecialistRequired && !hasSpecialization) ||
                (isAtCapacity && !isAlreadyAssigned);
              const unavailableReason = !agent.isActive
                ? ' - inactive'
                : isSpecialistRequired && !hasSpecialization
                  ? ` - not qualified for ${ticket.category.name}`
                  : isAtCapacity && !isAlreadyAssigned
                    ? ' - at capacity'
                    : '';

              return (
                <option key={agent.id} value={agent.id} disabled={isDisabled}>
                  {agent.fullName} ({agent.openTicketCount}/{agent.maxOpenTickets} open)
                  {unavailableReason}
                </option>
              );
            })}
          </select>
        )}
      </Field>

      {isSaving && <p className="field__hint">Saving...</p>}
    </div>
  );
}
