import { beforeEach, describe, expect, it } from 'vitest';
import {
  clearAuth,
  getStoredUser,
  getToken,
  saveAuth,
} from './auth';

const loginResponse = {
  token: 'test-jwt-token',
  expiresAtUtc: '2026-10-22T12:00:00Z',
  user: {
    id: 2,
    fullName: 'Alex Turner',
    email: 'alex.turner@northwind-support.example',
  },
};

describe('authentication session', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('stores the JWT and authenticated agent after login', () => {
    saveAuth(loginResponse);

    expect(getToken()).toBe('test-jwt-token');
    expect(getStoredUser()).toEqual(loginResponse.user);
  });

  it('clears the JWT and agent when logging out', () => {
    saveAuth(loginResponse);

    clearAuth();

    expect(getToken()).toBeNull();
    expect(getStoredUser()).toBeNull();
  });

  it('clears invalid stored user data safely', () => {
    sessionStorage.setItem('northwind_auth_token', 'test-jwt-token');
    sessionStorage.setItem('northwind_auth_user', '{invalid-json');

    expect(getStoredUser()).toBeNull();
    expect(getToken()).toBeNull();
  });
});
