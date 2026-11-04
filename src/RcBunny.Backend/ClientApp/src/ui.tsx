import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';

/** A coloured label: ok (green), warn (amber), bad (red), info (blue) or plain. */
export function Pill({ tone, children }: { tone?: string; children: ReactNode }) {
  return <span className={`pill ${tone ?? ''}`}>{children}</span>;
}

const statusTones: Record<string, string> = {
  Success: 'ok', Failed: 'bad', Reversed: 'warn', Pending: 'warn', Delivered: 'ok', Canceled: 'bad', Shipped: 'info', Placed: 'warn',
  Resolved: 'ok', Incomplete: 'warn', 'Follow-up Needed': 'info', Escalated: 'bad', High: 'bad', Medium: 'warn',
  current: 'ok', ended: '', 'not started': 'info', linked: 'ok', unlinked: 'warn', cancelled: 'bad', won: 'ok', lost: 'bad',
};

/** A pill coloured by what the word means (Success is green, Failed is red…). */
export const Status = ({ value }: { value: string | null | undefined }) => (value ? <Pill tone={statusTones[value]}>{value}</Pill> : <>—</>);

/** Previous / Next for a list that comes back in pages. */
export function Pager({ page, total, size, onPage }: { page: number; total: number; size: number; onPage: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / size));
  if (pages <= 1) return null;
  return (
    <div className="row">
      <button disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <span>Page {page} of {pages}</span>
      <button disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button>
    </div>
  );
}

/**
 * For buttons that change something: run(promise) shows the server's message (or error) and then reloads.
 * Returns [run, the message bar to show].
 */
export function useRun(reload?: () => void): [(action: Promise<unknown>, done?: string) => Promise<void>, ReactNode] {
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const run = (action: Promise<unknown>, done?: string) =>
    action.then(
      (result) => {
        const text = (result as { message?: string } | undefined)?.message ?? done;
        setMessage(text ? { ok: true, text } : null);
        reload?.();
      },
      (e: Error) => setMessage({ ok: false, text: e.message }),
    );
  const bar = message && <p className={message.ok ? 'notice-ok' : 'error'}>{message.text}</p>;
  return [run, bar];
}

/** The ID column: "#12" linking to the record's page. */
export const RecordLink = ({ type, id }: { type: string; id: number }) => <Link to={`/records/${type}/${id}`}><b>#{id}</b></Link>;

/** Minutes:seconds from seconds. */
export const duration = (sec: number) => `${Math.floor(sec / 60)}:${String(sec % 60).padStart(2, '0')}`;

// ---- ebunny's building blocks (backend/partials/*): the same markup and classes as ebunny's admin pages ----

/** The page title, a line under it, and buttons on the right (ebunny's partials/page-head). */
export function PageHead({ title, sub, actions }: { title: ReactNode; sub?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="page-head">
      <div>
        <h1>{title}</h1>
        {sub && <p className="muted">{sub}</p>}
      </div>
      {actions && <div className="actions">{actions}</div>}
    </div>
  );
}

/** A white panel with a heading row (title on the left, anything on the right). Set body to wrap the content in .panel-body. */
export function Panel({ title, right, body, children, id, className }: { title?: ReactNode; right?: ReactNode; body?: boolean; children?: ReactNode; id?: string; className?: string }) {
  return (
    <div className={`panel${className ? ' ' + className : ''}`} id={id}>
      {(title || right) && <div className="panel-head">{typeof title === 'string' ? <h2>{title}</h2> : title}{right}</div>}
      {body ? <div className="panel-body">{children}</div> : children}
    </div>
  );
}

/** A number tile (ebunny's .stat): label, big number, small note. alert makes the number red. */
export function Stat({ label, value, note, alert, onClick }: { label: ReactNode; value: ReactNode; note?: ReactNode; alert?: boolean; onClick?: () => void }) {
  return (
    <div className={`stat${alert ? ' alert' : ''}`} onClick={onClick} style={onClick ? { cursor: 'pointer' } : undefined}>
      <span>{label}</span><strong>{value}</strong>{note && <small>{note}</small>}
    </div>
  );
}

/** A labelled form field (ebunny's .field). */
export function Field({ label, htmlFor, grow, children }: { label: ReactNode; htmlFor?: string; grow?: boolean; children: ReactNode }) {
  return <div className={`field${grow ? ' grow' : ''}`}><label htmlFor={htmlFor}>{label}</label>{children}</div>;
}

/** An empty table row with ebunny's .empty text. */
export const EmptyRow = ({ cols, children }: { cols: number; children: ReactNode }) => <tr><td colSpan={cols} className="empty">{children}</td></tr>;

/** ebunny's pager: ‹ 1 2 3 … 9 › and "1–25 of 300". */
export function PagerBar({ page, total, size, onPage }: { page: number; total: number; size: number; onPage: (p: number) => void }) {
  const last = Math.max(1, Math.ceil(total / size));
  if (last <= 1) return null;
  const nums = [...new Set([1, last, ...Array.from({ length: 5 }, (_, i) => page - 2 + i)])].filter((n) => n >= 1 && n <= last).sort((a, b) => a - b);
  return (
    <nav className="pager" aria-label="Pages">
      <span>
        <button className="btn sm ghost" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Previous page">‹</button>{' '}
        {nums.map((n, i) => (
          <span key={n}>
            {i > 0 && n - nums[i - 1] > 1 && <span className="muted">… </span>}
            {n === page ? <span className="btn sm on" aria-current="page">{n}</span> : <button className="btn sm ghost" onClick={() => onPage(n)}>{n}</button>}{' '}
          </span>
        ))}
        <button className="btn sm ghost" disabled={page >= last} onClick={() => onPage(page + 1)} aria-label="Next page">›</button>
      </span>
      <span className="muted pager-of">{((page - 1) * size + 1).toLocaleString()}–{Math.min(page * size, total).toLocaleString()} of {total.toLocaleString()}</span>
    </nav>
  );
}
