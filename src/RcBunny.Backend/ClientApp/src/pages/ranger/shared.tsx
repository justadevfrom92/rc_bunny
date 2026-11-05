import { type MouseEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { AccountNo, age } from '../../ui';

// Ranger's lists as the API sends them, and the small pieces every Ranger page uses (ebunny's backend/ranger/_*.blade.php).

export interface Lists {
  statuses: Record<string, { label: string; tone: string }>;
  priorities: Record<string, { label: string; tone: string }>;
  topics: Record<string, string>; channels: Record<string, string>; teams: Record<string, string>; levels: Record<string, string>; via: Record<string, string>;
}

export interface Approval { rated: number; good: number; pct: number | null }

export interface Row {
  id: number; number: string; subject: string; name: string; customerId: number | null; account: string | null; topic: string; channel: string;
  status: string; priority: string; assignedToId: number | null; assignedTo: string | null; createdAt: string; updatedAt: string; managerRequestedAt: string | null; rating: string | null;
}

export const StatusPill = ({ lists, value }: { lists: Lists; value: string }) => <span className={`pill ${lists.statuses[value]?.tone ?? ''}`}>{lists.statuses[value]?.label ?? value}</span>;
export const PriorityPill = ({ lists, value }: { lists: Lists; value: string }) => <span className={`pill ${lists.priorities[value]?.tone ?? ''}`}>{lists.priorities[value]?.label ?? value}</span>;
export const TeamPill = ({ lists, team }: { lists: Lists; team: string }) => <span className={`pill ${team === 'office' ? 'info' : 'warn'}`}>{lists.teams[team]}</span>;

const tone = (pct: number) => (pct >= 90 ? 'ok' : pct >= 75 ? 'warn' : 'bad');
const pctText = (pct: number) => `${Math.round(pct * 10) / 10}%`;

/** ebunny's ranger/_approval: the %, a green/red bar and the counts. */
export const ApprovalText = ({ a }: { a: Approval }) => a.pct === null ? <span className="muted">No ratings yet</span> : (
  <span className="ap" title={`${a.good} 👍 and ${a.rated - a.good} 👎 = ${a.pct}% approval`}>
    <b className={`approval ${tone(a.pct)}`}>{pctText(a.pct)}</b><span className="ap-split"><span style={{ width: `${a.pct}%` }} /></span><span className="ap-n">👍 {a.good} · 👎 {a.rated - a.good}</span>
  </span>
);

/** ebunny's ranger/_approval-card: a panel with the big %, the bar and what it's out of. */
export const ApprovalCard = ({ title, a, note }: { title: string; a: Approval; note?: string }) => (
  <div className="panel"><div className="panel-head"><h2>{title}</h2>{note && <span className="help">{note}</span>}</div><div className="panel-body ap-card">
    {a.pct === null ? <span className="muted">No ratings yet</span> : <>
      <b className={`approval ${tone(a.pct)}`}>{pctText(a.pct)}</b>
      <span className="ap-split" aria-hidden="true"><span style={{ width: `${a.pct}%` }} /></span>
      <div className="ap-legend"><span>👍 {a.good.toLocaleString()} helped</span><span>👎 {(a.rated - a.good).toLocaleString()} didn't</span></div>
      <span className="help">{a.rated.toLocaleString()} rated {a.rated === 1 ? 'ticket' : 'tickets'}</span>
    </>}
  </div></div>
);

/** ebunny's approval % with only the bar (By Team Member). */
export const ApprovalBar = ({ a }: { a: Approval }) => a.pct === null ? <span className="muted">—</span> : <><b className={`approval ${tone(a.pct)}`}>{pctText(a.pct)}</b> <span className="ap-split"><span style={{ width: `${a.pct}%` }} /></span></>;

/** ebunny's ranger/_row: one ticket in a list; the whole row opens it. */
export function TicketRow({ t, lists, compact }: { t: Row; lists: Lists; compact?: boolean }) {
  const navigate = useNavigate();
  const stop = (e: MouseEvent) => e.stopPropagation();
  return (
    <tr className="click" onClick={() => navigate(`/ranger/tickets/${t.id}`)}>
      <td className="nowrap"><Link className="mono" to={`/ranger/tickets/${t.id}`}>{t.number}</Link><div className="help">{lists.channels[t.channel] ?? ''} {t.channel} · {age(t.createdAt)} ago</div></td>
      <td className="wrap"><b>{t.subject}</b><div className="help">{lists.topics[t.topic] ?? t.topic}</div></td>
      <td>{t.name}{t.customerId && <div className="help" onClick={stop}><AccountNo id={t.customerId} account={t.account} /></div>}</td>
      <td><StatusPill lists={lists} value={t.status} />{t.priority !== 'normal' && <> <PriorityPill lists={lists} value={t.priority} /></>}</td>
      {!compact && <>
        <td onClick={stop}>{t.assignedToId ? <Link to={`/ranger/employees/${t.assignedToId}`}>{t.assignedTo}</Link> : '—'}</td>
        <td className="nowrap">{t.rating ? (t.rating === 'good' ? '👍' : '👎') : <span className="muted">—</span>}</td>
      </>}
    </tr>
  );
}

/** "30 days", "90 days", "1 year" chips (ebunny's chip-links). */
export const dayChoices = [30, 90, 365];
export const daysLabel = (d: number) => (d === 365 ? '1 year' : `${d} days`);
