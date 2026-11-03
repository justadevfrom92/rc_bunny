import type { ReactNode } from 'react';
import { ApiError } from './api';

export const money = (n: number) => n.toLocaleString('en-US', { style: 'currency', currency: 'USD' });
export const num = (n: number) => n.toLocaleString('en-US');

export function date(s: string | null | undefined) {
  if (!s) return '—';
  const d = new Date(s.length === 10 ? `${s}T00:00:00` : s);
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export function dateTime(s: string | null | undefined) {
  if (!s) return '—';
  return new Date(s).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
}

export function Pill({ tone, children }: { tone?: string; children: ReactNode }) {
  return <span className={`pill ${tone ?? ''}`}>{children}</span>;
}

/** Placeholder rows while a section loads: the layout doesn't jump when the data arrives. */
export function Skeleton({ rows = 5, cols = 4 }: { rows?: number; cols?: number }) {
  return (
    <div className="skeleton" aria-busy="true" aria-label="Loading">
      {Array.from({ length: rows }, (_, r) => (
        <div className="sk-row" key={r}>
          {Array.from({ length: cols }, (_, c) => <span key={c} style={{ width: `${40 + ((r * 7 + c * 13) % 50)}%` }} />)}
        </div>
      ))}
    </div>
  );
}

export function ErrorBox({ error }: { error: unknown }) {
  if (!error) return null;
  const msg = error instanceof ApiError ? error.message : 'Something went wrong.';
  return <div className="alert bad" role="alert">{msg}</div>;
}

export function Panel({ title, actions, children, id }: { title?: ReactNode; actions?: ReactNode; children: ReactNode; id?: string }) {
  return (
    <section className="panel" id={id}>
      {(title || actions) && (
        <header className="panel-head">
          {title && <h2>{title}</h2>}
          {actions && <div className="panel-actions">{actions}</div>}
        </header>
      )}
      {children}
    </section>
  );
}

/** A small "updating…" marker for background refetches, so cached data can show immediately. */
export function Refreshing({ on }: { on: boolean }) {
  return on ? <span className="refreshing" aria-live="polite">updating…</span> : null;
}
