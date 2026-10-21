import { useEffect, useRef, useState } from 'react';
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
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
  slaStatus: undefined,
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });

  const previousSearch = useRef(filters.search);
  const requestId = useRef(0);
  const searchDebouncePending = useRef(false);
  const searchDebounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
  const searchChanged = previousSearch.current !== filters.search;
  previousSearch.current = filters.search;

  if (searchChanged) {
    searchDebouncePending.current = true;
  }

    const currentRequestId = ++requestId.current;

    const load = async () => {
      setState((current) => ({
        ...current,
        isLoading: true,
        error: undefined,
      }));

      try {
        const data = await ticketsApi.getTickets(filters);

        // Ignore responses from older requests.
        if (currentRequestId === requestId.current) {
          setState({ isLoading: false, data, error: undefined });
        }
      } catch (error) {
        if (currentRequestId === requestId.current) {
          const message =
            error instanceof ApiError
              ? error.message
              : 'Unable to load tickets.';

          setState((current) => ({
            ...current,
            isLoading: false,
            error: message,
          }));
        }
      }
    };

    // Search is debounced; all other filter changes run immediately.
    const handle = window.setTimeout(
    () => {
      searchDebouncePending.current = false;
      searchDebounceTimer.current = null;
      void load();
    },
    searchDebouncePending.current ? DEBOUNCE_MS : 0,
  );

  searchDebounceTimer.current = handle;

  return () => {
    window.clearTimeout(handle);
    searchDebounceTimer.current = null;
    requestId.current++;
  };
  }, [filters]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({
      ...current,
      ...patch,
      page: patch.page ?? 1,
    }));

  const resetFilters = () => setFilters(emptyFilters);

  const reload = async () => {
    const currentRequestId = ++requestId.current;

    setState((current) => ({
      ...current,
      isLoading: true,
      error: undefined,
    }));

    try {
      const data = await ticketsApi.getTickets(filters);

      if (currentRequestId === requestId.current) {
        setState({ isLoading: false, data, error: undefined });
      }
    } catch (error) {
      if (currentRequestId === requestId.current) {
        const message =
          error instanceof ApiError
            ? error.message
            : 'Unable to load tickets.';

        setState((current) => ({
          ...current,
          isLoading: false,
          error: message,
        }));
      }
    }
  };

  return {
    filters,
    updateFilters,
    resetFilters,
    reload,
    ...state,
  };
}