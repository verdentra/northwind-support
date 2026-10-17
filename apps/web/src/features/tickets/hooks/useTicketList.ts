import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const SEARCH_DEBOUNCE_MS = 500;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  slaStatus: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
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

const previousFilters = useRef<TicketFilters | null>(null);

  useEffect(() => {
    const previous = previousFilters.current;
    previousFilters.current = filters;

    const changedKeys = previous
      ? (Object.keys(filters) as (keyof TicketFilters)[]).filter(
          (key) => filters[key] !== previous[key],
        )
      : [];

    const searchChanged = changedKeys.includes('search');

    const onlySearchChanged =
      searchChanged &&
      changedKeys.every(
        (key) => key === 'search' || (key === 'page' && filters.page === 1),
      );

    let handle: number | undefined;

    if (previous === null || changedKeys.length === 0 || onlySearchChanged) {
      handle = window.setTimeout(() => {
        void load();
      }, SEARCH_DEBOUNCE_MS);
    } else if (changedKeys.length > 0) {
      void load();
    }

    return () => {
      if (handle !== undefined) {
        window.clearTimeout(handle);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  return { filters, updateFilters, resetFilters, reload: load, ...state };
}
