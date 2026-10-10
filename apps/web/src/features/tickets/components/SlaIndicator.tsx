import { Badge } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';
import { slaPresentation } from '../sla';

interface SlaIndicatorProps {
  status: SlaStatus;
}

/**
 * Where a ticket stands against its SLA. The one place an SLA status is drawn: the ticket list,
 * the ticket detail page and anything else use this, so the states always look the same.
 */
export function SlaIndicator({ status }: SlaIndicatorProps) {
  const { label, tone, icon, description } = slaPresentation[status];

  return (
    <span className="sla-indicator" data-sla-status={status} title={description}>
      <Badge tone={tone}>
        <span className="sla-indicator__icon" aria-hidden="true">
          {icon}
        </span>{' '}
        {label}
      </Badge>
    </span>
  );
}
