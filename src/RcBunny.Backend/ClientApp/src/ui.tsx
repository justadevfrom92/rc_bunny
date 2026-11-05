import { useState, type CSSProperties, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router';

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
        setMessage(null);
        if (text) toast(text);
        reload?.();
      },
      (e: Error) => setMessage({ ok: false, text: e.message }),
    );
  // ebunny: a success message is a toast at the bottom (above); an error is a red .flash on the page
  const bar = message && !message.ok && <div className="flash bad" role="alert">{message.text}</div>;
  return [run, bar];
}

/** ebunny's toast(): a message that shows at the bottom of the screen for a few seconds. */
let toastTimer = 0;
export function toast(text: string) {
  let t = document.getElementById('toast');
  if (!t) { t = document.createElement('div'); t.className = 'toast'; t.id = 'toast'; t.setAttribute('role', 'status'); t.setAttribute('aria-live', 'polite'); document.body.appendChild(t); }
  t.textContent = text;
  t.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => t!.classList.remove('show'), 3200);
}

/** The ID column: "#12" linking to the record's page, in the app you're in (ebunny's /admin/{app}/records/{type}/{id}). */
export function RecordLink({ type, id }: { type: string; id: number }) {
  const app = useLocation().pathname.split('/')[1] || 'corral';
  return <Link to={`/${app}/records/${type}/${id}`}><b>#{id}</b></Link>;
}

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

/** ebunny's config corral.resolutions: how a call, email or text ended, and its pill colour. */
export const resolutions: Record<string, string> = { Resolved: 'ok', Incomplete: 'warn', 'Follow-up Needed': 'info', Escalated: 'bad', 'No Answer': '' };

/** ebunny's partials/resolution-pill: the resolution, or "Open". */
export const ResolutionPill = ({ value }: { value?: string | null }) =>
  value ? <span className={`pill ${resolutions[value] ?? ''}`}>{value}</span> : <span className="pill">Open</span>;

/** ebunny's corral/_resolution: a dropdown that saves as soon as it changes. */
export function ResolutionForm({ value, by, onSave }: { value?: string | null; by?: string | null; onSave: (resolution: string) => void }) {
  return (
    <form className="resolution-form" onSubmit={(e) => e.preventDefault()}>
      <select aria-label="Resolution" value={value ?? ''} onChange={(e) => e.target.value && onSave(e.target.value)}>
        {!value && <option value="">Pick a resolution</option>}
        {Object.keys(resolutions).map((r) => <option key={r}>{r}</option>)}
      </select>
      {by && <div className="help" style={{ marginTop: 6 }}>Set by {by}</div>}
    </form>
  );
}

/** ebunny's partials/account-link: the account number linking to its Corral account. */
export const AccountNo = ({ id, account }: { id?: number | null; account?: string | null }) =>
  id && account ? <Link className="mono" to={`/corral/customers/${id}`}>{account}</Link> : <>—</>;

/** ebunny's partials/phone-actions: the number with call and text icons. */
export function PhoneActions({ phone, account }: { phone?: string | null; account?: string | null }) {
  if (!phone) return <>—</>;
  return (
    <span className="phone-actions"><span>{phone}</span>
      <a className="phone-ico" href={`tel:${phone.replace(/[^\d+]/g, '')}`} aria-label={`Call ${phone}`} data-tip="Call">
        <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2z" /></svg></a>
      {account && <Link className="phone-ico" to={`/corral/sms/new?account=${account}`} aria-label={`Text ${phone}`} data-tip="Text">
        <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" /></svg></Link>}
    </span>
  );
}

/** ebunny's 'n/j g:i A' (no year): 10/26 9:04 PM */
export const shortTime = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');
/** ebunny's 'l, F j, Y g:i A': Monday, October 26, 2026 9:04 PM */
export const longTime = (s: string) => new Date(s).toLocaleString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(' at ', ' ');
/** ebunny's 'n/j/Y g:i:s A' */
export const secondsTime = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit', second: '2-digit' }).replace(',', '');

/** ebunny's Customer::statusTone(): the pill colour for an account status. */
export const statusTone = (status?: string | null) =>
  !status ? '' : status.startsWith('Good') ? 'ok' : status === 'Submitted' ? 'info' : status.startsWith('Pending') ? 'warn' : 'bad';

/** How long ago, the way Carbon's diffForHumans(null, true) says it: "3 days", "2 hours". */
export function age(from: string, to?: string | null) {
  const s = Math.max(0, ((to ? new Date(to) : new Date()).getTime() - new Date(from).getTime()) / 1000);
  const units: [number, string][] = [[31536000, 'year'], [2592000, 'month'], [604800, 'week'], [86400, 'day'], [3600, 'hour'], [60, 'minute'], [1, 'second']];
  for (const [n, u] of units) if (s >= n) { const k = Math.floor(s / n); return `${k} ${u}${k === 1 ? '' : 's'}`; }
  return '1 second';
}

/** ebunny's components/bar-chart: the amount on every bar, the label under it, and "label: amount" on hover. */
export function BarChart({ bars, unit = '', decimals = 0, height = 170 }: { bars: { label: string; value: number; tip?: string }[]; unit?: string; decimals?: number; height?: number }) {
  const top = Math.max(1, ...bars.map((b) => b.value));
  const fmt = (v: number) => v.toLocaleString('en-US', { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
  const step = Math.max(1, Math.ceil(bars.length / 16)); // thin out column labels when there are many bars
  const say = (b: { label: string; value: number; tip?: string }) => `${b.tip ?? b.label}: ${fmt(b.value)}${unit ? ' ' + unit : ''}`;
  return (
    <div className="bc" style={{ '--bc-n': Math.max(1, bars.length), '--bc-h': `${height}px` } as CSSProperties} role="img" aria-label={bars.map(say).join('; ')}>
      {bars.map((b, i) => (
        <div key={i} className="bc-col" tabIndex={0} data-tip={say(b)}>
          <span className="bc-val">{fmt(b.value)}</span>
          <span className="bc-track"><b style={{ height: `${Math.round((b.value / top) * 1000) / 10}%` }} /></span>
          <span className="bc-lab">{i % step === 0 ? b.label : ''}</span>
        </div>
      ))}
    </div>
  );
}
