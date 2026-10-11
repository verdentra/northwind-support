import type { BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

/** Display labels for every SLA state — text, not colour alone, conveys meaning. */
export const slaStatusLabels: Record<SlaStatus, { text: string; tone: BadgeTone }> = {
  WithinSla: { text: 'Within SLA', tone: 'info' },
  AtRisk: { text: 'At risk', tone: 'warning' },
  Breached: { text: 'Breached', tone: 'danger' },
  Met: { text: 'Met', tone: 'success' },
  NotApplicable: { text: 'No SLA', tone: 'neutral' },
};

export const slaStatusOptions: SlaStatus[] = [
  'WithinSla',
  'AtRisk',
  'Breached',
  'Met',
  'NotApplicable',
];
