import { type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, useLoad } from '../../api';
import { track } from '../../fields';
import { PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const n2 = (v: number | null | undefined) => (v == null ? '' : Number(v).toFixed(2));

/** Astro → Term (ebunny's astro/terms): ¢/kWh discount by term and region. */
export function Terms() {
  const { data, error, reload } = useLoad<any>('/api/astro/terms');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const d: Record<string, Record<string, number>> = {};
    e.currentTarget.querySelectorAll<HTMLInputElement>('input[data-term]').forEach((i) => { (d[i.dataset.term!] ??= {})[i.dataset.region!] = Number(i.value || 0); });
    run(api.put('/api/astro/terms', d));
  };
  return (
    <form onSubmit={save} style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHead title="Term & Discounts" sub="Values are in ¢/kWh. A positive value lowers the rate; a negative value raises it. 1.0 is a one-cent discount." actions={<button className="btn cyan">Save Modifiers</button>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table data-page="0">
        <thead><tr><th className="num">Term</th>{data.regions.map((r: string) => <th key={r} className="num">{r}</th>)}</tr></thead>
        <tbody>{data.grid.map((row: any) => (
          <tr key={row.term}><td className="num"><b>{row.term}</b></td>
            {data.regions.map((r: string) => {
              const v = Number(row.values[r] ?? 0);
              return <td key={r} className="num"><input type="number" step="0.01" defaultValue={v.toFixed(2)} className={v < 0 ? 'neg' : undefined} data-term={row.term} data-region={r} onInput={track} aria-label={`Term ${row.term} ${r}`} /></td>;
            })}</tr>
        ))}</tbody>
      </table></div></div>
    </form>
  );
}

/** Astro → ETF (ebunny's astro/etfs): early termination fee by term. */
export function Etfs() {
  const { data, error, reload } = useLoad<any[]>('/api/astro/etfs');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.put('/api/astro/etfs', Object.fromEntries(data.map((x) => [x.term, Number(f.get(`etf-${x.term}`) || 0)]))));
  };
  return (
    <form onSubmit={save} style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHead title="ETF by Term" sub="Early termination fee charged when a customer leaves a fixed plan early. The website's plan builder shows these." actions={<button className="btn cyan">Save ETFs</button>} />
      {bar}
      <div className="panel" style={{ maxWidth: 520 }}><div className="table-wrap"><table data-page="0">
        <thead><tr><th className="num">Term (months)</th><th className="num">ETF ($)</th></tr></thead>
        <tbody>{data.map((x) => (
          <tr key={x.term}><td className="num">{x.term}</td><td className="num"><input type="number" step="1" min={0} name={`etf-${x.term}`} defaultValue={x.amount} onInput={track} aria-label={`ETF for ${x.term} months`} /></td></tr>
        ))}</tbody>
      </table></div></div>
    </form>
  );
}

/** Astro → Build Your Own (ebunny's astro/products): add-on prices and whether the website shows them. */
export function Products() {
  const { data, error, reload } = useLoad<any>('/api/astro/products');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.put('/api/astro/products', Object.fromEntries(data.products.map((p: any) => [p.id, { rateAdj: Number(f.get(`adj-${p.id}`) || 0), monthly: Number(f.get(`monthly-${p.id}`) || 0), active: f.get(`active-${p.id}`) === 'on' }]))));
  };
  return (
    <form onSubmit={save} style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHead title="BYOP Products" sub="Add-ons offered in the website's Build Your Own Plan. Prices here are what the website shows."
        actions={<><Link className="btn ghost" to="/astro/products/new">New Product</Link><button className="btn cyan">Save Products</button></>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table data-page="0">
        <thead><tr><th>Product</th><th>Model</th><th>Step</th><th>Type</th><th className="num">Rate change ¢/kWh</th><th className="num">Monthly $</th><th>Show on Website</th></tr></thead>
        <tbody>{data.products.map((p: any) => (
          <tr key={p.id}><td><Link to={`/astro/products/${p.id}/edit`}><b>{p.name}</b></Link>{p.description && <div className="help">{p.description.length > 60 ? p.description.slice(0, 57) + '...' : p.description}</div>}</td>
            <td className="mono">{p.model}</td><td>{p.step}</td><td>{p.type}</td>
            <td className="num"><input type="number" step="0.01" name={`adj-${p.id}`} defaultValue={n2(p.rateAdj)} className={p.rateAdj < 0 ? 'neg' : undefined} onInput={track} aria-label={`${p.name} rate change`} /></td>
            <td className="num"><input type="number" step="0.01" min={0} name={`monthly-${p.id}`} defaultValue={n2(p.monthly)} onInput={track} aria-label={`${p.name} monthly price`} /></td>
            <td><label className="check"><input type="checkbox" name={`active-${p.id}`} defaultChecked={p.active} onInput={track} /> <span data-label data-on="Shown" data-off="Hidden">{p.active ? 'Shown' : 'Hidden'}</span></label></td></tr>
        ))}</tbody>
      </table></div></div>
      <p className="help">A negative rate change is a discount. Discount limits are in Modifiers → <Link to="/astro/modifiers/products">Products</Link>. Hidden products disappear from the website's Build Your Own Plan page.</p>
    </form>
  );
}

/** New Product / edit (ebunny's astro/product-form): a Build Your Own Plan add-on. */
export function ProductForm() {
  const { id } = useParams();
  const { data: list } = useLoad<any>('/api/astro/products');
  const { data: one, error } = useLoad<any>(id ? `/api/astro/products/${id}` : null);
  const [run, bar] = useRun();
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!list || (id && !one)) return <div className="empty">Loading…</div>;
  const p = one ?? { name: '', description: '', key: '', step: list.steps[0], type: list.types[0], rateAdj: 0, monthly: 0, active: true };
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => String(f.get(k) ?? '');
    const body = { name: v('name'), description: v('description'), key: v('key'), step: v('step'), type: v('type'), rateAdj: Number(v('rate_adj')), monthly: Number(v('monthly')), active: f.get('active') === 'on' };
    run((id ? api.put(`/api/astro/products/${id}`, body) : api.post('/api/astro/products', body)).then((r) => { navigate('/astro/products'); return r; }));
  };
  return (
    <>
      <PageHead title={id ? p.name : 'New Product'} sub="An add-on on the website's Build Your Own Plan page. It shows on its step's tab with the name and line below."
        actions={<Link className="btn ghost btn-back" to="/astro/products">Back to Build Your Own</Link>} />
      {bar}
      <form className="panel" onSubmit={save}>
        <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={100} defaultValue={p.name} placeholder="e.g. Home Energy Audit" />
          <label htmlFor="description">Line under the name</label><input id="description" name="description" maxLength={300} defaultValue={p.description ?? ''} placeholder="One sentence about what the customer gets" />
          <label htmlFor="key">Key</label><div><input id="key" name="key" maxLength={40} defaultValue={p.key} placeholder="made from the name if left empty" disabled={!!id} />
            <div className="help">Used by the website and orders. It can't change after the product is made.</div></div>
          <label htmlFor="step">Step on the website</label><select id="step" name="step" defaultValue={p.step}>{list.steps.map((s: string) => <option key={s}>{s}</option>)}</select>
          <label htmlFor="type">Type</label><select id="type" name="type" defaultValue={p.type}>{list.types.map((t: string) => <option key={t}>{t}</option>)}</select>
          <label htmlFor="rate_adj">Rate change (¢/kWh)</label><div><input id="rate_adj" type="number" step="0.01" min={-10} max={10} name="rate_adj" required defaultValue={n2(p.rateAdj)} />
            <div className="help">Negative is a discount.</div></div>
          <label htmlFor="monthly">Monthly price ($)</label><input id="monthly" type="number" step="0.01" min={0} max={500} name="monthly" required defaultValue={n2(p.monthly)} />
          <label htmlFor="active">Website</label><label className="check"><input id="active" type="checkbox" name="active" defaultChecked={p.active} /> Show on the Build Your Own Plan page</label>
        </div>
        <div className="panel-body"><button className="btn cyan">{id ? 'Save Product' : 'Add Product'}</button></div>
      </form>
    </>
  );
}

/** Astro → Modifiers → Products (ebunny's astro/modifier-products): how far each add-on can be discounted. */
export function DiscountLimits() {
  const { data, error, reload } = useLoad<any>('/api/astro/products');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const num = (v: FormDataEntryValue | null) => (v === null || v === '' ? null : Number(v));
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.put('/api/astro/modifiers/products', Object.fromEntries(data.products.map((p: any) => [p.id, { minDiscount: num(f.get(`min-${p.id}`)), maxDiscount: num(f.get(`max-${p.id}`)) }]))));
  };
  return (
    <form onSubmit={save} style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHead title="Products" sub="How far each Build Your Own Plan add-on can be discounted, in ¢/kWh. Leave blank for no limit." actions={<button className="btn cyan">Save</button>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table data-page="0">
        <thead><tr><th>Product</th><th className="num">Current Rate Change</th><th className="num">Min Discount</th><th className="num">Max Discount</th></tr></thead>
        <tbody>{data.products.map((p: any) => (
          <tr key={p.id}><td><b>{p.name}</b></td><td className="num">{n2(p.rateAdj)}</td>
            <td className="num"><input type="number" step="0.01" name={`min-${p.id}`} defaultValue={n2(p.minDiscount)} onInput={track} aria-label={`${p.name} min discount`} placeholder="—" /></td>
            <td className="num"><input type="number" step="0.01" name={`max-${p.id}`} defaultValue={n2(p.maxDiscount)} onInput={track} aria-label={`${p.name} max discount`} placeholder="—" /></td></tr>
        ))}</tbody>
      </table></div></div>
      <p className="help">Discounts are negative: Min Discount is the smallest cut allowed, Max Discount the largest (e.g. 0 and -0.50). To change many at once use Upload → <Link to="/astro/uploads/byop">BYOP Discounts</Link>.</p>
    </form>
  );
}

/** Astro → BYOP Discounts (ebunny's astro/byop-upload): update add-on pricing from a CSV file. */
export function ByopUpload() {
  const { data, error, reload } = useLoad<any>('/api/astro/products');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const upload = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); const form = e.currentTarget; run(api.post('/api/astro/uploads/byop', new FormData(form)).then((r) => { form.reset(); return r; })); };
  const csv = ['key,rate_adj,min_discount,max_discount', ...data.products.map((p: any) => `${p.key},${n2(p.rateAdj)},${n2(p.minDiscount)},${n2(p.maxDiscount)}`)].join('\n');
  return (
    <>
      <PageHead title="Upload BYOP Discounts" sub="Update Build Your Own Plan add-on pricing from a CSV file. Nothing changes unless every row is valid." />
      {bar}
      <div className="grid-2">
        <form className="panel" onSubmit={upload}>
          <div className="panel-head"><h2>Upload</h2></div>
          <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <input type="file" name="file" accept=".csv,text/csv" required aria-label="CSV file" />
            <p className="help">Columns: <span className="mono">key, rate_adj, min_discount, max_discount</span>. A header row is optional; blank cells leave that value as is. Values are ¢/kWh; negative is a discount.</p>
            <div><button className="btn cyan">Upload &amp; Apply</button></div>
          </div>
        </form>
        <div className="panel"><div className="panel-head"><h2>Current Values</h2><span className="muted">copy as a starting file</span></div>
          <div className="panel-body"><pre className="mono" style={{ margin: 0, whiteSpace: 'pre-wrap', fontSize: '.8rem' }}>{csv}</pre></div></div>
      </div>
    </>
  );
}
