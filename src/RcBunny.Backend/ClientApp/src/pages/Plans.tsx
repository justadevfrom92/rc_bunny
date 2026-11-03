import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../api';

interface Market { id: number; name: string }
interface PlanRow { id: number; name: string; code: string; type: string; termMonths: number; active: boolean; rates: Record<string, number | null> }

export default function Plans() {
  const { data, error, reload } = useLoad<{ markets: Market[]; plans: PlanRow[] }>('/api/plans');
  const [editing, setEditing] = useState<PlanRow | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const toggle = (p: PlanRow) => api.post(`/api/plans/${p.id}/active?active=${!p.active}`).then(reload, (e: Error) => setProblem(e.message));

  const addRate = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    api.post(`/api/plans/${editing!.id}/rates`, {
      marketId: Number(form.get('market')), energyCents: Number(form.get('cents')), effectiveOn: form.get('effective'),
    }).then(() => { setEditing(null); setProblem(null); reload(); }, (err: Error) => setProblem(err.message));
  };

  return (
    <>
      <h1>Plans & Rates</h1>
      <p className="muted">Energy charge in cents per kWh, as of today. Delivery charges are added on the website.</p>
      {problem && <p className="error">{problem}</p>}

      {editing && (
        <form className="card" onSubmit={addRate}>
          <h2>New rate for {editing.name}</h2>
          <div className="row">
            <label>Market<select name="market">{data.markets.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}</select></label>
            <label>Cents per kWh<input name="cents" type="number" step="0.01" min="0.01" required /></label>
            <label>Starts on<input name="effective" type="date" defaultValue={new Date().toISOString().slice(0, 10)} required /></label>
          </div>
          <div className="row"><button className="primary">Save rate</button><button type="button" onClick={() => setEditing(null)}>Cancel</button></div>
        </form>
      )}

      <div className="scroll">
        <table>
          <thead>
            <tr><th>Plan</th><th>Type</th><th>Term</th>{data.markets.map((m) => <th key={m.id} className="num">{m.name}</th>)}<th /></tr>
          </thead>
          <tbody>
            {data.plans.map((p) => (
              <tr key={p.id} className={p.active ? '' : 'inactive'}>
                <td>{p.name} <span className="muted">{p.code}</span></td>
                <td>{p.type}</td>
                <td>{p.termMonths <= 1 ? 'Monthly' : `${p.termMonths} mo`}</td>
                {data.markets.map((m) => <td key={m.id} className="num">{p.rates[m.id]?.toFixed(2) ?? '—'}</td>)}
                <td className="row">
                  <button onClick={() => setEditing(p)}>New rate</button>
                  <button onClick={() => toggle(p)}>{p.active ? 'Hide' : 'Show'}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
