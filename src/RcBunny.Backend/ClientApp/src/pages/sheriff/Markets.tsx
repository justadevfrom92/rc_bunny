import { type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, useLoad } from '../../api';
import { PageHead, Panel, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Sheriff → Markets (ebunny's sheriff/markets): utility service territories. */
export default function Markets() {
  const { data, error } = useLoad<any[]>('/api/sheriff/markets');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Markets" sub="Utility service territories (TDSPs)." actions={<Link className="btn cyan" to="/sheriff/markets/create">Add a Market</Link>} />
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th className="num">ID</th><th>Name</th><th>Short</th><th>Description</th><th>Region</th><th>Commodity</th><th>Utility Phone</th><th className="num">Zip Ranges</th><th>Status</th></tr></thead>
        <tbody>{data.map((m) => (
          <tr key={m.id} className="click" onClick={() => navigate(`/sheriff/markets/${m.id}/edit`)}>
            <td className="num">{m.id}</td><td className="mono"><Link to={`/sheriff/markets/${m.id}/edit`}><b>{m.code}</b></Link></td><td>{m.short}</td><td>{m.name}</td><td>{m.region}</td>
            <td>Electric</td><td>{m.phone}</td><td className="num">{m.zips.length}</td><td><span className="pill ok">Active</span></td></tr>
        ))}</tbody>
      </table></div></div>
    </>
  );
}

/** Add a Market / Edit Market (ebunny's sheriff/markets/form), with its delivery charges and zip ranges. */
export function MarketForm() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any[]>('/api/sheriff/markets');
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const m = id ? data.find((x) => x.id === Number(id)) : null;
  if (id && !m) return <div className="flash bad">That market doesn't exist.</div>;

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), num = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
    const body = { code: f.get('code') ?? m?.code, name: f.get('description'), short: f.get('short'), region: f.get('region'), phone: f.get('phone'),
      deliveryCentsPerKwh: Number(f.get('perKwh')), deliveryPerMonth: Number(f.get('perMonth')), zipFrom: num('zipFrom'), zipTo: num('zipTo') };
    run((m ? api.put(`/api/sheriff/markets/${m.id}`, body) : api.post('/api/sheriff/markets', body)).then((r) => { navigate('/sheriff/markets'); return r; }));
  };

  return (
    <>
      <PageHead title={m ? `Edit Market: ${m.code}` : 'Add a Market'}
        sub={m ? `${m.zips.length} zip ranges · ${m.customers} customers` : 'After adding a market, set its rates so the website can price plans there.'} />
      {bar}
      <form className="panel" onSubmit={save} key={m?.id ?? 'new'}><div className="panel-body">
        <div className="form-grid">
          <label htmlFor="code">Name</label><input id="code" name="code" required className="mono" defaultValue={m?.code} disabled={!!m} placeholder="TX-E-ONCOR" />
          <label htmlFor="short">Short Name</label><input id="short" name="short" required defaultValue={m?.short} />
          <label htmlFor="description">Description</label><input id="description" name="description" required defaultValue={m?.name} />
          <label htmlFor="region">Region</label><input id="region" name="region" required defaultValue={m?.region} placeholder="Column in Astro's term discount grid" />
          <label htmlFor="phone">Utility Phone</label><input id="phone" name="phone" defaultValue={m?.phone} />
          <label htmlFor="perKwh">Delivery ¢ per kWh</label><input id="perKwh" name="perKwh" type="number" step="0.0001" min="0" required defaultValue={m?.deliveryCentsPerKwh ?? 0} style={{ maxWidth: '14ch' }} />
          <label htmlFor="perMonth">Delivery $ per month</label><input id="perMonth" name="perMonth" type="number" step="0.01" min="0" required defaultValue={m?.deliveryPerMonth ?? 0} style={{ maxWidth: '14ch' }} />
          <label>Add zip codes</label><div className="form-row"><input name="zipFrom" type="number" min="70000" max="79999" placeholder="From" aria-label="Zip from" style={{ maxWidth: '12ch' }} /><input name="zipTo" type="number" min="70000" max="79999" placeholder="To" aria-label="Zip to" style={{ maxWidth: '12ch' }} /></div>
        </div>
        <div className="actions" style={{ marginTop: 20 }}><button className="btn cyan">Save Market</button><Link className="btn ghost" to="/sheriff/markets">Cancel</Link></div>
      </div></form>
      {m && (
        <Panel title="Zip Ranges" right={<span className="muted">{m.zips.length}</span>}>
          <div className="table-wrap"><table><tbody>
            {m.zips.map((z: any) => <tr key={z.id}><td className="mono">{z.from} – {z.to}</td><td className="actions"><button className="btn sm ghost" onClick={() => run(api.del(`/api/sheriff/markets/zips/${z.id}`), 'Zip range removed')}>Remove</button></td></tr>)}
            {m.zips.length === 0 && <tr><td className="empty">No zip ranges yet.</td></tr>}
          </tbody></table></div>
        </Panel>
      )}
    </>
  );
}
