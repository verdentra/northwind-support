import type { CurrentAgent, LoginResponse } from '../../types/api';

/** What is kept for a signed-in agent between page loads. */
export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  agent: CurrentAgent;
}

/**
 * sessionStorage, not localStorage: the session survives a page reload but ends when the tab is
 * closed, and is not shared with other tabs. Like any script-readable storage it is exposed to
 * XSS; see the README for the trade-off against an HttpOnly cookie.
 */
const storageKey = 'northwind.session';

export function sessionFrom(response: LoginResponse): Session {
  return { accessToken: response.accessToken, expiresAtUtc: response.expiresAtUtc, agent: response.agent };
}

export function isExpired(session: Session, now: number = Date.now()): boolean {
  return new Date(session.expiresAtUtc).getTime() <= now;
}

/** The stored session, or null when there is none, it is unreadable, or it has expired. */
export function loadSession(): Session | null {
  try {
    const raw = window.sessionStorage.getItem(storageKey);

    if (!raw) {
      return null;
    }

    const session = JSON.parse(raw) as Session;

    if (!session.accessToken || !session.agent || isExpired(session)) {
      window.sessionStorage.removeItem(storageKey);
      return null;
    }

    return session;
  } catch {
    return null;
  }
}

export function saveSession(session: Session): void {
  try {
    window.sessionStorage.setItem(storageKey, JSON.stringify(session));
  } catch {
    // Storage can be unavailable (private mode, quota); the session then lasts until reload.
  }
}

export function clearSession(): void {
  try {
    window.sessionStorage.removeItem(storageKey);
  } catch {
    // Nothing to clear.
  }
}
