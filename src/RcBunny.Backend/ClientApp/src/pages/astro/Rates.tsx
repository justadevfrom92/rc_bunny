import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, useLoad } from '../../api';
import { DateInput, track } from '../../fields';
import { EmptyRow, PageHead, toast, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const today = () => new Date().toLocaleDateString('en-CA');

/** Astro → View Rates (ebunny's lando/rates/index): current energy charge, TDSP delivery and the average price. */
export default function Rates() {
  const [params, setParams] = useSearchParams();
  const { data, error } = useLoad<any>(`/api/astro/rates?${params}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const set = (k: string, v: string) => { const next = new URLSearchParams(params); if (v) next.set(k, v); else next.delete(k); setParams(next); };
  const search = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); set('plan', String(new FormData(e.currentTarget).get('plan') ?? '')); };
  return (
    <>
      <PageHead title="Search Rates" sub="Current energy charge plus TDSP delivery gives the average price customers see on the EFL." />
      <div className="search-layout">
        <aside className="search-side">
          <div className="panel"><div className="panel-head"><h2>Search Rates</h2></div><div className="panel-body">
            <form className="form-row" onSubmit={search} key={params.toString()}>
              <div className="field grow"><label htmlFor="plan">Plan</label><input id="plan" name="plan" defaultValue={params.get('plan') ?? ''} placeholder="Search plan name or code" /></div>
              <div className="field"><label htmlFor="market">Market</label><select id="market" name="market" value={params.get('market') ?? ''} onChange={(e) => set('market', e.target.value)}><option value="">-- All Markets --</option>
                {data.markets.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select></div>
              <div className="field"><label htmlFor="kwh">Usage</label><select id="kwh" name="kwh" value={data.kwh} onChange={(e) => set('kwh', e.target.value)}>{[500, 1000, 2000].map((k) => <option key={k} value={k}>{k.toLocaleString()} kWh</option>)}</select></div>
              <button className="btn">Search</button>
            </form>
          </div></div>
        </aside>
        <div className="search-main">
          <div className="panel"><div className="panel-head"><h2>Current Rates</h2><span className="muted">{data.rows.length} rates</span></div>
            <div className="table-wrap"><table>
              <thead><tr><th>Rate Class</th><th className="num">Term</th><th>Display Name</th><th>Internal</th><th>Market</th><th className="num">Energy ¢</th><th className="num">TDSP ¢</th><th className="num">Avg ¢ @ {data.kwh.toLocaleString()}</th><th>Effective</th></tr></thead>
              <tbody>
                {data.rows.map((r: any, i: number) => (
                  <tr key={i}><td>{r.type}</td><td className="num">{r.term}</td><td>{r.name}</td><td className="mono">{r.internal}</td><td>{r.market}</td>
                    <td className="num">{r.energy.toFixed(3)}</td><td className="num">{r.tdsp.toFixed(4)}</td><td className="num"><b>{r.avg.toFixed(1)}</b></td><td>{r.effective}</td></tr>
                ))}
                {data.rows.length === 0 && <EmptyRow cols={9}>No rates match.</EmptyRow>}
              </tbody>
            </table></div>
          </div>
        </div>
      </div>
    </>
  );
}

/** Astro → Update Rates (ebunny's lando/rates/edit): the grid of energy charges; changed cells are saved from the effective date. */
export function RatesEdit() {
  const [params] = useSearchParams();
  const { data, error, reload } = useLoad<any>(`/api/astro/rates/edit?${params}`);
  const [run, bar] = useRun(reload);
  const [adjusting, setAdjusting] = useState(false);
  const [version, setVersion] = useState(0);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const plan = params.get('plan');
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = e.currentTarget, rates: Record<string, Record<string, number>> = {};
    f.querySelectorAll<HTMLInputElement>('input[data-rate].dirty').forEach((i) => {
      if (i.value === '') return;
      (rates[i.dataset.plan!] ??= {})[i.dataset.market!] = Number(i.value);
    });
    run(api.put('/api/astro/rates', { effectiveOn: new FormData(f).get('effective_on'), rates }).then((r) => { setVersion(version + 1); return r; }));
  };
  const adjust = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const d = Number(new FormData(e.currentTarget).get('adj'));
    document.querySelectorAll<HTMLInputElement>('input[data-rate]').forEach((i) => {
      if (i.value === '') return;
      i.value = (Number(i.value) + d).toFixed(3);
      track({ currentTarget: i });
    });
    setAdjusting(false);
    toast('Adjusted. Review the highlighted cells, then Save Rates.');
  };
  return (
    <>
      <form onSubmit={save} style={{ display: 'flex', flexDirection: 'column', gap: 20 }} key={version}>
        <PageHead title={data.onePlan ? `Update Rates: ${data.onePlan}` : 'Update Rates'}
          sub={<>Energy charges in ¢/kWh by plan and market. Changed cells are highlighted; only those are saved.{' '}
            {plan ? <Link to="/astro/rates/edit">Show all plans</Link> : data.showAll ? <Link to="/astro/rates/edit">Hide inactive plans</Link> : <Link to="/astro/rates/edit?all=1">Show inactive plans</Link>}</>}
          actions={<><button type="button" className="btn ghost" onClick={() => setAdjusting(true)}>Adjust All…</button><button className="btn cyan">Save Rates</button></>} />
        {bar}
        <div className="panel">
          <div className="panel-body form-row">
            <div className="field"><label htmlFor="effective_on">Effective Date</label><DateInput id="effective_on" name="effective_on" required defaultValue={today()} /></div>
            <p className="help" style={{ margin: '0 0 6px' }}>The website switches to new rates on this date. Grid shows the newest scheduled rate.{data.pending > 0 && ` ${data.pending} rates are scheduled for a future date.`}</p>
          </div>
          <div className="table-wrap"><table data-page="0">
            <thead><tr><th>Plan</th><th>Internal</th>{data.markets.map((m: any) => <th key={m.id} className="num">{m.short}</th>)}</tr></thead>
            <tbody>{data.plans.map((p: any) => (
              <tr key={p.id}><td>{p.name}{!p.active && <> <span className="pill">inactive</span></>}</td><td className="mono">{p.internal}</td>
                {data.markets.map((m: any) => {
                  const r = data.rates[`${p.id}-${m.id}`];
                  return <td key={m.id} className="num"><input type="number" step="0.001" min={0} defaultValue={r != null ? r.toFixed(3) : ''} data-rate data-plan={p.id} data-market={m.id} onInput={track} aria-label={`${p.name} ${m.short}`} /></td>;
                })}
              </tr>
            ))}</tbody>
          </table></div>
        </div>
      </form>
      {adjusting && (
        <div className="modal-bg" onClick={(e) => e.target === e.currentTarget && setAdjusting(false)} onKeyDown={(e) => e.key === 'Escape' && setAdjusting(false)}>
          <form className="modal" role="dialog" aria-modal="true" onSubmit={adjust}>
            <h2>Adjust all rates</h2><p>Add an amount to every rate on this screen. Use a negative number to lower rates. Nothing is saved until you choose Save Rates.</p>
            <div className="field"><label htmlFor="adj">Change (¢/kWh)</label><input id="adj" name="adj" type="number" step="0.001" defaultValue="0.100" required autoFocus /></div>
            <div className="actions" style={{ justifyContent: 'flex-end', marginTop: 16 }}><button type="button" className="btn ghost" onClick={() => setAdjusting(false)}>Cancel</button><button className="btn cyan">Apply</button></div>
          </form>
        </div>
      )}
    </>
  );
}

/** Astro → TDSP Fees (ebunny's lando/fees): delivery charges; changing a value unlocks its effective date. */
export function Fees() {
  const { data, error, reload } = useLoad<any[]>('/api/astro/fees');
  const [run, bar] = useRun(reload);
  const [unlocked, setUnlocked] = useState<number[]>([]);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const unlock = (id: number) => !unlocked.includes(id) && setUnlocked([...unlocked, id]);
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.put('/api/astro/fees', data.map((m) => ({ marketId: m.id, perKwh: Number(f.get(`kwh-${m.id}`)), perBill: Number(f.get(`bill-${m.id}`)), effectiveOn: f.get(`eff-${m.id}`) || null })))
      .then((r) => { setUnlocked([]); navigate('/astro/fees'); return r; }));
  };
  return (
    <form onSubmit={save} style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHead title="TDSP Fees" sub="Utility delivery charges passed through on every bill. Changing a value unlocks its effective date." actions={<button className="btn cyan">Save Fees</button>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Market</th><th>Utility</th><th className="num">Per Kwh (¢)</th><th className="num">Per Bill ($)</th><th>Effective</th></tr></thead>
        <tbody>{data.map((m) => (
          <tr key={m.id}><td className="mono">{m.name}</td><td>{m.description}</td>
            <td className="num"><input type="number" step="0.0001" min={0} name={`kwh-${m.id}`} defaultValue={m.perKwh.toFixed(4)} onInput={(e) => { track(e); unlock(m.id); }} required /></td>
            <td className="num"><input type="number" step="0.01" min={0} name={`bill-${m.id}`} defaultValue={m.perBill.toFixed(2)} onInput={(e) => { track(e); unlock(m.id); }} required /></td>
            <td>{unlocked.includes(m.id)
              ? <DateInput id={`eff-${m.id}`} name={`eff-${m.id}`} defaultValue={today()} min={today()} aria-label={`Effective date for ${m.name}`} />
              : <DateInput defaultValue={m.effectiveOn ?? ''} disabled aria-label={`Effective date for ${m.name}`} />}</td></tr>
        ))}</tbody>
      </table></div></div>
      <p className="help">Sample values. Confirm against the current PUCT-approved tariffs before saving.</p>
    </form>
  );
}
