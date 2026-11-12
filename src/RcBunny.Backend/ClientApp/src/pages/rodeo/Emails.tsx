import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import { BarChart, EmptyRow, PageHead, PagerBar, Pill, useRun } from '../../ui';
import { DateInput, useCss } from '../../fields';
import { when } from '../walker/shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const stageTones: Record<string, string> = { clicked: 'ok', opened: 'ok', sent: 'info', dropped: 'bad', 'not sent': 'warn', suppressed: 'bad' };
const stageLabels: Record<string, string> = { queued: 'Queued', sent: 'Delivered', opened: 'Opened', clicked: 'Clicked', dropped: 'Dropped', 'not sent': 'Not sent', suppressed: 'Suppressed' };
/** ebunny's rodeo/emails/_stage */
const StagePill = ({ stage }: { stage: string }) => <Pill tone={stageTones[stage] ?? ''}>{stageLabels[stage] ?? stage.charAt(0).toUpperCase() + stage.slice(1)}</Pill>;
const Cat = ({ c }: { c: { name: string; color: string } | null }) => c ? <span className="sv-cat"><span className="sv-dot" style={{ ['--dot' as string]: c.color }} />{c.name}</span> : <span className="muted">—</span>;
const day = (s: string) => new Date(s).toLocaleDateString('en-US');
const short = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');
const secs = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit', second: '2-digit' }).replace(',', '');
const esc = (s: string) => s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);

/**
 * ebunny's HTML editor preview: variables ({{name}} or {{name|default}}) show their default text (or [name]) in the
 * preview frame; an email template's page wraps the body in the header and footer it picked.
 */
function HtmlPreview({ html, minHeight }: { html: string; minHeight: number }) {
  const filled = html.replace(/\{\{\s*([a-z][a-z0-9_:-]*)\s*(?:\|([^}]*))?\}\}/gi, (_m, name: string, def?: string) => (def?.trim() ? esc(def.trim()) : `[${esc(name)}]`));
  return <iframe id="b-prev" title="Email preview" sandbox="" style={{ width: '100%', minHeight, border: '1px solid var(--line)', borderRadius: 6, background: '#fff' }}
    srcDoc={`<style>body{font-family:sans-serif;padding:12px;color:#102247}</style>${filled}`} />;
}

/** Rodeo → Emails → Overview (ebunny's rodeo/emails/overview). */
export function Overview() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/emails?${qs({ days: sp.get('days') })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const s = data.stats, since = data.since;
  const max = Math.max(1, ...data.byCategory.map((c: any) => c.emails));
  return (
    <>
      <PageHead title="Emails" sub="Every email customers get: account emails from Corral and campaign emails. Rates are of delivered emails."
        actions={<><Link className="btn ghost" to="/rodeo/emails/sent">Sent Emails</Link><Link className="btn cyan" to="/rodeo/templates/new">New Template</Link></>} />
      <div className="actions" style={{ marginBottom: 14 }}>{[[7, 'Last 7 days'], [30, 'Last 30 days'], [90, 'Last 90 days'], [365, 'Last year']].map(([d, l]) => (
        <Link key={d} className={`btn sm ${data.days === d ? '' : 'ghost'}`} to={`/rodeo/emails?days=${d}`}>{l}</Link>))}</div>
      <div className="grid-4 stat-row">
        <Link className="stat" to={`/rodeo/emails/sent?${qs({ start: since })}`}><span>Emails</span><strong>{s.total.toLocaleString()}</strong><small>{s.delivered.toLocaleString()} delivered{s.unsent > 0 && ` · ${s.unsent.toLocaleString()} not sent`}</small></Link>
        <div className="stat"><span>Open Rate</span><strong>{s.open}%</strong><small>Click rate {s.click}%</small></div>
        <Link className="stat" to={`/rodeo/emails/sent?${qs({ stage: 'dropped', start: since })}`}><span>Dropped</span><strong>{s.drop}%</strong><small>bounced or blocked by the inbox</small></Link>
        <Link className="stat" to="/rodeo/emails/suppressions"><span>Suppressed Addresses</span><strong>{data.suppressed.toLocaleString()}</strong><small>never emailed</small></Link>
      </div>
      <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>Emails Sent</h2><span className="muted">{data.days <= 30 ? 'by day' : data.days <= 90 ? 'by week' : 'by month'}</span></div>
        <div className="panel-body"><BarChart bars={data.chart.map((c: any) => ({ label: c.label, value: c.value, tip: c.tip }))} unit="emails" /></div></div>
      <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>By Template</h2><Link className="btn sm ghost" to="/rodeo/templates">All Templates</Link></div>
        <div className="table-wrap"><table className="table">
          <thead><tr><th>Template</th><th>Category</th><th className="num">Emails</th><th className="num">Delivered</th><th className="num">Opened</th><th className="num">Clicked</th><th className="num">Dropped</th><th>Last Sent</th></tr></thead>
          <tbody>
            {data.rows.map((r: any) => {
              const pct = (n: number) => (r.delivered ? `${Math.round((100 * n) / r.delivered)}%` : '—');
              const to = `/rodeo/emails/sent?${qs({ template: r.template, start: since })}`;
              return (
                <tr key={r.template} className="click" onClick={() => navigate(to)}>
                  <td><Link to={to}><b>{r.template}</b></Link>{!r.isTemplate && <div className="muted" style={{ fontSize: '.78rem' }}>not a Rodeo template</div>}</td>
                  <td><Cat c={r.category && { name: r.category.name, color: r.category.color }} /></td>
                  <td className="num">{r.total.toLocaleString()}</td><td className="num">{r.delivered.toLocaleString()}</td>
                  <td className="num">{pct(r.opened)}</td><td className="num">{pct(r.clicked)}</td><td className="num">{r.dropped ? r.dropped.toLocaleString() : '—'}</td>
                  <td className="nowrap">{day(r.lastAt)}</td></tr>
              );
            })}
            {data.rows.length === 0 && <EmptyRow cols={8}>No emails in this time.</EmptyRow>}
          </tbody>
        </table></div></div>
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>By Category</h2><Link className="btn sm ghost" to="/rodeo/emails/categories">Categories</Link></div>
          <div className="panel-body"><div className="sv-bars">
            {data.byCategory.map((c: any) => (
              <div key={c.name} className="sv-bar" tabIndex={0} data-tip={`${c.name}: ${c.emails.toLocaleString()} emails`} style={{ ['--dot' as string]: c.color }}>
                <span>{c.name} <span className="muted">· {c.templates} {c.templates === 1 ? 'template' : 'templates'}</span></span><span className="track"><span className="fill" style={{ width: `${Math.round((c.emails / max) * 100)}%` }} /></span><span className="n">{c.emails.toLocaleString()}</span></div>
            ))}
            {data.byCategory.length === 0 && <p className="muted" style={{ margin: 0 }}>No categories yet.</p>}
          </div></div></div>
        <div className="panel"><div className="panel-head"><h2>Latest Emails</h2><Link className="btn sm ghost" to="/rodeo/emails/sent">All</Link></div>
          <div className="table-wrap"><table><tbody>
            {data.recent.map((e: any) => (
              <tr key={e.id} className="click" onClick={() => navigate(`/rodeo/emails/sent/${e.id}`)}><td className="nowrap">{short(e.createdAt)}</td>
                <td><Link to={`/rodeo/emails/sent/${e.id}`}>{e.template}</Link><div className="muted">{e.customer?.name}</div></td><td><StagePill stage={e.stage} /></td></tr>
            ))}
            {data.recent.length === 0 && <tr><td className="empty">No emails yet.</td></tr>}
          </tbody></table></div></div>
      </div>
    </>
  );
}

/** Rodeo → Sent Emails (ebunny's rodeo/emails/sent). */
export function Sent() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/emails/sent?${sp}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const f = Object.fromEntries(sp);
  const search = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    navigate(`/rodeo/emails/sent?${qs(Object.fromEntries([...new FormData(e.currentTarget)].filter(([, v]) => v !== '')) as Record<string, string>)}`);
  };
  return (
    <>
      <PageHead title="Sent Emails" sub={`${data.total.toLocaleString()} ${data.total === 1 ? 'email' : 'emails'} to customers: account emails from Corral, campaigns and tests.`} />
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={search} key={sp.toString()}><div className="panel-head"><h2>Search Emails</h2></div><div className="panel-body">
            <div className="field"><label htmlFor="start">Start</label><DateInput id="start" name="start" defaultValue={f.start ?? ''} /></div>
            <div className="field"><label htmlFor="end">End</label><DateInput id="end" name="end" defaultValue={f.end ?? ''} /></div>
            <div className="field"><label htmlFor="account">Account Number</label><input id="account" name="account" defaultValue={f.account ?? ''} inputMode="numeric" /></div>
            <div className="field"><label htmlFor="email">Email Address</label><input id="email" name="email" defaultValue={f.email ?? ''} /></div>
            <div className="field"><label htmlFor="template">Template</label><select id="template" name="template" defaultValue={f.template ?? ''}><option value="">Any</option>{data.templateNames.map((t: string) => <option key={t}>{t}</option>)}</select></div>
            <div className="field"><label htmlFor="stage">Status</label><select id="stage" name="stage" defaultValue={f.stage ?? ''}><option value="">Any</option>{Object.entries(data.stages as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></div>
            <div className="field"><label htmlFor="campaign">Campaign</label><select id="campaign" name="campaign" defaultValue={f.campaign ?? ''}><option value="">Any</option>{data.campaigns.map((c: any) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></div>
            <div className="field"><label htmlFor="user">Sent By</label><select id="user" name="user" defaultValue={f.user ?? ''}><option value="">Anyone</option><option value="system">System</option>{data.users.map((u: any) => <option key={u.id} value={u.id}>{u.name}</option>)}</select></div>
            <button className="btn">Search</button>{Object.values(f).some(Boolean) && <Link className="btn ghost" to="/rodeo/emails/sent">Clear</Link>}
          </div></form>
        </aside>
        <div className="search-main">
          <div className="panel"><div className="table-wrap"><table className="table" data-page="0">
            <thead><tr><th>ID</th><th>Sent</th><th>Account</th><th>Template</th><th>Status</th></tr></thead>
            <tbody>
              {data.emails.map((e: any) => (
                <tr key={e.id} className="click" onClick={() => navigate(`/rodeo/emails/sent/${e.id}`)}>
                  <td className="nowrap"><Link to={`/rodeo/emails/sent/${e.id}`}><b>#{e.id}</b></Link></td><td className="nowrap">{when(e.at)}</td>
                  <td onClick={(x) => x.stopPropagation()}>{e.customer && <><Link className="mono" to={`/rodeo/emails/sent?${qs({ account: e.customer.account })}`} title="Every email to this account">{e.customer.account}</Link><div className="muted">{e.customer.name}</div><div className="muted" style={{ fontSize: '.78rem' }}>{e.customer.email}</div></>}</td>
                  <td className="wrap"><Link to={`/rodeo/emails/sent/${e.id}`}>{e.template}</Link><div className="muted">{e.by}</div></td>
                  <td><StagePill stage={e.stage} />{e.openedAt && <div className="muted" style={{ fontSize: '.78rem' }}>opened {short(e.openedAt)}</div>}{e.clickedAt && <div className="muted" style={{ fontSize: '.78rem' }}>clicked {short(e.clickedAt)}</div>}</td></tr>
              ))}
              {data.emails.length === 0 && <EmptyRow cols={5}>No emails match.</EmptyRow>}
            </tbody>
          </table></div>
            <PagerBar page={data.page} total={data.total} size={50} onPage={(p) => navigate(`/rodeo/emails/sent?${qs({ ...f, page: p })}`)} /></div>
        </div>
      </div>
    </>
  );
}

/** One sent email (ebunny's rodeo/emails/show): what the customer got, its timeline, and other emails to the account. */
export function Email() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/emails/sent/${id}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const e = data.email, c = data.customer, t = data.template;
  return (
    <>
      <PageHead title={e.template} sub={<>Email <span className="mono">{e.id}</span>{c && <> to <span className="mono">{c.account}</span> · {c.name} · {c.email}</>}</>}
        actions={<>{c && <Link className="btn ghost" to={`/rodeo/emails/sent?${qs({ account: c.account })}`}>All Emails to This Account</Link>}
          {t && <><Link className="btn ghost" to={`/rodeo/templates/${t.id}`}>Template</Link><Link className="btn ghost" to={`/rodeo/templates/${t.id}/heatmap`}>Click Heat Map</Link></>}</>} />
      {data.suppression && <div className="flash bad" role="status" style={{ marginBottom: 16 }}>{data.suppression.email} is on the suppression list ({data.suppression.reason}, {day(data.suppression.createdAt)}). No more emails go to it.</div>}
      <div className="it-grid">
        <div className="panel">
          <div className="panel-head"><h2>What the customer got</h2><StagePill stage={e.stage} /></div>
          <div className="panel-body">
            {data.subject && <p style={{ margin: '0 0 10px' }}><b>Subject:</b> {data.subject}</p>}
            {data.html ? <iframe title="Email" sandbox="" srcDoc={data.html} style={{ width: '100%', minHeight: 440, border: '1px solid var(--line)', borderRadius: 6, background: '#fff' }} />
              : <p className="muted" style={{ margin: 0 }}>The body of this email wasn't kept, and there's no Rodeo template named “{e.template}” to show instead.</p>}
          </div>
        </div>
        <div className="it-side">
          <div className="panel"><div className="panel-head"><h2>Timeline</h2></div>
            <div className="table-wrap"><table data-page="0"><tbody>
              <tr><td>Created</td><td className="nowrap">{secs(e.createdAt)}</td></tr>
              <tr><td>Delivered</td><td className="nowrap">{e.sentAt ? secs(e.sentAt) : e.status === 'queued' ? 'Waiting in the queue' : 'Not sent'}</td></tr>
              <tr><td>Opened</td><td className="nowrap">{e.openedAt ? secs(e.openedAt) : '—'}</td></tr>
              <tr><td>Clicked</td><td className="nowrap">{e.clickedAt ? secs(e.clickedAt) : '—'}</td></tr>
              {e.droppedAt && <tr><td>Dropped</td><td className="nowrap">{secs(e.droppedAt)}</td></tr>}
              <tr><td>Sent by</td><td>{data.campaign ? <>Campaign: <Link to={`/rodeo/campaigns/${data.campaign.id}`}>{data.campaign.name}</Link></> : data.sentBy}</td></tr>
            </tbody></table></div>
            {c?.email && !data.suppression && <div className="panel-body"><Link className="btn sm ghost" to={`/rodeo/emails/suppressions/new?${qs({ email: c.email })}`}>Suppress this address</Link></div>}
          </div>
          <div className="panel"><div className="panel-head"><h2>Other emails to this account</h2>{c && <Link className="btn sm ghost" to={`/rodeo/emails/sent?${qs({ account: c.account })}`}>All</Link>}</div>
            <div className="table-wrap"><table data-page="0"><tbody>
              {data.others.map((o: any) => (
                <tr key={o.id} className="click" onClick={() => navigate(`/rodeo/emails/sent/${o.id}`)}><td className="nowrap">{day(o.createdAt)}</td><td><Link to={`/rodeo/emails/sent/${o.id}`}>{o.template}</Link></td><td><StagePill stage={o.stage} /></td></tr>
              ))}
              {data.others.length === 0 && <tr><td className="empty">None.</td></tr>}
            </tbody></table></div>
          </div>
        </div>
      </div>
    </>
  );
}

/** Rodeo → Templates (ebunny's rodeo/templates). */
export function Templates() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const only = sp.get('category');
  const { data, error } = useLoad<any>(`/api/rodeo/templates?${qs({ category: only })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Email Templates" sub="Emails customers get. Corral's Send Email action and campaigns use these."
        actions={<Link className="btn cyan" to={`/rodeo/templates/new${only && /^\d+$/.test(only) ? `?category=${only}` : ''}`}>New Template</Link>} />
      <div className="actions" style={{ marginBottom: 14 }}>
        <Link className={`btn sm ${only ? 'ghost' : ''}`} to="/rodeo/templates">All</Link>
        {data.categories.map((c: any) => <Link key={c.id} className={`btn sm ${only === String(c.id) ? '' : 'ghost'}`} to={`/rodeo/templates?category=${c.id}`}><span className="sv-dot" style={{ ['--dot' as string]: c.color, marginRight: 6 }} />{c.name}</Link>)}
        <Link className={`btn sm ${only === 'none' ? '' : 'ghost'}`} to="/rodeo/templates?category=none">Uncategorized</Link>
      </div>
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Template</th><th>Category</th><th>Subject</th><th className="num">Sent</th><th className="num">Campaigns</th><th>Last Test</th><th>Updated</th><th></th></tr></thead>
        <tbody>
          {data.templates.map((t: any) => (
            <tr key={t.id} className="click" onClick={() => navigate(`/rodeo/templates/${t.id}`)}><td><Link to={`/rodeo/templates/${t.id}`}><b>{t.name}</b></Link></td>
              <td className="nowrap"><Cat c={t.category} /></td><td>{t.subject}</td>
              <td className="num" onClick={(e) => e.stopPropagation()}>{t.sent ? <Link to={`/rodeo/emails/sent?${qs({ template: t.name })}`}>{t.sent.toLocaleString()}</Link> : 0}</td>
              <td className="num">{t.campaigns}</td><td>{t.lastTest ? day(t.lastTest) : '—'}{t.tests > 0 && <span className="muted"> · {t.tests} {t.tests === 1 ? 'test' : 'tests'}</span>}</td>
              <td>{day(t.updatedAt)}</td><td className="actions" onClick={(e) => e.stopPropagation()}><Link className="btn sm" to={`/rodeo/templates/${t.id}/test`}>Send Test</Link></td></tr>
          ))}
          {data.templates.length === 0 && <EmptyRow cols={8}>No templates here.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** New Template and a template's page (ebunny's rodeo/template): its fields, header and footer, and a live preview. */
export function TemplateForm() {
  const { id } = useParams();
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(id ? `/api/rodeo/templates/${id}` : `/api/rodeo/templates/new?${qs({ category: sp.get('category') })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <TemplateEditor data={data} id={id} key={id ?? 'new'} onSaved={(newId) => !id && navigate(`/rodeo/templates/${newId}`)} />;
}

function TemplateEditor({ data, id, onSaved }: { data: any; id?: string; onSaved: (id: number) => void }) {
  const t = data.template, parts: any[] = data.options.parts;
  const [f, setF] = useState({ name: t.name, subject: t.subject, body: t.body, emailCategoryId: t.emailCategoryId ?? '', emailHeaderId: t.emailHeaderId ?? '', emailFooterId: t.emailFooterId ?? '' });
  const set = (k: string, v: string) => setF((x) => ({ ...x, [k]: v }));
  const [run, bar] = useRun();
  const part = (kind: string, picked: string | number) => (picked ? parts.find((p) => p.id === Number(picked)) : parts.find((p) => p.kind === kind && p.isDefault))?.html ?? '';
  const save = (e: React.FormEvent) => {
    e.preventDefault();
    const n = (v: string | number) => (v === '' ? null : Number(v));
    const body = { ...f, emailCategoryId: n(f.emailCategoryId), emailHeaderId: n(f.emailHeaderId), emailFooterId: n(f.emailFooterId) };
    run((id ? api.put<any>(`/api/rodeo/templates/${id}`, body) : api.post<any>('/api/rodeo/templates', body)).then((r) => { onSaved(r.id); return r; }));
  };
  const fill = <><span className="mono">{'{{first_name}}'}</span> <span className="mono">{'{{account}}'}</span> <span className="mono">{'{{balance}}'}</span> <span className="mono">{'{{plan}}'}</span></>;
  return (
    <>
      <PageHead title={id ? t.name : 'New Template'} sub={<>Fill-ins: {fill} and <span className="mono">{'{{survey:slug}}'}</span> for a survey link.</>}
        actions={id ? <><Link className="btn ghost" to={`/rodeo/templates/${id}/heatmap`}>Click Heat Map</Link><Link className="btn cyan" to={`/rodeo/templates/${id}/test`}>Send Test</Link></> : undefined} />
      {bar}
      <div className="grid-2">
        <form className="panel" onSubmit={save}>
          <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
            <label htmlFor="name">Name</label><input id="name" required value={f.name} onChange={(e) => set('name', e.target.value)} />
            <label htmlFor="email_category_id">Category</label><select id="email_category_id" value={f.emailCategoryId} onChange={(e) => set('emailCategoryId', e.target.value)}><option value="">None</option>{data.options.categories.map((c: any) => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
            <label htmlFor="subject">Subject</label><input id="subject" required value={f.subject} onChange={(e) => set('subject', e.target.value)} />
            {(['header', 'footer'] as const).map((kind) => {
              const field = kind === 'header' ? 'emailHeaderId' : 'emailFooterId';
              const def = parts.find((p) => p.kind === kind && p.isDefault);
              return [
                <label key={kind + 'l'} htmlFor={`email_${kind}_id`}>{kind === 'header' ? 'Header' : 'Footer'}</label>,
                <div key={kind}><select id={`email_${kind}_id`} data-part={kind} value={(f as any)[field]} onChange={(e) => set(field, e.target.value)}>
                  <option value="">Default{def ? ` (${def.name})` : ''}</option>{parts.filter((p) => p.kind === kind).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select><div className="help"><Link to="/rodeo/headers-footers">Edit headers &amp; footers</Link></div></div>,
              ];
            })}
            <label htmlFor="b-html">Body (HTML)</label><textarea id="b-html" spellCheck={false} value={f.body} onChange={(e) => set('body', e.target.value)} />
          </div>
          <div className="panel-body"><button className="btn cyan">Save Template</button></div>
        </form>
        <div className="panel"><div className="panel-head"><h2>Preview</h2>{id && data.sample && <span className="muted">for {data.sample.firstName} ({data.sample.account})</span>}</div>
          <div className="panel-body"><HtmlPreview html={part('header', f.emailHeaderId) + f.body + part('footer', f.emailFooterId)} minHeight={520} /></div>
        </div>
      </div>
    </>
  );
}

const TT_CSS = `
.tt-grid { display: grid; grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr); gap: 20px; align-items: start; }
.tt-col { display: grid; gap: 20px; min-width: 0; }
.tt-col > .panel { min-width: 0; }
.tt-col.tt-sticky { position: sticky; top: 72px; }
.tt-step { display: inline-grid; place-items: center; width: 24px; height: 24px; border-radius: 50%; background: var(--navy); color: #fff; font-size: .8rem; margin-right: 8px; }
.tt-modes { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
.tt-mode { position: relative; border: 1px solid var(--line); border-radius: 8px; padding: 10px 12px; cursor: pointer; background: #fff; }
.tt-mode input { position: absolute; opacity: 0; pointer-events: none; }
.tt-mode b { display: block; color: var(--navy); }
.tt-mode small { color: var(--muted); }
.tt-mode:has(input:checked) { border-color: var(--cyan); box-shadow: inset 0 0 0 1px var(--cyan); background: var(--info-bg); }
.tt-mode:has(input:focus-visible) { outline: 2px solid var(--cyan); outline-offset: 2px; }
.tt-pane[hidden] { display: none; }
.tt-list { max-height: 260px; overflow: auto; border-top: 1px solid var(--line); }
.tt-summary { display: flex; flex-wrap: wrap; gap: 10px 18px; align-items: center; justify-content: space-between; }
.tt-meta { display: grid; grid-template-columns: 70px 1fr; gap: 6px 10px; font-size: .9rem; padding: 12px 18px; border-bottom: 1px solid var(--line); }
.tt-meta span { color: var(--muted); }
.tt-frame-wrap { background: var(--row); padding: 16px; display: flex; justify-content: center; }
.tt-frame { width: 100%; height: 520px; border: 1px solid var(--line); border-radius: 6px; background: #fff; transition: width .2s; }
.tt-frame.phone { width: 375px; }
.tt-seg { display: inline-flex; border: 1px solid var(--line); border-radius: 6px; overflow: hidden; }
.tt-seg button { border: 0; background: #fff; padding: 5px 10px; cursor: pointer; color: var(--muted); }
.tt-seg button.on { background: var(--navy); color: #fff; }
.tt-status { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; }
.tt-recips { margin: 0; padding: 0; list-style: none; }
.tt-recips li { padding: 2px 0; }
@media (max-width: 1100px) { .tt-grid { grid-template-columns: 1fr; } .tt-col.tt-sticky { position: static; } .tt-modes { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) { .tt-modes, .tt-status { grid-template-columns: 1fr; } .tt-frame.phone { width: 100%; } }`;

const resultLabels: Record<string, string> = { sent: 'Sent', logged: 'Logged only', failed: 'Failed', partial: 'Partly sent', suppressed: 'Suppressed' };
const resultTones: Record<string, string> = { sent: 'ok', failed: 'bad', partial: 'warn' };

/** Send Test (ebunny's rodeo/template-test): who gets it, options, preview, and the test history. */
export function TemplateTest() {
  useCss('tt-css', TT_CSS);
  const { id } = useParams();
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const result = sp.get('result') ?? '';
  const { data, error, reload } = useLoad<any>(`/api/rodeo/templates/${id}/test?${qs({ result: result || null, page: sp.get('page') })}`);
  const [f, setF] = useState<any>({ mode: 'me', emails: '', accounts: '', roleId: '', status: [] as string[], type: '', marketId: '', limit: 5, sample: '', prefix: '[TEST]', note: '', logContact: true });
  const set = (k: string, v: unknown) => setF((x: any) => ({ ...x, [k]: v }));
  const [check, setCheck] = useState<any>(null);
  const [flash, setFlash] = useState<{ ok: boolean; message: string } | null>(null);
  const [prev, setPrev] = useState<any>(null);
  const [phone, setPhone] = useState(false);
  useEffect(() => { if (data && !f.sample && data.sample) set('sample', data.sample.account); }, [data]); // eslint-disable-line react-hooks/exhaustive-deps
  // Live preview: sample account, prefix and note
  useEffect(() => {
    const t = setTimeout(() => api.post<any>(`/api/rodeo/templates/${id}/test/preview`, { sample: f.sample, prefix: f.prefix, note: f.note }).then(setPrev, () => {}), 350);
    return () => clearTimeout(t);
  }, [id, f.sample, f.prefix, f.note]);
  useEffect(() => setCheck(null), [f.mode]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  // Only the chosen mode's fields, so a hidden pane's values don't confuse the check
  const body = () => {
    const keep: Record<string, string[]> = { me: [], emails: ['emails'], accounts: ['accounts'], bookmarks: [], team: ['roleId'], category: ['status', 'type', 'marketId', 'limit'] };
    const b: any = { mode: f.mode, sample: f.sample, prefix: f.prefix, note: f.note, logContact: f.logContact };
    for (const k of keep[f.mode]) b[k] = f[k];
    if (b.roleId !== undefined) b.roleId = b.roleId ? Number(b.roleId) : null;
    if (b.marketId !== undefined) b.marketId = b.marketId ? Number(b.marketId) : null;
    if (b.limit !== undefined) b.limit = Number(b.limit);
    return b;
  };
  const doCheck = () => { setCheck({ checking: true }); api.post<any>(`/api/rodeo/templates/${id}/test/recipients`, body()).then(setCheck, (e) => setCheck({ error: e.message })); };
  const send = (e: React.FormEvent) => {
    e.preventDefault();
    if (!confirm('Send this test?\nIt goes out now, marked as a test.')) return;
    api.post<any>(`/api/rodeo/templates/${id}/test`, body()).then((r) => { setFlash(r); reload(); }, (x) => setFlash({ ok: false, message: x.message }));
  };
  const again = (t: number) => confirm('Send this test again?\nSame people, same options.') && api.post<any>(`/api/rodeo/templates/${id}/test/${t}/again`).then((r) => { setFlash(r); reload(); }, (x) => setFlash({ ok: false, message: x.message }));
  const modes: [string, string, string][] = [['me', 'Just me', data.me], ['emails', 'Email addresses', 'type one or more'], ['accounts', 'Account numbers', 'their own fill-ins'],
    ['bookmarks', 'My bookmarks', `${data.bookmarks.length} with an email`], ['team', 'An admin team', 'everyone in a role'], ['category', 'Customer category', 'status, type, market']];
  const total = Object.values(data.counts as Record<string, number>).reduce((a, b) => a + b, 0);
  const n = (k: string) => ({ sent: 'sent', logged: 'logged', failed: 'failed', partial: 'partly sent' } as Record<string, string>)[k] ?? k;
  return (
    <>
      <PageHead title={`Send Test: ${data.template.name}`} sub={`Sends this template, as saved, to up to ${data.max} people. The subject gets a test prefix and the email opens with a banner saying who sent the test.`}
        actions={<Link className="btn ghost" to={`/rodeo/templates/${id}`}>Edit Template</Link>} />
      {flash && <div className={`flash ${flash.ok ? 'ok' : 'bad'}`} role="status" style={{ marginBottom: 16 }}>{flash.message}</div>}
      <div className="tt-status" style={{ marginBottom: 20 }}>
        <div className="stat"><span>Delivery</span><strong style={{ fontSize: '1.1rem' }}>{data.delivers ? 'Real email' : 'Mail log only'}</strong><small>{data.delivers ? 'SMTP is set up' : 'set Integrations:Smtp:Host to deliver'}</small></div>
        <div className="stat"><span>From</span><strong style={{ fontSize: '1.1rem' }}>{data.from.name}</strong><small>{data.from.address}</small></div>
        <div className="stat"><span>Tests of this template</span><strong style={{ fontSize: '1.1rem' }}>{total}</strong><small>{total ? Object.entries(data.counts as Record<string, number>).map(([k, v]) => `${v} ${n(k)}`).join(' · ') : 'none yet'}</small></div>
      </div>
      <form id="tt-form" onSubmit={send}>
        <div className="tt-grid">
          <div className="tt-col">
            <div className="panel">
              <div className="panel-head"><h2><span className="tt-step">1</span>Who gets it</h2></div>
              <div className="panel-body" style={{ display: 'grid', gap: 16 }}>
                <div className="tt-modes" role="radiogroup" aria-label="Send to">{modes.map(([k, l, h]) => (
                  <label key={k} className="tt-mode"><input type="radio" name="mode" value={k} checked={f.mode === k} onChange={() => set('mode', k)} /><b>{l}</b><small>{h}</small></label>
                ))}</div>
                <div className="tt-pane" hidden={f.mode !== 'me'}><p className="muted" style={{ margin: 0 }}>Goes to <b>{data.me}</b>, filled in with the sample account below.</p></div>
                <div className="tt-pane form-grid" hidden={f.mode !== 'emails'}>
                  <label htmlFor="t-emails">Addresses</label><textarea id="t-emails" value={f.emails} onChange={(e) => set('emails', e.target.value)} placeholder="name@example.com, other@example.com" style={{ minHeight: 80, fontFamily: 'inherit' }} />
                  <span /><p className="help" style={{ margin: 0 }}>Separate with commas, spaces or new lines. Filled in with the sample account below.</p>
                </div>
                <div className="tt-pane form-grid" hidden={f.mode !== 'accounts'}>
                  <label htmlFor="t-accounts">Accounts</label><textarea id="t-accounts" value={f.accounts} onChange={(e) => set('accounts', e.target.value)} placeholder="1219000000, 1219000001" style={{ minHeight: 80, fontFamily: 'inherit' }} />
                  <span /><p className="help" style={{ margin: 0 }}>Each account gets it at its email on file, with its own name, balance and plan. Accounts with no email are skipped.</p>
                </div>
                <div className="tt-pane" hidden={f.mode !== 'bookmarks'}>
                  {data.bookmarks.length === 0 ? <p className="muted" style={{ margin: 0 }}>You have no bookmarked accounts with an email address. Bookmark accounts in Corral to test with them here.</p>
                    : <><p style={{ margin: '0 0 6px' }}>Your Corral bookmarks with an email address:</p><ul className="tt-recips">{data.bookmarks.map((b: any) => <li key={b.id}>{b.name} <Link className="mono" to={`/corral/customers/${b.id}`}>{b.account}</Link> · {b.email}</li>)}</ul></>}
                </div>
                <div className="tt-pane form-grid" hidden={f.mode !== 'team'}>
                  <label htmlFor="t-role">Team</label><select id="t-role" value={f.roleId} onChange={(e) => set('roleId', e.target.value)}><option value="">Pick a team</option>{data.roles.map((r: any) => <option key={r.id} value={r.id}>{r.name} ({r.users} {r.users === 1 ? 'person' : 'people'})</option>)}</select>
                </div>
                <div className="tt-pane form-grid" hidden={f.mode !== 'category'}>
                  <label>Account status</label><div className="chips small">{data.statuses.map((s: string) => <label key={s}><input type="checkbox" checked={f.status.includes(s)} onChange={() => set('status', f.status.includes(s) ? f.status.filter((x: string) => x !== s) : [...f.status, s])} /><span>{s}</span></label>)}</div>
                  <label htmlFor="t-type">Customer type</label><select id="t-type" value={f.type} onChange={(e) => set('type', e.target.value)}><option value="">Any</option>{['Residential', 'Small Business'].map((t) => <option key={t}>{t}</option>)}</select>
                  <label htmlFor="t-market">Market</label><select id="t-market" value={f.marketId} onChange={(e) => set('marketId', e.target.value)}><option value="">Any</option>{data.markets.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select>
                  <label htmlFor="t-limit">How many</label><input id="t-limit" type="number" min={1} max={data.max} value={f.limit} onChange={(e) => set('limit', e.target.value)} style={{ maxWidth: '8ch' }} />
                  <span /><p className="help" style={{ margin: 0 }}>These are real customers. Each gets their own fill-ins.</p>
                </div>
                <div className="tt-summary"><span className="muted" id="tt-count">
                  {!check ? 'Check who this will go to before sending.' : check.checking ? 'Checking…' : check.error ? <span style={{ color: 'var(--bad)' }}>{check.error}</span>
                    : <><b>{check.count}</b> {check.count === 1 ? 'person' : 'people'}{check.target && ` · ${check.target}`}{check.count > check.max && <span style={{ color: 'var(--bad)' }}> — over the limit of {check.max}; narrow it down</span>}</>}
                </span><button type="button" className="btn sm ghost" id="tt-check" onClick={doCheck}>Check Recipients</button></div>
              </div>
              {check?.recipients && (
                <div className="tt-list" id="tt-list"><div className="table-wrap"><table className="table" data-page="0"><thead><tr><th>Name</th><th>Email</th><th>Account</th><th>Fill-ins From</th></tr></thead>
                  <tbody>{check.recipients.map((r: any, i: number) => <tr key={i}><td>{r.name || '—'}</td><td>{r.email}</td><td className="mono">{r.account || '—'}</td><td className="muted">{r.fills}</td></tr>)}
                    {check.recipients.length === 0 && <EmptyRow cols={4}>Nobody with an email address matches.</EmptyRow>}</tbody></table></div></div>
              )}
            </div>
            <div className="panel">
              <div className="panel-head"><h2><span className="tt-step">2</span>Options</h2></div>
              <div className="panel-body form-grid">
                <label htmlFor="t-sample">Sample account</label><div><input id="t-sample" value={f.sample} onChange={(e) => set('sample', e.target.value)} inputMode="numeric" style={{ maxWidth: '16ch' }} /> <span className="muted" id="tt-sample-name">{prev ? (prev.customer.found ? prev.customer.name : 'not found') : data.sample?.name}</span></div>
                <span /><p className="help" style={{ margin: 0 }}>Fills in the name, balance and plan for people who aren't customers (Just me, typed addresses, admin teams), and for the preview.</p>
                <label htmlFor="t-prefix">Subject prefix</label><input id="t-prefix" value={f.prefix} onChange={(e) => set('prefix', e.target.value)} maxLength={30} style={{ maxWidth: '20ch' }} />
                <label htmlFor="t-note">Note in the banner</label><textarea id="t-note" value={f.note} onChange={(e) => set('note', e.target.value)} maxLength={500} placeholder="e.g. Checking the new logo and links" style={{ minHeight: 60, fontFamily: 'inherit' }} />
                <span /><div><label className="check"><input type="checkbox" checked={f.logContact} onChange={(e) => set('logContact', e.target.checked)} /> Add the test to each customer's Contact Log</label></div>
              </div>
            </div>
            <div className="panel">
              <div className="panel-head"><h2><span className="tt-step">3</span>Send</h2></div>
              <div className="panel-body tt-summary">
                <p style={{ margin: 0 }}>{data.delivers ? 'Sends real email now.' : 'Written to the mail log (not delivered) until an SMTP host is set.'} Up to {data.max} people.</p>
                <button className="btn cyan">Send Test</button>
              </div>
            </div>
          </div>
          <div className="tt-col tt-sticky">
            <div className="panel">
              <div className="panel-head"><h2>Preview</h2><div className="tt-seg" role="group" aria-label="Preview width">
                <button type="button" className={phone ? '' : 'on'} onClick={() => setPhone(false)}>Desktop</button><button type="button" className={phone ? 'on' : ''} onClick={() => setPhone(true)}>Phone</button></div></div>
              <div className="tt-meta"><span>From</span><div>{data.from.name} &lt;{data.from.address}&gt;</div><span>Subject</span><div><b id="tt-subject">{prev?.subject ?? '…'}</b></div>
                <span>As</span><div id="tt-as">{prev ? <>{prev.customer.name} <span className="mono muted">{prev.customer.account}</span>{!prev.customer.found && <span className="muted"> (that account wasn't found; showing the first account)</span>}</> : '…'}</div></div>
              <div className="tt-frame-wrap"><iframe className={`tt-frame${phone ? ' phone' : ''}`} id="tt-frame" title="Email preview" sandbox=""
                srcDoc={prev ? `<!doctype html><html><head><meta charset="utf-8"><style>body{margin:0;font-family:Arial,sans-serif}</style></head><body>${prev.html}</body></html>` : ''} /></div>
            </div>
          </div>
        </div>
      </form>
      <div className="panel" id="history" style={{ marginTop: 20 }}>
        <div className="panel-head"><h2>Test History</h2>
          <div className="actions">{[['', 'All'], ['sent', 'Sent'], ['logged', 'Logged only'], ['failed', 'Failed'], ['partial', 'Partly sent']].map(([v, l]) => (
            <Link key={v} className={`btn sm ${result === v ? '' : 'ghost'}`} to={`/rodeo/templates/${id}/test${v ? `?result=${v}` : ''}`}>{l}</Link>))}</div></div>
        <div className="table-wrap"><table className="table" data-page="0">
          <thead><tr><th>ID</th><th>When</th><th>By</th><th>Sent To</th><th>Subject &amp; Note</th><th>Recipients</th><th>Result</th><th></th></tr></thead>
          <tbody>
            {data.tests.map((t: any) => (
              <tr key={t.id}><td className="nowrap"><Link to={`/rodeo/records/email_test/${t.id}`}><b>#{t.id}</b></Link></td><td className="nowrap">{when(t.createdAt)}</td><td>{t.by}</td>
                <td>{data.modes[t.mode] ?? t.mode}{t.target && <div className="muted">{t.target.length > 70 ? t.target.slice(0, 67) + '...' : t.target}</div>}</td>
                <td>{t.subject ?? '—'}{t.note && <div className="muted">“{t.note.length > 80 ? t.note.slice(0, 77) + '...' : t.note}”</div>}</td>
                <td><details><summary>{t.recipients.length} {t.recipients.length === 1 ? 'person' : 'people'}</summary><ul className="tt-recips" style={{ marginTop: 6 }}>{t.recipients.map((r: any, i: number) => (
                  <li key={i}>{r.email}{r.account && <> <span className="mono muted">{r.account}</span></>} <Pill tone={({ sent: 'ok', failed: 'bad' } as Record<string, string>)[r.status] ?? ''}>{({ sent: 'Sent', logged: 'Logged', failed: 'Failed', suppressed: 'Suppressed' } as Record<string, string>)[r.status] ?? '—'}</Pill>
                    {r.error && <span className="muted"> {r.error}</span>}</li>
                ))}</ul></details></td>
                <td><Pill tone={resultTones[t.status] ?? ''}>{resultLabels[t.status] ?? t.status}</Pill></td>
                <td className="actions">{t.again && <button className="btn sm ghost" onClick={() => again(t.id)}>Send Again</button>}</td></tr>
            ))}
            {data.tests.length === 0 && <EmptyRow cols={8}>No tests {result ? 'with that result' : 'yet'}.</EmptyRow>}
          </tbody>
        </table></div>
        <PagerBar page={data.page} total={data.total} size={15} onPage={(p) => navigate(`/rodeo/templates/${id}/test?${qs({ result: result || null, page: p })}`)} />
      </div>
    </>
  );
}

/** Rodeo → Headers & Footers (ebunny's rodeo/parts). */
export function Parts() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any>('/api/rodeo/headers-footers');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Headers & Footers" sub="The top and bottom of every email. Each template picks a header and a footer; templates that don't pick one use the default."
        actions={<><Link className="btn ghost" to="/rodeo/headers-footers/new?kind=header">New Header</Link><Link className="btn cyan" to="/rodeo/headers-footers/new?kind=footer">New Footer</Link></>} />
      {(['header', 'footer'] as const).map((kind) => {
        const label = kind === 'header' ? 'Header' : 'Footer';
        return (
          <div key={kind} className="panel"><div className="panel-head"><h2>{label}s</h2></div><div className="table-wrap"><table>
            <thead><tr><th>Name</th><th>Default</th><th className="num">Templates Using It</th><th>Changed</th></tr></thead>
            <tbody>
              {data[kind].map((p: any) => (
                <tr key={p.id} className="click" onClick={() => navigate(`/rodeo/headers-footers/${p.id}`)}><td><Link to={`/rodeo/headers-footers/${p.id}`}><b>{p.name}</b></Link>{p.empty && <div className="help">Empty: the email has no {kind}</div>}</td>
                  <td>{p.isDefault && <Pill tone="ok">Default</Pill>}</td><td className="num">{p.templates}</td><td className="nowrap">{day(p.updatedAt)}</td></tr>
              ))}
              {data[kind].length === 0 && <EmptyRow cols={4}>No {kind}s yet.</EmptyRow>}
            </tbody>
          </table></div></div>
        );
      })}
    </>
  );
}

/** New Header / New Footer / a header or footer's page (ebunny's rodeo/part-form). */
export function PartForm() {
  const { id } = useParams();
  const [sp] = useSearchParams();
  const { data, error } = useLoad<any>(id ? `/api/rodeo/headers-footers/${id}` : `/api/rodeo/headers-footers/new?${qs({ kind: sp.get('kind') })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <PartEditor data={data} id={id} key={(id ?? 'new') + data.part.kind} />;
}

function PartEditor({ data, id }: { data: any; id?: string }) {
  const navigate = useNavigate();
  const p = data.part, label = p.kind === 'header' ? 'Header' : 'Footer';
  const [f, setF] = useState({ name: p.name, html: p.html, isDefault: p.isDefault });
  const [run, bar] = useRun();
  const save = (e: React.FormEvent) => {
    e.preventDefault();
    const body = { ...f, kind: p.kind };
    run((id ? api.put<any>(`/api/rodeo/headers-footers/${id}`, body) : api.post<any>('/api/rodeo/headers-footers', body)).then((r) => { if (!id) navigate(`/rodeo/headers-footers/${r.id}`); return r; }));
  };
  const remove = () => confirm(`Delete ${p.name}?\nTemplates using it switch to the default ${p.kind}.`) && run(api.del(`/api/rodeo/headers-footers/${id}`).then((r) => { navigate('/rodeo/headers-footers'); return r; }));
  return (
    <>
      <PageHead title={id ? p.name : `New ${label}`} sub={<>Email {p.kind}. Fill-ins work here too: <span className="mono">{'{{first_name}}'}</span> <span className="mono">{'{{account}}'}</span> <span className="mono">{'{{balance}}'}</span> <span className="mono">{'{{plan}}'}</span>.</>}
        actions={<><Link className="btn ghost btn-back" to="/rodeo/headers-footers">Headers &amp; Footers</Link>{id && !p.isDefault && <button className="btn red" onClick={remove}>Delete</button>}</>} />
      {bar}
      <div className="grid-2">
        <form className="panel" onSubmit={save}>
          <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
            <label htmlFor="name">Name</label><input id="name" required maxLength={100} value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} />
            <label htmlFor="b-html">{label} (HTML)</label><textarea id="b-html" spellCheck={false} style={{ minHeight: 260 }} value={f.html} onChange={(e) => setF({ ...f, html: e.target.value })} />
            <label className="check"><input type="checkbox" checked={f.isDefault} disabled={p.isDefault} onChange={(e) => setF({ ...f, isDefault: e.target.checked })} /> Default {p.kind} (for templates that don't pick one)</label>
          </div>
          <div className="panel-body"><button className="btn cyan">Save {label}</button></div>
        </form>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20, minWidth: 0 }}>
          <div className="panel"><div className="panel-head"><h2>Preview</h2></div><div className="panel-body"><HtmlPreview html={f.html} minHeight={240} /></div></div>
          {id && (
            <div className="panel"><div className="panel-head"><h2>Templates Using It</h2><span className="muted">{data.templates.length}</span></div><div className="table-wrap"><table data-page="0"><tbody>
              {data.templates.map((t: any) => <tr key={t.id} className="click" onClick={() => navigate(`/rodeo/templates/${t.id}`)}><td><Link to={`/rodeo/templates/${t.id}`}>{t.name}</Link></td><td className="help">{t.picked ? 'picked' : 'by default'}</td></tr>)}
              {data.templates.length === 0 && <tr><td className="empty">No templates use it yet.</td></tr>}
            </tbody></table></div></div>
          )}
        </div>
      </div>
    </>
  );
}

/** Rodeo → Emails → Categories (ebunny's rodeo/emails/categories). */
export function Categories() {
  const { data, error, reload } = useLoad<any>('/api/rodeo/emails/categories');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Email Categories" sub="Groups email templates are filed under. The Templates list and the Emails overview are grouped by these, in this order."
        actions={<Link className="btn cyan" to="/rodeo/emails/categories/new">New Category</Link>} />
      {bar}
      <div className="panel" style={{ marginBottom: 20 }}><div className="table-wrap"><table className="table">
        <thead><tr><th className="num">Order</th><th>Category</th><th>Description</th><th>Templates</th><th></th></tr></thead>
        <tbody>
          {data.categories.map((c: any) => (
            <tr key={c.id}><td className="num">{c.position}</td><td className="nowrap"><Cat c={c} /></td><td className="wrap">{c.description}</td>
              <td className="wrap">{c.templates.length ? c.templates.map((t: any, i: number) => <span key={t.id}><Link to={`/rodeo/templates/${t.id}`}>{t.name}</Link>{i < c.templates.length - 1 && ', '}</span>) : <span className="muted">None</span>}</td>
              <td className="actions"><Link className="btn sm ghost" to={`/rodeo/emails/categories/${c.id}/edit`}>Edit</Link>
                <button className="btn sm ghost" onClick={() => confirm(`Delete ${c.name}?\nIts ${c.templates.length} ${c.templates.length === 1 ? 'template' : 'templates'} become uncategorized; nothing else changes.`) && run(api.del(`/api/rodeo/emails/categories/${c.id}`))}>Delete</button></td></tr>
          ))}
          {data.categories.length === 0 && <EmptyRow cols={5}>No categories yet.</EmptyRow>}
        </tbody>
      </table></div></div>
      {data.uncategorized.length > 0 && (
        <div className="panel"><div className="panel-head"><h2>Uncategorized</h2><span className="muted">{data.uncategorized.length}</span></div>
          <div className="panel-body">{data.uncategorized.map((t: any, i: number) => <span key={t.id}><Link to={`/rodeo/templates/${t.id}`}>{t.name}</Link>{i < data.uncategorized.length - 1 && ', '}</span>)}</div></div>
      )}
    </>
  );
}

/** New Category / Edit Category (ebunny's rodeo/emails/category-form). */
export function CategoryForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/emails/categories/form${id ? `?id=${id}` : ''}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.category;
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const body = { name: fd.get('name'), description: fd.get('description'), color: fd.get('color'), position: fd.get('position') ? Number(fd.get('position')) : null, templates: fd.getAll('templates').map(Number) };
    run((id ? api.put(`/api/rodeo/emails/categories/${id}`, body) : api.post('/api/rodeo/emails/categories', body)).then((r) => { navigate('/rodeo/emails/categories'); return r; }));
  };
  return (
    <>
      <PageHead title={id ? 'Edit Category' : 'New Category'} sub="A group email templates are filed under." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={60} defaultValue={c.name} />
          <label htmlFor="description">Description</label><input id="description" name="description" maxLength={200} defaultValue={c.description ?? ''} />
          <label htmlFor="color">Color</label><input id="color" type="color" name="color" defaultValue={c.color} style={{ height: 38, padding: 2, maxWidth: 80 }} />
          <label htmlFor="position">Order</label><input id="position" type="number" name="position" min={0} max={999} defaultValue={c.position ?? ''} placeholder="Last" style={{ maxWidth: '10ch' }} />
          <label>Templates</label>
          <div className="check-list">{data.templates.map((t: any) => (
            <label key={t.id} className="check"><input type="checkbox" name="templates" value={t.id} defaultChecked={id != null && t.emailCategoryId === Number(id)} /> {t.name}
              {t.category && t.emailCategoryId !== Number(id) && <span className="muted"> · now in {t.category}</span>}</label>
          ))}</div>
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save Category' : 'Add Category'}</button><Link className="btn ghost" to="/rodeo/emails/categories">Cancel</Link></div>
      </form>
    </>
  );
}

/** Rodeo → Suppression List (ebunny's rodeo/emails/suppressions). */
export function Suppressions() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/rodeo/emails/suppressions?${sp}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Suppression List" sub="No email goes to these addresses: not account emails from Corral, not campaigns, not tests. Remove an address to let emails go to it again."
        actions={<Link className="btn cyan" to="/rodeo/emails/suppressions/new">Add Address</Link>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table className="table" data-page="0">
        <thead><tr><th>Email</th><th>Account</th><th>Reason</th><th>Note</th><th>Added</th><th></th></tr></thead>
        <tbody>
          {data.list.map((s: any) => (
            <tr key={s.id}><td>{s.email}</td>
              <td>{s.customer ? <><Link className="mono" to={`/rodeo/emails/sent?${qs({ account: s.customer.account })}`} title="Every email to this account">{s.customer.account}</Link><div className="muted">{s.customer.name}</div></> : <span className="muted">—</span>}</td>
              <td><Pill tone={['bounce', 'complaint'].includes(s.reason) ? 'bad' : ''}>{s.reasonLabel}</Pill></td><td className="wrap">{s.note}</td>
              <td className="nowrap">{day(s.createdAt)}<div className="muted">{s.by ?? 'Mail service'}</div></td>
              <td className="actions"><button className="btn sm ghost" onClick={() => confirm(`Remove ${s.email}?\nEmails can go to this address again.`) && run(api.del(`/api/rodeo/emails/suppressions/${s.id}`))}>Remove</button></td></tr>
          ))}
          {data.list.length === 0 && <EmptyRow cols={6}>No addresses {data.filtered ? 'match' : 'on the list'}.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={data.page} total={data.total} size={50} onPage={(p) => navigate(`/rodeo/emails/suppressions?${qs({ ...Object.fromEntries(sp), page: p })}`)} /></div>
    </>
  );
}

/** Add to Suppression List (ebunny's rodeo/emails/suppression-form). */
export function SuppressionForm() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const [run, bar] = useRun();
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    run(api.post('/api/rodeo/emails/suppressions', Object.fromEntries(new FormData(e.currentTarget))).then((r) => { navigate('/rodeo/emails/suppressions'); return r; }));
  };
  const reasons: Record<string, string> = { bounce: 'Hard bounce', unsubscribe: 'Unsubscribed', complaint: 'Spam complaint', manual: 'Added by hand' };
  return (
    <>
      <PageHead title="Add to Suppression List" sub="No email will go to this address until it is removed from the list." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="email">Email</label><input id="email" name="email" type="email" required maxLength={150} defaultValue={sp.get('email') ?? ''} />
          <label htmlFor="reason">Reason</label><select id="reason" name="reason" defaultValue="manual">{Object.entries(reasons).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
          <label htmlFor="note">Note</label><input id="note" name="note" maxLength={250} placeholder="Optional, e.g. asked on the phone to stop emails" />
        </div>
        <div className="panel-body actions"><button className="btn cyan">Add Address</button><Link className="btn ghost" to="/rodeo/emails/suppressions">Cancel</Link></div>
      </form>
    </>
  );
}

