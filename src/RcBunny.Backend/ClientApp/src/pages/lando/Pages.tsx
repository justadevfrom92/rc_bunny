import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, useLoad } from '../../api';
import { websiteUrl } from '../../Sidebar';
import { BarChart, EmptyRow, PageHead, PagerBar, age, secondsTime, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

export const live = (url: string) => websiteUrl.replace(/\/$/, '') + url;
const headline = (s: string) => s.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());

/** Lando → List Pages (ebunny's lando/pages/index): every page, sub-pages indented under their parent. */
export default function Pages() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/lando/pages${params.get('site') ? `?site=${params.get('site')}` : ''}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title={`Pages for ${data.site.name}`} sub={<>{data.pages.length.toLocaleString()} pages on <span className="mono">{data.site.domain}</span>. Sub-pages are indented under their parent.</>}
        actions={<><button className="btn ghost" onClick={() => run(api.post('/api/lando/sitemap'))}>Rebuild Sitemap</button><Link className="btn cyan" to="/lando/pages/create">Add a Page</Link></>} />
      {bar}
      {data.sites.length > 1 && <div className="actions" style={{ marginBottom: 14 }}><span className="muted">Site:</span>{data.sites.map((s: any) =>
        <Link key={s.id} className={`btn sm${s.id === data.site.id ? '' : ' ghost'}`} to={`/lando/pages?site=${s.id}`}>{s.name}</Link>)}</div>}
      <div className="panel"><div className="table-wrap"><table className="table pages-table" data-page="0">
        <thead><tr><th>Page</th><th>Title</th><th>Layout</th><th>Status</th><th className="num">Views this week</th><th>Right now</th><th>Updated</th><th /></tr></thead>
        <tbody>{data.pages.map((p: any) => {
          const depth = p.path === '/' ? 0 : p.path.split('/').length - 1;
          const edit = `/lando/pages/${p.id}/edit`;
          return (
            <tr key={p.id} className="click" onClick={() => navigate(edit)}>
              <td style={{ paddingLeft: 12 + depth * 22 }} className="nowrap">{depth > 0 && <span className="muted">└ </span>}<Link to={edit} className="mono">{p.path === '/' ? '/' : '/' + p.path}</Link> <span className="help mono">#{p.id}</span>
                {p.redirect && <div className="help">redirects to {p.redirect}</div>}</td>
              <td className="wrap">{p.title}</td>
              <td>{p.file ? `Built-in: ${p.file}` : p.template ?? 'No template'}{p.components > 0 && <div className="help">{p.components} {p.components === 1 ? 'component' : 'components'}</div>}</td>
              <td><span className={`pill ${p.status === 'Published' ? 'ok' : 'warn'}`}>{p.status}</span>{p.blocked && <><br /><span className="pill bad">Blocked</span></>}</td>
              <td className="num">{p.views.toLocaleString()}</td>
              <td>{p.online > 0 && <span className="pill ok">{p.online} on it</span>} {p.editing && <div className="help">Editing: {p.editing.join(', ')}</div>}{!p.online && !p.editing && <span className="muted">—</span>}</td>
              <td className="nowrap">{new Date(p.updatedAt).toLocaleDateString('en-US')}</td>
              <td className="actions" onClick={(e) => e.stopPropagation()}><Link className="btn sm ghost" to={edit}>Edit</Link><Link className="btn sm ghost" to={`/lando/pages/${p.id}/activity`}>Activity</Link></td></tr>
          );
        })}</tbody>
      </table></div></div>
    </>
  );
}

/** Add a Page / Edit Page (ebunny's lando/pages/form): Basic, Meta Data, Content, Price Grid, Page Components, Edits. */
export function PageForm() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/lando/pages/form${id ? `?id=${id}` : ''}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <PageEditor data={data} reload={reload} key={data.page.id + data.page.updatedAt} />;
}

function PageEditor({ data, reload }: { data: any; reload: () => void }) {
  const p = data.page, c = data.content, exists = p.id > 0;
  const navigate = useNavigate();
  const [run, bar] = useRun(reload);
  const [chosen, setChosen] = useState<Record<string, string>>(() => Object.fromEntries(Object.keys(data.areas).map((a) => [a, String(c[`content${a[0].toUpperCase()}${a.slice(1)}Id`] ?? '')])));
  const byCategory = Object.entries(data.blocks.reduce((g: Record<string, any[]>, b: any) => ((g[b.category] ??= []).push(b), g), {})) as [string, any[]][];
  const BlockOptions = () => <>{byCategory.map(([cat, list]) => <optgroup key={cat} label={cat}>{list.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}</optgroup>)}</>;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) || null, n = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
    const body = {
      siteId: n('site_id') ?? p.siteId, title: v('title'), path: v('path'), templateId: n('template_id'), status: v('status'), redirect: v('redirect'), promoCode: v('promo_code'),
      marketLabel: v('market_label'), repId: n('rep_id'), copyFromId: n('copy_from_id'), copyMode: v('copy_mode'), htmlTitle: v('html_title'), metaDescription: v('meta_description'),
      metaKeywords: v('meta_keywords'), headContent: v('head_content'), addressBoxTitle: v('address_box_title'), phone: v('phone'), canonical: f.get('canonical') === 'on',
      noIndex: f.get('no_index') === 'on', hardCache: f.get('hard_cache') === 'on', pageTitle: v('page_title'),
      contentPrimaryId: n('content_primary_id'), contentSecondaryId: n('content_secondary_id'), contentParentId: n('content_parent_id'), contentAuxiliaryId: n('content_auxiliary_id'), contentAmpId: n('content_amp_id'),
      pricegridType: v('pricegrid_type'), pricegridHeader: v('pricegrid_header'), pricegridGroupId: n('pricegrid_group_id'), ratingFormula: v('rating_formula'), marketId: n('market_id'),
    };
    run((exists ? api.put<any>(`/api/lando/pages/${p.id}`, body) : api.post<any>('/api/lando/pages', body)).then((r) => { if (!exists) navigate(`/lando/pages/${r.id}/edit`); return r; }));
  };
  const addComponent = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    run(api.post(`/api/lando/pages/${p.id}/components`, { contentBlockId: Number(f.get('content_block_id')), zone: f.get('zone') }).then((r) => { form.reset(); return r; }));
  };
  const saveVars = (e: FormEvent<HTMLFormElement>, cid: number) => {
    e.preventDefault();
    run(api.put(`/api/lando/page-components/${cid}`, { vars: Object.fromEntries([...new FormData(e.currentTarget).entries()].map(([k, val]) => [k, String(val)])) }));
  };
  const copyMode = p.copyFromId ? 'sync' : 'once';
  return (
    <>
      <PageHead title={exists ? `Edit Page: ${p.title}` : 'Add a Page'}
        sub={<>{data.sites.find((s: any) => s.id === p.siteId)?.name}{exists && <> · <span className="mono">{data.url}</span></>}</>}
        actions={exists ? <><Link className="btn ghost" to={`/lando/pages/${p.id}/activity`}>Activity</Link>{!p.redirect && <a className="btn ghost" href={live(data.url)} target="_blank" rel="noopener">View Page Live</a>}</> : undefined} />
      {bar}
      {exists && <nav className="jump" aria-label="Sections">{[['basic', 'Basic'], ['meta', 'Meta Data'], ['content', 'Content'], ['pricegrid', 'Price Grid'], ['components', 'Page Components'], ['edits', 'Edits']].map(([h, l]) => <a key={h} href={`#${h}`}>{l}</a>)}</nav>}
      {p.file && <div className="alert info"><b>Built-in page.</b> This page's layout is the website's own page <span className="mono">{p.file}</span>. Its title, meta data and status are used here; content areas don't apply. Add components to its top and bottom zones below.</div>}

      <form onSubmit={save} style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <div className="panel" id="basic"><div className="panel-head"><h2>Basic</h2><button className="btn cyan">Save Page</button></div><div className="panel-body form-grid">
          {!exists && <><label htmlFor="site_id">Site</label><select id="site_id" name="site_id" defaultValue={p.siteId}>{data.sites.map((s: any) => <option key={s.id} value={s.id}>{s.name} ({s.domain})</option>)}</select></>}
          <label htmlFor="title">Title</label><input id="title" name="title" required defaultValue={p.title} />
          <label htmlFor="path">Page Slug / Path</label><input id="path" name="path" required className="mono" defaultValue={p.path} placeholder="get-to-learnin/new-article" readOnly={!!p.file} />
          <label htmlFor="template_id">Template</label>
          <select id="template_id" name="template_id" defaultValue={p.templateId ?? ''}><option value="">—</option>{data.templates.map((t: any) => <option key={t.id} value={t.id}>{t.name}</option>)}</select>
          <label htmlFor="status">Status</label><select id="status" name="status" defaultValue={p.status}>{['Published', 'Draft'].map((s) => <option key={s}>{s}</option>)}</select>
          <label htmlFor="redirect">Redirect To</label><input id="redirect" name="redirect" defaultValue={p.redirect ?? ''} placeholder="Leave blank for none, e.g. /plans.html" />
          <label htmlFor="promo_code">Promo Code</label><input id="promo_code" name="promo_code" className="mono" defaultValue={p.promoCode ?? ''} placeholder="Applied to orders that start on this page" />
          <label htmlFor="market_label">Market Label</label><input id="market_label" name="market_label" defaultValue={p.marketLabel ?? ''} placeholder="e.g. Houston — shown above the page title" />
          <label htmlFor="rep_id">REP ID</label>
          <select id="rep_id" name="rep_id" defaultValue={p.repId ?? ''}><option value="">None</option>{Object.entries(data.reps).map(([k, v]) => <option key={k} value={k}>{v as string}</option>)}</select>
          {!p.file && <>
            <label htmlFor="copy_from_id">Copy From Page ID</label>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              <input id="copy_from_id" name="copy_from_id" inputMode="numeric" defaultValue={p.copyFromId ?? ''} placeholder="Page ID (shown in the page list)" style={{ maxWidth: 220 }} />
              <label className="check"><input type="radio" name="copy_mode" value="once" defaultChecked={copyMode === 'once'} /> Copy contents one time</label>
              <label className="check"><input type="radio" name="copy_mode" value="sync" defaultChecked={copyMode === 'sync'} /> Keep contents in sync</label>
              {data.copyFrom && <span className="help">Showing the content of <Link to={`/lando/pages/${data.copyFrom.id}/edit`}>#{data.copyFrom.id} {data.copyFrom.title}</Link>. Clear the ID to edit this page's own content.</span>}
            </div>
          </>}
          {exists && <><label>Created / Modified</label><span className="muted">{secondsTime(p.createdAt)} · {secondsTime(p.updatedAt)}</span></>}
        </div></div>

        <div className="panel" id="meta"><div className="panel-head"><h2>Details, Meta Data</h2></div><div className="panel-body form-grid">
          <label htmlFor="html_title">HTML Title</label><input id="html_title" name="html_title" defaultValue={p.htmlTitle ?? ''} placeholder="Browser tab / search result title; blank uses Title" />
          <label htmlFor="meta_description">Meta Description</label><input id="meta_description" name="meta_description" defaultValue={p.metaDescription ?? ''} />
          <label htmlFor="meta_keywords">Meta Keywords</label><input id="meta_keywords" name="meta_keywords" defaultValue={p.metaKeywords ?? ''} />
          <label htmlFor="head_content">Head Content</label><textarea id="head_content" name="head_content" style={{ minHeight: 90 }} placeholder="Extra meta tags, etc." defaultValue={p.headContent ?? ''} />
          <label htmlFor="address_box_title">Address Box Title</label><input id="address_box_title" name="address_box_title" defaultValue={p.addressBoxTitle ?? ''} placeholder="Leave blank to use the site default" />
          <label htmlFor="phone">Display Phone Number</label><input id="phone" name="phone" defaultValue={p.phone ?? ''} placeholder="Leave blank to use the site's" />
          <label>Options</label>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <label className="check"><input type="checkbox" name="canonical" defaultChecked={p.canonical} /> Canonical tag</label>
            <label className="check"><input type="checkbox" name="no_index" defaultChecked={p.noIndex} /> Disable SEO index (also leaves it out of the sitemap)</label>
            <label className="check"><input type="checkbox" name="hard_cache" defaultChecked={p.hardCache} /> Hard cache (serve the saved HTML; refreshes when the page or a content block is saved, or hourly)</label>
          </div>
        </div></div>

        {!p.file && (
          <div className="panel" id="content"><div className="panel-head"><h2>Content</h2><span className="muted">Pick predefined content for each area. Content is written in Lando → Content Blocks.</span></div>
            <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
              <label htmlFor="page_title">Page Title (the banner heading; blank uses Title)</label><input id="page_title" name="page_title" defaultValue={c.pageTitle ?? ''} disabled={!!data.copyFrom} />
              {Object.entries(data.areas).map(([area, label]) => {
                const f = `content_${area}_id`;
                return [
                  <label key={f + 'l'} htmlFor={f}>{label as string}{area === 'parent' ? ' (shown under the banner heading)' : area === 'amp' ? ` (served at /amp/${p.path || 'path'})` : ''}</label>,
                  <div key={f} className="form-row">
                    <select id={f} name={f} disabled={!!data.copyFrom} style={{ flex: 1 }} value={chosen[area]} onChange={(e) => setChosen({ ...chosen, [area]: e.target.value })}>
                      <option value="">-- None --</option><BlockOptions />
                    </select>
                    {chosen[area] && <Link className="btn sm ghost" to={`/lando/blocks/${chosen[area]}/edit`}>Edit content</Link>}
                  </div>,
                ];
              })}
            </div>
          </div>
        )}

        <div className="panel" id="pricegrid"><div className="panel-head"><h2>Price Grid Settings</h2><span className="muted">A list of plans with prices, shown after the page content</span></div><div className="panel-body form-grid">
          <label htmlFor="pricegrid_type">Price Grid Type</label>
          <select id="pricegrid_type" name="pricegrid_type" defaultValue={p.pricegridType ?? ''}><option value="">No price grid</option>{Object.entries(data.priceGrids).map(([k, l]) => <option key={k} value={k}>{l as string}</option>)}</select>
          <label htmlFor="pricegrid_header">Price Grid Header</label><input id="pricegrid_header" name="pricegrid_header" defaultValue={p.pricegridHeader ?? ''} placeholder="Blank: “Plans for (market label)”" />
          <h3 style={{ gridColumn: '1/-1', margin: '8px 0 0' }}>Markets and Bundles</h3>
          <label htmlFor="pricegrid_group_id">Plans (Plan Group)</label>
          <select id="pricegrid_group_id" name="pricegrid_group_id" defaultValue={p.pricegridGroupId ?? ''}><option value="">--Select--</option>{data.groups.map((g: any) => <option key={g.id} value={g.id}>{g.name} ({g.slug})</option>)}</select>
          <label htmlFor="rating_formula">Rating Formula</label>
          <select id="rating_formula" name="rating_formula" defaultValue={p.ratingFormula ?? ''}><option value="">--Select--</option>{Object.entries(data.ratingFormulas).map(([k, l]) => <option key={k} value={k}>{l as string}</option>)}</select>
          <label htmlFor="market_id">Default Market</label>
          <select id="market_id" name="market_id" defaultValue={p.marketId ?? ''}><option value="">--None--</option>{data.markets.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select>
        </div></div>

        <div className="actions"><button className="btn cyan">Save Page</button><Link className="btn ghost" to="/lando/pages">Cancel</Link></div>
      </form>

      {exists && <>
        <div className="panel" id="components"><div className="panel-head"><h2>Page Components</h2><span className="muted">Content blocks shown on this page, by zone</span></div>
          <div className="panel-body">
            {Object.entries(data.zones).map(([zone, zoneLabel]) => {
              const list = data.components.filter((x: any) => x.zone === zone);
              return (
                <div key={zone}>
                  <h3>{zoneLabel as string}</h3>
                  {list.map((x: any) => (
                    <div key={x.id}>
                      <div className="row-line"><span><b>{x.block.name}</b> <span className="mono help">{x.block.slug}</span>{x.block.category && <span className="help"> · {x.block.category}</span>}</span>
                        <span className="actions">
                          {[['up', '↑'], ['down', '↓']].map(([dir, arrow]) => <button key={dir} className="btn sm ghost" aria-label={`Move ${x.block.name} ${dir}`} onClick={() => run(api.post(`/api/lando/page-components/${x.id}/move`, { dir }))}>{arrow}</button>)}
                          <Link className="btn sm ghost" to={`/lando/blocks/${x.block.id}/edit`}>Edit Block</Link>
                          <button className="link-btn" onClick={() => run(api.del(`/api/lando/page-components/${x.id}`))}>[ remove ]</button>
                        </span></div>
                      {Object.keys(x.variables).length > 0 && (
                        <form className="comp-vars" onSubmit={(e) => saveVars(e, x.id)}>
                          {Object.entries(x.variables).map(([name, def]) => (
                            <div key={name} className="field"><label htmlFor={`v${x.id}-${name}`}>{headline(name)}</label>
                              <input id={`v${x.id}-${name}`} name={name} defaultValue={x.vars[name] ?? ''} placeholder={(def as string) !== '' ? (def as string) : `Text for ${name}`} /></div>
                          ))}
                          <button className="btn sm">Save Text</button>
                        </form>
                      )}
                    </div>
                  ))}
                  {list.length === 0 && <p className="help" style={{ marginBottom: 10 }}>Nothing here.</p>}
                </div>
              );
            })}
            <form className="form-row" style={{ marginTop: 16 }} onSubmit={addComponent}>
              <select name="content_block_id" required aria-label="Content block" style={{ flex: 2 }}><option value="">-- Add a content block --</option><BlockOptions /></select>
              <select name="zone" aria-label="Zone">{Object.entries(data.zones).map(([z, l]) => <option key={z} value={z}>{l as string}</option>)}</select>
              <button className="btn sm">Add Component</button>
            </form>
          </div>
        </div>
        <div className="panel" id="edits"><div className="panel-head"><h2>Edits</h2></div>
          <div className="table-wrap"><table><thead><tr><th>ID</th><th>Date</th><th>User</th><th>Change</th></tr></thead><tbody>
            {data.edits.map((e: any) => <tr key={e.id} className="click" onClick={() => navigate(`/lando/records/history_item/${e.id}`)}><td className="nowrap"><Link to={`/lando/records/history_item/${e.id}`}><b>#{e.id}</b></Link></td>
              <td>{dateTime(e.createdAt)}</td><td>{e.who || 'System'}</td><td>{e.summary}</td></tr>)}
            {data.edits.length === 0 && <EmptyRow cols={4}>No edits recorded.</EmptyRow>}
          </tbody></table></div></div>
        {data.canDelete && <div><button className="btn red" onClick={() => confirm(`Delete page?\nThis removes ${data.url}.`) && run(api.del(`/api/lando/pages/${p.id}`).then((r) => { navigate('/lando/pages'); return r; }))}>Delete This Page</button></div>}
      </>}
    </>
  );
}

/** A page's Activity (ebunny's lando/pages/activity): who is on it, views, where people came from, its history. */
export function Activity() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/lando/pages/${id}/activity?page=${params.get('page') ?? 1}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const p = data.page, s = data.stats;
  const refs = Object.entries(data.referrers);
  return (
    <>
      <PageHead title={`Activity: ${p.title || p.path}`} sub={<><span className="mono">{p.url}</span> · {p.site} · {p.status}</>}
        actions={<><Link className="btn ghost" to="/lando/pages">List Pages</Link><Link className="btn cyan" to={`/lando/pages/${p.id}/edit`}>Edit Page</Link></>} />
      {data.blocks.map((b: any) => <div key={b.id} className="flash bad" role="status" style={{ marginBottom: 12 }}>Blocked area <span className="mono">/{b.value}</span> covers this page{b.inForce ? '' : ' (not in force)'}: {b.reason ?? 'no reason given'}. <Link to={`/lando/blocked-areas/${b.id}/edit`}>Edit</Link></div>)}
      <div className="grid-4 stat-row">
        <div className="stat"><span>On It Now</span><strong>{data.online.length}</strong><small>{data.online.filter((v: any) => v.customerId).length} signed in to My Account</small></div>
        <div className="stat"><span>Views Today</span><strong>{s.today.toLocaleString()}</strong><small>{s.week.toLocaleString()} in the last 7 days</small></div>
        <div className="stat"><span>Last 30 Days</span><strong>{s.month.toLocaleString()}</strong><small>{s.people.toLocaleString()} {s.people === 1 ? 'visitor' : 'visitors'}{s.blocked > 0 && ` · ${s.blocked} turned away`}</small></div>
        <div className="stat"><span>Being Edited By</span><strong style={{ fontSize: '1.05rem' }}>{data.editing.join(', ') || 'Nobody'}</strong><small>edit screen opened in the last 10 minutes</small></div>
      </div>
      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head"><h2>Views by Day</h2><span className="muted">last 30 days</span></div>
        <div className="panel-body"><BarChart unit="views" bars={data.days.map((d: any) => {
          const day = new Date(d.date + 'T00:00');
          return { label: day.toLocaleDateString('en-US', { month: 'numeric', day: 'numeric' }), value: d.n, tip: day.toLocaleDateString('en-US', { weekday: 'short', month: 'numeric', day: 'numeric' }) };
        })} /></div>
      </div>
      <div className="grid-2" style={{ marginBottom: 20 }}>
        <div className="panel"><div className="panel-head"><h2>On This Page Now</h2></div>
          <div className="table-wrap"><table><tbody>
            {data.online.map((v: any, i: number) => <tr key={i}><td className="mono">{v.ip}</td><td>{v.customer ?? 'Visitor'}</td><td className="nowrap muted">opened {age(v.createdAt)} ago</td></tr>)}
            {data.online.length === 0 && <tr><td className="empty">Nobody right now.</td></tr>}
          </tbody></table></div>
        </div>
        <div className="panel"><div className="panel-head"><h2>Where Visitors Came From</h2><span className="muted">30 days</span></div>
          <div className="table-wrap"><table><tbody>
            {refs.map(([h, n]) => <tr key={h}><td>{h}</td><td className="num">{n as number}</td></tr>)}
            {refs.length === 0 && <tr><td className="empty">Direct visits and links inside the site only.</td></tr>}
          </tbody></table></div>
        </div>
      </div>
      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head"><h2>History</h2><span className="muted">changes to the page and its components</span></div>
        <div className="table-wrap"><table className="table"><thead><tr><th>ID</th><th>Date</th><th>User</th><th>What</th><th>Changes</th></tr></thead><tbody>
          {data.history.map((h: any) => {
            const changes = h.action === 'updated' && h.changes ? Object.entries(JSON.parse(h.changes) as Record<string, [unknown, unknown]>) : [];
            const cut = (v: unknown) => { const t = v == null ? '' : String(v); return t.length > 60 ? t.slice(0, 57) + '...' : t; };
            return (
              <tr key={h.id}><td className="nowrap"><Link to={`/lando/records/history_item/${h.id}`}><b>#{h.id}</b></Link></td><td className="nowrap">{dateTime(h.createdAt)}</td><td>{h.who || 'System'}</td><td className="wrap">{h.summary}</td>
                <td className="wrap">{changes.length ? <details><summary>{changes.length} {changes.length === 1 ? 'field' : 'fields'}</summary><ul style={{ margin: '6px 0 0 16px', fontSize: '.82rem' }}>
                  {changes.map(([f, [o, n]]) => <li key={f}><b>{f.replace(/_/g, ' ')}</b>: {cut(o) || '(empty)'} → {cut(n) || '(empty)'}</li>)}</ul></details> : <span className="muted">—</span>}</td></tr>
            );
          })}
          {data.history.length === 0 && <EmptyRow cols={5}>No changes recorded.</EmptyRow>}
        </tbody></table></div>
        <PagerBar page={data.pageNo} total={data.total} size={10} onPage={(n) => navigate(`/lando/pages/${p.id}/activity?page=${n}`)} />
      </div>
      <div className="panel"><div className="panel-head"><h2>Recent Visits</h2><Link className="btn sm ghost" to={`/lando/visitors${p.path === '/' ? '' : `?path=${encodeURIComponent(p.path)}`}`}>All</Link></div>
        <div className="table-wrap"><table><tbody>
          {data.recent.map((v: any) => <tr key={v.id}><td className="nowrap">{dateTime(v.createdAt)}</td><td className="mono">{v.ip}</td><td>{v.customer ?? 'Visitor'}</td><td>{v.blocked && <span className="pill bad">Turned away</span>}</td></tr>)}
          {data.recent.length === 0 && <tr><td className="empty">No visits yet.</td></tr>}
        </tbody></table></div>
      </div>
    </>
  );
}
