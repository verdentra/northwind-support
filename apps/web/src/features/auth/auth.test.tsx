import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect } from 'react';
import { MemoryRouter, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError, request, setAccessToken } from '../../api/client';
import type { LoginResponse } from '../../types/api';
import { AuthProvider } from './AuthProvider';
import { LoginPage } from './LoginPage';
import { RequireAuth } from './RequireAuth';
import { saveSession } from './session';
import { useAuth } from './useAuth';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn(), me: vi.fn() } }));

const login = vi.mocked(authApi.login);
const fetchMock = vi.fn<typeof fetch>();

const signedIn: LoginResponse = {
  accessToken: 'header.payload.signature',
  expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara.lindqvist@northwind-support.example' },
};

function Where() {
  const location = useLocation();
  return <p data-testid="where">{location.pathname}</p>;
}

function Protected() {
  const { agent } = useAuth();
  return <p>Hello {agent?.fullName}</p>;
}

/** A protected page whose data call comes back 401, as with an expired token. */
function ExpiresOnLoad() {
  useEffect(() => {
    request('/tickets').catch(() => undefined);
  }, []);
  return <p>Loading tickets</p>;
}

function renderApp(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          element={
            <AuthProvider>
              <Outlet />
              <Where />
            </AuthProvider>
          }
        >
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/tickets/42"
            element={
              <RequireAuth>
                <Protected />
              </RequireAuth>
            }
          />
          <Route
            path="/tickets"
            element={
              <RequireAuth>
                <ExpiresOnLoad />
              </RequireAuth>
            }
          />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

async function signIn(email: string, password: string) {
  const user = userEvent.setup();
  if (email) {
    await user.type(screen.getByLabelText('Email'), email);
  }
  if (password) {
    await user.type(screen.getByLabelText('Password'), password);
  }
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('authentication', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    setAccessToken(null);
    vi.clearAllMocks();
    fetchMock.mockReset();
    vi.unstubAllGlobals();
  });

  it('sends a visitor without a session to the sign-in page', () => {
    renderApp('/tickets/42');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.getByTestId('where')).toHaveTextContent('/login');
    expect(screen.queryByText(/Hello/)).not.toBeInTheDocument();
  });

  it('returns the agent to the page they wanted after signing in', async () => {
    login.mockResolvedValue(signedIn);
    renderApp('/tickets/42');

    await signIn('sara.lindqvist@northwind-support.example', 'LocalDev-Only-Pa55!');

    expect(await screen.findByText('Hello Sara Lindqvist')).toBeInTheDocument();
    expect(screen.getByTestId('where')).toHaveTextContent('/tickets/42');
    expect(login).toHaveBeenCalledWith('sara.lindqvist@northwind-support.example', 'LocalDev-Only-Pa55!');
  });

  it('does not call the API when the form is invalid', async () => {
    renderApp('/login');

    await signIn('not-an-email', '');

    expect(login).not.toHaveBeenCalled();
    expect(screen.getByText('Enter your work email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
  });

  it('shows a clear error and stays on the page when the credentials are refused', async () => {
    login.mockRejectedValue(new ApiError(401, 'Email or password is incorrect.'));
    renderApp('/login');

    await signIn('sara.lindqvist@northwind-support.example', 'wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
    expect(screen.getByTestId('where')).toHaveTextContent('/login');
    expect(screen.getByLabelText('Password')).toHaveValue('');
  });

  it('restores a saved session on load', () => {
    saveSession(signedIn);

    renderApp('/tickets/42');

    expect(screen.getByText('Hello Sara Lindqvist')).toBeInTheDocument();
  });

  it('ignores an expired saved session', () => {
    saveSession({ ...signedIn, expiresAtUtc: new Date(Date.now() - 1000).toISOString() });

    renderApp('/tickets/42');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('clears the session and asks the agent to sign in again when the API answers 401', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ title: 'Unauthorized' }), { status: 401 }));
    saveSession(signedIn);

    renderApp('/tickets');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    await waitFor(() => expect(window.sessionStorage.length).toBe(0));
    expect(fetchMock.mock.calls[0][1]?.headers).toMatchObject({ Authorization: 'Bearer header.payload.signature' });
  });
});
