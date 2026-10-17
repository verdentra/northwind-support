import { createContext } from 'react';
import type {
  AgentIdentity,
  LoginRequest,
  LoginResponse,
} from '../../types/api';

export interface AuthContextValue {
  user: AgentIdentity | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (credentials: LoginRequest) => Promise<LoginResponse>;
  logout: () => void;
}

export const AuthContext = createContext<
  AuthContextValue | undefined
>(undefined);
