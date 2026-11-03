import { Link } from 'react-router';
import { date, useLoad } from '../../api';

interface DashboardData {
  byStatus: { status: string; count: number }[]; unassigned: number; mine: number; highPriority: number; approval: number | null;
  oldest: { id: number; subject: string; name: string; createdAt: string }[];
}

export default function Dashboard() {
  const { data, error } = useLoad<DashboardData>('/api/ranger/dashboard');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const count = (s: string) => data.byStatus.find((b) => b.status === s)?.count ?? 0;

  return (
    <>
      <h1>Ranger</h1>
      <div className="stats">
        <Link className="card" to="/ranger/tickets?who=unassigned"><span className="muted">Unassigned</span><strong>{data.unassigned}</strong></Link>
        <Link className="card" to="/ranger/tickets?who=mine"><span className="muted">Mine</span><strong>{data.mine}</strong></Link>
        <div className="card"><span className="muted">High priority</span><strong>{data.highPriority}</strong></div>
        <div className="card"><span className="muted">New</span><strong>{count('new')}</strong></div>
        <div className="card"><span className="muted">Open</span><strong>{count('open')}</strong></div>
        <div className="card"><span className="muted">Waiting on customer</span><strong>{count('waiting')}</strong></div>
        <Link className="card" to="/ranger/ratings"><span className="muted">Approval</span><strong>{data.approval === null ? '—' : `${data.approval}%`}</strong></Link>
      </div>
      <section className="card">
        <h2>Oldest open tickets</h2>
        <table>
          <tbody>{data.oldest.map((t) => <tr key={t.id}><td><Link to={`/ranger/tickets/${t.id}`}>#{t.id} {t.subject}</Link></td><td>{t.name}</td><td>{date(t.createdAt)}</td></tr>)}</tbody>
        </table>
      </section>
    </>
  );
}
