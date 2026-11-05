import { useState } from 'react';
import { api, useLoad } from '../../api';
import { PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const effects: Record<string, string> = { credit: 'Bill credit', gift: 'Gift card', drawing: 'Drawing entry', product: 'Product' };

/** Bounty → Offers (ebunny's bounty/offers): every offer in one editable table; Save Offers saves them all. */
export default function Offers() {
  const { data, error, reload } = useLoad<any>('/api/bounty/offers');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <OffersForm offers={data.offers} reload={reload} key={JSON.stringify(data.offers)} />;
}

function OffersForm({ offers, reload }: { offers: any[]; reload: () => void }) {
  const [rows, setRows] = useState<any[]>(offers.map((o) => ({ ...o })));
  const [run, bar] = useRun(reload);
  const set = (i: number, k: string, v: unknown) => setRows(rows.map((r, j) => (j === i ? { ...r, [k]: v } : r)));
  const save = () => run(Promise.all(rows.map((r, i) => {
    const body = { name: r.name, description: r.description, stars: Number(r.stars), effect: r.effect, value: r.value ?? '', active: r.active, position: r.position ?? i };
    return r.id ? api.put(`/api/bounty/offers/${r.id}`, body) : api.post('/api/bounty/offers', body);
  })), 'Offers saved');
  return (
    <form style={{ display: 'flex', flexDirection: 'column', gap: 20 }} onSubmit={(e) => { e.preventDefault(); save(); }}>
      <PageHead title="Offers" sub="What customers can spend stars on. Corral (Spend Stars) and My Account (My Rewards) show the active offers."
        actions={<><button type="button" className="btn ghost" onClick={() => setRows([...rows, { name: '', description: '', stars: '', effect: 'credit', value: '', active: true, position: rows.length }])}>Add an Offer</button><button className="btn cyan">Save Offers</button></>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Offer</th><th>Description</th><th className="num">Stars</th><th>Effect</th><th>Value</th><th>Active</th></tr></thead>
        <tbody>{rows.map((o, i) => (
          <tr key={o.id ?? `new${i}`}>
            <td><input className="cell-input" value={o.name} required aria-label="Offer name" onChange={(e) => set(i, 'name', e.target.value)} /></td>
            <td><input className="cell-input" value={o.description} required aria-label="Description" onChange={(e) => set(i, 'description', e.target.value)} /></td>
            <td className="num"><input type="number" min="0" value={o.stars} required aria-label="Stars" style={{ width: '9ch' }} onChange={(e) => set(i, 'stars', e.target.value)} /></td>
            <td><select value={o.effect} aria-label="Effect" onChange={(e) => set(i, 'effect', e.target.value)}>{Object.entries(effects).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
            <td><input className="cell-input" value={o.value ?? ''} aria-label="Value" placeholder="$ or product" style={{ width: '12ch' }} onChange={(e) => set(i, 'value', e.target.value)} /></td>
            <td>{o.id ? <label className="check"><input type="checkbox" checked={o.active} onChange={(e) => set(i, 'active', e.target.checked)} /> Active</label> : 'New'}</td>
          </tr>
        ))}</tbody>
      </table></div></div>
      <p className="help">Value: the dollar amount for a bill credit, or the product name (e.g. ecobee) for a product. Bill credits go to Caboose → Credits &amp; Debits to be applied.</p>
    </form>
  );
}
