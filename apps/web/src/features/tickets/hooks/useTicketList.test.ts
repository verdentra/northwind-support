import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('fetches immediately when the status filter changes', async () => {
  const { result } = renderHook(() => useTicketList());

  // Allow the initial request to settle.
  await act(async () => {
    await Promise.resolve();
  });

  const initialCallCount = getTickets.mock.calls.length;

  act(() => {
    result.current.updateFilters({
      status: 'InProgress', // Replace with the valid project value.
    });
  });

  // Status changes should trigger a request immediately,
  // without advancing the search debounce timer.
  expect(getTickets).toHaveBeenCalledTimes(initialCallCount + 1);
  expect(getTickets).toHaveBeenLastCalledWith(
    expect.objectContaining({
      status: 'InProgress',
      page: 1,
    }),
  );
});
});
