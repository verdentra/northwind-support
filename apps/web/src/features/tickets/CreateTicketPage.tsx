import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { formatDateTime } from '../../lib/format';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status">
          <p>
            Raised <strong>{created.reference}</strong> for {created.customer.name}.
          </p>

          <dl className="summary summary--compact">
            <div>
              <dt>Priority</dt>
              <dd>
                {created.priority}
                {created.triage?.priorityReason && <> — {created.triage.priorityReason}</>}
              </dd>
            </div>

            <div>
              <dt>Due date</dt>
              <dd>
                {formatDateTime(created.dueAtUtc)}
                {created.triage?.slaWindowReason && <> — {created.triage.slaWindowReason}</>}
              </dd>
            </div>

            <div>
              <dt>Assigned agent</dt>
              <dd>
                {created.assignedAgent
                  ? created.assignedAgent.fullName
                  : created.triage?.assignmentReason ?? 'Unassigned'}
              </dd>
            </div>
          </dl>

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setCreated(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
