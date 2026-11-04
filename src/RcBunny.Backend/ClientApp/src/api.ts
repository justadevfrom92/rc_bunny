import { useCallback, useEffect, useState } from 'react';

// Small helpers for calling the backend. Errors come back as { error: "..." }.

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) {
    const data = await res.json().catch(() => null);
    const message = data?.error ?? (res.status === 403 ? "You don't have access to that." : 'Something went wrong.');
    // Signed out, or access changed: let the app re-check who we are.
    if ((res.status === 401 || res.status === 403) && url !== '/api/me') window.dispatchEvent(new Event('access-changed'));
    throw new ApiError(res.status, message);
  }
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T = void>(url: string, body?: unknown) => request<T>('POST', url, body ?? {}),
  put: <T = void>(url: string, body: unknown) => request<T>('PUT', url, body),
  del: <T = void>(url: string) => request<T>('DELETE', url),
};

// Remembers the last answer for each URL, so going back to a screen shows it instantly while it refreshes.
const cache = new Map<string, unknown>();

/** Loads JSON from url (pass null to load nothing yet). */
export function useLoad<T>(url: string | null) {
  const [data, setData] = useState<T | undefined>(url ? (cache.get(url) as T | undefined) : undefined);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(() => {
    if (!url) return;
    setError(null);
    api.get<T>(url).then(
      (result) => { cache.set(url, result); setData(result); },
      (err: Error) => setError(err.message),
    );
  }, [url]);

  useEffect(() => {
    setData(url ? (cache.get(url) as T | undefined) : undefined);
    reload();
  }, [url, reload]);

  return { data, error, reload };
}

export const money = (n: number) => n.toLocaleString('en-US', { style: 'currency', currency: 'USD' });
export const date = (s: string) => new Date(s.length === 10 ? s + 'T00:00' : s).toLocaleDateString('en-US');
/** ebunny's date and time: 10/26/2026 9:04 PM */
export const dateTime = (s: string) =>
  new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');
/** Builds a query string from the values that aren't empty. */
export const qs = (values: Record<string, string | number | boolean | null | undefined>) =>
  new URLSearchParams(Object.entries(values).filter(([, v]) => v !== '' && v != null && v !== false).map(([k, v]) => [k, String(v)])).toString();

/** Sends a file (multipart form), for uploads. */
export async function upload(url: string, file: File) {
  const body = new FormData();
  body.append('file', file);
  const res = await fetch(url, { method: 'POST', body });
  if (!res.ok) throw new ApiError(res.status, (await res.json().catch(() => null))?.error ?? 'Upload failed.');
}
