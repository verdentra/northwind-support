import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';

/** Loads a ticket's escalation history and exposes a refresh operation. */
export function useTicketEscalations(ticketId: number) {
  const load = useCallback(
    () => ticketsApi.getEscalations(ticketId),
    [ticketId],
  );

  const { data, isLoading, error, reload } = useAsyncData(
    load,
    'Unable to load escalation history.',
  );

  return {
    escalations: data ?? [],
    isLoading,
    error,
    reload,
  };
}
