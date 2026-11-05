import { Link, useNavigate } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { EmptyRow, PageHead } from '../../ui';
import type { Lists } from './shared';

interface Person {
  id: number; name: string; email: string; title: string | null; role: string; company: string | null; active: boolean;
  agent: { id: number; team: string; level: string; active: boolean } | null; handled: number; open: number; last: string | null;
}

/** Ranger → Employees (ebunny's ranger/people): everyone who has handled a ticket. */
export default function Employees() {
  const { data, error } = useLoad<{ people: Person[]; lists: Lists }>('/api/ranger/employees');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const l = data.lists, n = data.people.length;
  return (
    <>
      <PageHead title="Employees" sub="Everyone who has owned, replied to or written a note on a ticket. Open someone to see every ticket they handled." />
      <div className="panel"><div className="panel-head"><h2>{n} {n === 1 ? 'employee' : 'employees'}</h2></div>
        <div className="table-wrap"><table data-page="10">
          <thead><tr><th>ID</th><th>Name</th><th>Role</th><th>Ticket Team</th><th className="num">Tickets Handled</th><th className="num">Open Now</th><th>Last Ticket Activity</th></tr></thead>
          <tbody>
            {data.people.map((p) => (
              <tr key={p.id} className="click" onClick={() => navigate(`/ranger/employees/${p.id}`)}><td className="nowrap"><Link to={`/ranger/employees/${p.id}`}><b>#{p.id}</b></Link></td>
                <td><b>{p.name}</b><div className="help">{p.title ?? p.email}</div></td>
                <td>{p.role ?? '—'}</td>
                <td>{p.agent ? `${l.teams[p.agent.team]} · ${l.levels[p.agent.level]}` : <span className="muted">Not on the team</span>}</td>
                <td className="num">{p.handled.toLocaleString()}</td><td className="num">{p.open.toLocaleString()}</td>
                <td className="nowrap">{p.last ? dateTime(p.last) : '—'}</td></tr>
            ))}
            {n === 0 && <EmptyRow cols={7}>Nobody has handled a ticket yet.</EmptyRow>}
          </tbody>
        </table></div>
      </div>
    </>
  );
}
