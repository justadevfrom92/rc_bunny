import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, PagerBar } from '../../ui';
import { StatusPill, type Approval, type Lists, type Row } from './shared';

interface Data {
  user: { id: number; name: string; email: string; title: string | null; role: string; company: string | null; active: boolean };
  agent: { id: number; team: string; level: string; active: boolean } | null;
  stats: { handled: number; owned: number; open: number; solved: number; replies: number; approval: Approval };
  page: number; total: number; tickets: Row[]; replies: Record<string, number>; notes: Record<string, number>; lists: Lists;
}

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/** One employee (ebunny's ranger/person): every ticket they owned, replied to or wrote a note on. */
export default function Employee() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<Data>(`/api/ranger/employees/${id}?page=${params.get('page') ?? 1}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const { user: u, agent, stats: s, lists: l } = data, ap = s.approval;
  return (
    <>
      <PageHead title={u.name} sub={[u.title, u.role, agent ? `${l.teams[agent.team]} ticket team · ${l.levels[agent.level]}` : 'Not on the ticket team'].filter(Boolean).join(' · ')}
        actions={<><Link className="btn ghost btn-back" to="/ranger/employees">Back to Employees</Link>
          {agent && <Link className="btn ghost" to={`/ranger/team/${agent.id}`}>Ticket Team Profile</Link>}
          <Link className="btn ghost" to={`/ranger/tickets?view=all&handledBy=${u.id}`}>Search Their Tickets</Link></>} />
      <div className="grid-4 stat-row">
        <div className="stat"><span>Tickets Handled</span><strong>{s.handled.toLocaleString()}</strong><small>owned, replied or noted</small></div>
        <Link className="stat" to={`/ranger/tickets?view=active&owner=${u.id}`}><span>Open Now</span><strong>{s.open.toLocaleString()}</strong><small>of {s.owned.toLocaleString()} owned</small></Link>
        <div className="stat"><span>Replies Sent</span><strong>{s.replies.toLocaleString()}</strong><small>to customers</small></div>
        <div className="stat"><span>Approval</span><strong>{ap.pct === null ? '—' : `${Math.round(ap.pct)}%`}</strong><small>{s.solved} solved · {ap.rated} rated, last 90 days</small></div>
      </div>
      <div className="panel"><div className="panel-head"><h2>Tickets Handled</h2><span className="muted">most recent activity first</span></div>
        <div className="table-wrap"><table>
          <thead><tr><th>Ticket</th><th>Opened</th><th>Subject</th><th>Customer</th><th>Status</th><th>Their Part</th><th>Last Activity</th></tr></thead>
          <tbody>
            {data.tickets.map((t) => {
              const r = data.replies[t.id] ?? 0, n = data.notes[t.id] ?? 0;
              const part = [t.assignedToId === u.id ? 'Owner' : null, r ? plural(r, 'reply', 'replies') : null, n ? plural(n, 'note', 'notes') : null].filter(Boolean).join(' · ');
              return (
                <tr key={t.id} className="click" onClick={() => navigate(`/ranger/tickets/${t.id}`)}><td className="nowrap"><Link className="mono" to={`/ranger/tickets/${t.id}`}><b>{t.number}</b></Link></td>
                  <td className="nowrap">{new Date(t.createdAt).toLocaleDateString('en-US')}</td>
                  <td className="wrap"><b>{t.subject}</b><div className="help">{l.topics[t.topic] ?? t.topic}</div></td>
                  <td>{t.name}{t.customerId && <div className="help" onClick={(e) => e.stopPropagation()}><AccountNo id={t.customerId} account={t.account} /></div>}</td>
                  <td><StatusPill lists={l} value={t.status} /></td>
                  <td>{part || '—'}{t.assignedToId && t.assignedToId !== u.id && <div className="help">owner: {t.assignedTo}</div>}</td>
                  <td className="nowrap">{dateTime(t.updatedAt ?? t.createdAt)}</td></tr>
              );
            })}
            {data.tickets.length === 0 && <EmptyRow cols={7}>No tickets yet.</EmptyRow>}
          </tbody>
        </table></div>
        <PagerBar page={data.page} total={data.total} size={10} onPage={(p) => navigate(`/ranger/employees/${u.id}?page=${p}`)} />
      </div>
    </>
  );
}
