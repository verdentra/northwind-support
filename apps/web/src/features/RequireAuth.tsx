
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { getToken } from '../api/auth';

export function RequireAuth() {
  const location = useLocation();

  if (!getToken()) {
    const returnTo = location.pathname + location.search;

    return (
      <Navigate
        to={`/login?returnTo=${encodeURIComponent(returnTo)}`}
        replace
      />
    );
  }

  return <Outlet />;
}
