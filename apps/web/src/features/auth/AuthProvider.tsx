import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { setAccessToken, setUnauthorizedHandler } from '../../api/client';
import { clearSession, loadSession, saveSession, sessionFrom, type Session } from './session';
import { AuthContext, type AuthState } from './useAuth';

/** Where to go after signing in: the page the visitor was sent away from. */
export interface LoginLocationState {
  from?: string;
}

function startSession(): Session | null {
  const session = loadSession();
  setAccessToken(session?.accessToken ?? null);
  return session;
}

/**
 * Owns the session: restores it on load, attaches its token to every API call, and ends it on
 * logout or on any 401 from the API (an expired or revoked token), sending the agent to the
 * sign-in page and back afterwards.
 */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session | null>(startSession);
  const navigate = useNavigate();
  const location = useLocation();

  const end = useCallback(() => {
    clearSession();
    setAccessToken(null);
    setSession(null);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(() => {
      end();
      const from = `${location.pathname}${location.search}`;
      void navigate('/login', { replace: true, state: { from } satisfies LoginLocationState });
    });

    return () => setUnauthorizedHandler(null);
  }, [end, navigate, location.pathname, location.search]);

  const login = useCallback(async (email: string, password: string) => {
    const next = sessionFrom(await authApi.login(email, password));
    saveSession(next);
    setAccessToken(next.accessToken);
    setSession(next);
  }, []);

  const value = useMemo<AuthState>(
    () => ({ agent: session?.agent ?? null, login, logout: end }),
    [session, login, end],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
