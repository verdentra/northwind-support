import type { AuthenticatedAgent } from '../api/auth';

const sessionStorageKey = 'northwind-support-session';
const sessionChangedEvent = 'northwind-support-session-changed';

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedAgent;
}

export function getAuthSession(): AuthSession | null {
  const storedSession = localStorage.getItem(sessionStorageKey);

  if (storedSession === null) {
    return null;
  }

  let session: unknown;
  try {
    session = JSON.parse(storedSession) as unknown;
  } catch {
    clearAuthSession();
    return null;
  }

  if (!isAuthSession(session) || Date.parse(session.expiresAtUtc) <= Date.now()) {
    clearAuthSession();
    return null;
  }

  return session;
}

export function saveAuthSession(session: AuthSession): void {
  localStorage.setItem(sessionStorageKey, JSON.stringify(session));
  notifySessionChanged();
}

export function clearAuthSession(): void {
  localStorage.removeItem(sessionStorageKey);
  notifySessionChanged();
}

export function subscribeToSessionChanges(listener: () => void): () => void {
  window.addEventListener(sessionChangedEvent, listener);
  window.addEventListener('storage', listener);

  return () => {
    window.removeEventListener(sessionChangedEvent, listener);
    window.removeEventListener('storage', listener);
  };
}

function notifySessionChanged(): void {
  window.dispatchEvent(new Event(sessionChangedEvent));
}

function isAuthSession(value: unknown): value is AuthSession {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const session = value as Partial<AuthSession>;
  const user = session.user;

  return (
    typeof session.accessToken === 'string' &&
    session.accessToken.length > 0 &&
    typeof session.expiresAtUtc === 'string' &&
    Number.isFinite(Date.parse(session.expiresAtUtc)) &&
    typeof user === 'object' &&
    user !== null &&
    typeof user.id === 'number' &&
    typeof user.fullName === 'string' &&
    typeof user.email === 'string'
  );
}
