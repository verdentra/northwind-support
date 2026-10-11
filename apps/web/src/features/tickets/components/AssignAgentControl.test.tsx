import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { Agent, TicketDetail } from '../../../types/api';
import { AssignAgentControl } from './AssignAgentControl';

const ticket: TicketDetail = {
  id: 12,
  reference: 'TCK-0012',
  title: 'Security alert',
  description: 'A possible security incident requires investigation.',
  status: 'New',
  priority: 'Critical',
  customer: {
    id: 2,
    name: 'Fabrikam Inc',
    email: 'support@example.com',
    phone: null,
    tier: 'Standard',
  },
  category: { id: 5, name: 'Security', requiresSpecialist: true },
  assignedAgent: { id: 3, fullName: 'Priya Nair' },
  createdAtUtc: '2026-09-24T08:12:00Z',
  updatedAtUtc: '2026-09-25T10:03:00Z',
  dueAtUtc: '2026-09-25T14:12:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

const agents: Agent[] = [
  {
    id: 1,
    fullName: 'Alex Turner',
    email: 'alex@example.com',
    isActive: true,
    maxOpenTickets: 10,
    openTicketCount: 5,
    specializations: [{ id: 1, name: 'General' }],
  },
  {
    id: 2,
    fullName: 'Marcus Doyle',
    email: 'marcus@example.com',
    isActive: true,
    maxOpenTickets: 6,
    openTicketCount: 6,
    specializations: [{ id: 5, name: 'Security' }],
  },
  {
    id: 3,
    fullName: 'Priya Nair',
    email: 'priya@example.com',
    isActive: true,
    maxOpenTickets: 8,
    openTicketCount: 8,
    specializations: [{ id: 5, name: 'Security' }],
  },
];

describe('AssignAgentControl', () => {
  it('disables agents who lack the required specialization', () => {
    render(<AssignAgentControl ticket={ticket} agents={agents} onUpdated={vi.fn()} />);

    expect(
      screen.getByRole('option', { name: /Alex Turner.*not qualified for Security/ }),
    ).toBeDisabled();
  });

  it('disables a specialist at capacity unless they already own the ticket', () => {
    render(<AssignAgentControl ticket={ticket} agents={agents} onUpdated={vi.fn()} />);

    expect(screen.getByRole('option', { name: /Marcus Doyle.*at capacity/ })).toBeDisabled();
    expect(screen.getByRole('option', { name: /Priya Nair/ })).toBeEnabled();
  });
});
