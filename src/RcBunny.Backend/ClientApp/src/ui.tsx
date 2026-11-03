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

/** A big number with a label. */
export function Stat({ label, value, to, tone }: { label: string; value: ReactNode; to?: string; tone?: string }) {
  const body = (<><strong className={tone ? `${tone}-text` : ''}>{value}</strong><span>{label}</span></>);
  return to ? <a className="stat" href={to}>{body}</a> : <div className="stat">{body}</div>;
}

export function Stats({ children }: { children: ReactNode }) {
  return <div className="stats">{children}</div>;
}

/** Horizontal bars: label, bar, value. */
export function Bars({ rows, format = (n: number) => n.toLocaleString('en-US') }: { rows: { label: string; value: number; hint?: string }[]; format?: (n: number) => string }) {
  const max = Math.max(1, ...rows.map((r) => r.value));
  return (
    <table className="table compact bars">
      <tbody>
        {rows.map((r) => (
          <tr key={r.label}>
            <td>{r.label}{r.hint && <div className="muted small">{r.hint}</div>}</td>
            <td style={{ width: '50%' }}><div className="bar"><span style={{ width: `${(100 * r.value) / max}%` }} /></div></td>
            <td className="num">{format(r.value)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

/** Vertical columns over time (days/weeks). */
export function Columns({ rows, format = (n: number) => n.toLocaleString('en-US') }: { rows: { label: string; value: number }[]; format?: (n: number) => string }) {
  const max = Math.max(1, ...rows.map((r) => r.value));
  return (
    <div className="columns" role="img" aria-label="Chart">
      {rows.map((r) => (
        <div key={r.label} className="col" title={`${r.label}: ${format(r.value)}`}>
          <span style={{ height: `${Math.max(2, (100 * r.value) / max)}%` }} />
          <small>{r.label}</small>
        </div>
      ))}
    </div>
  );
}

export function pct(n: number | null | undefined) {
  return n == null ? '—' : `${n}%`;
}
