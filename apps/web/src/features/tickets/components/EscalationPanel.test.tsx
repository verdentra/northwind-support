import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

const ticket: TicketDetail = {
  id: 23,
  reference: 'TCK-0023',
  title: 'Test ticket',
  description: 'Test description',
  status: 'Open',
  priority: 'High',
  customer: {
    id: 1,
    name: 'Test Customer',
    email: 'test@example.com',
    phone: '000',
    tier: 'Standard',
  },
  category: {
    id: 1,
    name: 'General',
    requiresSpecialist: false,
  },
  assignedAgent: {
    id: 1,
    fullName: 'Test Agent',
  },
  createdAtUtc: '2026-10-16T10:00:00Z',
  updatedAtUtc: '2026-10-16T10:00:00Z',
  dueAtUtc: '2026-10-17T10:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
  triage: undefined,
};

describe('EscalationPanel', () => {
  it('shows a validation error and does not call the API for a short reason', async () => {
    const escalateTicket = vi
      .spyOn(ticketsApi, 'escalateTicket')
      .mockResolvedValue({
        ticket,
        escalation: {
          id: 1,
          ticketId: ticket.id,
          fromPriority: 'High',
          toPriority: 'Critical',
          fromAgentId: 1,
          toAgentId: 1,
          fromDueAtUtc: ticket.dueAtUtc,
          toDueAtUtc: '2026-10-16T14:00:00Z',
          reason: 'Valid reason',
          escalatedBy: 'alex.lead',
          escalatedAtUtc: '2026-10-16T12:00:00Z',
        },
      });

    render(
      <EscalationPanel
        ticket={ticket}
        onUpdated={vi.fn()}
        onEscalated={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByLabelText('Reason'), {
      target: { value: 'abc' },
    });

    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(
      await screen.findByText('Escalation reason must be at least 5 characters.'),
    ).toBeInTheDocument();

    expect(escalateTicket).not.toHaveBeenCalled();

    escalateTicket.mockRestore();
  });

  it('shows a conflict error and does not update the ticket after HTTP 409', async () => {
    const escalateTicket = vi
      .spyOn(ticketsApi, 'escalateTicket')
      .mockRejectedValue(new ApiError(409, 'Ticket is already Critical.'));

    const onUpdated = vi.fn();
    const onEscalated = vi.fn();

    render(
      <EscalationPanel
        ticket={ticket}
        onUpdated={onUpdated}
        onEscalated={onEscalated}
      />,
    );

    fireEvent.change(screen.getByLabelText('Reason'), {
      target: { value: 'The issue is becoming urgent.' },
    });

    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(
      await screen.findByText(
        'Escalation conflict: Ticket is already Critical.',
      ),
    ).toBeInTheDocument();

    await waitFor(() => {
      expect(onUpdated).not.toHaveBeenCalled();
      expect(onEscalated).not.toHaveBeenCalled();
    });

    expect(escalateTicket).toHaveBeenCalledTimes(1);
    expect(escalateTicket).toHaveBeenCalledWith(ticket.id, {
      reason: 'The issue is becoming urgent.',
    });

    escalateTicket.mockRestore();
  });
});
