import { useEffect, useState, type ReactNode } from 'react';
import { authApi } from '../api/auth';
import type { LoginCredentials } from '../api/auth';
import {
  clearAuthSession,
  getAuthSession,
  saveAuthSession,
  subscribeToSessionChanges,
  type AuthSession,
} from './session';
import { AuthContext } from './context';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() => getAuthSession());

  useEffect(
    () => subscribeToSessionChanges(() => setSession(getAuthSession())),
    [],
  );

  const signIn = async (credentials: LoginCredentials): Promise<void> => {
    const result = await authApi.login(credentials);
    const nextSession: AuthSession = {
      accessToken: result.accessToken,
      expiresAtUtc: result.expiresAtUtc,
      user: result.user,
    };

    saveAuthSession(nextSession);
    setSession(nextSession);
  };

  const signOut = (): void => {
    clearAuthSession();
    setSession(null);
  };

  return (
    <AuthContext.Provider value={{ session, signIn, signOut }}>
      {children}
    </AuthContext.Provider>
  );
}
