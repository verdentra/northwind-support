import { createContext } from 'react';
import type { LoginCredentials } from '../api/auth';
import type { AuthSession } from './session';

export interface AuthContextValue {
  session: AuthSession | null;
  signIn: (credentials: LoginCredentials) => Promise<void>;
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
