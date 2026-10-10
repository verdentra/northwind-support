import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail, ticketEscalation } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

async function fillAndSubmit(reason: string) {
  const user = userEvent.setup();

  if (reason) {
    await user.type(screen.getByLabelText('Reason'), reason);
  }

  await user.click(screen.getByRole('button', { name: /escalate/i }));
}

describe('EscalationPanel', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('does not call the API when the reason is too short', async () => {
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillAndSubmit('abcd');

    expect(escalate).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByText('Give a reason of 5 to 500 characters.')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
  });

  it('counts the reason after trimming, like the server', async () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillAndSubmit('   ab   ');

    expect(escalate).not.toHaveBeenCalled();
  });

  it('shows a 409 from the server clearly and reports nothing as escalated', async () => {
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical, the highest priority, and cannot be escalated further.'),
    );
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillAndSubmit('Customer cannot issue invoices');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Not escalated');
    expect(alert).toHaveTextContent('already Critical');
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /escalate/i })).toBeEnabled();
  });

  it('sends only the trimmed reason and reports the result', async () => {
    const updated = ticketDetail({ priority: 'Critical', canBeEscalated: false });
    escalate.mockResolvedValue({
      ticket: updated,
      escalation: ticketEscalation(),
      assignmentReason: 'Kept Sara Lindqvist: still eligible.',
    });
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillAndSubmit('  Customer cannot issue invoices  ');

    await waitFor(() => expect(onEscalated).toHaveBeenCalledTimes(1));
    // Only the reason: who escalated comes from the access token on the server.
    expect(escalate).toHaveBeenCalledWith(12, { reason: 'Customer cannot issue invoices' });
    expect(onEscalated.mock.calls[0][0].ticket).toBe(updated);
    expect(screen.getByRole('status')).toHaveTextContent('Escalated from High to Critical');
  });

  it('shows a pending state while the request is in flight', async () => {
    let resolve: (value: Awaited<ReturnType<typeof ticketsApi.escalate>>) => void = () => {};
    escalate.mockReturnValue(new Promise((r) => (resolve = r)));
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillAndSubmit('Customer cannot issue invoices');

    expect(screen.getByRole('button', { name: 'Escalating...' })).toBeDisabled();

    resolve({ ticket: ticketDetail(), escalation: ticketEscalation(), assignmentReason: 'Kept.' });
    await screen.findByRole('button', { name: /escalate to/i });
  });

  it.each([
    [ticketDetail({ priority: 'Critical', canBeEscalated: false }), 'already Critical'],
    [ticketDetail({ status: 'Resolved', canBeEscalated: false }), 'resolved'],
    [ticketDetail({ status: 'Closed', canBeEscalated: false }), 'closed'],
  ])('is disabled, with the reason, when the ticket cannot be escalated (%#)', (ticket, reason) => {
    render(<EscalationPanel ticket={ticket} onEscalated={vi.fn()} />);

    expect(screen.getByRole('button', { name: /escalate/i })).toBeDisabled();
    expect(screen.getByLabelText('Reason')).toBeDisabled();
    expect(screen.getByRole('note')).toHaveTextContent(reason);
  });
});
