import { type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, qs, useLoad } from '../../api';
import { DateInput } from '../../fields';
import { AccountNo, EmptyRow, PageHead, PagerBar, age, shortTime, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const slash = (p: string) => '/' + (p === '/' ? '' : p.replace(/^\//, ''));
const hourLabel = (h: number) => `${h % 12 || 12}${h < 12 ? 'am' : 'pm'}`;
const host = (u: string | null) => { if (!u) return null; try { return new URL(u).host; } catch { return u; } };
const plural = (n: number, w: string) => `${n.toLocaleString()} ${w}${n === 1 ? '' : 's'}`;

/** ebunny's lando/site/_checks: site health checks, grouped. compact hides the "what to do" column. */
export function ChecksTable({ checks, compact }: { checks: any[]; compact?: boolean }) {
  const groups = [...new Set(checks.map((c) => c.group))];
  const label: Record<string, string> = { ok: 'OK', warn: 'Needs a look', bad: 'Problem' };
  return (
    <div className="table-wrap"><table className="table health" data-page="0">
      <tbody>{groups.map((g) => [
        <tr key={g} className="health-group"><th colSpan={compact ? 3 : 4}>{g}</th></tr>,
        ...checks.filter((c) => c.group === g).map((c) => (
          <tr key={g + c.name}><td style={{ width: 34 }}><span className={`health-dot ${c.state}`} title={label[c.state]} /></td>
            <td className="nowrap"><b>{c.name}</b></td>
            <td className="wrap">{c.found}{compact && c.fix && <div className="muted" style={{ fontSize: '.8rem' }}>{c.fix}</div>}</td>
            {!compact && <td className="wrap muted">{c.fix}</td>}</tr>
        )),
      ])}</tbody>
    </table></div>
  );
}

/** Lando's home (ebunny's lando/site/dashboard): who is on the website, site health, what's blocked. */
export default function Dashboard() {
  const { data, error } = useLoad<any>('/api/lando/dashboard');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const today = new Date().toLocaleDateString('en-CA');
  const top = Math.max(1, ...data.hours.map((h: any) => Math.max(h.today, h.yesterday)));
  return (
    <>
      <PageHead title="Website Dashboard" sub="Site health, who is on the website, and what is blocked. Visitors count as online while a page is open and showing (it checks in every minute)."
        actions={<><Link className="btn ghost" to="/lando/visitors">Visitors</Link><Link className="btn cyan" to="/lando/pages">List Pages</Link></>} />
      <div className="grid-4 stat-row">
        <Link className="stat" to="/lando/visitors?who=online"><span>On the Site Now</span><strong>{data.online.toLocaleString()}</strong><small>{data.onlineCustomers} signed in to My Account</small></Link>
        <Link className="stat" to={`/lando/visitors?start=${today}`}><span>Visitors Today</span><strong>{data.visitorsToday.toLocaleString()}</strong><small>{data.viewsToday.toLocaleString()} page views · {data.visitors30.toLocaleString()} in 30 days</small></Link>
        <div className="stat"><span>My Account Users</span><strong>{data.accounts.toLocaleString()}</strong><small>of {data.customers.toLocaleString()} customers · {data.signedInToday} signed in today</small></div>
        <Link className="stat" to="/lando/health"><span>Site Health</span><strong style={{ color: `var(--${data.problems ? 'warn' : 'ok'})` }}>{data.problems ? `${data.problems} to look at` : 'All good'}</strong><small>{data.checkCount} checks</small></Link>
      </div>
      <div className="grid-4 stat-row">
        <Link className="stat" to="/lando/blocked-ips"><span>Blocked IPs</span><strong>{data.blockedIps}</strong></Link>
        <Link className="stat" to="/lando/blocked-areas"><span>Blocked Areas</span><strong>{data.blockedAreas}</strong></Link>
        <Link className="stat" to={`/lando/visitors?blocked=1&start=${today}`}><span>Turned Away Today</span><strong>{data.blockedToday.toLocaleString()}</strong><small>by a blocked IP or area</small></Link>
        <Link className="stat" to="/lando/pages"><span>Pages</span><strong>{data.pages}</strong><small>published</small></Link>
      </div>

      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head"><h2>Page Views by Hour</h2><span className="muted">today and yesterday</span></div>
        <div className="panel-body">
          <div className="hour-chart">{data.hours.map((h: any) => (
            <div key={h.hour} className="col" tabIndex={0} data-tip={`${hourLabel(h.hour)}: ${h.today} today, ${h.yesterday} yesterday`}>
              <div className="bars"><i style={{ height: `${Math.round((h.yesterday / top) * 100)}%` }} /><b style={{ height: `${Math.round((h.today / top) * 100)}%` }}><em>{h.today}</em></b></div>
              <small>{h.hour % 2 === 0 ? hourLabel(h.hour) : ''}</small>
            </div>
          ))}</div>
          <div className="hour-legend"><span><b /> Today</span><span><i /> Yesterday</span></div>
        </div>
      </div>

      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head"><h2>On the Site Now</h2><span className="actions"><span className="muted">{data.online.toLocaleString()} online · top 10</span><Link className="btn sm ghost" to="/lando/visitors?who=online">All Visitors Online</Link></span></div>
        <div className="table-wrap"><table>
          <thead><tr><th>Who</th><th>On Page</th><th className="num">Pages Today</th><th>Arrived</th><th>Last Seen</th><th>Came From</th><th>Device</th><th>IP</th></tr></thead>
          <tbody>
            {data.onSite.map((o: any) => {
              const url = `/lando/visitors?visitor=${o.visitor}`;
              return (
                <tr key={o.id} className="click" onClick={() => navigate(url)}>
                  <td>{o.customer ? <><Link to={url}><b>{o.customer}</b></Link><div className="help" onClick={(e) => e.stopPropagation()}><Link className="mono" to={`/corral/customers/${o.customerId}`}>account</Link> · signed in</div></>
                    : <><Link to={url}><b>Visitor</b></Link><div className="help mono">{o.visitor.slice(0, 10)}…</div></>}</td>
                  <td className="path-cell">{slash(o.path)}{o.trail.length > 0 && <div className="help">before: {o.trail.map(slash).join(', ')}</div>}</td>
                  <td className="num">{o.pages}</td>
                  <td className="nowrap">{new Date(o.arrived).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}<div className="help">{age(o.arrived)} on site</div></td>
                  <td className="nowrap">{age(o.seenAt)} ago</td>
                  <td className="wrap">{host(o.from) ?? 'Direct'}</td>
                  <td className="nowrap">{o.device}<div className="help">{o.browser}</div></td>
                  <td className="mono">{o.ip}</td></tr>
              );
            })}
            {data.onSite.length === 0 && <EmptyRow cols={8}>Nobody is on the website right now.</EmptyRow>}
          </tbody>
        </table></div>
      </div>

      <div className="grid-2" style={{ marginBottom: 20 }}>
        <div className="panel"><div className="panel-head"><h2>Site Health</h2><Link className="btn sm ghost" to="/lando/health">Details</Link></div><ChecksTable checks={data.checks} compact /></div>
        <div className="panel"><div className="panel-head"><h2>Top Pages Today</h2></div>
          <div className="table-wrap"><table><thead><tr><th>Page</th><th className="num">Views</th><th className="num">People</th></tr></thead><tbody>
            {data.topPages.map((r: any) => <tr key={r.path}><td className="path-cell">{r.pageId ? <Link to={`/lando/pages/${r.pageId}/activity`}>{r.path}</Link> : r.path}</td><td className="num">{r.n}</td><td className="num">{r.people}</td></tr>)}
            {data.topPages.length === 0 && <EmptyRow cols={3}>No views yet today.</EmptyRow>}
          </tbody></table></div>
        </div>
      </div>
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Recently Turned Away</h2><Link className="btn sm ghost" to="/lando/visitors?blocked=1">All</Link></div>
          <div className="table-wrap"><table><tbody>
            {data.recentBlocks.map((v: any) => <tr key={v.id}><td className="nowrap">{shortTime(v.createdAt)}</td><td className="mono">{v.ip}</td><td>{v.path}</td></tr>)}
            {data.recentBlocks.length === 0 && <tr><td className="empty">Nobody.</td></tr>}
          </tbody></table></div>
        </div>
        <div className="panel"><div className="panel-head"><h2>Latest Website Changes</h2></div>
          <div className="table-wrap"><table><tbody>
            {data.changes.map((h: any) => <tr key={h.id}><td className="nowrap">{shortTime(h.createdAt)}</td><td>{h.who}</td><td className="wrap">{h.summary}</td></tr>)}
            {data.changes.length === 0 && <tr><td className="empty">No changes recorded.</td></tr>}
          </tbody></table></div>
        </div>
      </div>
    </>
  );
}

/** Lando → Site Health (ebunny's lando/site/health). */
export function Health() {
  const { data, error, reload } = useLoad<any>('/api/lando/health');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const n = (s: string) => data.checks.filter((c: any) => c.state === s).length;
  return (
    <>
      <PageHead title="Site Health" sub={`Checked just now: ${n('ok')} OK, ${n('warn')} to look at, ${n('bad')} ${n('bad') === 1 ? 'problem' : 'problems'}.`}
        actions={<button className="btn ghost" onClick={reload}>Check Again</button>} />
      <div className="panel" style={{ marginBottom: 20 }}><ChecksTable checks={data.checks} /></div>
      <div className="panel"><div className="panel-head"><h2>About This Server</h2></div>
        <div className="table-wrap"><table data-page="0"><tbody>{Object.entries(data.about).map(([k, v]) => <tr key={k}><td style={{ width: 220 }}>{k}</td><td className="mono">{v as string}</td></tr>)}</tbody></table></div>
      </div>
    </>
  );
}

/** Lando → Visitors (ebunny's lando/site/visitors): every page view, with a search. */
export function Visitors() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/lando/visitors?${params}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const get = (k: string) => params.get(k) ?? '';
  const search = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), next = new URLSearchParams();
    for (const [k, v] of f.entries()) if (v) next.set(k, String(v));
    navigate(`/lando/visitors?${next}`);
  };
  const filtered = ['who', 'start', 'end', 'ip', 'path', 'blocked', 'visitor'].some((k) => get(k));
  return (
    <>
      <PageHead title="Visitors" sub={`${plural(data.total, 'page view')} · ${plural(data.online, 'visitor')} on the site now. Each browser gets its own visitor id.`} />
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={search} key={params.toString()}><div className="panel-head"><h2>Search Visitors</h2></div><div className="panel-body">
            <div className="field"><label htmlFor="who">Show</label><select id="who" name="who" defaultValue={get('who')}><option value="">Everyone</option><option value="online">On the site now</option><option value="customers">Signed in to My Account</option></select></div>
            <div className="field"><label htmlFor="start">Start</label><DateInput id="start" name="start" defaultValue={get('start')} /></div>
            <div className="field"><label htmlFor="end">End</label><DateInput id="end" name="end" defaultValue={get('end')} /></div>
            <div className="field"><label htmlFor="ip">IP Address</label><input id="ip" name="ip" defaultValue={get('ip')} placeholder="203.0.113.7 or 203.0.113.*" /></div>
            <div className="field"><label htmlFor="path">Page</label><input id="path" name="path" defaultValue={get('path')} placeholder="plans" /></div>
            <div className="field"><label htmlFor="blocked">Blocked</label><select id="blocked" name="blocked" defaultValue={get('blocked')}><option value="">Either</option><option value="0">Let in</option><option value="1">Turned away</option></select></div>
            {get('visitor') && <><input type="hidden" name="visitor" value={get('visitor')} /><p className="muted" style={{ margin: 0 }}>One visitor: <span className="mono">{get('visitor').slice(0, 10)}…</span></p></>}
            <button className="btn">Search</button>{filtered && <Link className="btn ghost" to="/lando/visitors">Clear</Link>}
          </div></form>
        </aside>
        <div className="search-main">
          <div className="panel"><div className="table-wrap"><table className="table">
            <thead><tr><th>ID</th><th>When</th><th>Page</th><th>Visitor</th><th>Status</th><th /></tr></thead>
            <tbody>
              {data.visits.map((v: any) => {
                const stayed = (new Date(v.seenAt).getTime() - new Date(v.createdAt).getTime()) > 60000;
                return (
                  <tr key={v.id}><td className="nowrap"><Link to={`/lando/records/site_visit/${v.id}`}><b>#{v.id}</b></Link></td>
                    <td className="nowrap">{dateTime(v.createdAt)}{stayed && <div className="muted" style={{ fontSize: '.78rem' }}>stayed {age(v.createdAt, v.seenAt)}</div>}</td>
                    <td>{v.pageId ? <Link to={`/lando/pages/${v.pageId}/activity`}>{v.path}</Link> : v.path}{v.referrer && <div className="muted" style={{ fontSize: '.78rem' }}>from {v.referrer}</div>}</td>
                    <td><span className="mono">{v.ip}</span>{v.customer && <div>{v.customer} <AccountNo id={v.customerId} account={v.account} /></div>}
                      <div><Link className="muted" style={{ fontSize: '.78rem' }} to={`/lando/visitors?visitor=${v.visitor}`}>all pages this visitor saw</Link></div></td>
                    <td>{v.blocked ? <span className="pill bad">Turned away</span> : v.on ? <span className="pill ok">On now</span> : <span className="muted">—</span>}</td>
                    <td className="actions">{v.block ? <Link className="btn sm ghost" to={`/lando/blocked-ips/${v.block}/edit`}>Blocked</Link> : <Link className="btn sm ghost" to={`/lando/blocked-ips/new?value=${v.ip}`}>Block IP</Link>}</td></tr>
                );
              })}
              {data.visits.length === 0 && <EmptyRow cols={6}>No visits match.</EmptyRow>}
            </tbody>
          </table></div>
            <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => navigate(`/lando/visitors?${qs({ ...Object.fromEntries(params), page: p })}`)} /></div>
        </div>
      </div>
    </>
  );
}

/** Lando → Blocked IPs / Blocked Areas (ebunny's lando/site/blocked). */
export function Blocked({ kind }: { kind: 'ips' | 'areas' }) {
  const { data, error, reload } = useLoad<any[]>(`/api/lando/blocked/${kind}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const ip = kind === 'ips';
  return (
    <>
      <PageHead title={ip ? 'Blocked IPs' : 'Blocked Areas'}
        sub={ip ? 'Visitors from these addresses see “Not available” instead of the website. The admin is never blocked.'
          : "Nobody can open these parts of the website: a path covers itself and everything under it. The home page and the admin can't be blocked."}
        actions={<Link className="btn cyan" to={`/lando/blocked-${kind}/new`}>{ip ? 'Block an IP' : 'Block an Area'}</Link>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table className="table">
        <thead><tr><th>{ip ? 'IP Address' : 'Area'}</th><th>Reason</th><th>Until</th><th className="num">Turned Away</th><th>{ip ? 'Last Visit' : 'Pages Covered'}</th><th>Status</th><th /></tr></thead>
        <tbody>
          {data.map((b) => (
            <tr key={b.id}><td className="mono">{ip ? b.value : '/' + b.value}</td>
              <td className="wrap">{b.reason ?? '—'}<div className="muted" style={{ fontSize: '.78rem' }}>by {b.by ?? 'System'}, {new Date(b.createdAt).toLocaleDateString('en-US')}</div></td>
              <td className="nowrap">{b.expiresAt ? dateTime(b.expiresAt) : 'Until unblocked'}</td>
              <td className="num">{b.hits.toLocaleString()}{b.lastHitAt && <div className="muted" style={{ fontSize: '.78rem' }}>last {age(b.lastHitAt)} ago</div>}</td>
              <td>{ip ? (b.lastSeen ? <><Link to={`/lando/visitors?ip=${b.value}`}>{new Date(b.lastSeen.at).toLocaleDateString('en-US')}</Link> <span className="muted">· {b.lastSeen.n}</span></>
                : b.value.endsWith('*') ? <Link to={`/lando/visitors?ip=${encodeURIComponent(b.value)}`}>Search</Link> : <span className="muted">—</span>) : plural(b.covers, 'page')}</td>
              <td>{b.inForce ? <span className="pill bad">Blocked</span> : <span className="pill">{b.active ? 'Expired' : 'Off'}</span>}</td>
              <td className="actions"><Link className="btn sm ghost" to={`/lando/blocked-${kind}/${b.id}/edit`}>Edit</Link>
                <button className="btn sm ghost" onClick={() => run(api.post(`/api/lando/blocked/${kind}/${b.id}/toggle`))}>{b.active ? 'Unblock' : 'Block'}</button>
                <button className="btn sm ghost" onClick={() => confirm(`Remove ${b.value}?\nIt comes off the list for good. Use Unblock to keep it for later.`) && run(api.del(`/api/lando/blocked/${kind}/${b.id}`))}>Remove</button></td></tr>
          ))}
          {data.length === 0 && <EmptyRow cols={7}>Nothing blocked.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** Block an IP / Area, or edit one (ebunny's lando/site/block-form). */
export function BlockForm({ kind }: { kind: 'ips' | 'areas' }) {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/lando/blocked/${kind}/form${id ? `?id=${id}` : ''}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const ip = kind === 'ips', label = ip ? 'IP' : 'Area';
  const b = data.block ?? { value: params.get('value') ?? '', active: true };
  const list = `/lando/blocked-${kind}`;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) || null;
    const body = { value: v('value'), reason: v('reason'), message: v('message'), expiresAt: v('expires_at'), active: f.get('active') === 'on' };
    run((id ? api.put(`/api/lando/blocked/${kind}/${id}`, body) : api.post(`/api/lando/blocked/${kind}`, body)).then((r) => { navigate(list); return r; }));
  };
  const views = data.views;
  return (
    <>
      <PageHead title={id ? `Edit Blocked ${label}` : `Block an ${label}`}
        sub={ip ? "Visitors from this address see “Not available” instead of the website. The admin isn't affected." : 'Nobody can open this part of the website, or anything under it.'}
        actions={id ? <><button className="btn ghost" onClick={() => run(api.post(`/api/lando/blocked/${kind}/${id}/toggle`))}>{b.active ? 'Unblock' : 'Block Again'}</button>
          <button className="btn red" onClick={() => confirm(`Remove ${b.value}?\nIt comes off the list for good. Use Unblock to keep it for later.`) && run(api.del(`/api/lando/blocked/${kind}/${id}`).then((r) => { navigate(list); return r; }))}>Remove</button></> : undefined} />
      {bar}
      {views && <div className="banner-note" role="status" style={{ marginBottom: 16 }}>{ip ? 'This address' : 'This area'} had {plural(views.n, 'page view')} from {plural(views.people, 'visitor')} in the last 30 days{views.last ? `, the last ${age(views.last)} ago` : ''}.</div>}
      <form className="panel simple-form" onSubmit={save} key={`${b.id}${b.active}`}>
        <div className="panel-body form-grid">
          <label htmlFor="value">{ip ? 'IP address' : 'Area (path)'}</label>
          <div><input id="value" name="value" required maxLength={200} defaultValue={b.value} placeholder={ip ? '203.0.113.7 or 203.0.113.*' : 'wp-admin or careers'} className="mono" />
            <div className="help">{ip ? 'End with * for a whole range.' : 'Without the leading slash. “careers” also covers careers/lead-developer.'}</div></div>
          <label htmlFor="reason">Reason</label><input id="reason" name="reason" maxLength={200} defaultValue={b.reason ?? ''} placeholder="For the team, e.g. scraping the rates pages" />
          <label htmlFor="message">Message to the visitor</label><input id="message" name="message" maxLength={250} defaultValue={b.message ?? ''} placeholder="This page isn't available." />
          <label htmlFor="expires_at">Blocked until</label><div><input id="expires_at" name="expires_at" type="datetime-local" defaultValue={b.expiresAt ? new Date(b.expiresAt).toLocaleString('sv-SE').slice(0, 16).replace(' ', 'T') : ''} />
            <div className="help">Leave empty to block until you unblock it.</div></div>
          <label htmlFor="active">Blocked now</label><label className="check"><input id="active" type="checkbox" name="active" defaultChecked={b.active} /> On</label>
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save' : 'Block'}</button><Link className="btn ghost" to={list}>Cancel</Link></div>
      </form>
    </>
  );
}
