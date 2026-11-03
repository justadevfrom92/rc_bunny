import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../../api';

interface Offer { id: number; name: string; description: string; stars: number; effect: string; value: string; active: boolean; position: number; redeemed: number }

const effectLabels: Record<string, string> = { credit: 'Bill credit ($)', gift: 'E-gift card (staff send it)', drawing: 'Monthly drawing entry', product: 'Product (staff send it)' };

/** What customers can spend stars on, in My Account. */
export default function Offers() {
  const { data, error, reload } = useLoad<{ effects: string[]; offers: Offer[] }>('/api/bounty/offers');
  const [editing, setEditing] = useState<Offer | 'new' | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { name: f.get('name'), description: f.get('description'), stars: Number(f.get('stars')), effect: f.get('effect'), value: f.get('value'), active: f.get('active') === 'on', position: Number(f.get('position') || 0) };
    const call = editing === 'new' ? api.post('/api/bounty/offers', body) : api.put(`/api/bounty/offers/${(editing as Offer).id}`, body);
    call.then(() => { setEditing(null); setProblem(null); reload(); }, (err: Error) => setProblem(err.message));
  };
  const o = editing === 'new' ? null : editing;

  return (
    <>
      <div className="row spread"><h1>Offers</h1><button onClick={() => setEditing('new')}>New offer</button></div>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {editing && data && (
        <form key={o?.id ?? 'new'} className="card" onSubmit={save}>
          <div className="row">
            <label>Name<input name="name" defaultValue={o?.name} required /></label>
            <label>Stars<input name="stars" type="number" min="1" defaultValue={o?.stars} required /></label>
            <label>Order<input name="position" type="number" defaultValue={o?.position ?? 0} /></label>
          </div>
          <div className="row">
            <label>What it does<select name="effect" defaultValue={o?.effect ?? 'credit'}>{data.effects.map((x) => <option key={x} value={x}>{effectLabels[x]}</option>)}</select></label>
            <label>Value (dollars for a credit, or what to send)<input name="value" defaultValue={o?.value} /></label>
          </div>
          <label>Description (customers see this)<input name="description" defaultValue={o?.description} /></label>
          <label style={{ flexDirection: 'row' }}><input name="active" type="checkbox" defaultChecked={o?.active ?? true} /> Offered in My Account</label>
          <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(null)}>Cancel</button></div>
        </form>
      )}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Offer</th><th className="num">Stars</th><th>Does</th><th className="num">Redeemed</th><th /></tr></thead>
          <tbody>
            {data.offers.map((x) => (
              <tr key={x.id} className={x.active ? '' : 'inactive'}>
                <td>{x.name}<div className="muted">{x.description}</div></td><td className="num">{x.stars.toLocaleString()}</td>
                <td>{effectLabels[x.effect]}{x.value && ` · ${x.effect === 'credit' ? `$${x.value}` : x.value}`}</td><td className="num">{x.redeemed}</td>
                <td><button onClick={() => setEditing(x)}>Edit</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
