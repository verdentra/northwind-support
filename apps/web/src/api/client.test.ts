import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { request } from './client';
import { getAuthSession, saveAuthSession } from '../auth/session';

describe('API authentication', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('attaches the stored bearer token to API requests', async () => {
    saveAuthSession({
      accessToken: 'signed-jwt',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      user: { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' },
    });
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 1 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    await request<{ id: number }>('/tickets');

    const requestInit = fetchMock.mock.calls[0]?.[1];
    expect(new Headers(requestInit?.headers).get('Authorization')).toBe('Bearer signed-jwt');
  });

  it('clears the stored session after any unauthorized API response', async () => {
    saveAuthSession({
      accessToken: 'expired-jwt',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      user: { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' },
    });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));

    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });

    expect(getAuthSession()).toBeNull();
    expect(localStorage.length).toBe(0);
  });
});
