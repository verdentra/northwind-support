import {
  render,
  screen,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
import { App } from './App';
import { useAuth } from './features/auth/useAuth';

vi.mock('./features/auth/useAuth', () => ({
  useAuth: vi.fn(),
}));

const mockedUseAuth = vi.mocked(useAuth);

describe('App authentication controls', () => {
  beforeEach(() => {
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
  });

  it('shows the signed-in agent and logs out', async () => {
    const user = userEvent.setup();
    const logout = vi.fn();

    mockedUseAuth.mockReturnValue({
      user: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
      isAuthenticated: true,
      isLoading: false,
      login: vi.fn(),
      logout,
    });

    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <Routes>
          <Route path="/" element={<App />}>
            <Route
              path="tickets"
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
      screen.getByText(/Alex Turner/),
    ).toBeInTheDocument();

    await user.click(
      screen.getByRole('button', { name: 'Logout' }),
    );

    expect(logout).toHaveBeenCalledTimes(1);

    expect(
      screen.getByText('Login page'),
    ).toBeInTheDocument();
  });
});
