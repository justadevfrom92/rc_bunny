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
    if (res.status === 401 || res.status === 403) window.dispatchEvent(new Event('access-changed'));
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
