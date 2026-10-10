import { createContext, useContext } from 'react';
import type { CurrentAgent } from '../../types/api';

export interface AuthState {
  /** The signed-in agent, or null. */
  agent: CurrentAgent | null;
  /** Signs in; rejects with the API's error when the credentials are refused. */
  login: (email: string, password: string) => Promise<void>;
  /** Forgets the session on this device. */
  logout: () => void;
}

export const AuthContext = createContext<AuthState | null>(null);

/** The current session and the sign-in/out actions. Must be used inside an AuthProvider. */
export function useAuth(): AuthState {
  const auth = useContext(AuthContext);

  if (!auth) {
    throw new Error('useAuth must be used inside an AuthProvider.');
  }

  return auth;
}
