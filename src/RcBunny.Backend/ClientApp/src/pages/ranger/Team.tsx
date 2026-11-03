import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../../api';

interface Agent { id: number; userId: number; name: string; email: string; team: string; isManager: boolean; active: boolean; open: number; solved: number }

export default function Team() {
  const { data, error, reload } = useLoad<{ agents: Agent[]; notOnTeam: { id: number; name: string }[] }>('/api/ranger/team');
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); }, (e: Error) => setProblem(e.message));
  const update = (a: Agent, changes: Partial<Agent>) => {
    const next = { ...a, ...changes };
    run(api.put(`/api/ranger/team/${a.id}`, { userId: a.userId, team: next.team, isManager: next.isManager, active: next.active }));
  };
  const add = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/ranger/team', { userId: Number(f.get('userId')), team: f.get('team'), isManager: f.get('manager') === 'on', active: true }));
  };

  return (
    <>
      <h1>Ticket Team</h1>
      <p className="muted">Managers can assign tickets to anyone, take tickets over, and change the team.</p>
      {problem && <p className="error">{problem}</p>}
      {data.notOnTeam.length > 0 && (
        <form className="card row" onSubmit={add}>
          <strong>Add a team member</strong>
          <select name="userId">{data.notOnTeam.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}</select>
          <select name="team"><option>In-office</option><option>3rd party</option></select>
          <label style={{ flexDirection: 'row', margin: 0 }}><input name="manager" type="checkbox" /> Manager</label>
          <button className="primary">Add</button>
        </form>
      )}
      <table>
        <thead><tr><th>Name</th><th>Team</th><th>Manager</th><th>Active</th><th className="num">Open</th><th className="num">Solved</th></tr></thead>
        <tbody>
          {data.agents.map((a) => (
            <tr key={a.id} className={a.active ? '' : 'inactive'}>
              <td>{a.name} <span className="muted">{a.email}</span></td>
              <td><select value={a.team} onChange={(e) => update(a, { team: e.target.value })}><option>In-office</option><option>3rd party</option></select></td>
              <td><input type="checkbox" checked={a.isManager} onChange={(e) => update(a, { isManager: e.target.checked })} /></td>
              <td><input type="checkbox" checked={a.active} onChange={(e) => update(a, { active: e.target.checked })} /></td>
              <td className="num">{a.open}</td><td className="num">{a.solved}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
