import {
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import {
  AuthContext,
  type AuthContextValue,
} from '../authContext';
import {
  clearAccessToken,
  getAccessToken,
  setAccessToken,
  unauthorizedEventName,
} from '../../../api/client';
import {
  getCurrentUser,
  login as loginRequest,
} from '../../../api/auth';
import type {
  AgentIdentity,
  LoginRequest,
  LoginResponse,
} from '../../../types/api';

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({
  children,
}: AuthProviderProps) {
  const [user, setUser] = useState<AgentIdentity | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    function handleUnauthorized(): void {
      setUser(null);
    }

    window.addEventListener(
      unauthorizedEventName,
      handleUnauthorized,
    );

    async function loadCurrentUser(): Promise<void> {
      const accessToken = getAccessToken();

      if (!accessToken) {
        setIsLoading(false);
        return;
      }

      try {
        const currentUser = await getCurrentUser();
        setUser(currentUser);
      } catch {
        clearAccessToken();
        setUser(null);
      } finally {
        setIsLoading(false);
      }
    }

    void loadCurrentUser();

    return () => {
      window.removeEventListener(
        unauthorizedEventName,
        handleUnauthorized,
      );
    };
  }, []);

  async function login(
    credentials: LoginRequest,
  ): Promise<LoginResponse> {
    const response = await loginRequest(credentials);

    setAccessToken(response.accessToken);
    setUser(response.user);

    return response;
  }

  function logout(): void {
    clearAccessToken();
    setUser(null);
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isLoading,
      login,
      logout,
    }),
    [user, isLoading],
  );

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}
