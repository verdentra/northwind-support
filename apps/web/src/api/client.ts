import type { ProblemDetails } from '../types/api';

/**
 * Where the API lives. In development this stays '/api' and the Vite dev server proxies it
 * to the backend; set VITE_API_BASE_URL to point a build somewhere else. See .env.example.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

/** A failed HTTP call, carrying the problem document the API returned. */
export class ApiError extends Error {
  readonly status: number;

  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Field name to messages, when the failure was a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

export interface RequestOptions {
  /**
   * Send without the access token, and do not treat a 401 as an expired session - for the
   * sign-in call itself, where a 401 just means wrong credentials.
   */
  anonymous?: boolean;
}

let accessToken: string | null = null;
let onUnauthorized: (() => void) | null = null;

/** The token attached to every request from now on; null to stop sending one. */
export function setAccessToken(token: string | null): void {
  accessToken = token;
}

/** Called whenever an authenticated request comes back 401 (e.g. the token expired). */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler;
}

/**
 * The single place that talks to the API. Attaches the access token, returns parsed JSON, or
 * throws an {@link ApiError} that callers can show to the user.
 */
export async function request<T>(path: string, init?: RequestInit, options: RequestOptions = {}): Promise<T> {
  const authorization: Record<string, string> =
    accessToken && !options.anonymous ? { Authorization: `Bearer ${accessToken}` } : {};

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...authorization,
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    const problem = await readProblem(response);

    if (response.status === 401 && !options.anonymous) {
      onUnauthorized?.();
    }

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

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    // Not every failure has a JSON body - a dead server certainly will not.
    return undefined;
  }
}
