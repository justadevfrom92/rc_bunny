import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import { EmptyRow, PageHead, PagerBar, Pill, age, useRun } from '../../ui';
import { ReportActions, when } from './shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const subs: Record<string, string> = {
  ETL: "Files from the scheduled feeds: what came in and what went out. Each feed's run history is under ETL Feeds.",
  'Legal Documents': 'Terms of service, required disclosures and policies. Keep the current version here.',
  'Customer Templates': 'Letters and notices sent to customers, like the monthly payment reminder. {{merge.fields}} are filled in for each customer.',
};
const kb = (n: number) => (n >= 1048576 ? `${(n / 1048576).toFixed(1)} MB` : `${Math.max(0.1, n / 1024).toFixed(1)} KB`);
const day = (s: string) => new Date(s).toLocaleDateString('en-US');
const accept = '.xlsx,.xls,.csv,.pdf,.txt,.zip';

/** Walker → All Files and the folders (ebunny's walker/uploads). */
export default function Uploads() {
  const { folder } = useParams();
  const [sp, setSp] = useSearchParams();
  const navigate = useNavigate();
  const page = Number(sp.get('page') ?? 1);
  const { data, error, reload } = useLoad<any>(`/api/walker/uploads?${qs({ folder, category: folder ? null : sp.get('category'), page })}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.category as string | null;
  const total = Object.values(data.counts as Record<string, number>).reduce((a, b) => a + b, 0);
  const slugOf = (cat: string) => Object.entries(data.folders as Record<string, string>).find(([, v]) => v === cat)?.[0];
  return (
    <>
      <PageHead title={c ?? 'All Files'} sub={(c && subs[c]) ?? 'Documents and report files: ETL files, legal documents, customer templates, finance workbooks, utility and vendor reports.'}
        actions={<Link className="btn cyan" to={`/walker/uploads/new${c ? `?${qs({ category: c })}` : ''}`}>Upload a File</Link>} />
      {bar}
      <div className="chips small" style={{ marginBottom: 14 }}>
        <Link className={`chip-link ${c ? '' : 'on'}`} to="/walker/uploads">All · {total}</Link>
        {data.categories.filter((x: string) => data.counts[x]).map((x: string) => {
          const slug = slugOf(x);
          return <Link key={x} className={`chip-link ${c === x ? 'on' : ''}`} to={slug ? `/walker/files/${slug}` : `/walker/uploads?${qs({ category: x })}`}>{x} · {data.counts[x]}</Link>;
        })}
      </div>
      <div>
        <div className="panel"><div className="table-wrap"><table data-page="0">
          <thead><tr><th>File</th><th>Folder</th><th>Name</th><th>Added</th><th></th></tr></thead>
          <tbody>
            {data.uploads.map((u: any) => (
              <tr key={u.id} className="click" onClick={() => navigate(`/walker/uploads/${u.id}`)}><td><Link to={`/walker/uploads/${u.id}`}><b>{u.title}</b></Link>{u.description && <><br /><span className="help">{u.description}</span></>}</td><td>{u.category}</td>
                <td className="mono help">{u.originalName}<br />{(u.size / 1024).toFixed(1)} KB</td>
                <td>{day(u.createdAt)}<br /><span className="help">{u.by}</span></td>
                <td className="actions"><ReportActions download={`/api/walker/uploads/${u.id}/download`} label={`Download ${u.originalName}`} />
                  {data.canDelete && <span className="inline" onClick={(e) => e.stopPropagation()}><button className="btn sm ghost" onClick={() => confirm(`Delete ${u.title}?\nThe file is removed for everyone.`) && run(api.del(`/api/walker/uploads/${u.id}`))}>Delete</button></span>}</td></tr>
            ))}
            {data.uploads.length === 0 && <EmptyRow cols={5}>No files{c ? ` in ${c}` : ''}.</EmptyRow>}
          </tbody>
        </table></div>
          <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => setSp({ ...Object.fromEntries(sp), page: String(p) })} /></div>
      </div>
    </>
  );
}

/** Upload a File (ebunny's walker/upload-form). */
export function UploadForm() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data } = useLoad<any>('/api/walker/uploads?page=1');
  const [run, bar] = useRun();
  const submit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    run(api.post<any>('/api/walker/uploads', new FormData(e.currentTarget)).then((x) => { navigate(`/walker/uploads/${x.id}`); return x; }));
  };
  return (
    <>
      <PageHead title="Upload a File" sub="Excel, CSV, PDF, text or zip, up to 20 MB." />
      {bar}
      <form className="panel simple-form" onSubmit={submit}>
        <div className="panel-body form-grid">
          <label htmlFor="u-title">Title</label><input id="u-title" name="title" required />
          <label htmlFor="up-folder">Folder</label>
          <select name="category" id="up-folder" required style={{ maxWidth: 280 }} defaultValue={sp.get('category') ?? 'Other'} key={data ? 1 : 0}>
            {(data?.categories ?? [sp.get('category') ?? 'Other']).map((c: string) => <option key={c}>{c}</option>)}</select>
          <label htmlFor="u-desc">Description</label><textarea id="u-desc" name="description" style={{ minHeight: 70, fontFamily: 'inherit' }} />
          <label htmlFor="u-file">File</label><input id="u-file" type="file" name="file" required accept={accept} />
        </div>
        <div className="panel-body actions"><button className="btn cyan">Upload</button><Link className="btn ghost" to="/walker/uploads">Cancel</Link></div>
      </form>
    </>
  );
}

/** One file (ebunny's walker/upload): details, a preview, a new version, edit, delete, and the others in its folder. */
export function Upload() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/walker/uploads/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const u = data.upload, p = data.preview;
  const size = kb(u.size);
  const save = (e: React.FormEvent<HTMLFormElement>) => { e.preventDefault(); const form = e.currentTarget; run(api.put(`/api/walker/uploads/${u.id}`, new FormData(form)).then((x) => { form.reset(); return x; })); };
  const n = p?.type === 'table' ? Math.max(0, p.rows.length - 1) : 0;
  return (
    <>
      <PageHead title={u.title} sub={<>{u.category} · <span className="mono">{u.originalName}</span> · {size}</>}
        actions={<><Link className="btn ghost btn-back" to={data.folder ? `/walker/files/${data.folder}` : `/walker/uploads?${qs({ category: u.category })}`}>{u.category}</Link>
          {data.exists && <a className="btn cyan" href={`/api/walker/uploads/${u.id}/download`}>Download</a>}</>} />
      {bar}
      {!data.exists && <div className="alert bad"><b>The file is missing from storage.</b> Upload a new version below.</div>}
      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div className="panel"><div className="panel-head"><h2>Preview</h2>{p?.type === 'table' && <span className="muted">{n} {n === 1 ? 'row' : 'rows'}{n >= 50 ? ' (first 50)' : ''}</span>}</div>
          {!p ? <div className="panel-body muted">No preview for .{data.ext} files. Download it to open it.</div>
            : p.type === 'table' ? (
              <div className="table-wrap" style={{ maxHeight: 520, overflowY: 'auto' }}><table data-page="0">
                <thead><tr>{(p.rows[0] ?? []).map((h: string, i: number) => <th key={i}>{h}</th>)}</tr></thead>
                <tbody>{p.rows.slice(1).map((row: string[], i: number) => <tr key={i}>{row.map((v, j) => <td key={j}>{v}</td>)}</tr>)}</tbody>
              </table></div>
            ) : p.type === 'text' ? <div className="panel-body"><pre className="file-text">{p.text}</pre></div>
              : p.type === 'pdf' ? <iframe className="file-frame" src={`/api/walker/uploads/${u.id}/view`} title={u.title} />
                : <div className="panel-body"><img src={`/api/walker/uploads/${u.id}/view`} alt={u.title} style={{ maxWidth: '100%', border: '1px solid var(--line)', borderRadius: 6 }} /></div>}
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20, minWidth: 0 }}>
          <div className="panel"><div className="panel-head"><h2>File</h2></div><div className="panel-body"><dl className="kv">
            <dt>Name</dt><dd className="mono" style={{ overflowWrap: 'anywhere' }}>{u.originalName}</dd>
            <dt>Type</dt><dd>.{data.ext}</dd>
            <dt>Size</dt><dd>{size}</dd>
            <dt>Folder</dt><dd>{u.category}</dd>
            <dt>Added</dt><dd>{when(u.createdAt)}</dd>
            <dt>Last change</dt><dd>{when(u.updatedAt)}{data.by && <div className="help">by {data.by}</div>}</dd>
            {u.description && <><dt>About</dt><dd>{u.description}</dd></>}
          </dl></div></div>
          <form className="panel" onSubmit={save} key={u.updatedAt}>
            <div className="panel-head"><h2>Edit</h2></div>
            <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div className="field"><label htmlFor="u-title">Title</label><input id="u-title" name="title" required maxLength={150} defaultValue={u.title} /></div>
              <div className="field"><label htmlFor="up-folder">Folder</label><select name="category" id="up-folder" style={{ maxWidth: 280 }} defaultValue={u.category}>{data.categories.map((c: string) => <option key={c}>{c}</option>)}</select></div>
              <div className="field"><label htmlFor="u-desc">Description</label><textarea id="u-desc" name="description" maxLength={1000} style={{ minHeight: 70, fontFamily: 'inherit' }} defaultValue={u.description ?? ''} /></div>
              <div className="field"><label htmlFor="u-file">New version (optional)</label><input id="u-file" type="file" name="file" accept={accept} /><div className="help">Replaces the file; the title and folder stay.</div></div>
              <div className="actions"><button className="btn cyan">Save</button></div>
            </div>
          </form>
          {data.canDelete && <div><button className="btn red" onClick={() => confirm(`Delete ${u.title}?\nThe file is removed for everyone.`)
            && run(api.del(`/api/walker/uploads/${u.id}`).then((x) => { navigate(data.folder ? `/walker/files/${data.folder}` : '/walker/uploads'); return x; }))}>Delete File</button></div>}
        </div>
      </div>
      {data.siblings.length > 0 && (
        <div className="panel"><div className="panel-head"><h2>Other Files in {u.category}</h2></div><div className="table-wrap"><table>
          <tbody>{data.siblings.map((s: any) => (
            <tr key={s.id} className="click" onClick={() => navigate(`/walker/uploads/${s.id}`)}><td><Link to={`/walker/uploads/${s.id}`}><b>{s.title}</b></Link><div className="help mono">{s.originalName}</div></td><td className="nowrap">{day(s.createdAt)}</td></tr>
          ))}</tbody>
        </table></div></div>
      )}
    </>
  );
}

const tones: Record<string, string> = { OK: 'ok', Warning: 'warn', Failed: 'bad' };
const short = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');

/** Walker → ETL Feeds (ebunny's walker/etl). */
export function Etl() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any[]>('/api/walker/etl');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="ETL Feeds" sub="Files that come in from, or go out to, other companies on a schedule. Open a feed for every run and its errors." />
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Feed</th><th>Direction</th><th>Schedule</th><th>Last Run</th><th>Last Status</th><th>Last 7 Days</th></tr></thead>
        <tbody>{data.map(({ key, feed, last: l, week: w }) => (
          <tr key={key} className="click" onClick={() => navigate(`/walker/etl/${key}`)}>
            <td><Link to={`/walker/etl/${key}`}><b>{feed.name}</b></Link><div className="help mono">{feed.file}</div></td>
            <td><Pill tone={feed.direction === 'In' ? 'info' : ''}>{feed.direction === 'In' ? 'Inbound' : 'Outbound'}</Pill><div className="help">{feed.direction === 'In' ? 'from' : 'to'} {feed.party}</div></td>
            <td className="nowrap">{feed.schedule}</td>
            <td>{l ? short(l.startedAt) : '—'}{l && <div className="help">{age(l.startedAt)} ago</div>}</td>
            <td>{l ? <Pill tone={tones[l.status] ?? ''}>{l.status}</Pill> : '—'}</td>
            <td>{w.runs} {w.runs === 1 ? 'run' : 'runs'} {(w.failed > 0 || w.warned > 0) && <div>{w.failed > 0 && <span className="pill bad">{w.failed} failed</span>} {w.warned > 0 && <span className="pill warn">{w.warned} warning</span>}</div>}</td>
          </tr>
        ))}</tbody>
      </table></div></div>
    </>
  );
}

/** One ETL feed (ebunny's walker/etl-feed): what it is, its health and every run. */
export function EtlFeed() {
  const { feed: key } = useParams();
  const [sp, setSp] = useSearchParams();
  const navigate = useNavigate();
  const status = sp.get('status');
  const { data, error } = useLoad<any>(`/api/walker/etl/${key}?${qs({ status, page: sp.get('page') ?? 1 })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const f = data.feed, inbound = f.direction === 'In';
  const all = Object.values(data.counts as Record<string, number>).reduce((a, b) => a + b, 0);
  return (
    <>
      <PageHead title={f.name} sub={`${inbound ? 'Inbound from ' : 'Outbound to '}${f.party} · ${f.schedule}`} />
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Feed</h2></div><div className="panel-body"><dl className="kv">
          <dt>Direction</dt><dd>{inbound ? 'Inbound' : 'Outbound'}</dd>
          <dt>{inbound ? 'From' : 'To'}</dt><dd>{f.party}</dd>
          <dt>Schedule</dt><dd>{f.schedule} <span className="mono help">{f.cron}</span></dd>
          <dt>Format</dt><dd>{f.format}</dd>
          <dt>File Name</dt><dd className="mono">{f.file}</dd>
          <dt>{inbound ? 'Loads Into' : 'Built From'}</dt><dd>{f.target}</dd>
          <dt>Command</dt><dd className="mono">etl:{key}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Health</h2></div><div className="panel-body"><dl className="kv">
          <dt>Last Good Run</dt><dd>{data.lastOk ? <>{when(data.lastOk)} <span className="help">{age(data.lastOk)} ago</span></> : 'Never'}</dd>
          <dt>Runs</dt><dd>{all.toLocaleString()}</dd>
          <dt>Failed</dt><dd>{(data.counts.Failed ?? 0).toLocaleString()}</dd>
          <dt>Warnings</dt><dd>{(data.counts.Warning ?? 0).toLocaleString()}</dd>
        </dl></div></div>
      </div>
      <div className="panel"><div className="panel-head"><h2>Runs</h2>
        <div className="chips small">
          <Link className={`chip-link ${status ? '' : 'on'}`} to={`/walker/etl/${key}`}>All <span className="help">{all}</span></Link>
          {['OK', 'Warning', 'Failed'].map((s) => <Link key={s} className={`chip-link ${status === s ? 'on' : ''}`} to={`/walker/etl/${key}?${qs({ status: s })}`}>{s} <span className="help">{data.counts[s] ?? 0}</span></Link>)}
        </div></div>
        <div className="table-wrap"><table data-page="0">
          <thead><tr><th>ID</th><th>Started</th><th>Status</th><th>Result</th><th className="num">Duration</th></tr></thead>
          <tbody>
            {data.runs.map((r: any) => (
              <tr key={r.id} className="click" onClick={() => navigate(`/walker/records/job_run/${r.id}`)}><td className="nowrap"><Link to={`/walker/records/job_run/${r.id}`}><b>#{r.id}</b></Link></td><td className="nowrap">{when(r.startedAt)}</td>
                <td><Pill tone={tones[r.status] ?? ''}>{r.status}</Pill></td><td>{r.message}</td><td className="num nowrap">{r.durationMs != null ? `${(r.durationMs / 1000).toFixed(1)}s` : '—'}</td></tr>
            ))}
            {data.runs.length === 0 && <EmptyRow cols={5}>No runs{status ? ` with status ${status}` : ''}.</EmptyRow>}
          </tbody>
        </table></div>
        <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => setSp({ ...Object.fromEntries(sp), page: String(p) })} />
      </div>
    </>
  );
}
