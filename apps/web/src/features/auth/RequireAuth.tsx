import { Navigate, useLocation } from 'react-router-dom';
import type { LoginLocationState } from './AuthProvider';
import { useAuth } from './useAuth';

/** Renders its children for a signed-in agent; sends anyone else to sign in, then back here. */
export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { agent } = useAuth();
  const location = useLocation();

  if (!agent) {
    const state: LoginLocationState = { from: `${location.pathname}${location.search}` };
    return <Navigate to="/login" replace state={state} />;
  }

  return children;
}
