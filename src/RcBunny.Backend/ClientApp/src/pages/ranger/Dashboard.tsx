import { Link, useNavigate } from 'react-router';
import { useLoad } from '../../api';
import { EmptyRow, PageHead, age } from '../../ui';
import { ApprovalCard, TicketRow, type Approval, type Lists, type Row } from './shared';

interface Data {
  counts: { new: number; unassigned: number; open: number; solvedToday: number };
  overall: Approval; teams: { team: string; label: string; approval: Approval }[];
  open: Row[]; manager: boolean; lists: Lists;
  managerRequests: { id: number; number: string; subject: string; name: string; managerRequestedAt: string; requestedBy: string | null; managerRequestNote: string | null }[];
}

/** Ranger's dashboard (ebunny's ranger/dashboard): the day's numbers, approval and manager requests for managers, and every open ticket. */
export default function Dashboard() {
  const { data, error } = useLoad<Data>('/api/ranger/dashboard');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.counts;
  return (
    <>
      <PageHead title="Ranger" sub="Support tickets customers open from My Account." actions={<Link className="btn cyan" to="/ranger/tickets?view=unassigned">Pick Up a Ticket</Link>} />
      <div className="grid-4 stat-row">
        <Link className="stat" to="/ranger/tickets?status=new"><span>New</span><strong>{c.new}</strong><small>not answered yet</small></Link>
        <Link className={`stat${c.unassigned ? ' alert' : ''}`} to="/ranger/tickets?view=unassigned"><span>Unassigned</span><strong>{c.unassigned}</strong><small>nobody has it</small></Link>
        <Link className="stat" to="/ranger/tickets"><span>Open</span><strong>{c.open}</strong><small>new, open or waiting</small></Link>
        <Link className="stat" to="/ranger/tickets?view=done"><span>Solved Today</span><strong>{c.solvedToday}</strong></Link>
      </div>
      {data.manager && <>
        <div className="grid-3">
          <ApprovalCard title="Approval — Everyone" a={data.overall} note="last 30 days" />
          {data.teams.map((t) => <ApprovalCard key={t.team} title={`Approval — ${t.label}`} a={t.approval} note="last 30 days" />)}
        </div>
        <div className="panel"><div className="panel-head"><h2>Manager Requests</h2><span className="muted">agents asking for a manager's help</span></div><div className="table-wrap"><table data-page="10">
          <thead><tr><th>Ticket</th><th>Subject</th><th>Asked By</th><th>Why</th><th>Asked</th></tr></thead>
          <tbody>
            {data.managerRequests.map((t) => (
              <tr key={t.id} className="click" onClick={() => navigate(`/ranger/tickets/${t.id}`)}><td><Link className="mono" to={`/ranger/tickets/${t.id}`}>{t.number}</Link></td><td className="wrap">{t.subject}</td>
                <td>{t.requestedBy ?? '—'}</td><td className="wrap">{t.managerRequestNote ?? '—'}</td><td className="nowrap">{age(t.managerRequestedAt)} ago</td></tr>
            ))}
            {data.managerRequests.length === 0 && <EmptyRow cols={5}>Nobody needs a manager right now.</EmptyRow>}
          </tbody>
        </table></div></div>
      </>}
      <div className="panel"><div className="panel-head"><h2>Open Tickets</h2><Link className="btn sm ghost" to="/ranger/tickets">All Tickets</Link></div><div className="table-wrap"><table data-page="10">
        <thead><tr><th>Ticket</th><th>Subject</th><th>Customer</th><th>Status</th><th>Owner</th><th>Rating</th></tr></thead>
        <tbody>
          {data.open.map((t) => <TicketRow key={t.id} t={t} lists={data.lists} />)}
          {data.open.length === 0 && <EmptyRow cols={6}>No open tickets. Nice.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}
