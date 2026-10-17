import {
  render,
  screen,
} from '@testing-library/react';
import {
  MemoryRouter,
  Route,
  Routes,
} from 'react-router-dom';
import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest';
import { ProtectedRoute } from './ProtectedRoute';
import { useAuth } from './useAuth';

vi.mock('./useAuth', () => ({
  useAuth: vi.fn(),
}));

const mockedUseAuth = vi.mocked(useAuth);

describe('ProtectedRoute', () => {
  beforeEach(() => {
    mockedUseAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      login: vi.fn(),
      logout: vi.fn(),
    });
  });

  it('redirects unauthenticated users to login', () => {
    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route
              path="/tickets"
              element={<p>Tickets page</p>}
            />
          </Route>

          <Route
            path="/login"
            element={<p>Login page</p>}
          />
        </Routes>
      </MemoryRouter>,
    );

    expect(
      screen.getByText('Login page'),
    ).toBeInTheDocument();

    expect(
      screen.queryByText('Tickets page'),
    ).not.toBeInTheDocument();
  });

  it('shows the protected page for an authenticated user', () => {
    mockedUseAuth.mockReturnValue({
      user: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
      isAuthenticated: true,
      isLoading: false,
      login: vi.fn(),
      logout: vi.fn(),
    });

    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route
              path="/tickets"
              element={<p>Tickets page</p>}
            />
          </Route>

          <Route
            path="/login"
            element={<p>Login page</p>}
          />
        </Routes>
      </MemoryRouter>,
    );

    expect(
      screen.getByText('Tickets page'),
    ).toBeInTheDocument();

    expect(
      screen.queryByText('Login page'),
    ).not.toBeInTheDocument();
  });
});
