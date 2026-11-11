import { useEffect } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import { EmptyRow, PageHead, PagerBar, toast, useRun } from '../../ui';
import { ReportActions, RowActions, StatePill, rerunRun, when } from './shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const labels: Record<string, string> = { running: 'Running', problem: 'Errors & Did Not Finish', done: 'Completed' };
const empty: Record<string, string> = { running: 'Nothing is running right now.', problem: 'No errors.', done: 'No completed runs yet.' };
const secs = (ms: number | null, d = 1) => (ms == null ? '—' : `${(ms / 1000).toFixed(d)}s`);
const sentence = (s: string) => s.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());

/** Walker → Report Status (ebunny's walker/home): Running, Errors and Completed; updates live. */
export default function Status() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/walker/runs?${params}`);
  // Every 5 seconds while something is running, every 30 seconds otherwise
  useEffect(() => {
    const t = setTimeout(() => { if (!document.hidden) reload(); }, data?.running.total ? 5000 : 30000);
    return () => clearTimeout(t);
  }, [data, reload]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const rerun = (id: number) => rerunRun(id).then((r) => { toast(r.message); navigate(`/walker/runs/${r.id}`); }, (e) => toast(e.message));
  return (
    <>
      <PageHead title="Report Status" sub="Every report run from Walker and Corral. Runs move from Running to Errors or Completed on their own; this page updates live."
        actions={<Link className="btn cyan" to="/walker/reports">Run a Report</Link>} />
      <div className="stack" style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        {Object.keys(labels).map((k) => {
          const t = data[k];
          return (
            <div key={k} className="panel" id={`runs-${k}`}>
              <div className="panel-head"><h2>{labels[k]} <span className="count-pill">{t.total.toLocaleString()}</span></h2>{k === 'running' && t.total > 0 && <span className="muted">updating live</span>}</div>
              <div className="table-wrap"><table style={{ tableLayout: 'fixed', minWidth: 1100 }} data-page="0">
                <colgroup><col style={{ width: 60 }} /><col style={{ width: '20%' }} /><col /><col style={{ width: 120 }} /><col style={{ width: 72 }} /><col style={{ width: 150 }} /><col style={{ width: 80 }} /><col style={{ width: 62 }} /><col style={{ width: 112 }} /><col style={{ width: 92 }} /></colgroup>
                <thead><tr><th>ID</th><th>Report</th><th>Model</th><th>Requested By</th><th>From</th><th>{k === 'done' ? 'Finished' : 'Started'}</th><th className="num">Duration</th><th className="num">Rows</th><th>Status</th><th><span className="sr-only">Download or rerun</span></th></tr></thead>
                <tbody>
                  {t.runs.map((run: any) => (
                    <tr key={run.id} className="click" onClick={() => navigate(`/walker/runs/${run.id}`)}>
                      <td className="nowrap"><Link to={`/walker/runs/${run.id}`}><b>#{run.id}</b></Link></td>
                      <td style={{ whiteSpace: 'normal', overflowWrap: 'anywhere' }}><b>{run.title}</b>{k === 'problem' && run.error && <div className="help">{run.error.length > 90 ? run.error.slice(0, 87) + '...' : run.error}</div>}</td>
                      <td className="mono" style={{ whiteSpace: 'normal', overflowWrap: 'anywhere' }}>{run.model ?? '—'}</td>
                      <td>{run.by ?? 'System'}</td><td>{sentence(run.app)}</td>
                      <td>{when(k === 'done' ? run.finishedAt ?? run.startedAt ?? run.createdAt : run.startedAt ?? run.createdAt)}</td>
                      <td className="num">{secs(run.durationMs)}</td><td className="num">{run.status === 'done' ? run.rows.toLocaleString() : '—'}</td>
                      <td><StatePill state={run.state} /></td><td className="actions"><RowActions run={run} onRerun={rerun} /></td></tr>
                  ))}
                  {t.runs.length === 0 && <EmptyRow cols={10}>{empty[k]}</EmptyRow>}
                </tbody>
              </table></div>
              <PagerBar page={t.page} total={t.total} size={t.size} onPage={(p) => navigate(`/walker?${qs({ ...Object.fromEntries(params), [`${k}_page`]: p })}`)} />
            </div>
          );
        })}
      </div>
    </>
  );
}

/** One report run (ebunny's walker/run): what it read, its parameters, its file, the first rows, and other runs. */
export function Run() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/walker/runs/${id}`);
  const [run, bar] = useRun();
  const busy = data && ['queued', 'running'].includes(data.run.state);
  useEffect(() => { if (!busy) return; const t = setInterval(reload, 5000); return () => clearInterval(t); }, [busy, reload]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const r = data.run, def = data.def;
  const rerun = (rid: number) => rerunRun(rid).then((x) => { toast(x.message); navigate(`/walker/runs/${x.id}`); }, (e) => toast(e.message));
  const t = (s: string | null) => (s ? new Date(s).toLocaleString('en-US').replace(',', '') : '—');
  const size = data.fileSize == null ? null : data.fileSize >= 1048576 ? `${(data.fileSize / 1048576).toFixed(1)} MB` : `${Math.max(0.1, data.fileSize / 1024).toFixed(1)} KB`;
  const saveAs = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post<any>('/api/walker/saved', { name: f.get('name'), report: r.report, run: r.id, shared: f.get('shared') === 'on' }).then((x) => { navigate(`/walker/saved-reports/${x.id}`); return x; }));
  };
  return (
    <>
      <PageHead title={`${r.title} · #${r.id}`} sub={<>Model <span className="mono">{r.model ?? '—'}</span> · <StatePill state={r.state} /></>}
        actions={<ReportActions size="page" download={r.file ? `/api/walker/runs/${r.id}/download` : null} label={`Download ${r.format}`} rerun={def ? () => rerun(r.id) : null}
          why={busy ? 'still running' : 'this run did not save a file'} />} />
      {bar}
      {r.state === 'failed' && <div className="alert bad"><b>Error</b> {r.error ?? 'The report stopped with an error.'}</div>}
      {r.state === 'stalled' && <div className="alert bad"><b>Did not finish</b> This run was still {r.status} after 60 minutes. Rerun it.</div>}
      {busy && <div className="alert info"><b>{r.state === 'queued' ? 'Waiting to start' : 'Running'}</b> This page refreshes every few seconds.</div>}
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Run</h2></div><div className="panel-body"><dl className="kv">
          <dt>Report</dt><dd>{def ? <Link to={`/walker/reports/${def.key}`}>{def.title}</Link> : r.report}</dd>
          <dt>Requested By</dt><dd>{r.by ?? 'System'}</dd>
          <dt>From</dt><dd>{sentence(r.app)}</dd>
          <dt>Queued</dt><dd>{t(r.createdAt)}</dd><dt>Started</dt><dd>{t(r.startedAt)}</dd><dt>Finished</dt><dd>{t(r.finishedAt)}</dd>
          <dt>Duration</dt><dd>{r.durationMs != null ? `${(r.durationMs / 1000).toFixed(2)} seconds` : '—'}</dd>
          <dt>Rows</dt><dd>{r.status === 'done' ? r.rows.toLocaleString() : '—'}</dd>
          {data.rerunOf && <><dt>Rerun Of</dt><dd><Link to={`/walker/runs/${data.rerunOf}`}>#{data.rerunOf}</Link></dd></>}
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Parameters</h2></div><div className="panel-body"><dl className="kv">
          {Object.entries(r.params ?? {}).map(([k, v]) => [<dt key={k + 'a'}>{sentence(k)}</dt>, <dd key={k + 'b'}>{Array.isArray(v) ? v.join(', ') : String(v)}</dd>])}
          {Object.keys(r.params ?? {}).length === 0 && <><dt>None</dt><dd>—</dd></>}
          {data.savedReport && <><dt>Saved Report</dt><dd><Link to={`/walker/saved-reports/${data.savedReport.id}`}>{data.savedReport.name}</Link></dd></>}
        </dl></div>
          {def && (
            <form className="panel-body save-report" onSubmit={saveAs}>
              <div className="form-row"><input name="name" maxLength={120} required aria-label="Saved report name" placeholder="Save these settings as…" /><button className="btn sm ghost">Save Report</button></div>
              <label className="check"><input type="checkbox" name="shared" /> Share with everyone who has Walker</label>
            </form>
          )}
        </div>
        <div className="panel"><div className="panel-head"><h2>File</h2></div><div className="panel-body"><dl className="kv">
          <dt>Name</dt><dd className="mono">{data.fileName ?? '—'}</dd>
          <dt>Format</dt><dd>{r.format}{r.summary ? ' · summary' : ''}</dd>
          <dt>Size</dt><dd>{size ?? '—'}</dd>
          <dt>Saved In</dt><dd className="mono">{data.folder ?? '—'}</dd>
          <dt>Rows</dt><dd>{r.status === 'done' ? r.rows.toLocaleString() : '—'}</dd>
          <dt>Status</dt><dd>{r.file ? <a href={`/api/walker/runs/${r.id}/download`}>Download</a> : busy ? 'Saving when the run finishes' : `No file — ${r.state === 'failed' ? 'the run failed' : 'the run did not finish'}. Rerun it.`}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Model Reference</h2></div><div className="panel-body">
          <p><span className="mono"><b>{r.model ?? '—'}</b></span></p>
          {def && <p className="muted" style={{ margin: '8px 0 12px' }}>{def.description}</p>}
          {data.columns.length > 0 && <><h3>Columns</h3><p className="help">{data.columns.join(' · ')}</p></>}
        </div></div>
      </div>
      <div className="panel"><div className="panel-head"><h2>Results</h2><span className="muted">{data.preview && `${data.preview.rows.length} of ${r.rows.toLocaleString()} rows shown${r.file ? ' · download for all' : ''}`}</span></div>
        {data.preview ? (
          <div className="table-wrap"><table data-page="0">
            <thead><tr>{data.preview.header.map((h: string, i: number) => <th key={i}>{h}</th>)}</tr></thead>
            <tbody>
              {data.preview.rows.map((row: any[], i: number) => <tr key={i}>{row.map((v, j) => <td key={j}>{typeof v === 'number' && !Number.isInteger(v) ? v.toFixed(2) : v}</td>)}</tr>)}
              {data.preview.rows.length === 0 && <EmptyRow cols={data.preview.header.length}>No rows.</EmptyRow>}
            </tbody>
          </table></div>
        ) : <div className="panel-body muted">Results show here when the run completes.</div>}
      </div>
      {data.history.length > 0 && (
        <div className="panel"><div className="panel-head"><h2>Other Runs of This Report</h2></div><div className="table-wrap"><table>
          <tbody>{data.history.map((h: any) => (
            <tr key={h.id} className="click" onClick={() => navigate(`/walker/runs/${h.id}`)}><td><Link to={`/walker/runs/${h.id}`}>#{h.id}</Link></td><td>{when(h.createdAt)}</td><td>{h.by ?? 'System'}</td>
              <td><StatePill state={h.state} /></td><td className="actions"><RowActions run={h} onRerun={rerun} /></td></tr>
          ))}</tbody>
        </table></div></div>
      )}
    </>
  );
}
