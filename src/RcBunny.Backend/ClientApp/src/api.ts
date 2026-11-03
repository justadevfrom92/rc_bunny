import { QueryClient } from '@tanstack/react-query';

/** An error from the API, with the server's machine-readable code (missing_perm, invalid, signed_out…). */
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
    public perm?: string,
    public field?: string,
  ) {
    super(message);
  }
}

/**
 * One cache for the whole client. Defaults favour showing cached data instantly and refreshing quietly:
 *  - staleTime 30s: moving between screens within half a minute makes no request at all
 *  - gcTime 10 min: going back to a screen you saw recently paints immediately from cache, then refetches
 *  - every refetch is a conditional GET (the server sends ETags), so unchanged data costs a 304
 * Individual queries override these (reference data is cached far longer, see queries.ts).
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime: 10 * 60_000,
      refetchOnWindowFocus: true,
      retry: (count, err) => !(err instanceof ApiError && err.status < 500) && count < 2,
    },
    mutations: { retry: false },
  },
});

/** The access version the client's copy of "me" was built from. Set by auth.tsx. */
let knownAccessVersion: number | null = null;
export const setKnownAccessVersion = (v: number | null) => {
  knownAccessVersion = v;
};

/**
 * The session ended under us (disabled, password changed, access ended, cookie expired): forget every cached
 * response and go to sign-in with a full page load, so nothing from this person stays in memory.
 */
let leaving = false;
function signedOut() {
  if (leaving) return;
  leaving = true;
  knownAccessVersion = null;
  queryClient.clear();
  const here = window.location.pathname + window.location.search;
  window.location.assign(`/login?next=${encodeURIComponent(here)}`);
}

/**
 * fetch() for the admin API. Adds the CSRF header and cookie, turns error bodies into ApiError, and watches two
 * response headers the server sets on every signed-in request:
 *  - X-Access-Version: if it differs from the version our "me" came from, someone changed this person's
 *    access (role, allow/deny, company, end date) — refetch "me" right away so menus and guards update.
 *  - X-Signed-Out: the sign-in was ended (disabled, password changed, access ended) — drop all cached data.
 */
export async function api<T>(path: string, init: RequestInit & { json?: unknown } = {}): Promise<T> {
  const { json, headers, ...rest } = init;
  const res = await fetch(path, {
    credentials: 'same-origin',
    ...rest,
    headers: {
      Accept: 'application/json',
      'X-RCB': '1',
      ...(json !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...headers,
    },
    body: json !== undefined ? JSON.stringify(json) : rest.body,
  });

  const version = res.headers.get('X-Access-Version');
  if (version && knownAccessVersion !== null && Number(version) !== knownAccessVersion) {
    knownAccessVersion = Number(version);
    void queryClient.invalidateQueries({ queryKey: ['me'] });
  }
  // /api/auth/* answers its own 401s (the sign-in form, and "me" on first load, which MeProvider turns into a redirect).
  if (!path.startsWith('/api/auth/') && (res.headers.get('X-Signed-Out') || res.status === 401)) {
    signedOut();
  }

  if (res.status === 204) return undefined as T;
  const body = res.headers.get('content-type')?.includes('json') ? await res.json() : null;
  if (!res.ok) {
    throw new ApiError(
      res.status,
      body?.code ?? 'error',
      body?.message ?? (res.status === 429 ? 'Too many tries. Wait a minute and try again.' : `Something went wrong (${res.status}).`),
      body?.perm,
      body?.field,
    );
  }
  return body as T;
}

export const post = <T>(path: string, json?: unknown) => api<T>(path, { method: 'POST', json: json ?? {} });
export const put = <T>(path: string, json: unknown) => api<T>(path, { method: 'PUT', json });

export const qs = (params: Record<string, string | number | boolean | undefined | null>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v));
  const s = p.toString();
  return s ? `?${s}` : '';
};
