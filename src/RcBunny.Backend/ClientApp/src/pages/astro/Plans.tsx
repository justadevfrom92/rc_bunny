import { useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, useLoad } from '../../api';
import { slugify } from '../../fields';
import { PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Astro → View Plans (ebunny's lando/plans/index): active and inactive plans. */
export default function Plans() {
  const { data, error } = useLoad<any>('/api/astro/plans');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Plans" sub="Every plan the site can sell." actions={<Link className="btn" to="/astro/plans/create">Add a Plan</Link>} />
      {([['Active Plans', data.active], ['Inactive Plans', data.inactive]] as [string, any[]][]).map(([heading, list]) => (
        <div key={heading} className="panel"><div className="panel-head"><h2>{heading}</h2><span className="muted">{list.length}</span></div>
          <div className="table-wrap"><table>
            <thead><tr><th>Type</th><th className="num">Term</th><th>Display Name</th><th>Internal Name</th><th>Rolloff</th><th>ETF</th><th className="num">MRC</th><th className="num">Green</th><th /></tr></thead>
            <tbody>{list.map((p) => (
              <tr key={p.id}><td>{p.type}</td><td className="num">{p.term}</td><td><b>{p.name}</b></td><td className="mono">{p.internal}</td><td className="mono">{p.rolloff}</td>
                <td>{p.etf}</td><td className="num">{p.mrc ? `$${p.mrc.toFixed(2)}` : '-'}</td><td className="num">{p.green}%</td>
                <td><Link className="btn sm ghost" to={`/astro/plans/${p.id}/edit`}>Edit</Link> <Link className="btn sm ghost" to={`/astro/rates/edit?plan=${p.id}`}>Rates</Link>
                  {p.active && <> <Link className="btn sm ghost" to={`/astro/rates?plan=${p.internal}`}>Prices</Link></>}</td></tr>
            ))}</tbody>
          </table></div>
        </div>
      ))}
    </>
  );
}

/** Add a Plan / Editing a plan (ebunny's lando/plans/form). */
export function PlanForm() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/astro/plans/form${id ? `?id=${id}` : ''}`);
  const [run, bar] = useRun();
  const navigate = useNavigate();
  const slug = useRef<HTMLInputElement>(null);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const p = data.plan;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => String(f.get(k) ?? '');
    const body = { name: v('name'), internal: v('internal'), slug: v('slug'), active: v('active') === '1', type: v('type'), term: Number(v('term')), rolloff: v('rolloff'),
      etf: v('etf'), mrc: Number(v('mrc') || 0), green: Number(v('green') || 0), tags: v('tags'), perks: v('perks') };
    run((p.id ? api.put(`/api/astro/plans/${p.id}`, body) : api.post('/api/astro/plans', body)).then((r) => { navigate('/astro/plans'); return r; }));
  };
  return (
    <>
      <PageHead title={p.id ? `Editing ${p.name}` : 'Add a Plan'} sub={p.id ? `Plan ID ${p.id}` : undefined} />
      {bar}
      <form className="panel" onSubmit={save}><div className="panel-body">
        <div className="form-grid">
          <label htmlFor="name">Plan Name</label><input id="name" name="name" required defaultValue={p.name} onInput={p.id ? undefined : (e) => { if (slug.current) slug.current.value = slugify(e.currentTarget.value); }} />
          <label htmlFor="internal">Internal Name</label><input id="internal" name="internal" required className="mono" defaultValue={p.internal} />
          <label htmlFor="slug">URL Slug</label><input id="slug" name="slug" className="mono" defaultValue={p.slug} placeholder="plan-name-12" ref={slug} />
          <label htmlFor="active">Status</label>
          <select id="active" name="active" defaultValue={p.active ? '1' : '0'}><option value="1">Active</option><option value="0">Inactive</option></select>
          <label htmlFor="type">Rate Class</label>
          <select id="type" name="type" defaultValue={p.type}>{['Resi', 'Biz'].map((t) => <option key={t}>{t}</option>)}</select>
          <label htmlFor="term">Term (months)</label><input id="term" name="term" type="number" min={1} max={60} defaultValue={p.term} />
          <label htmlFor="rolloff">Rolloff Plan</label>
          <select id="rolloff" name="rolloff" defaultValue={p.rolloff}><option>None</option>{data.rolloffs.map((r: string) => <option key={r}>{r}</option>)}</select>
          <label htmlFor="etf">Early Termination Fee</label><input id="etf" name="etf" defaultValue={p.etf} />
          <label htmlFor="mrc">Monthly Base Charge</label><input id="mrc" name="mrc" type="number" step="0.01" min={0} defaultValue={p.mrc} />
          <label htmlFor="green">Renewable %</label><input id="green" name="green" type="number" min={0} max={100} defaultValue={p.green} />
          <label htmlFor="tags">Website Filters</label><input id="tags" name="tags" defaultValue={p.tags} placeholder="fixed, green, flex, month" />
          <label htmlFor="perks">Website Bullets</label><textarea id="perks" name="perks" style={{ minHeight: 100, fontFamily: 'inherit' }} placeholder="One per line" defaultValue={p.perks} />
        </div>
        <div className="actions" style={{ marginTop: 20 }}><button className="btn cyan">Save Plan</button><Link className="btn ghost" to="/astro/plans">Cancel</Link></div>
      </div></form>
    </>
  );
}

/** Astro → Plan Groups (ebunny's lando/groups): each group's plans, remove with ×, add from a dropdown. */
export function Groups() {
  const { data, error, reload } = useLoad<any>('/api/astro/groups');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Plan Groups" sub={'Groups control which plans each website section shows. "featured" is the home page; "resi" is the Plans page.'}
        actions={<Link className="btn cyan" to="/astro/groups/create">Add a Group</Link>} />
      {bar}
      <div className="grid-2">
        {data.groups.map((g: any) => (
          <div key={g.id} className="panel"><div className="panel-head"><h2>{g.name}</h2><span className="actions"><span className="mono muted">{g.slug}</span><Link className="btn sm ghost" to={`/astro/groups/${g.id}/edit`}>Edit</Link></span></div><div className="panel-body">
            <div className="actions">
              {g.plans.map((p: any) => (
                <span key={p.id} className="inline"><span className="pill info">{p.internal} <button className="link-btn" aria-label={`Remove ${p.internal}`} onClick={() => run(api.del(`/api/astro/groups/${g.id}/plans/${p.id}`))}>×</button></span></span>
              ))}
              {g.plans.length === 0 && <span className="muted">No plans in this group.</span>}
            </div>
            <form className="form-row" style={{ marginTop: 12 }} onSubmit={(e) => e.preventDefault()}>
              <select aria-label={`Add a plan to ${g.name}`} value="" onChange={(e) => e.target.value && run(api.post(`/api/astro/groups/${g.id}/plans`, { planId: Number(e.target.value) }))}>
                <option value="">Add a plan…</option>
                {data.plans.filter((p: any) => !g.plans.some((x: any) => x.id === p.id)).map((p: any) => <option key={p.id} value={p.id}>{p.name} ({p.internal})</option>)}
              </select>
            </form>
          </div></div>
        ))}
      </div>
    </>
  );
}

/** Add a Group / Edit Group (ebunny's lando/groups-form): name, slug and the plans in order. */
export function GroupForm() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/astro/groups/form${id ? `?id=${id}` : ''}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <GroupEditor data={data} />;
}

function GroupEditor({ data }: { data: any }) {
  const g = data.group;
  const [run, bar] = useRun();
  const navigate = useNavigate();
  const slug = useRef<HTMLInputElement>(null);
  // Chosen plans first, in their current order, then the rest
  const [order, setOrder] = useState<any[]>(() => [...data.chosen.map((cid: number) => data.plans.find((p: any) => p.id === cid)).filter(Boolean), ...data.plans.filter((p: any) => !data.chosen.includes(p.id))]);
  const [chosen, setChosen] = useState<number[]>(data.chosen);
  const move = (i: number, by: number) => { const j = i + by; if (j < 0 || j >= order.length) return; const next = [...order]; [next[i], next[j]] = [next[j], next[i]]; setOrder(next); };
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { name: f.get('name'), slug: f.get('slug'), plans: order.filter((p) => chosen.includes(p.id)).map((p) => p.id) };
    run((g.id ? api.put(`/api/astro/groups/${g.id}`, body) : api.post('/api/astro/groups', body)).then((r) => { navigate('/astro/groups'); return r; }));
  };
  return (
    <>
      <PageHead title={g.id ? `Edit Group: ${g.name}` : 'Add a Group'} sub="Check the plans this group shows. Plans appear on the website in the order listed here." />
      {bar}
      <form className="panel" onSubmit={save}><div className="panel-body">
        <div className="form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required defaultValue={g.name} onInput={g.id ? undefined : (e) => { if (slug.current) slug.current.value = slugify(e.currentTarget.value); }} />
          <label htmlFor="slug">Slug</label><input id="slug" name="slug" required className="mono" defaultValue={g.slug} ref={slug} />
        </div>
        <h3 style={{ marginTop: 20 }}>Plans</h3>
        <ol className="sortable">
          {order.map((p, i) => (
            <li key={p.id}><label className="check"><input type="checkbox" checked={chosen.includes(p.id)} onChange={(e) => setChosen(e.target.checked ? [...chosen, p.id] : chosen.filter((x) => x !== p.id))} /> {p.name} <span className="mono help">{p.internal}</span></label>
              <span className="actions"><button type="button" className="btn sm ghost" onClick={() => move(i, -1)} aria-label={`Move ${p.name} up`}>↑</button><button type="button" className="btn sm ghost" onClick={() => move(i, 1)} aria-label={`Move ${p.name} down`}>↓</button></span></li>
          ))}
        </ol>
        <div className="actions" style={{ marginTop: 20 }}><button className="btn cyan">Save Group</button><Link className="btn ghost" to="/astro/groups">Cancel</Link></div>
      </div></form>
    </>
  );
}
