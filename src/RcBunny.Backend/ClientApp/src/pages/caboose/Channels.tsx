import { useState, type FormEvent } from 'react';
import { api, date, money, useLoad } from '../../api';

interface MsidRow { id: number; code: string; name: string; channel: string; active: boolean; customers: number }
interface Promo { id: number; code: string; description: string; credit: number; startsOn: string | null; endsOn: string | null; active: boolean; customers: number }

/** MSIDs say where a customer came from; promo codes give a bill credit. Both show how many customers used them. */
export default function Channels() {
  const { data, error, reload } = useLoad<{ msids: MsidRow[]; promoCodes: Promo[] }>('/api/caboose/channels');
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const run = (action: Promise<unknown>, form?: HTMLFormElement) => action.then(() => { setProblem(null); form?.reset(); reload(); }, (e: Error) => setProblem(e.message));
  const addMsid = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/caboose/msids', { code: f.get('code'), name: f.get('name'), channel: f.get('channel'), active: true }), e.currentTarget);
  };
  const addPromo = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/caboose/promo-codes', { code: f.get('code'), description: f.get('description'), credit: Number(f.get('credit') || 0), startsOn: f.get('startsOn') || null, endsOn: f.get('endsOn') || null, active: true }), e.currentTarget);
  };
  const toggleMsid = (m: MsidRow) => run(api.put(`/api/caboose/msids/${m.id}`, { ...m, active: !m.active }));
  const togglePromo = (p: Promo) => run(api.put(`/api/caboose/promo-codes/${p.id}`, { ...p, active: !p.active }));

  return (
    <>
      <h1>MSIDs & Promo Codes</h1>
      {problem && <p className="error">{problem}</p>}
      <section className="card">
        <h2>MSIDs (marketing sources)</h2>
        <form className="row" onSubmit={addMsid}>
          <input name="code" placeholder="Code" required style={{ maxWidth: 120 }} />
          <input name="name" placeholder="Name" required />
          <select name="channel"><option>Web</option><option>Direct mail</option><option>Broker</option><option>Paid search</option><option>Partner</option><option>Event</option></select>
          <button className="primary">Add</button>
        </form>
        <table>
          <thead><tr><th>Code</th><th>Name</th><th>Channel</th><th className="num">Customers</th><th>Active</th></tr></thead>
          <tbody>{data.msids.map((m) => <tr key={m.id} className={m.active ? '' : 'inactive'}><td>{m.code}</td><td>{m.name}</td><td>{m.channel}</td><td className="num">{m.customers}</td><td><input type="checkbox" checked={m.active} onChange={() => toggleMsid(m)} /></td></tr>)}</tbody>
        </table>
      </section>
      <section className="card">
        <h2>Promo codes</h2>
        <form className="row" onSubmit={addPromo}>
          <input name="code" placeholder="Code" required style={{ maxWidth: 140 }} />
          <input name="description" placeholder="Description" required />
          <input name="credit" type="number" step="0.01" min="0" placeholder="Credit $" style={{ maxWidth: 110 }} />
          <input name="startsOn" type="date" title="Starts" style={{ maxWidth: 160 }} />
          <input name="endsOn" type="date" title="Ends" style={{ maxWidth: 160 }} />
          <button className="primary">Add</button>
        </form>
        <table>
          <thead><tr><th>Code</th><th>Description</th><th className="num">Credit</th><th>Dates</th><th className="num">Customers</th><th>Active</th></tr></thead>
          <tbody>
            {data.promoCodes.map((p) => (
              <tr key={p.id} className={p.active ? '' : 'inactive'}>
                <td>{p.code}</td><td>{p.description}</td><td className="num">{money(p.credit)}</td>
                <td>{p.startsOn ? date(p.startsOn) : 'Any time'} – {p.endsOn ? date(p.endsOn) : 'no end'}</td>
                <td className="num">{p.customers}</td><td><input type="checkbox" checked={p.active} onChange={() => togglePromo(p)} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}
