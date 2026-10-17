import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SlaCell } from './SlaCell';

describe('SlaCell', () => {
  it.each([
    ['WithinSla', 'Within SLA'],
    ['AtRisk', 'At risk'],
    ['Breached', 'Breached'],
    ['Met', 'Met'],
    ['NotApplicable', 'No SLA'],
  ] as const)('renders the %s state visibly', (status, label) => {
    render(
      <SlaCell
        dueAtUtc="2026-10-17T10:00:00Z"
        slaStatus={status}
      />,
    );

    expect(screen.getByText(label)).toBeInTheDocument();
  });
});
