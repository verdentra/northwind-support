import {
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  MemoryRouter,
  useLocation,
} from 'react-router-dom';
import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest';
import { LoginPage } from './LoginPage';
import { useAuth } from './useAuth';

vi.mock('./useAuth', () => ({
  useAuth: vi.fn(),
}));

const mockedUseAuth = vi.mocked(useAuth);

function CurrentPath() {
  const location = useLocation();

  return (
    <p data-testid="current-path">
      {location.pathname}
    </p>
  );
}

describe('LoginPage', () => {
  beforeEach(() => {
    mockedUseAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      login: vi.fn(),
      logout: vi.fn(),
    });
  });

  it('shows validation errors and does not submit empty fields', async () => {
    const user = userEvent.setup();
    const login = vi.fn();

    mockedUseAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      login,
      logout: vi.fn(),
    });

    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    );

    await user.click(
      screen.getByRole('button', { name: 'Sign in' }),
    );

    expect(
      screen.getByText('Email is required.'),
    ).toBeInTheDocument();

    expect(
      screen.getByText('Password is required.'),
    ).toBeInTheDocument();

    expect(login).not.toHaveBeenCalled();
  });

  it('does not submit an invalid email address', async () => {
    const user = userEvent.setup();
    const login = vi.fn();

    mockedUseAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      login,
      logout: vi.fn(),
    });

    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    );

    await user.type(
      screen.getByLabelText('Email address'),
      'invalid-email',
    );

    await user.type(
      screen.getByLabelText('Password'),
      'some-password',
    );

    await user.click(
      screen.getByRole('button', { name: 'Sign in' }),
    );

    expect(
      screen.getByText('Enter a valid email address.'),
    ).toBeInTheDocument();

    expect(login).not.toHaveBeenCalled();
  });

  it('logs in successfully and redirects to the ticket list', async () => {
    const user = userEvent.setup();

    const login = vi.fn().mockResolvedValue({
      accessToken: 'test-access-token',
      expiresAtUtc: '2026-10-17T10:00:00Z',
      user: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
    });

    mockedUseAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      login,
      logout: vi.fn(),
    });

    render(
      <MemoryRouter initialEntries={['/login']}>
        <LoginPage />
        <CurrentPath />
      </MemoryRouter>,
    );

    await user.type(
      screen.getByLabelText('Email address'),
      'alex.turner@northwind-support.example',
    );

    await user.type(
      screen.getByLabelText('Password'),
      'DevOnly-Password1!',
    );

    await user.click(
      screen.getByRole('button', { name: 'Sign in' }),
    );

    await waitFor(() => {
      expect(
        screen.getByTestId('current-path'),
      ).toHaveTextContent('/tickets');
    });

    expect(login).toHaveBeenCalledWith({
      email: 'alex.turner@northwind-support.example',
      password: 'DevOnly-Password1!',
    });
  });

  it('returns to the originally requested page after login', async () => {
    const user = userEvent.setup();

    const login = vi.fn().mockResolvedValue({
      accessToken: 'test-access-token',
      expiresAtUtc: '2026-10-17T10:00:00Z',
      user: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
    });

    mockedUseAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      login,
      logout: vi.fn(),
    });

    render(
      <MemoryRouter
        initialEntries={[
          {
            pathname: '/login',
            state: {
              from: {
                pathname: '/tickets/23',
                search: '',
                hash: '',
              },
            },
          },
        ]}
      >
        <LoginPage />
        <CurrentPath />
      </MemoryRouter>,
    );

    await user.type(
      screen.getByLabelText('Email address'),
      'alex.turner@northwind-support.example',
    );

    await user.type(
      screen.getByLabelText('Password'),
      'DevOnly-Password1!',
    );

    await user.click(
      screen.getByRole('button', { name: 'Sign in' }),
    );

    await waitFor(() => {
      expect(
        screen.getByTestId('current-path'),
      ).toHaveTextContent('/tickets/23');
    });
  });
});
