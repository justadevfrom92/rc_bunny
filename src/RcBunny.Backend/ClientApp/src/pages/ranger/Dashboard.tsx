import { Link } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { ApprovalCard, TicketTable, type Approval, type Lists, type Row } from './shared';

interface Data {
  counts: { new: number; unassigned: number; open: number; solvedToday: number };
  overall: Approval; teams: { team: string; label: string; approval: Approval }[];
  open: Row[]; manager: boolean; lists: Lists;
  managerRequests: { id: number; number: string; subject: string; name: string; managerRequestedAt: string; requestedBy: string | null; managerRequestNote: string | null }[];
}

/** The day's numbers, approval for the last 30 days, manager requests, and every open ticket, most urgent first. */
export default function Dashboard() {
  const { data, error } = useLoad<Data>('/api/ranger/dashboard');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const c = data.counts;

  return (
    <>
      <h1>Ranger</h1>
      <div className="stats">
        <Link className="card" to="/ranger/tickets?view=active&status=new"><span className="muted">New</span><strong>{c.new}</strong></Link>
        <Link className="card" to="/ranger/tickets?view=unassigned"><span className="muted">Unassigned</span><strong>{c.unassigned}</strong></Link>
        <Link className="card" to="/ranger/tickets?view=active"><span className="muted">Open</span><strong>{c.open}</strong></Link>
        <Link className="card" to="/ranger/tickets?view=done"><span className="muted">Solved today</span><strong>{c.solvedToday}</strong></Link>
      </div>
      <h2>Approval · last 30 days</h2>
      <div className="stats">
        <ApprovalCard label="Overall" a={data.overall} />
        {data.teams.map((t) => <ApprovalCard key={t.team} label={t.label} a={t.approval} />)}
        <Link className="card" to="/ranger/satisfaction"><span className="muted">More</span><strong>→</strong><small className="muted">Satisfaction</small></Link>
      </div>

      {data.managerRequests.length > 0 && (
        <section className="card">
          <h2>Manager Requests</h2>
          <table>
            <thead><tr><th>Ticket</th><th>Asked by</th><th>Why</th><th>When</th></tr></thead>
            <tbody>
              {data.managerRequests.map((r) => (
                <tr key={r.id}>
                  <td><Link to={`/ranger/tickets/${r.id}`}><span className="mono">{r.number}</span> {r.subject}</Link><div className="muted">{r.name}</div></td>
                  <td>{r.requestedBy ?? '—'}</td><td>{r.managerRequestNote ?? <span className="muted">—</span>}</td><td className="nowrap">{dateTime(r.managerRequestedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}

      <section className="card">
        <h2>Open Tickets <span className="muted">· most urgent first</span></h2>
        <TicketTable rows={data.open} lists={data.lists} empty="Nothing open. Nice work!" />
      </section>
    </>
  );
}
