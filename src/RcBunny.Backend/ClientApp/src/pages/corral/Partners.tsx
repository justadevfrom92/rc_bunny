import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../../api';

interface Partner { id: number; name: string; kind: string; contact: string | null; email: string | null; phone: string | null; commissionPercent: number; active: boolean; leads: number }

export default function Partners() {
  const { data, error, reload } = useLoad<Partner[]>('/api/crm/partners');
  const [editing, setEditing] = useState<Partner | 'new' | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { name: f.get('name'), kind: f.get('kind'), contact: f.get('contact'), email: f.get('email'), phone: f.get('phone'), commissionPercent: Number(f.get('commission') || 0), active: f.get('active') === 'on' };
    const call = editing === 'new' ? api.post('/api/crm/partners', body) : api.put(`/api/crm/partners/${(editing as Partner).id}`, body);
    call.then(() => { setEditing(null); setProblem(null); reload(); }, (err: Error) => setProblem(err.message));
  };
  const p = editing === 'new' ? null : editing;

  return (
    <>
      <div className="row spread"><h1>Partners & Brokers</h1><button onClick={() => setEditing('new')}>Add</button></div>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {editing && (
        <form key={p?.id ?? 'new'} className="card" onSubmit={save}>
          <div className="row">
            <label>Name<input name="name" defaultValue={p?.name} required /></label>
            <label>Kind<select name="kind" defaultValue={p?.kind ?? 'Broker'}><option>Broker</option><option>Partner</option></select></label>
            <label>Commission %<input name="commission" type="number" step="0.1" min="0" defaultValue={p?.commissionPercent ?? 0} /></label>
          </div>
          <div className="row">
            <label>Contact<input name="contact" defaultValue={p?.contact ?? ''} /></label>
            <label>Email<input name="email" type="email" defaultValue={p?.email ?? ''} /></label>
            <label>Phone<input name="phone" defaultValue={p?.phone ?? ''} /></label>
          </div>
          <label style={{ flexDirection: 'row' }}><input name="active" type="checkbox" defaultChecked={p?.active ?? true} /> Active</label>
          <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(null)}>Cancel</button></div>
        </form>
      )}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Name</th><th>Kind</th><th>Contact</th><th className="num">Commission</th><th className="num">Leads</th><th /></tr></thead>
          <tbody>
            {data.map((x) => (
              <tr key={x.id} className={x.active ? '' : 'inactive'}>
                <td>{x.name}</td><td>{x.kind}</td><td>{x.contact} {x.email && <a href={`mailto:${x.email}`}>{x.email}</a>} {x.phone}</td>
                <td className="num">{x.commissionPercent}%</td><td className="num">{x.leads}</td>
                <td><button onClick={() => setEditing(x)}>Edit</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
