import type { BadgeTone } from '../../components/Badge';
import type { SlaStatus } from '../../types/api';

export interface SlaPresentation {
  label: string;
  tone: BadgeTone;
  /** A shape as well as a colour, so the state never depends on colour alone. */
  icon: string;
  description: string;
}

/** Every status, so adding one to the API type without a presentation fails the type check. */
export const slaPresentation: Record<SlaStatus, SlaPresentation> = {
  WithinSla: {
    label: 'Within SLA',
    tone: 'info',
    icon: '◷',
    description: 'Open and comfortably inside its response window.',
  },
  AtRisk: {
    label: 'At risk',
    tone: 'warning',
    icon: '⚠',
    description: 'Open, not yet due, with a quarter or less of its window left.',
  },
  Breached: {
    label: 'Breached',
    tone: 'danger',
    icon: '✕',
    description: 'Past its due date without a resolution, or resolved late.',
  },
  Met: {
    label: 'Met',
    tone: 'success',
    icon: '✓',
    description: 'Resolved on or before its due date.',
  },
  NotApplicable: {
    label: 'No SLA',
    tone: 'neutral',
    icon: '–',
    description: 'No due date, so there is nothing to measure against.',
  },
};

/** The order statuses are offered in, e.g. in the filter bar. */
export const slaStatuses: SlaStatus[] = ['WithinSla', 'AtRisk', 'Breached', 'Met', 'NotApplicable'];
