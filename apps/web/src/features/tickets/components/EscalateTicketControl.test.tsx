import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail, TicketEscalation } from '../../../types/api';
import { EscalateTicketControl } from './EscalateTicketControl';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

const ticket: TicketDetail = {
  id: 12,
  reference: 'TCK-0012',
  title: 'Invoice issue',
  description: 'The invoice total does not match the order.',
  status: 'InProgress',
  priority: 'High',
  customer: {
    id: 2,
    name: 'Fabrikam Inc',
    email: 'support@example.com',
    phone: null,
    tier: 'Standard',
  },
  category: { id: 2, name: 'Billing', requiresSpecialist: true },
  assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
  createdAtUtc: '2026-09-24T08:12:00Z',
  updatedAtUtc: '2026-09-25T10:03:00Z',
  dueAtUtc: '2026-09-26T16:12:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

const escalation: TicketEscalation = {
  id: 101,
  ticketId: 12,
  fromPriority: 'High',
  toPriority: 'Critical',
  fromAgentId: 4,
  toAgentId: 4,
  fromDueAtUtc: '2026-09-26T16:12:00Z',
  toDueAtUtc: '2026-09-25T14:12:00Z',
  reason: 'Customer is blocked.',
  escalatedBy: 'Priya Nair',
  escalatedAtUtc: '2026-09-25T10:30:00Z',
};

const updatedTicket: TicketDetail = {
  ...ticket,
  priority: 'Critical',
  updatedAtUtc: escalation.escalatedAtUtc,
  dueAtUtc: escalation.toDueAtUtc,
};

describe('EscalateTicketControl', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('blocks a short reason without calling the API', async () => {
    render(<EscalateTicketControl ticket={ticket} onEscalated={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'nope' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(await screen.findByText('Give a reason of at least 5 characters.')).toBeInTheDocument();
    expect(escalate).not.toHaveBeenCalled();
  });

  it('shows a 409 rejection and leaves the displayed ticket unchanged', async () => {
    escalate.mockRejectedValue(new ApiError(409, 'Ticket TCK-0012 is already Critical.'));
    const onEscalated = vi.fn();
    render(<EscalateTicketControl ticket={ticket} onEscalated={onEscalated} />);

    fireEvent.change(screen.getByLabelText('Reason'), {
      target: { value: 'Customer is blocked.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ticket TCK-0012 is already Critical.',
    );
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Escalate ticket' })).toBeEnabled();
  });

  it('disables submission while the request is pending and reports success', async () => {
    let completeRequest: ((result: { ticket: TicketDetail; escalation: TicketEscalation }) => void)
      | undefined;
    escalate.mockImplementation(
      () =>
        new Promise((resolve) => {
          completeRequest = resolve;
        }),
    );
    const onEscalated = vi.fn();
    render(<EscalateTicketControl ticket={ticket} onEscalated={onEscalated} />);

    fireEvent.change(screen.getByLabelText('Reason'), {
      target: { value: 'Customer is blocked.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(screen.getByRole('button', { name: 'Escalating...' })).toBeDisabled();

    completeRequest?.({ ticket: updatedTicket, escalation });
    await waitFor(() => expect(onEscalated).toHaveBeenCalledWith(updatedTicket, escalation));
    expect(screen.getByRole('button', { name: 'Escalate ticket' })).toBeEnabled();
  });
});
