import { Link } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { Pill } from '../../ui';
import type { Lists } from './shared';

interface Person {
  id: number; name: string; email: string; role: string; company: string | null; active: boolean;
  agent: { id: number; team: string; level: string; active: boolean } | null; handled: number; open: number; last: string | null;
}

/** Everyone who has handled a ticket (owned, replied or noted): the ticket team and anyone else. */
export default function Employees() {
  const { data, error } = useLoad<{ people: Person[]; lists: Lists }>('/api/ranger/employees');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const l = data.lists;
  return (
    <>
      <h1>Employees</h1>
      <p className="muted">Everyone who has owned, answered or written a note on a ticket.</p>
      <table>
        <thead><tr><th>Name</th><th>Role</th><th>Ticket team</th><th className="num">Handled</th><th className="num">Open</th><th>Last activity</th></tr></thead>
        <tbody>
          {data.people.map((p) => (
            <tr key={p.id} className={p.active ? '' : 'inactive'}>
              <td><Link to={`/ranger/employees/${p.id}`}>{p.name}</Link><div className="muted">{p.email}</div></td>
              <td>{p.role}{p.company && <div className="muted">{p.company}</div>}</td>
              <td>{p.agent ? <Link to={`/ranger/team/${p.agent.id}`}>{l.teams[p.agent.team]} · {l.levels[p.agent.level]}</Link> : <Pill>Not on the team</Pill>}</td>
              <td className="num">{p.handled}</td><td className="num">{p.open}</td><td className="nowrap">{p.last ? dateTime(p.last) : '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
