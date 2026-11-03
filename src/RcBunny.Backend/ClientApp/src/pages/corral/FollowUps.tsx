import { Link } from 'react-router';
import { api, date, useLoad } from '../../api';

interface FollowUp { id: number; leadId: number; lead: string; company: string | null; dueOn: string; task: string }

export default function FollowUps() {
  const { data, error, reload } = useLoad<FollowUp[]>('/api/crm/follow-ups');
  const today = new Date().toISOString().slice(0, 10);

  return (
    <>
      <h1>Follow-ups</h1>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : data.length === 0 ? <p className="muted">All caught up.</p> : (
        <table>
          <thead><tr><th>Due</th><th>Lead</th><th>Task</th><th /></tr></thead>
          <tbody>
            {data.map((f) => (
              <tr key={f.id}>
                <td className={f.dueOn < today ? 'overdue' : ''}>{date(f.dueOn)}{f.dueOn < today && ' (overdue)'}</td>
                <td><Link to={`/corral/crm/leads/${f.leadId}`}>{f.company ?? f.lead}</Link></td>
                <td>{f.task}</td>
                <td><button onClick={() => api.post(`/api/crm/follow-ups/${f.id}/done`).then(reload)}>Done</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
