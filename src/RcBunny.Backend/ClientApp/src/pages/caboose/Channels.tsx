import { type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, money, useLoad } from '../../api';
import { DateInput } from '../../fields';
import { PageHead, Panel, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const short = (d: string | null) => (d ? new Date(d + 'T00:00').toLocaleDateString('en-US', { month: 'numeric', day: 'numeric', year: '2-digit' }) : null);
const live = (p: any) => { const t = new Date().toISOString().slice(0, 10); return p.active && (!p.startsOn || p.startsOn <= t) && (!p.endsOn || p.endsOn >= t); };

/** Caboose → MSIDs & Promo Codes (ebunny's rodeo/channels). */
export default function Channels() {
  const { data, error } = useLoad<any>('/api/caboose/channels');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="MSIDs & Promo Codes" sub="Website links carry ?msid= and ?promo= (e.g. /checkout?msid=3001&promo=FALL25); orders keep them for reporting." />
      <div className="grid-2" style={{ alignItems: 'start' }}>
        <Panel title="MSIDs (Sales Channels)" right={<Link className="btn sm cyan" to="/caboose/channels/new">New MSID</Link>}>
          <div className="table-wrap"><table>
            <thead><tr><th>MSID</th><th>Channel</th><th>Type</th><th className="num">Orders</th><th>Last Order</th></tr></thead>
            <tbody>{data.msids.map((m: any) => (
              <tr key={m.id} className={`click${m.active ? '' : ' muted'}`} onClick={() => navigate(`/caboose/channels/${m.id}/edit`)}>
                <td className="mono"><Link to={`/caboose/channels/${m.id}/edit`}>{m.code}</Link></td><td>{m.name}</td><td>{m.channel}</td><td className="num">{m.customers}</td><td>—</td></tr>
            ))}</tbody>
          </table></div>
        </Panel>
        <Panel title="Promo Codes" right={<Link className="btn sm cyan" to="/caboose/promos/new">New Promo Code</Link>}>
          <div className="table-wrap"><table>
            <thead><tr><th>Code</th><th>Description</th><th className="num">Credit</th><th>Dates</th><th className="num">Uses</th><th>Status</th></tr></thead>
            <tbody>{data.promoCodes.map((p: any) => (
              <tr key={p.id} className="click" onClick={() => navigate(`/caboose/promos/${p.id}/edit`)}>
                <td className="mono"><Link to={`/caboose/promos/${p.id}/edit`}><b>{p.code}</b></Link></td><td>{p.description}</td><td className="num">{money(p.credit)}</td>
                <td className="help">{short(p.startsOn) ?? 'any'} – {short(p.endsOn) ?? 'open'}</td><td className="num">{p.customers}</td>
                <td><span className={`pill ${live(p) ? 'ok' : ''}`}>{live(p) ? 'Live' : 'Off'}</span></td></tr>
            ))}</tbody>
          </table></div>
        </Panel>
      </div>
    </>
  );
}

/** New MSID / Edit MSID (ebunny's rodeo/channel-form). */
export function ChannelForm() {
  const { id } = useParams();
  const { data } = useLoad<any>('/api/caboose/channels');
  const [run, bar] = useRun();
  const navigate = useNavigate();
  if (!data) return <div className="empty">Loading…</div>;
  const m = id ? data.msids.find((x: any) => x.id === Number(id)) : null;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), body = { code: m?.code ?? f.get('msid'), name: f.get('name'), channel: f.get('type'), active: f.get('active') === 'on' };
    run((m ? api.put(`/api/caboose/msids/${m.id}`, body) : api.post('/api/caboose/msids', body)).then((r) => { navigate('/caboose/channels'); return r; }));
  };
  return (
    <>
      <PageHead title={m ? `Edit MSID ${m.code}` : 'New MSID'} sub="A sales channel. Website links carry it as ?msid=." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="msid">MSID</label>{m ? <div><input value={m.code} disabled style={{ maxWidth: '12ch' }} /></div> : <input id="msid" name="msid" required inputMode="numeric" placeholder="3 to 8 digits" style={{ maxWidth: '12ch' }} />}
          <label htmlFor="name">Channel name</label><input id="name" name="name" required maxLength={100} defaultValue={m?.name} />
          <label htmlFor="type">Type</label><select id="type" name="type" defaultValue={m?.channel}>{['Organic', 'Paid', 'Partner', 'Broker', 'Agent', 'Web', 'Direct mail', 'Paid search'].map((t) => <option key={t}>{t}</option>)}</select>
          <span /><label className="check"><input type="checkbox" name="active" defaultChecked={m?.active ?? true} /> Active</label>
        </div>
        <div className="panel-body actions"><button className="btn cyan">{m ? 'Save MSID' : 'Add MSID'}</button><Link className="btn ghost" to="/caboose/channels">Cancel</Link></div>
      </form>
    </>
  );
}

/** New Promo Code / Edit (ebunny's rodeo/promo-form). */
export function PromoForm() {
  const { id } = useParams();
  const { data } = useLoad<any>('/api/caboose/channels');
  const [run, bar] = useRun();
  const navigate = useNavigate();
  if (!data) return <div className="empty">Loading…</div>;
  const p = id ? data.promoCodes.find((x: any) => x.id === Number(id)) : null;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { code: p?.code ?? String(f.get('code')).toUpperCase(), description: f.get('description'), credit: Number(f.get('credit') || 0), startsOn: f.get('starts_on') || null, endsOn: f.get('ends_on') || null, active: f.get('active') === 'on' };
    run((p ? api.put(`/api/caboose/promo-codes/${p.id}`, body) : api.post('/api/caboose/promo-codes', body)).then((r) => { navigate('/caboose/channels'); return r; }));
  };
  return (
    <>
      <PageHead title={p ? `Edit Promo Code ${p.code}` : 'New Promo Code'} sub="A bill credit customers get with ?promo= on a sign-up link." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="code">Code</label>{p ? <div><input value={p.code} disabled style={{ maxWidth: '16ch' }} /></div> : <input id="code" name="code" required placeholder="e.g. FALL25" style={{ maxWidth: '16ch', textTransform: 'uppercase' }} />}
          <label htmlFor="description">Description</label><input id="description" name="description" required maxLength={150} defaultValue={p?.description} />
          <label htmlFor="credit">Bill credit ($)</label><input id="credit" name="credit" type="number" step="0.01" min="0" max="1000" defaultValue={p?.credit} style={{ maxWidth: '12ch' }} />
          <label htmlFor="starts_on">Starts</label><DateInput id="starts_on" name="starts_on" defaultValue={p?.startsOn ?? ''} style={{ maxWidth: '20ch' }} />
          <label htmlFor="ends_on">Ends</label><DateInput id="ends_on" name="ends_on" defaultValue={p?.endsOn ?? ''} style={{ maxWidth: '20ch' }} />
          <span /><label className="check"><input type="checkbox" name="active" defaultChecked={p?.active ?? true} /> Active</label>
        </div>
        <div className="panel-body actions"><button className="btn cyan">{p ? 'Save Promo Code' : 'Add Promo Code'}</button><Link className="btn ghost" to="/caboose/channels">Cancel</Link></div>
      </form>
    </>
  );
}
