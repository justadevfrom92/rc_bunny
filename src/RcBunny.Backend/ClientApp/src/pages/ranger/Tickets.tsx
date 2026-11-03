import { Link, useSearchParams } from 'react-router';
import { date, useLoad } from '../../api';

interface Row { id: number; subject: string; name: string; topic: string; status: string; priority: string; assignedTo: string | null; createdAt: string; updatedAt: string }

const statusLabels: Record<string, string> = { active: 'Active', new: 'New', open: 'Open', waiting: 'Waiting', solved: 'Solved', closed: 'Closed' };
const whoLabels: Record<string, string> = { all: 'Everyone', mine: 'Mine', unassigned: 'Unassigned' };

export default function Tickets() {
  const [params, setParams] = useSearchParams();
  const status = params.get('status') ?? 'active', who = params.get('who') ?? 'all';
  const { data, error } = useLoad<Row[]>(`/api/ranger/tickets?status=${status}&who=${who}`);

  return (
    <>
      <h1>Tickets</h1>
      <div className="row">
        {Object.entries(statusLabels).map(([k, label]) => <button key={k} className={k === status ? 'primary' : ''} onClick={() => setParams({ status: k, who })}>{label}</button>)}
      </div>
      <div className="row">
        {Object.entries(whoLabels).map(([k, label]) => <button key={k} className={k === who ? 'primary' : ''} onClick={() => setParams({ status, who: k })}>{label}</button>)}
      </div>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : data.length === 0 ? <p className="muted">No tickets here.</p> : (
        <table>
          <thead><tr><th>Ticket</th><th>From</th><th>Topic</th><th>Status</th><th>Assigned to</th><th>Updated</th></tr></thead>
          <tbody>
            {data.map((t) => (
              <tr key={t.id}>
                <td><Link to={`/ranger/tickets/${t.id}`}>#{t.id} {t.subject}</Link>{t.priority === 'high' && <span className="tag high"> high</span>}</td>
                <td>{t.name}</td><td>{t.topic}</td><td><span className="tag">{t.status}</span></td><td>{t.assignedTo ?? <span className="muted">nobody</span>}</td><td>{date(t.updatedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
