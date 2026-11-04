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
