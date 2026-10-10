import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { slaStatuses } from '../sla';
import { SlaIndicator } from './SlaIndicator';

const expectedLabels: Record<SlaStatus, string> = {
  WithinSla: 'Within SLA',
  AtRisk: 'At risk',
  Breached: 'Breached',
  Met: 'Met',
  NotApplicable: 'No SLA',
};

describe('SlaIndicator', () => {
  it.each(Object.entries(expectedLabels))('labels %s as "%s"', (status, label) => {
    render(<SlaIndicator status={status as SlaStatus} />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('draws every status with its own text, icon and tone, so none relies on colour alone', () => {
    const rendered = slaStatuses.map((status) => {
      const { container, unmount } = render(<SlaIndicator status={status} />);
      const indicator = container.querySelector(`[data-sla-status="${status}"]`);
      const badge = indicator?.querySelector('.badge');
      const result = {
        text: indicator?.textContent?.trim() ?? '',
        icon: indicator?.querySelector('[aria-hidden="true"]')?.textContent ?? '',
        tone: badge?.className ?? '',
      };
      unmount();
      return result;
    });

    expect(rendered).toHaveLength(5);
    expect(new Set(rendered.map((r) => r.text)).size).toBe(5);
    expect(new Set(rendered.map((r) => r.icon)).size).toBe(5);
    expect(new Set(rendered.map((r) => r.tone)).size).toBe(5);
    rendered.forEach((r) => expect(r.text).not.toBe(''));
  });

  it('offers every status the API can return', () => {
    expect([...slaStatuses].sort()).toEqual(Object.keys(expectedLabels).sort());
  });
});
