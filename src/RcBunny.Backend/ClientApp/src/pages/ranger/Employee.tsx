import { Link, useParams, useSearchParams } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { Pager } from '../../ui';
import { ApprovalCard, PriorityPill, StatusPill, type Approval, type Lists, type Row } from './shared';

interface Data {
  user: { id: number; name: string; email: string; role: string; company: string | null; active: boolean };
  agent: { id: number; team: string; level: string; active: boolean } | null;
  stats: { handled: number; owned: number; open: number; solved: number; replies: number; approval: Approval };
  page: number; total: number; tickets: Row[]; replies: Record<string, number>; notes: Record<string, number>; lists: Lists;
}

/** One employee: who they are and every ticket they handled (owned, replied to or wrote a note on). */
export default function Employee() {
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const { data, error } = useLoad<Data>(`/api/ranger/employees/${id}?${params}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { user: u, stats: s, lists: l } = data;

  return (
    <>
      <p><Link to="/ranger/employees">← Employees</Link></p>
      <h1>{u.name}</h1>
      <p className="muted">{u.role}{u.company && ` · ${u.company}`} · {u.email}
        {data.agent ? <> · <Link to={`/ranger/team/${data.agent.id}`}>{l.teams[data.agent.team]} {l.levels[data.agent.level]}</Link></> : ' · not on the ticket team'}</p>
      <div className="stats">
        <div className="card"><span className="muted">Handled</span><strong>{s.handled}</strong></div>
        <div className="card"><span className="muted">Owned</span><strong>{s.owned}</strong></div>
        <Link className="card" to={`/ranger/tickets?view=active&owner=${u.id}`}><span className="muted">Open</span><strong>{s.open}</strong></Link>
        <div className="card"><span className="muted">Solved · 90 days</span><strong>{s.solved}</strong></div>
        <div className="card"><span className="muted">Replies</span><strong>{s.replies}</strong></div>
        <ApprovalCard label="Approval · 90 days" a={s.approval} />
      </div>
      <section className="card">
        <h2>Tickets They Handled</h2>
        {data.tickets.length === 0 ? <p className="muted">None.</p> : (
          <table>
            <thead><tr><th>Ticket</th><th>Customer</th><th>Status</th><th>Priority</th><th>Owner</th><th className="num">Their replies</th><th className="num">Their notes</th><th>Last activity</th></tr></thead>
            <tbody>{data.tickets.map((t) => (
              <tr key={t.id}>
                <td><Link to={`/ranger/tickets/${t.id}`}><span className="mono">{t.number}</span> {t.subject}</Link></td><td>{t.name}</td>
                <td><StatusPill lists={l} value={t.status} /></td><td><PriorityPill lists={l} value={t.priority} /></td><td>{t.assignedTo ?? '—'}</td>
                <td className="num">{data.replies[t.id] ?? 0}</td><td className="num">{data.notes[t.id] ?? 0}</td><td className="nowrap">{dateTime(t.updatedAt)}</td>
              </tr>
            ))}</tbody>
          </table>
        )}
        <Pager page={data.page} total={data.total} size={10} onPage={(p) => setParams({ page: String(p) })} />
      </section>
    </>
  );
}
