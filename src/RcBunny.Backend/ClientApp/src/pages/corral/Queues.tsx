import { Link, useParams } from 'react-router';
import { api, dateTime, useLoad } from '../../api';
import { useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const History = ({ rows }: { rows: any[] }) => (
  <section className="card"><h2>Recently fixed</h2>
    {rows.length === 0 ? <p className="muted">Nothing fixed yet.</p> : (
      <table>
        <thead><tr><th>Fixed</th><th>Account</th><th>What</th><th>By</th></tr></thead>
        <tbody>{rows.map((w) => <tr key={w.id}><td>{dateTime(w.resolvedAt)}</td><td><Link to={`/corral/customers/${w.customerId}`}>{w.customer}</Link> <span className="muted">#{w.account}</span></td><td>{w.summary}</td><td>{w.resolvedBy}</td></tr>)}</tbody>
      </table>
    )}
  </section>
);

/** Exceptions overview: every work queue with how many accounts are waiting. */
export function Queues() {
  const { data, error } = useLoad<any>('/api/corral/queues');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  return (
    <>
      <h1>Exceptions</h1>
      <div className="tiles">
        {data.queues.map((q: any) => (
          <Link key={q.key} to={`/corral/queues/${q.key}`} className="card tile"><h2>{q.name} <span className={q.count ? 'overdue' : 'muted'}>{q.count}</span></h2><p>{q.about}</p></Link>
        ))}
      </div>
      <History rows={data.history} />
    </>
  );
}

/** One queue: the accounts waiting in it, each with "Mark as fixed". */
export function Queue() {
  const { key } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/queues/${key}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  return (
    <>
      <p><Link to="/corral/queues">← Exceptions</Link></p>
      <h1>{data.name}</h1>
      <p className="muted">{data.about}</p>
      {bar}
      {data.items.length === 0 ? <p className="muted">Nothing waiting.</p> : (
        <table>
          <thead><tr><th>Since</th><th>Account</th><th>Status</th><th>Plan</th><th>What</th><th /></tr></thead>
          <tbody>{data.items.map((w: any) => (
            <tr key={w.id}><td>{dateTime(w.createdAt)}</td><td><Link to={`/corral/customers/${w.customerId}`}>{w.customer}</Link> <span className="muted">#{w.account}</span></td>
              <td>{w.status}</td><td>{w.plan ?? '—'}</td><td>{w.summary}</td><td><button onClick={() => run(api.post(`/api/corral/work-items/${w.id}/resolve`), 'Marked as fixed')}>Mark as fixed</button></td></tr>
          ))}</tbody>
        </table>
      )}
      <History rows={data.history} />
    </>
  );
}
