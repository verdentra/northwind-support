import { useEffect, useState, useRef  } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  slaStatus: undefined,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });
  const shouldFetchImmediately = useRef(true);  


  const load = async () => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(filters);
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  };

  useEffect(() => {
    
    if (shouldFetchImmediately.current) {
      shouldFetchImmediately.current = false;
      void load();
      return;
    }

    const handle = window.setTimeout(() => {
      void load();
    }, DEBOUNCE_MS);


    return () => window.clearTimeout(handle);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- filters object identity changes every render
  }, [
      filters.search,
      filters.status,
      filters.priority,
      filters.categoryId,
      filters.customerId,
      filters.assignedAgentId,
      filters.unassignedOnly,
      filters.slaStatus,
      filters.page,
      filters.pageSize,
      filters.sortBy,
      filters.sortDirection,
  ]);

  const updateFilters = (patch: Partial<TicketFilters>) =>{
  if (Object.keys(patch).some((key) => key !== 'search')) {
    shouldFetchImmediately.current = true;
  }

  setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));
};
  const resetFilters = () => {
  shouldFetchImmediately.current = true;
  setFilters(emptyFilters);
};

  return { filters, updateFilters, resetFilters, reload: load, ...state };
}
