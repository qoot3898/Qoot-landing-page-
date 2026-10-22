/**
 * The one HTTP client for the website and the dashboard.
 *
 * - The API origin comes from VITE_API_BASE_URL (empty = same origin, the recommended deployment).
 * - Cookies are always sent, so the dashboard session works across the site.
 * - Every POST/PUT/PATCH/DELETE carries the antiforgery token; an expired token is renewed once.
 * - Failures become ApiError with the API's stable `code`, a localised `message` and field `errors`.
 */

export const API_BASE = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '');

export type FieldErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly errors: FieldErrors;
  readonly details: unknown;

  constructor(status: number, code: string, message: string, errors: FieldErrors = {}, details?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.errors = errors;
    this.details = details;
  }

  get isNetwork(): boolean {
    return this.status === 0;
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  form?: FormData;
  lang?: string;
  signal?: AbortSignal;
  timeoutMs?: number;
  /** Extra headers (If-None-Match ...). */
  headers?: Record<string, string>;
}

let csrfToken: string | null = null;
let csrfPromise: Promise<string> | null = null;

export function apiUrl(path: string): string {
  return path.startsWith('http') ? path : API_BASE + path;
}

/** Fetches (once) the antiforgery token tied to the current session. Call reset after signing in or out. */
export async function antiforgeryToken(force = false): Promise<string> {
  if (csrfToken && !force) return csrfToken;
  if (!csrfPromise || force) {
    csrfPromise = fetch(apiUrl('/api/auth/antiforgery'), { credentials: 'include', headers: { Accept: 'application/json' } })
      .then(async (r) => {
        if (!r.ok) throw await toError(r);
        const json = (await r.json()) as { token: string };
        csrfToken = json.token;
        return json.token;
      })
      .finally(() => {
        csrfPromise = null;
      });
  }
  return csrfPromise;
}

export function resetAntiforgery(): void {
  csrfToken = null;
}

function currentLang(): string {
  return typeof document !== 'undefined' && document.documentElement.lang === 'en' ? 'en' : 'ar';
}

export async function api<T>(path: string, options: RequestOptions = {}, retried = false): Promise<T> {
  const method = options.method ?? 'GET';
  const headers: Record<string, string> = {
    Accept: 'application/json',
    'Accept-Language': options.lang ?? currentLang(),
    ...options.headers,
  };
  let body: BodyInit | undefined;
  if (options.form) {
    body = options.form;
  } else if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(options.body);
  }

  if (method !== 'GET') {
    headers['X-XSRF-TOKEN'] = await antiforgeryToken();
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), options.timeoutMs ?? 20000);
  const onAbort = () => controller.abort();
  options.signal?.addEventListener('abort', onAbort, { once: true });

  let response: Response;
  try {
    response = await fetch(apiUrl(path), { method, headers, body, credentials: 'include', signal: controller.signal });
  } catch (error) {
    if (options.signal?.aborted) throw error;
    throw new ApiError(0, 'network', networkMessage(options.lang ?? currentLang()));
  } finally {
    clearTimeout(timeout);
    options.signal?.removeEventListener('abort', onAbort);
  }

  if (response.status === 304) {
    return undefined as T;
  }

  if (!response.ok) {
    const error = await toError(response);
    if (error.code === 'antiforgery' && !retried) {
      resetAntiforgery();
      return api<T>(path, options, true);
    }
    throw error;
  }

  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function toError(response: Response): Promise<ApiError> {
  try {
    const problem = (await response.json()) as { code?: string; title?: string; errors?: FieldErrors; details?: unknown };
    return new ApiError(response.status, problem.code ?? `http_${response.status}`, problem.title ?? response.statusText, problem.errors ?? {}, problem.details);
  } catch {
    return new ApiError(response.status, `http_${response.status}`, response.statusText || `HTTP ${response.status}`);
  }
}

function networkMessage(lang: string): string {
  return lang === 'ar' ? 'تعذّر الاتصال بالخادم. تحقق من الاتصال وحاول مجددًا.' : "Couldn't reach the server. Check your connection and try again.";
}
