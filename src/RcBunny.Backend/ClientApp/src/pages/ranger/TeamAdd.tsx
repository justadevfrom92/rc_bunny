import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { api, useLoad } from '../../api';
import { useRun } from '../../ui';
import type { Lists } from './shared';

interface Data { people: { id: number; name: string; email: string; role: string; company: string | null }[]; manager: boolean; company: string | null; lists: Lists }

/** Add someone already in the admin (they get Ranger), or a new person with a temporary password. */
export default function TeamAdd() {
  const { data, error } = useLoad<Data>('/api/ranger/team/add');
  const [run, bar] = useRun();
  const [who, setWho] = useState('existing');
  const [team, setTeam] = useState('office');
  const navigate = useNavigate();
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const l = data.lists;
  const thirdParty = data.company !== null || team === 'third_party';

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = Object.fromEntries(new FormData(e.currentTarget));
    run(api.post<{ message: string }>('/api/ranger/team', { ...f, who, userId: f.userId ? Number(f.userId) : null }).then((r) => { navigate('/ranger/team'); return r; }));
  };

  return (
    <>
      <p><Link to="/ranger/team">← Ticket Team</Link></p>
      <h1>Add to the Ticket Team</h1>
      {bar}
      <form className="card" onSubmit={save}>
        <div className="row">
          <label style={{ flexDirection: 'row', margin: 0 }}><input type="radio" checked={who === 'existing'} onChange={() => setWho('existing')} /> Someone already in the admin</label>
          <label style={{ flexDirection: 'row', margin: 0 }}><input type="radio" checked={who === 'new'} onChange={() => setWho('new')} /> A new person</label>
        </div>
        {who === 'existing' ? (
          data.people.length === 0 ? <p className="muted">Everyone{data.company && ` from ${data.company}`} is already on the team.</p> : (
            <label>Person<select name="userId" required>{data.people.map((p) => <option key={p.id} value={p.id}>{p.name} · {p.role}{p.company ? ` · ${p.company}` : ''}</option>)}</select></label>
          )
        ) : (
          <div className="row">
            <label>Name<input name="name" required maxLength={150} /></label>
            <label>Email<input name="email" type="email" required maxLength={200} /></label>
            <label>Temporary password<input name="password" type="text" required minLength={12} placeholder="At least 12 characters" /></label>
          </div>
        )}
        <div className="row">
          {data.company === null && <label>Team<select name="team" value={team} onChange={(e) => setTeam(e.target.value)}>{Object.entries(l.teams).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>}
          {who === 'new' && thirdParty && data.company === null && <label>Company<input name="company" required maxLength={120} placeholder="The 3rd-party company they work for" /></label>}
          <label>Level<select name="level" defaultValue="agent">
            <option value="agent">{l.levels.agent}</option>
            {data.manager && <option value="manager">{l.levels.manager}</option>}
            {data.manager && data.company === null && team === 'third_party' && <option value="company_admin">{l.levels.company_admin}</option>}
          </select></label>
        </div>
        <p className="muted">Managers can assign tickets, take them over and join in. A company admin manages their own company's people.</p>
        <button className="primary">Add</button>
      </form>
    </>
  );
}
