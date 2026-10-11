import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { AuthProvider } from '../../auth/AuthContext';
import { RequireAuth } from '../../auth/RequireAuth';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    getCurrentUser: vi.fn(),
  },
}));

const login = vi.mocked(authApi.login);

function renderAuthRoutes(initialEntry: string) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/tickets/:id"
            element={
              <RequireAuth>
                <p>Requested ticket</p>
              </RequireAuth>
            }
          />
          <Route path="/tickets" element={<p>Ticket list</p>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

describe('login flow', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.resetAllMocks();
  });

  it('shows a clear credentials error and stores no token after a rejected login', async () => {
    login.mockRejectedValue(new ApiError(401, 'Email or password is incorrect.'));
    renderAuthRoutes('/login');

    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'alex.turner@northwind-support.example' },
    });
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'wrong-password' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Email or password is incorrect.',
    );
    expect(localStorage.length).toBe(0);
  });

  it('returns to the requested protected page after successful sign in', async () => {
    login.mockResolvedValue({
      accessToken: 'signed-jwt',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      user: { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' },
    });
    renderAuthRoutes('/tickets/42');

    fireEvent.change(await screen.findByLabelText('Email'), {
      target: { value: 'alex@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'LocalDev123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Requested ticket')).toBeInTheDocument();
    expect(localStorage.length).toBe(1);
  });
});
