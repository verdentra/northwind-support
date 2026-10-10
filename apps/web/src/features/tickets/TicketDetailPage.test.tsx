import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { agentsApi } from '../../api/agents';
import { categoriesApi } from '../../api/categories';
import { ApiError } from '../../api/client';
import { customersApi } from '../../api/customers';
import { ticketsApi } from '../../api/tickets';
import { ticketDetail, ticketEscalation } from '../../test/fixtures';
import { TicketDetailPage } from './TicketDetailPage';

vi.mock('../../api/tickets', () => ({
  ticketsApi: {
    getTicket: vi.fn(),
    getEscalations: vi.fn(),
    escalate: vi.fn(),
    updateStatus: vi.fn(),
    assignAgent: vi.fn(),
  },
}));
vi.mock('../../api/agents', () => ({ agentsApi: { getAgents: vi.fn() } }));
vi.mock('../../api/categories', () => ({ categoriesApi: { getCategories: vi.fn() } }));
vi.mock('../../api/customers', () => ({ customersApi: { getCustomers: vi.fn() } }));

const api = vi.mocked(ticketsApi);

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/tickets/12']}>
      <Routes>
        <Route path="/tickets/:id" element={<TicketDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

/** The Priority value in the read-only summary. */
function summaryPriority() {
  const term = screen.getByText('Priority', { selector: 'dt' });
  return within(term.parentElement as HTMLElement).getByText(/Low|Medium|High|Critical/);
}

async function escalate() {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Reason'), 'Customer cannot issue invoices');
  await user.click(screen.getByRole('button', { name: /escalate to critical/i }));
}

describe('TicketDetailPage escalation', () => {
  beforeEach(() => {
    api.getTicket.mockResolvedValue(ticketDetail());
    api.getEscalations.mockResolvedValue([]);
    vi.mocked(agentsApi.getAgents).mockResolvedValue([]);
    vi.mocked(categoriesApi.getCategories).mockResolvedValue([]);
    vi.mocked(customersApi.getCustomers).mockResolvedValue([]);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('leaves the ticket and history untouched when the server rejects the escalation', async () => {
    api.escalate.mockRejectedValue(new ApiError(409, 'Ticket TCK-0012 is Resolved and cannot be escalated.'));
    renderPage();
    await screen.findByText('Never escalated');

    await escalate();

    expect(await screen.findByRole('alert')).toHaveTextContent('Resolved and cannot be escalated');
    expect(summaryPriority()).toHaveTextContent('High');
    expect(screen.getByText('Sara Lindqvist', { selector: 'dd' })).toBeInTheDocument();
    expect(screen.getByText('Never escalated')).toBeInTheDocument();
    expect(api.getTicket).toHaveBeenCalledTimes(1);
    expect(api.getEscalations).toHaveBeenCalledTimes(1);
  });

  it('refreshes the ticket and its history after a successful escalation, without reloading the page', async () => {
    api.escalate.mockResolvedValue({
      ticket: ticketDetail({ priority: 'Critical', canBeEscalated: false }),
      escalation: ticketEscalation(),
      assignmentReason: 'Kept Sara Lindqvist: still eligible.',
    });
    renderPage();
    await screen.findByText('Never escalated');
    api.getEscalations.mockResolvedValue([ticketEscalation()]);

    await escalate();

    await waitFor(() => expect(summaryPriority()).toHaveTextContent('Critical'));
    expect(await screen.findByText('Customer cannot issue invoices', { selector: 'blockquote' })).toBeInTheDocument();
    expect(screen.getByText(/by/, { selector: '.history__meta' })).toHaveTextContent('Team Lead');
    expect(api.getTicket).toHaveBeenCalledTimes(1);
    expect(api.getEscalations).toHaveBeenCalledTimes(2);
    expect(screen.getByRole('button', { name: /escalate/i })).toBeDisabled();
  });
});
