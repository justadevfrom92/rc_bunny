import { Link } from 'react-router';
import { dateTime } from '../../api';
import { Pill } from '../../ui';

// Ranger's lists as the API sends them, and the small pieces every Ranger page uses.

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

export const StatusPill = ({ lists, value }: { lists: Lists; value: string }) => <Pill tone={lists.statuses[value]?.tone}>{lists.statuses[value]?.label ?? value}</Pill>;
export const PriorityPill = ({ lists, value }: { lists: Lists; value: string }) => <Pill tone={lists.priorities[value]?.tone}>{lists.priorities[value]?.label ?? value}</Pill>;

/** "92% (23 of 25)", or "No ratings" */
export const ApprovalText = ({ a }: { a: Approval }) =>
  a.pct === null ? <span className="muted">No ratings</span> : <><b>{a.pct}%</b> <span className="muted">({a.good} of {a.rated})</span></>;

/** A big number card for approval. */
export const ApprovalCard = ({ label, a }: { label: string; a: Approval }) => (
  <div className="card"><span className="muted">{label}</span><strong>{a.pct === null ? '—' : `${a.pct}%`}</strong><small className="muted">{a.rated} rated</small></div>
);

/** The ticket table used on the dashboard, the lists, a team member and an employee. */
export function TicketTable({ rows, lists, empty = 'No tickets here.' }: { rows: Row[]; lists: Lists; empty?: string }) {
  if (rows.length === 0) return <p className="muted">{empty}</p>;
  return (
    <div className="scroll">
      <table>
        <thead><tr><th>Ticket</th><th>Customer</th><th>Topic</th><th>Status</th><th>Priority</th><th>Owner</th><th>Opened</th><th>Last activity</th></tr></thead>
        <tbody>
          {rows.map((t) => (
            <tr key={t.id}>
              <td><Link to={`/ranger/tickets/${t.id}`}><span className="mono">{t.number}</span> {t.subject}</Link>
                {t.managerRequestedAt && <> <Pill tone="warn">Manager requested</Pill></>}</td>
              <td>{t.name}{t.account && <div className="muted">#{t.account}</div>}</td>
              <td>{lists.topics[t.topic] ?? t.topic}<div className="muted">{lists.channels[t.channel]} {t.channel}</div></td>
              <td><StatusPill lists={lists} value={t.status} /></td>
              <td><PriorityPill lists={lists} value={t.priority} /></td>
              <td>{t.assignedTo ?? <span className="muted">Unassigned</span>}</td>
              <td className="nowrap">{dateTime(t.createdAt)}</td>
              <td className="nowrap">{dateTime(t.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Approval for each of the last 12 weeks, as a simple bar row. */
export function WeekBars({ weeks }: { weeks: { week: string; approval: Approval }[] }) {
  return (
    <table>
      <thead><tr><th>Week of</th><th>Approval</th><th></th></tr></thead>
      <tbody>
        {weeks.map((w) => (
          <tr key={w.week}>
            <td className="nowrap">{new Date(w.week).toLocaleDateString('en-US')}</td>
            <td><ApprovalText a={w.approval} /></td>
            <td style={{ width: '50%' }}>{w.approval.pct !== null && <div className="bar"><span style={{ width: `${w.approval.pct}%` }} /></div>}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
