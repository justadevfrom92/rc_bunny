import { useEffect, useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import AppShell, { ago, type Menu } from '../../Sidebar';
import { EmptyRow, PageHead, PagerBar, Pill, toast, useRun } from '../../ui';
import { useAuth } from '../../auth';
import { ReportActions, StatePill, when } from '../walker/shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

// ebunny's config/admin.php → account menu: your notifications, My Files and Settings
const menu: Menu = [{ heading: 'Account', links: [['Notifications', 'my/notifications', { badge: 'unread_notifications' }], ['My Files', 'files'], ['Settings', 'my/settings']] }];

export const AccountShell = ({ children }: { children: ReactNode }) => <AppShell app="account" menu={menu}>{children}</AppShell>;

const headline = (s: string) => s.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
const appName = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);

/** Everything you've been notified about, newest first; open one to go to it (it's marked read). */
export function Notifications() {
  const [sp, setSp] = useSearchParams();
  const navigate = useNavigate();
  const kind = sp.get('kind');
  const { data, error, reload } = useLoad<any>(`/api/my/notifications?${qs({ kind, page: sp.get('page') ?? 1 })}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Notifications" sub={data.unread ? `${data.unread} unread. Open one to go to it.` : 'All caught up.'}
        actions={<>{data.unread > 0 && <button className="btn ghost" onClick={() => run(api.post('/api/my/notifications/read-all'))}>Mark All Read</button>}
          <Link className="btn ghost" to="/my/settings">Settings</Link></>} />
      {bar}
      <select className="pill-select" aria-label="Kind" style={{ marginBottom: 18 }} value={kind ?? ''} onChange={(e) => setSp(e.target.value ? { kind: e.target.value } : {})}>
        <option value="">All notifications</option>
        {Object.entries(data.kinds as Record<string, string>).map(([k, l]) => <option key={k} value={k}>{l}</option>)}
      </select>
      <div className="panel"><div className="table-wrap"><table data-page="0">
        <thead><tr><th>ID</th><th>When</th><th>What</th><th>Kind</th></tr></thead>
        <tbody>
          {data.items.map((n: any) => (
            <tr key={n.id} className={`click ${n.readAt ? '' : 'row-unread'}`} onClick={() => navigate(`/my/notifications/${n.id}`)}>
              <td className="nowrap"><Link to={`/my/notifications/${n.id}`}><b>#{n.id}</b></Link></td>
              <td className="nowrap">{when(n.createdAt)}<div className="help">{ago(n.createdAt)}</div></td>
              <td className="wrap"><b>{n.icon} {n.title}</b>{n.body && <div className="help">{n.body}</div>}</td>
              <td><Pill tone={n.readAt ? '' : 'info'}>{n.kindLabel}</Pill></td></tr>
          ))}
          {data.items.length === 0 && <EmptyRow cols={4}>No notifications.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => setSp({ ...Object.fromEntries(sp), page: String(p) })} /></div>
    </>
  );
}

/** Opening a notification marks it read and goes to what it's about. */
export function OpenNotification() {
  const { id } = useParams();
  const navigate = useNavigate();
  useEffect(() => {
    api.post<{ url: string }>(`/api/my/notifications/${id}/open`).then(
      (r) => navigate(r.url.replace(/^\/admin(?=\/|$)/, '') || '/', { replace: true }),
      () => navigate('/my/notifications', { replace: true }));
  }, [id, navigate]);
  return <div className="empty">Opening…</div>;
}

/** Your settings: which notifications you get, and (testing only) switching to another sample user. */
export function Settings() {
  const { data, error } = useLoad<any>('/api/my/settings');
  const { setMe } = useAuth();
  const navigate = useNavigate();
  const [run, bar] = useRun();
  const [find, setFind] = useState('');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const save = (e: React.FormEvent<HTMLFormElement>) => { e.preventDefault(); run(api.put('/api/my/settings', { kinds: new FormData(e.currentTarget).getAll('kinds') })); };
  const users = data.testUsers.filter((u: any) => `${u.name} ${u.role} ${u.email}`.toLowerCase().includes(find.trim().toLowerCase()));
  return (
    <>
      <PageHead title="Settings" sub={`${data.name} · ${data.email}`} />
      {bar}
      <form className="panel" onSubmit={save}>
        <div className="panel-head"><h2>Notifications</h2><span className="help">they add to the number on your avatar</span></div>
        <div className="table-wrap"><table data-page="0">
          <thead><tr><th style={{ width: 60 }}>On</th><th>Notification</th><th>When you get it</th></tr></thead>
          <tbody>{data.kinds.map((k: any) => (
            <tr key={k.key}><td><input type="checkbox" id={`nk-${k.key}`} name="kinds" value={k.key} defaultChecked={k.on} /></td>
              <td><label htmlFor={`nk-${k.key}`}><b>{k.icon} {k.label}</b></label></td><td className="wrap help">{k.when}</td></tr>
          ))}</tbody>
        </table></div>
        <div className="panel-body"><button className="btn cyan">Save Settings</button></div>
      </form>
      {data.testUsers.length > 0 && (
        <div className="panel"><div className="panel-head"><h2>Switch User</h2><span className="help">testing only: never shown in production</span></div>
          <div className="panel-body"><input type="search" className="search-box" placeholder="Find a user" aria-label="Find a user to switch to" value={find} onChange={(e) => setFind(e.target.value)} /></div>
          <div className="table-wrap"><table data-page="0">
            <thead><tr><th>Name</th><th>Role</th><th><span className="sr-only">Switch</span></th></tr></thead>
            <tbody>{users.map((u: any) => (
              <tr key={u.id}><td><b>{u.name}</b><div className="help">{u.email}</div></td><td>{u.role ?? '—'}</td>
                <td className="actions"><button className="btn sm" onClick={() => api.post<any>(`/api/login/test/${u.id}`).then((m) => { setMe(m); navigate('/'); }, (e) => toast(e.message))}>Switch to {u.name.split(' ')[0]}</button></td></tr>
            ))}</tbody>
          </table></div>
        </div>
      )}
    </>
  );
}

/** My Files: every report you ran, from any app: name, when, format, rows, size, status. */
export function Files() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const status = sp.get('status');
  const { data, error } = useLoad<any>(`/api/files?${qs({ status, page: sp.get('page') ?? 1 })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const rerun = (id: number) => api.post<any>(`/api/files/${id}/rerun`).then((x) => { toast(x.message); navigate(`/files/${x.id}`); }, (e) => toast(e.message));
  const chip = (s: string | null, label: string, n: number) => <Link key={label} to={s ? `/files?${qs({ status: s })}` : '/files'} className={`chip-link${status === s ? ' on' : ''}`}>{label} · {n}</Link>;
  return (
    <>
      <PageHead title="My Files" sub="Reports you ran, from every app. They run in the background; the number by your name (top right) counts finished ones you haven't opened yet." />
      <div className="chips small">{chip(null, 'All', data.counts.all)}{chip('running', 'Running', data.counts.running)}{chip('done', 'Ready', data.counts.done)}{chip('failed', 'Errors', data.counts.failed)}</div>
      <div className="panel"><div className="table-wrap"><table data-page="0">
        <thead><tr><th>Report</th><th>Requested</th><th>From</th><th>Format</th><th className="num">Rows</th><th className="num">Size</th><th>Status</th><th></th></tr></thead>
        <tbody>
          {data.runs.map((r: any) => (
            <tr key={r.id} className="click" onClick={() => navigate(`/files/${r.id}`)}>
              <td><Link to={`/files/${r.id}`}><b>{r.label}</b></Link>{data.unseen.includes(r.id) && <> <span className="pill info">New</span></>}<div className="help">#{r.id}</div></td>
              <td className="nowrap">{when(r.createdAt)}</td><td>{appName(r.app)}</td><td className="nowrap">{r.format}</td>
              <td className="num">{r.status === 'done' ? r.rows.toLocaleString() : '…'}</td><td className="num">{r.size ?? '—'}</td>
              <td><StatePill state={r.state} /></td>
              <td className="actions"><ReportActions download={r.file ? `/api/files/${r.id}/download` : null} label={`Download ${r.format}`}
                why={['queued', 'running'].includes(r.status) ? 'still running' : 'no file'} rerun={() => rerun(r.id)} /></td></tr>
          ))}
          {data.runs.length === 0 && <EmptyRow cols={8}>No files yet. Run a report from Walker → Reports.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => navigate(`/files?${qs({ status, page: p })}`)} /></div>
    </>
  );
}

/** One file: the report, its settings, the file (download / run again) and its first rows, kept with the run so nothing runs again. */
export function File() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/files/${id}`);
  const busy = data && ['queued', 'running'].includes(data.run.state);
  // This page refreshes itself while the report runs
  useEffect(() => { if (!busy) return; const t = setInterval(reload, 6000); return () => clearInterval(t); }, [busy, reload]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const r = data.run, p = data.preview;
  const rerun = () => api.post<any>(`/api/files/${r.id}/rerun`).then((x) => { toast(x.message); navigate(`/files/${x.id}`); }, (e) => toast(e.message));
  return (
    <>
      <PageHead title={r.label} sub={<>Run #{r.id} · <StatePill state={r.state} /></>}
        actions={<><Link className="btn ghost" to="/files">All My Files</Link><ReportActions size="page" label={`Download ${r.format}`} download={r.file ? `/api/files/${r.id}/download` : null}
          why={busy ? 'still running' : 'no file saved'} rerun={rerun} /></>} />
      {busy && <div className="alert info"><b>{r.state === 'queued' ? 'Waiting to start' : 'Running'}</b> You'll get a notice at the top right when the file is ready; this page refreshes itself.</div>}
      {r.state === 'failed' && <div className="alert bad"><b>Error</b> {r.error ?? 'The report stopped with an error.'}</div>}
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>File</h2></div><div className="panel-body"><dl className="kv">
          <dt>Name</dt><dd className="mono">{r.fileName ?? '—'}</dd>
          <dt>Format</dt><dd>{r.format}{r.summary ? ' · summary' : ''}</dd>
          <dt>Rows</dt><dd>{r.status === 'done' ? r.rows.toLocaleString() : '—'}</dd>
          <dt>Size</dt><dd>{r.size ?? '—'}</dd>
          <dt>Requested</dt><dd>{when(r.createdAt)} from {appName(r.app)}</dd>
          <dt>Finished</dt><dd>{r.finishedAt ? when(r.finishedAt) : '—'}{r.durationMs != null && <> <span className="muted">({(r.durationMs / 1000).toFixed(1)}s)</span></>}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Settings</h2></div><div className="panel-body"><dl className="kv">
          {Object.entries(r.params ?? {}).map(([k, v]: [string, any]) => [<dt key={k + 'a'}>{headline(k)}</dt>, <dd key={k + 'b'}>{Array.isArray(v) ? v.join(', ') : String(v)}</dd>])}
          {Object.keys(r.params ?? {}).length === 0 && <><dt>Settings</dt><dd>Defaults</dd></>}
          {data.model && <><dt>Reads</dt><dd className="mono">{data.model}</dd></>}
        </dl></div></div>
      </div>
      <div className="panel"><div className="panel-head"><h2>First Rows</h2><span className="muted">{p && `${Math.min(p.rows.length, r.rows)} of ${r.rows.toLocaleString()} · download for all`}</span></div>
        {p ? (
          <div className="table-wrap"><table data-page="0"><thead><tr>{p.header.map((h: string, i: number) => <th key={i}>{h}</th>)}</tr></thead>
            <tbody>
              {p.rows.map((row: any[], i: number) => <tr key={i}>{row.map((v, j) => <td key={j}>{typeof v === 'number' && !Number.isInteger(v) ? v.toFixed(2) : v}</td>)}</tr>)}
              {p.rows.length === 0 && <EmptyRow cols={p.header.length}>The report found no rows for these settings.</EmptyRow>}
            </tbody></table></div>
        ) : <div className="panel-body muted">{busy ? 'The first rows show here when it finishes.' : 'This run kept no preview. Download the file, or run it again.'}</div>}
      </div>
    </>
  );
}
