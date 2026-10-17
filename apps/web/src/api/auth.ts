import { request } from './client';
import type {
  AgentIdentity,
  LoginRequest,
  LoginResponse,
} from '../types/api';

export function login(
  credentials: LoginRequest,
): Promise<LoginResponse> {
  return request<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(credentials),
    skipUnauthorizedHandling: true,
  });
}

export function getCurrentUser(): Promise<AgentIdentity> {
  return request<AgentIdentity>('/auth/me');
}
