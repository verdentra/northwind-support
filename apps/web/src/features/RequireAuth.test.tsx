import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
} from 'react-router-dom';
import { clearAuth, saveAuth } from '../api/auth';
import { RequireAuth } from './RequireAuth';

function CurrentLocation() {
  const location = useLocation();
  return (
    <p>
      {location.pathname}
      {location.search}
    </p>
  );
}

function renderProtectedRoute(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/login" element={<CurrentLocation />} />
        <Route element={<RequireAuth />}>
          <Route
            path="/tickets"
            element={<p>Protected tickets page</p>}
          />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('RequireAuth', () => {
  beforeEach(() => {
    clearAuth();
  });

  it('redirects unauthenticated users to login and preserves returnTo', () => {
    renderProtectedRoute('/tickets?status=Open');

    expect(
      screen.getByText(
        '/login?returnTo=%2Ftickets%3Fstatus%3DOpen'
      )
    ).toBeTruthy();
  });

  it('allows authenticated users to access protected routes', () => {
    saveAuth({
      token: 'test-jwt-token',
      expiresAtUtc: '2026-10-22T12:00:00Z',
      user: {
        id: 2,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
    });

    renderProtectedRoute('/tickets');

    expect(
      screen.getByText('Protected tickets page')
    ).toBeTruthy();
  });
});
