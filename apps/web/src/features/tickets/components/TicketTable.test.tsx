import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { ticketListItem } from '../../../test/fixtures';
import { TicketTable } from './TicketTable';

describe('TicketTable', () => {
  it('shows the reference, owner and SLA state of each ticket', () => {
    render(
      <MemoryRouter>
        <TicketTable
          tickets={[
            ticketListItem(),
            ticketListItem({
              id: 21,
              reference: 'TCK-0021',
              title: 'Possible data exposure in shared links',
              priority: 'Critical',
              assignedAgent: null,
              slaStatus: 'WithinSla',
            }),
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'TCK-0012' })).toHaveAttribute('href', '/tickets/12');
    expect(screen.getByText('Sara Lindqvist')).toBeInTheDocument();
    expect(screen.getByText('Breached')).toBeInTheDocument();

    // An unassigned ticket says so, rather than showing an empty cell.
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('Within SLA')).toBeInTheDocument();
  });

  it('renders all SLA states with distinct text labels', () => {
    render(
      <MemoryRouter>
        <TicketTable
          tickets={[
            ticketListItem({ slaStatus: 'WithinSla' }),
            ticketListItem({ id: 13, slaStatus: 'AtRisk' }),
            ticketListItem({ id: 14, slaStatus: 'Breached' }),
            ticketListItem({ id: 15, slaStatus: 'Met' }),
            ticketListItem({ id: 16, slaStatus: 'NotApplicable', dueAtUtc: null }),
          ]}
        />
      </MemoryRouter>,
    );

    const table = screen.getByRole('table');
    const expectedLabels = ['Within SLA', 'At risk', 'Breached', 'Met', 'No SLA'];

    for (const label of expectedLabels) {
      expect(within(table).getByText(label)).toBeInTheDocument();
    }
  });
});
