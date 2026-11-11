import { useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, useLoad } from '../../api';
import { EmptyRow, PageHead, PagerBar, Pill, age, toast, useRun } from '../../ui';
import { DateInput } from '../../fields';
import { ReportActions, RowActions, StatePill, rerunRun, when } from './shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const headline = (s: string) => s.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());

/** Walker → Run a Report (ebunny's walker/reports): the report library. */
export default function Library() {
  const { data, error } = useLoad<any[]>('/api/walker/reports');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Run a Report" sub="Reports run in the background. You can leave the page; finished reports show on Report Status with a download." />
      {data.map((g) => (
        <div key={g.category}>
          <h2 style={{ margin: '8px 0 12px' }}>{g.category}</h2>
          <div className="grid-3" style={{ marginBottom: 20 }}>
            {g.reports.map((r: any) => (
              <Link key={r.key} className="panel report-card" to={`/walker/reports/${r.key}`}><div className="panel-body">
                <h3>{r.title}</h3>
                <p className="mono help">{r.model}</p>
                <p className="muted">{r.description}</p>
                <p className="help">{r.lastAt ? `${r.runs.toLocaleString()} runs · last ${age(r.lastAt)} ago` : 'Never run'}</p>
              </div></Link>
            ))}
          </div>
        </div>
      ))}
    </>
  );
}

type Col = { key: string; title: string; order: number };

/** ebunny's walker/_export-columns: the columns this report's files have, as rows or as a key array. */
function ExportColumns({ data, onSaved }: { data: any; onSaved: () => void }) {
  const [rows, setRows] = useState<Col[]>(() => data.export.map((c: any, i: number) => ({ key: c.key, title: c.title, order: i + 1 })));
  const [run, bar] = useRun(onSaved);
  const nested: any = {};
  for (const c of data.export) {
    const parts = c.key.replace(/\*/g, '_all').split('.');
    let o = nested;
    parts.slice(0, -1).forEach((p: string) => (o = o[p] = typeof o[p] === 'object' ? o[p] : {}));
    o[parts[parts.length - 1]] = c.title;
  }
  const [keyArray, setKeyArray] = useState(data.export.length ? JSON.stringify(nested, null, 4) : '{\n    "customer": { "name": "Name", "account": "Account" },\n    "status": "Status"\n}');
  const set = (i: number, k: keyof Col, v: string) => setRows(rows.map((r, j) => (j === i ? { ...r, [k]: k === 'order' ? Number(v) : v } : r)));
  const move = (i: number, d: number) => {
    const j = i + d;
    if (j < 0 || j >= rows.length) return;
    const next = [...rows];
    [next[i], next[j]] = [next[j], next[i]];
    setRows(next.map((r, n) => ({ ...r, order: n + 1 })));
  };
  const add = (key = '') => setRows([...rows, { key, title: key ? headline(key.split('.').pop()!.replace('*', '')) : '', order: rows.length + 1 }]);
  const url = `/api/walker/reports/${data.def.key}/columns`;
  const n = data.export.length;
  return (
    <div className="panel" id="export-columns" style={{ marginBottom: 20 }}>
      <div className="panel-head"><h2>Export Columns</h2>
        <span className="help">{n ? <>{n} custom {n === 1 ? 'column' : 'columns'}{data.exportBy && <> · {data.exportBy}, {age(data.exportAt)} ago</>}</> : "Using the report's own columns"}</span></div>
      {!n && <div className="panel-body help" style={{ paddingBottom: 0 }}>Now: {data.columns.join(' · ')}</div>}
      <form onSubmit={(e) => { e.preventDefault(); run(api.post(url, { cols: rows })); }}>
        <div className="table-wrap"><table className="table export-cols" data-page="0">
          <thead><tr><th style={{ width: 70 }}>Order</th><th>Model key</th><th>Column title</th><th style={{ width: 90 }}><span className="sr-only">Move or remove</span></th></tr></thead>
          <tbody id="export-rows">
            {rows.map((c, i) => (
              <tr key={i}><td><input type="number" value={c.order} className="ec-order" aria-label="Order" onChange={(e) => set(i, 'order', e.target.value)} /></td>
                <td><input value={c.key} className="mono" list="export-fields" aria-label="Model key" placeholder="e.g. customer.name" onChange={(e) => set(i, 'key', e.target.value)} /></td>
                <td><input value={c.title} aria-label="Column title" placeholder="e.g. Name" onChange={(e) => set(i, 'title', e.target.value)} /></td>
                <td className="actions nowrap"><button type="button" className="btn sm ghost icon" aria-label="Move up" onClick={() => move(i, -1)}>↑</button><button type="button" className="btn sm ghost icon" aria-label="Move down" onClick={() => move(i, 1)}>↓</button>
                  <button type="button" className="btn sm ghost icon" aria-label="Remove" onClick={() => setRows(rows.filter((_, j) => j !== i))}>×</button></td></tr>
            ))}
          </tbody>
        </table></div>
        <datalist id="export-fields">{data.fields.map((k: string) => <option key={k} value={k} />)}</datalist>
        {data.fields.length > 0 && (
          <div className="panel-body" style={{ paddingBottom: 0 }}><div className="help" style={{ marginBottom: 6 }}>Keys on these records — click one to add it:</div>
            <div className="ec-fields">{data.fields.map((k: string) => <button key={k} type="button" className="ec-field mono" onClick={() => add(k)}>{k}</button>)}</div></div>
        )}
        {bar && <div className="panel-body">{bar}</div>}
        <div className="panel-body actions"><button type="button" className="btn ghost" onClick={() => add()}>Add Column</button><button className="btn cyan">Save Columns</button>
          {n > 0 && <button type="button" className="btn ghost" onClick={() => confirm("Reset export columns?\nFiles go back to the report's own columns.") && run(api.post(url, { reset: true }))}>Reset</button>}</div>
      </form>
      <details className="panel-body ec-array"><summary>As a key array</summary>
        <form onSubmit={(e) => { e.preventDefault(); run(api.post(url, { keyArray })); }}>
          <p className="help">The same columns as a nested <span className="mono">key =&gt; title</span> array, in running order. Paste one to replace them all. Use <span className="mono">_all</span> for every item of a list.</p>
          <textarea className="mono" rows={8} style={{ width: '100%' }} value={keyArray} onChange={(e) => setKeyArray(e.target.value)} />
          <div className="actions" style={{ marginTop: 8 }}><button className="btn">Save Key Array</button></div>
        </form>
      </details>
    </div>
  );
}

/** One report (ebunny's walker/report): parameters, export columns and recent runs. */
export function Report() {
  const { key } = useParams();
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const savedId = sp.get('saved');
  const { data, error, reload } = useLoad<any>(`/api/walker/reports/${key}${savedId ? `?saved=${savedId}` : ''}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const def = data.def;
  const latest = data.runs[0], latestFile = data.runs.find((r: any) => r.file);
  const rerun = (id: number) => rerunRun(id).then((x) => { toast(x.message); navigate(`/walker/runs/${x.id}`); }, (e) => toast(e.message));
  const read = (form: HTMLFormElement) => {
    const f = new FormData(form);
    const params: Record<string, unknown> = {};
    for (const flt of data.filters) params[flt.name] = flt.type === 'multi' ? f.getAll(flt.name) : f.get(flt.name);
    return { params, format: f.get('format'), summary: f.get('summary') === 'on', f };
  };
  const submit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const save = (e.nativeEvent as SubmitEvent).submitter?.getAttribute('data-save') != null;
    const { params, format, summary, f } = read(e.currentTarget);
    if (save) run(api.post<any>('/api/walker/saved', { name: f.get('name'), report: def.key, shared: f.get('shared') === 'on', format, summary, params }).then((x) => { navigate(`/walker/saved-reports/${x.id}`); return x; }));
    else run(api.post<any>(`/api/walker/reports/${def.key}`, { params, format, summary }).then((x) => { navigate(`/walker/runs/${x.id}`); return x; }));
  };
  const runDefaults = () => run(api.post<any>(`/api/walker/reports/${def.key}`, { params: data.defaults, format: 'xlsx', summary: false }).then((x) => { navigate(`/walker/runs/${x.id}`); return x; }));
  return (
    <>
      <PageHead title={def.title} sub={<>{def.description} · Model <span className="mono">{def.model}</span>{latest && <> · Last run #{latest.id} {age(latest.createdAt)} ago</>}</>}
        actions={<ReportActions size="page" label={`Download latest ${latestFile?.format ?? 'file'}`} download={latestFile ? `/api/walker/runs/${latestFile.id}/download` : null}
          why={latest ? 'no saved file yet' : 'not run yet'} rerun={latest ? () => rerun(latest.id) : runDefaults} />} />
      {bar}
      {data.from && <div className="alert info"><b>Saved report: {data.from.name}</b> Its settings are filled in below. <Link to={`/walker/saved-reports/${data.from.id}`}>Open the saved report</Link></div>}
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={submit} key={savedId ?? ''}>
            <div className="panel-head"><h2>Parameters</h2></div>
            <div className="panel-body">
              {data.filters.map((flt: any) => {
                const value = data.defaults[flt.name];
                const opts = Object.entries(flt.options ?? {}) as [string, string][];
                if (flt.type === 'multi') return (
                  <div key={flt.name} className="field"><label>{flt.label}</label><div className="check-stack">
                    {opts.map(([v, l]) => <label key={v} className="check"><input type="checkbox" name={flt.name} value={v} defaultChecked={[].concat(value ?? []).map(String).includes(v)} /> {l}</label>)}
                  </div></div>
                );
                if (flt.type === 'select') return (
                  <div key={flt.name} className="field"><label htmlFor={`f-${flt.name}`}>{flt.label}</label><select id={`f-${flt.name}`} name={flt.name} defaultValue={value ?? ''}><option value="">Any</option>
                    {opts.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></div>
                );
                return (
                  <div key={flt.name} className="field"><label htmlFor={`f-${flt.name}`}>{flt.label}</label>
                    {flt.type === 'date' ? <DateInput id={`f-${flt.name}`} name={flt.name} defaultValue={value ?? ''} /> : <input id={`f-${flt.name}`} name={flt.name} defaultValue={value ?? ''} />}</div>
                );
              })}
              <div className="field"><label>Export as</label><div className="chips small">
                {Object.entries(data.formats as Record<string, string>).map(([v, l]) => <label key={v}><input type="radio" name="format" value={v} defaultChecked={(data.from?.format ?? 'xlsx') === v} /><span>{l}</span></label>)}
              </div></div>
              <label className="check"><input type="checkbox" name="summary" defaultChecked={!!data.from?.summary} /> Summary only (counts instead of every row)</label>
              <button className="btn cyan">Run Report</button>
              <p className="help">Runs in the background and saves the file. You'll get a notice at the top right when it's ready.</p>
              <div className="save-report">
                <div className="field"><label htmlFor="save-name">Save as a Saved Report</label><input id="save-name" name="name" maxLength={120} placeholder="Name, e.g. Monthly on-flow orders" /></div>
                <label className="check"><input type="checkbox" name="shared" /> Share with everyone who has Walker</label>
                <button className="btn ghost" data-save="1">Save Report</button>
              </div>
            </div>
          </form>
        </aside>
        <div className="search-main">
          <ExportColumns key={JSON.stringify(data.export)} data={data} onSaved={reload} />
          <div className="panel"><div className="panel-head"><h2>Recent Runs</h2></div><div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Requested</th><th>By</th><th className="num">Rows</th><th>Status</th><th><span className="sr-only">Download or rerun</span></th></tr></thead>
            <tbody>
              {data.runs.map((r: any) => (
                <tr key={r.id} className="click" onClick={() => navigate(`/walker/runs/${r.id}`)}><td className="nowrap"><Link to={`/walker/runs/${r.id}`}><b>#{r.id}</b></Link></td><td>{when(r.createdAt)}</td>
                  <td>{r.by ?? 'System'}</td><td className="num">{r.status === 'done' ? r.rows.toLocaleString() : '—'}</td><td><StatePill state={r.state} /></td><td className="actions"><RowActions run={r} onRerun={rerun} /></td></tr>
              ))}
              {data.runs.length === 0 && <EmptyRow cols={6}>Not run yet.</EmptyRow>}
            </tbody>
          </table></div></div>
        </div>
      </div>
    </>
  );
}

/** Walker → Saved Reports (ebunny's walker/saved). */
export function Saved() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any[]>('/api/walker/saved');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Saved Reports" sub="Reports saved with their settings, yours and the ones others shared. Open one to run it again in one click. Save one from any report under Run a Report (or from a past run)."
        actions={<Link className="btn cyan" to="/walker/reports">Run a Report</Link>} />
      <div className="panel"><div className="table-wrap"><table data-page="10">
        <thead><tr><th>ID</th><th>Name</th><th>Report</th><th>Settings</th><th>Saved By</th><th className="num">Runs</th><th>Last Run</th></tr></thead>
        <tbody>
          {data.map((s) => (
            <tr key={s.id} className="click" onClick={() => navigate(`/walker/saved-reports/${s.id}`)}><td className="nowrap"><Link to={`/walker/saved-reports/${s.id}`}><b>#{s.id}</b></Link></td>
              <td><b>{s.name}</b>{s.shared && <> <Pill tone="info">Shared</Pill></>}</td>
              <td>{s.title}<div className="help">{s.format}{s.summary ? ' · summary' : ''}</div></td>
              <td className="wrap help">{s.settings}</td>
              <td>{s.by}</td><td className="num">{s.runs.toLocaleString()}</td>
              <td className="nowrap">{s.lastRunAt ? when(s.lastRunAt) : 'Not yet'}</td></tr>
          ))}
          {data.length === 0 && <EmptyRow cols={7}>No saved reports yet. Open a report under Run a Report and use Save Report.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** One saved report (ebunny's walker/saved-show): its settings, Run Now, and the runs it made. */
export function SavedShow() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const { data, error } = useLoad<any>(`/api/walker/saved/${id}?page=${page}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const s = data.saved, def = data.def;
  return (
    <>
      <PageHead title={s.name} sub={`${def?.title ?? s.report} · saved by ${s.mine ? 'you' : s.by ?? '—'} ${new Date(s.createdAt).toLocaleDateString('en-US')}${s.shared ? ' · shared with everyone' : ''}`}
        actions={<><Link className="btn ghost btn-back" to="/walker/saved-reports">Back to Saved Reports</Link>
          {def && <><Link className="btn ghost" to={`/walker/reports/${s.report}?saved=${s.id}`}>Change Settings</Link>
            <button className="btn cyan" onClick={() => run(api.post<any>(`/api/walker/saved/${s.id}/run`).then((x) => { navigate(`/walker/runs/${x.id}`); return x; }))}>Run Now</button></>}</>} />
      {bar}
      {!def && <div className="alert bad"><b>Report removed</b> This report is no longer in the library, so it can't run.</div>}
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Settings</h2></div><div className="panel-body"><dl className="kv">
          <dt>Report</dt><dd>{def ? <Link to={`/walker/reports/${s.report}`}>{def.title}</Link> : s.report}</dd>
          {Object.entries(s.params ?? {}).map(([k, v]: [string, any]) => [<dt key={k + 'a'}>{headline(k)}</dt>, <dd key={k + 'b'}>{Array.isArray(v) ? v.join(', ') : v === '' || v == null ? '—' : String(v)}</dd>])}
          <dt>Export As</dt><dd>{s.format}</dd>
          <dt>Summary Only</dt><dd>{s.summary ? 'Yes' : 'No'}</dd>
          <dt>Last Run</dt><dd>{s.lastRunAt ? when(s.lastRunAt) : 'Not yet'}</dd>
        </dl></div>
          {data.canRemove && <div className="panel-body"><button className="btn sm ghost" onClick={() => confirm(`Remove ${s.name}?\nIt comes off Saved Reports. Its past runs and files are kept.`)
            && run(api.del(`/api/walker/saved/${s.id}`).then((x) => { navigate('/walker/saved-reports'); return x; }))}>Remove Saved Report</button></div>}
        </div>
        <div className="panel"><div className="panel-head"><h2>Runs</h2></div><div className="table-wrap"><table data-page="0">
          <thead><tr><th>ID</th><th>Requested</th><th>By</th><th className="num">Rows</th><th>Status</th></tr></thead>
          <tbody>
            {data.runs.map((r: any) => (
              <tr key={r.id} className="click" onClick={() => navigate(`/walker/runs/${r.id}`)}><td className="nowrap"><Link to={`/walker/runs/${r.id}`}><b>#{r.id}</b></Link></td><td className="nowrap">{when(r.createdAt)}</td>
                <td>{r.by ?? 'System'}</td><td className="num">{r.status === 'done' ? r.rows.toLocaleString() : '—'}</td><td><StatePill state={r.state} /></td></tr>
            ))}
            {data.runs.length === 0 && <EmptyRow cols={5}>Not run yet. Use Run Now at the top right.</EmptyRow>}
          </tbody>
        </table></div>
          <PagerBar page={data.page} total={data.total} size={10} onPage={setPage} /></div>
      </div>
    </>
  );
}
