import { useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ticketsApi } from '../../api/tickets';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useAsyncData } from '../../hooks/useAsyncData';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail, TicketEscalation } from '../../types/api';
import { AssignAgentControl } from './components/AssignAgentControl';
import { EscalateTicketControl } from './components/EscalateTicketControl';
import { EscalationHistory } from './components/EscalationHistory';
import { StatusActions } from './components/StatusActions';
import { TicketSummary } from './components/TicketSummary';
import { useTicket } from './hooks/useTicket';

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const ticketId = Number(id);

  const { data: ticket, isLoading, error, reload, setData } = useTicket(ticketId);
  const reference = useReferenceData();
  const loadEscalations = useCallback(
    () => ticketsApi.getEscalations(ticketId),
    [ticketId],
  );
  const {
    data: escalations,
    isLoading: isLoadingEscalations,
    error: escalationError,
    reload: reloadEscalations,
    setData: setEscalations,
  } = useAsyncData(loadEscalations, 'Unable to load escalation history.');

  const handleEscalated = (updatedTicket: TicketDetail, escalation: TicketEscalation) => {
    setData(updatedTicket);
    setEscalations([escalation, ...(escalations ?? [])]);
  };

  if (Number.isNaN(ticketId)) {
    return <ErrorMessage message="That is not a valid ticket reference." />;
  }

  if (isLoading && !ticket) {
    return <Spinner label="Loading ticket" />;
  }

  if (error && !ticket) {
    return <ErrorMessage message={error} onRetry={() => void reload()} />;
  }

  if (!ticket) {
    return null;
  }

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / {ticket.reference}
          </p>
          <h1>{ticket.title}</h1>
        </div>
      </header>

      {error && <ErrorMessage message={error} onRetry={() => void reload()} />}

      <div className="detail-layout">
        <div className="detail-layout__main">
          <div className="card">
            <h2>Description</h2>
            <p className="description">{ticket.description}</p>
          </div>

          <StatusActions ticket={ticket} onUpdated={setData} />
        </div>

        <aside className="detail-layout__side">
          <TicketSummary ticket={ticket} />

          <AssignAgentControl
            ticket={ticket}
            agents={reference.data?.agents ?? []}
            onUpdated={setData}
          />

          <EscalateTicketControl ticket={ticket} onEscalated={handleEscalated} />

          <EscalationHistory
            escalations={escalations ?? []}
            agents={reference.data?.agents ?? []}
            isLoading={isLoadingEscalations}
            error={escalationError}
            onRetry={() => void reloadEscalations()}
          />
        </aside>
      </div>
    </section>
  );
}
