import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { api, useLoad } from '../../api';
import { PasswordInput } from '../../fields';
import { PageHead, useRun } from '../../ui';
import type { Lists } from './shared';

interface Data { people: { id: number; name: string; email: string; role: string; company: string | null; title: string | null }[]; manager: boolean; company: string | null; lists: Lists }

/** Ranger → Add a Team Member (ebunny's ranger/team-form): someone already in the admin, or a new person with a temporary password. */
export default function TeamAdd() {
  const { data, error } = useLoad<Data>('/api/ranger/team/add');
  const [run, bar] = useRun();
  const [who, setWho] = useState('existing');
  const [team, setTeam] = useState('office');
  const [level, setLevel] = useState('agent');
  const [find, setFind] = useState('');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const l = data.lists;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string | null) || null;
    run(api.post('/api/ranger/team', { who, userId: v('user_id') ? Number(v('user_id')) : null, name: v('name'), email: v('email'), password: v('password'), title: v('title'),
      team: data.company ? 'third_party' : team, company: v('company'), level }).then((r) => { navigate('/ranger/team'); return r; }));
  };
  const show = (w: string) => ({ hidden: who !== w });
  return (
    <>
      <PageHead title="Add a Team Member" sub="They can open Ranger and work tickets. Pick someone already in the admin, or add a new person." />
      {bar}
      <form className="panel" onSubmit={save}>
        <div className="panel-body form-grid">
          <label>Who</label><div className="chips small">
            <label><input type="radio" name="who" value="existing" checked={who === 'existing'} onChange={() => setWho('existing')} /><span>Someone already in the admin</span></label>
            <label><input type="radio" name="who" value="new" checked={who === 'new'} onChange={() => setWho('new')} /><span>A new person</span></label></div>

          <label {...show('existing')}>Person</label>
          <div {...show('existing')}><input type="search" className="search-box" placeholder="Find by name, company or role" aria-label="Find a person" style={{ marginBottom: 8 }} value={find} onChange={(e) => setFind(e.target.value)} />
            <div className="table-wrap pick-list" style={{ maxHeight: 260 }}><table className="table"><tbody>
              {data.people.map((p) => {
                const text = `${p.name} ${p.company ?? ''} ${p.role} ${p.title ?? ''}`.toLowerCase();
                return (
                  <tr key={p.id} data-filter-row={text} hidden={!!find && !text.includes(find.toLowerCase())}><td style={{ width: 32 }}><input type="radio" id={`p${p.id}`} name="user_id" value={p.id} /></td>
                    <td><label htmlFor={`p${p.id}`}><b>{p.name}</b></label> <span className="help">{p.role}{p.company ? ` · ${p.company}` : ''}</span></td></tr>
                );
              })}
              {data.people.length === 0 && <tr><td className="empty">Everyone in the admin is already on the team.</td></tr>}
            </tbody></table></div></div>

          <label {...show('new')} htmlFor="tm-name">Name</label><div {...show('new')}><input id="tm-name" name="name" maxLength={150} /></div>
          <label {...show('new')} htmlFor="tm-email">Email</label><div {...show('new')}><input id="tm-email" name="email" type="email" maxLength={200} /></div>
          <label {...show('new')} htmlFor="tm-pw">Temporary password</label><div {...show('new')}><PasswordInput id="tm-pw" name="password" minLength={12} autoComplete="new-password" /><div className="help">At least 12 characters. Share it with them securely.</div></div>
          <label {...show('new')} htmlFor="tm-title">Job title</label><input {...show('new')} id="tm-title" name="title" maxLength={100} placeholder="Support Agent" />

          {data.company ? <>
            <label>Company</label><div><b>{data.company}</b><div className="help">You add people to your own company's team (3rd Party).</div></div>
          </> : <>
            <label htmlFor="tm-team">Team</label><select id="tm-team" name="team" value={team} onChange={(e) => { setTeam(e.target.value); if (level === 'company_admin') setLevel('agent'); }}>{Object.entries(l.teams).map(([k, label]) => <option key={k} value={k}>{label}</option>)}</select>
            <label htmlFor="tm-company" hidden={!(who === 'new' && team === 'third_party')}>Company</label>
            <div hidden={!(who === 'new' && team === 'third_party')}><input id="tm-company" name="company" maxLength={120} placeholder="e.g. Brightline Contact Solutions" /><div className="help">The 3rd-party company they work for.</div></div>
          </>}
          <label htmlFor="tm-level">Level</label><div><select id="tm-level" name="level" value={level} onChange={(e) => setLevel(e.target.value)}><option value="agent">Agent</option>
            {data.manager && <option value="manager">Manager</option>}
            {data.manager && !data.company && team === 'third_party' && <option value="company_admin">Company Admin</option>}</select>
            <div className="help">Managers can take over a ticket, join in on one, and assign tickets to others.{data.manager ? '' : ' Only a manager can add a manager.'}{data.manager && !data.company && ' A company admin (3rd party only) also runs their company\'s team: adds and changes their people and sees their numbers.'}</div></div>
        </div>
        <div className="panel-body actions"><button className="btn cyan">Add to Team</button><Link className="btn ghost" to="/ranger/team">Cancel</Link></div>
      </form>
    </>
  );
}
