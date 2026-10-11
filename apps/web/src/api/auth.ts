import { request } from './client';

export interface AuthenticatedAgent {
  id: number;
  fullName: string;
  email: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedAgent;
}

export interface LoginCredentials {
  email: string;
  password: string;
}

export const authApi = {
  login: (credentials: LoginCredentials) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(credentials),
    }),

  getCurrentUser: () => request<AuthenticatedAgent>('/auth/me'),
};
