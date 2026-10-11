import { Badge } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaStatusLabels } from './slaStatus';

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** Reusable SLA indicator used by both the ticket list and the detail page. */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  const label = slaStatusLabels[slaStatus];

  return (
    <div className="sla-cell">
      <Badge tone={label.tone}>{label.text}</Badge>
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
