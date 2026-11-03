import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../../api';

interface Market { id: number; code: string; name: string; deliveryCentsPerKwh: number; deliveryPerMonth: number; zips: { id: number; from: number; to: number }[]; customers: number }

export default function Markets() {
  const { data, error, reload } = useLoad<Market[]>('/api/sheriff/markets');
  const [editing, setEditing] = useState<Market | 'new' | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const number = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
    const body = { code: f.get('code'), name: f.get('name'), deliveryCentsPerKwh: Number(f.get('perKwh')), deliveryPerMonth: Number(f.get('perMonth')), zipFrom: number('zipFrom'), zipTo: number('zipTo') };
    const call = editing === 'new' ? api.post('/api/sheriff/markets', body) : api.put(`/api/sheriff/markets/${(editing as Market).id}`, body);
    call.then(() => { setEditing(null); setProblem(null); reload(); }, (err: Error) => { setProblem(err.message); reload(); });
  };
  const m = editing === 'new' ? null : editing;

  return (
    <>
      <div className="row spread"><h1>Markets</h1><button onClick={() => setEditing('new')}>Add a market</button></div>
      <p className="muted">Each market is a utility area with its own delivery charges. Zip codes decide which market a customer is in.</p>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {editing && (
        <form key={m?.id ?? 'new'} className="card" onSubmit={save}>
          <div className="row">
            <label>Code<input name="code" defaultValue={m?.code} required disabled={!!m} /></label>
            <label>Name<input name="name" defaultValue={m?.name} required /></label>
          </div>
          <div className="row">
            <label>Delivery ¢ per kWh<input name="perKwh" type="number" step="0.0001" min="0" defaultValue={m?.deliveryCentsPerKwh ?? 0} required /></label>
            <label>Delivery $ per month<input name="perMonth" type="number" step="0.01" min="0" defaultValue={m?.deliveryPerMonth ?? 0} required /></label>
          </div>
          <div className="row">
            <label>Add zip codes from<input name="zipFrom" type="number" min="70000" max="79999" /></label>
            <label>to<input name="zipTo" type="number" min="70000" max="79999" /></label>
          </div>
          <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(null)}>Cancel</button></div>
        </form>
      )}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Market</th><th className="num">¢/kWh</th><th className="num">$/month</th><th>Zip codes</th><th className="num">Customers</th><th /></tr></thead>
          <tbody>
            {data.map((x) => (
              <tr key={x.id}>
                <td>{x.name} <span className="muted">{x.code}</span></td>
                <td className="num">{x.deliveryCentsPerKwh}</td><td className="num">{x.deliveryPerMonth.toFixed(2)}</td>
                <td>{x.zips.map((z) => (
                  <span key={z.id} className="tag">{z.from}–{z.to} <button className="link" title="Remove" onClick={() => api.del(`/api/sheriff/markets/zips/${z.id}`).then(reload)}>×</button></span>
                ))}</td>
                <td className="num">{x.customers}</td>
                <td><button onClick={() => setEditing(x)}>Edit</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
