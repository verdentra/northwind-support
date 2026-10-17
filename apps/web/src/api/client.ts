import type { ProblemDetails } from '../types/api';

/**
 * Where the API lives. In development this stays '/api' and the Vite dev server proxies it
 * to the backend; set VITE_API_BASE_URL to point a build somewhere else. See .env.example.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

const accessTokenStorageKey = 'support-desk.access-token';

export const unauthorizedEventName = 'support-desk:unauthorized';

/** A failed HTTP call, carrying the problem document the API returned. */
export class ApiError extends Error {
  readonly status: number;

  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.problem = problem;
    this.status = status;
  }

  /** Field name to messages, when the failure was a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

export function getAccessToken(): string | null {
  return sessionStorage.getItem(accessTokenStorageKey);
}

export function setAccessToken(accessToken: string): void {
  sessionStorage.setItem(accessTokenStorageKey, accessToken);
}

export function clearAccessToken(): void {
  sessionStorage.removeItem(accessTokenStorageKey);
}

export interface RequestOptions extends RequestInit {
  /**
   * Login itself can return 401 for invalid credentials.
   * That 401 should be shown on the login page instead of triggering
   * the global logout redirect.
   */
  skipUnauthorizedHandling?: boolean;
}

/**
 * The single place that talks to the API. Returns parsed JSON, or throws an
 * ApiError that callers can show to the user.
 */
export async function request<T>(
  path: string,
  init?: RequestOptions,
): Promise<T> {
  const {
    skipUnauthorizedHandling = false,
    ...fetchOptions
  } = init ?? {};

  const accessToken = getAccessToken();

  const response = await fetch(`${baseUrl}${path}`, {
    ...fetchOptions,
    headers: {
      'Content-Type': 'application/json',
      ...(fetchOptions.headers ?? {}),
      ...(accessToken
        ? { Authorization: `Bearer ${accessToken}` }
        : {}),
    },
  });

  if (
    response.status === 401 &&
    !skipUnauthorizedHandling
  ) {
    clearAccessToken();

    window.dispatchEvent(new Event(unauthorizedEventName));
  }

  if (!response.ok) {
    const problem = await readProblem(response);

    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? response.statusText,
      problem,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Turns anything thrown by a fetch into a message worth showing a user. */
export function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return `${fallback} (${error.message})`;
  }

  return fallback;
}

async function readProblem(
  response: Response,
): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    // Not every failure has a JSON body - a dead server certainly will not.
    return undefined;
  }
}
