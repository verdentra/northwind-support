import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, request, setAccessToken, setUnauthorizedHandler } from './client';

const fetchMock = vi.fn<typeof fetch>();

function respond(status: number, body: unknown = {}) {
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  );
}

function sentHeaders(): Record<string, string> {
  const init = fetchMock.mock.calls[0][1];
  return (init?.headers ?? {}) as Record<string, string>;
}

describe('request', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    setAccessToken(null);
    setUnauthorizedHandler(null);
    fetchMock.mockReset();
    vi.unstubAllGlobals();
  });

  it('attaches the access token to every call', async () => {
    respond(200, []);
    setAccessToken('abc.def.ghi');

    await request('/tickets');

    expect(sentHeaders().Authorization).toBe('Bearer abc.def.ghi');
  });

  it('sends no token when there is no session', async () => {
    respond(200, []);

    await request('/tickets');

    expect(sentHeaders().Authorization).toBeUndefined();
  });

  it('ends the session when an authenticated call comes back 401', async () => {
    respond(401, { title: 'Unauthorized', detail: 'Sign in to continue.' });
    setAccessToken('expired.token.value');
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('treats a 401 from the sign-in call as wrong credentials, not an expired session', async () => {
    respond(401, { detail: 'Email or password is incorrect.' });
    setAccessToken('stale.token.value');
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);

    await expect(request('/auth/login', { method: 'POST' }, { anonymous: true })).rejects.toMatchObject({
      status: 401,
      message: 'Email or password is incorrect.',
    });

    expect(onUnauthorized).not.toHaveBeenCalled();
    expect(sentHeaders().Authorization).toBeUndefined();
  });
});
