import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest';
import {
  clearAccessToken,
  getAccessToken,
  request,
  setAccessToken,
  unauthorizedEventName,
} from './client';

describe('API client authentication', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('attaches the stored bearer token to API requests', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({ items: [] }),
        {
          status: 200,
          headers: {
            'Content-Type': 'application/json',
          },
        },
      ),
    );

    vi.stubGlobal('fetch', fetchMock);

    setAccessToken('test-access-token');

    await request<{ items: unknown[] }>('/tickets');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/tickets',
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-access-token',
        }),
      }),
    );
  });

  it('clears the token and emits an event after a 401 response', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'Unauthorized',
          detail: 'Token expired.',
        }),
        {
          status: 401,
          headers: {
            'Content-Type': 'application/json',
          },
        },
      ),
    );

    vi.stubGlobal('fetch', fetchMock);

    const unauthorizedHandler = vi.fn();

    window.addEventListener(
      unauthorizedEventName,
      unauthorizedHandler,
    );

    setAccessToken('expired-token');

    await expect(
      request('/tickets'),
    ).rejects.toMatchObject({
      status: 401,
    });

    expect(getAccessToken()).toBeNull();
    expect(unauthorizedHandler).toHaveBeenCalledTimes(1);

    window.removeEventListener(
      unauthorizedEventName,
      unauthorizedHandler,
    );
  });

  it('does not clear the session for a login 401', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'Unauthorized',
          detail: 'Invalid email or password.',
        }),
        {
          status: 401,
          headers: {
            'Content-Type': 'application/json',
          },
        },
      ),
    );

    vi.stubGlobal('fetch', fetchMock);

    const unauthorizedHandler = vi.fn();

    window.addEventListener(
      unauthorizedEventName,
      unauthorizedHandler,
    );

    setAccessToken('existing-token');

    await expect(
      request('/auth/login', {
        method: 'POST',
        body: JSON.stringify({
          email: 'wrong@example.com',
          password: 'wrong-password',
        }),
        skipUnauthorizedHandling: true,
      }),
    ).rejects.toMatchObject({
      status: 401,
    });

    expect(getAccessToken()).toBe('existing-token');
    expect(unauthorizedHandler).not.toHaveBeenCalled();

    window.removeEventListener(
      unauthorizedEventName,
      unauthorizedHandler,
    );

    clearAccessToken();
  });
});
