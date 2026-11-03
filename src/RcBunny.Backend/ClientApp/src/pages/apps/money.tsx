import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, post, put, qs } from '../../api';
import { Bars, Columns, date, dateTime, ErrorBox, money, num, Panel, Pill, Refreshing, Skeleton, Stat, Stats } from '../../ui';

// ---------------------------------------------------------------- Walker

interface RunRow { id: number; title: string; status: string; rows: number; format: string; createdAt: string; finishedAt: string | null; durationMs: number | null; who: string; app: string }
const statusTone = (s: string) => (s === 'completed' ? 'ok' : s === 'failed' || s === 'timeout' ? 'bad' : 'info');

export function ReportStatus() {
  const [tab, setTab] = useState('running');
  const q = useQuery({ queryKey: ['walker', 'status', tab], queryFn: () => api<{ counts: Record<string, number>; rows: RunRow[] }>(`/api/walker/status${qs({ tab })}`), refetchInterval: 5000 });
  const c = q.data?.counts ?? {};
  return (
    <>
      <div className="page-head"><h1>Report Status</h1><Link className="btn" to="/walker/reports">Run a Report</Link></div>
      <div className="tabs">
        <button className={tab === 'running' ? 'on' : ''} onClick={() => setTab('running')}>Running ({(c.running ?? 0) + (c.queued ?? 0)})</button>
        <button className={tab === 'completed' ? 'on' : ''} onClick={() => setTab('completed')}>Completed ({c.completed ?? 0})</button>
        <button className={tab === 'problems' ? 'on' : ''} onClick={() => setTab('problems')}>Errored / did not finish ({(c.failed ?? 0) + (c.timeout ?? 0)})</button>
      </div>
      <Panel actions={<Refreshing on={q.isFetching && !q.isPending} />}>
        {q.isPending ? <Skeleton rows={8} /> : q.data!.rows.length === 0 ? <p className="muted">Nothing here.</p> : (
          <table className="table"><thead><tr><th>Report</th><th>Who</th><th>Status</th><th className="num">Rows</th><th>Started</th><th className="num">Took</th></tr></thead>
            <tbody>{q.data!.rows.map((r) => <tr key={r.id}><td><Link to={`/walker/runs/${r.id}`}>{r.title}</Link> <span className="muted small">{r.format}</span></td><td>{r.who}</td><td><Pill tone={statusTone(r.status)}>{r.status}</Pill></td><td className="num">{num(r.rows)}</td><td>{dateTime(r.createdAt)}</td><td className="num">{r.durationMs != null ? `${(r.durationMs / 1000).toFixed(1)}s` : ''}</td></tr>)}</tbody></table>
        )}
      </Panel>
    </>
  );
}

interface ReportDef { key: string; title: string; category: string; description: string; filters: { name: string; label: string; type: string; options: string[] | null }[]; defaults: Record<string, string> | null }
const useReports = () => useQuery({ queryKey: ['walker', 'reports'], queryFn: () => api<ReportDef[]>('/api/walker/reports'), staleTime: 30 * 60_000 });

export function ReportsIndex() {
  const q = useReports();
  const cats = [...new Set(q.data?.map((r) => r.category))];
  return (
    <>
      <h1>Run a Report</h1>
      {q.isPending ? <Skeleton rows={8} /> : cats.map((c) => (
        <Panel key={c} title={c}>
          <table className="table"><tbody>{q.data!.filter((r) => r.category === c).map((r) => <tr key={r.key}><td><Link to={`/walker/reports/${r.key}`}>{r.title}</Link></td><td className="muted">{r.description}</td></tr>)}</tbody></table>
        </Panel>
      ))}
    </>
  );
}

export function ReportForm() {
  const { key = '' } = useParams();
  const navigate = useNavigate();
  const q = useReports();
  const def = q.data?.find((r) => r.key === key);
  const [params, setParams] = useState<Record<string, string>>({});
  const [format, setFormat] = useState('xlsx');
  const [summary, setSummary] = useState(false);
  const [saveAs, setSaveAs] = useState('');
  useEffect(() => { if (def?.defaults) setParams(def.defaults); }, [def]);
  const run = useMutation({ mutationFn: () => post<{ id: number }>(`/api/walker/reports/${key}`, { params, format, summary, saveAs: saveAs || null }), onSuccess: (r) => navigate(`/walker/runs/${r.id}`) });
  if (!def) return q.isPending ? <Skeleton rows={6} /> : <ErrorBox error={new Error('No such report')} />;
  const submit = (e: FormEvent) => { e.preventDefault(); run.mutate(); };
  return (
    <form onSubmit={submit}>
      <p className="crumbs"><Link to="/walker/reports">Run a Report</Link> › {def.title}</p>
      <h1>{def.title}</h1>
      <p className="muted">{def.description} It runs in the background; you'll get a notification when the file is ready.</p>
      <ErrorBox error={run.error} />
      <Panel title="Filters">
        <div className="form-grid">
          {def.filters.map((f) => (
            <label key={f.name}>{f.label}
              {f.type === 'date' ? <input type="date" value={params[f.name] ?? ''} onChange={(e) => setParams({ ...params, [f.name]: e.target.value })} />
                : f.type === 'select' ? <select value={params[f.name] ?? ''} onChange={(e) => setParams({ ...params, [f.name]: e.target.value })}><option value="">Any</option>{f.options?.map((o) => <option key={o}>{o}</option>)}</select>
                  : f.type === 'multi' ? (
                    <select multiple size={6} value={(params[f.name] ?? '').split(',').filter(Boolean)} onChange={(e) => setParams({ ...params, [f.name]: [...e.target.selectedOptions].map((o) => o.value).join(',') })}>{f.options?.map((o) => <option key={o}>{o}</option>)}</select>
                  ) : <input value={params[f.name] ?? ''} onChange={(e) => setParams({ ...params, [f.name]: e.target.value })} />}
            </label>
          ))}
        </div>
      </Panel>
      <Panel title="Output">
        <div className="form-grid">
          <label>Format<select value={format} onChange={(e) => setFormat(e.target.value)}><option value="xlsx">Excel</option><option value="csv">CSV</option><option value="tsv">Tab-separated</option><option value="json">JSON</option></select></label>
          <label className="check"><input type="checkbox" checked={summary} onChange={(e) => setSummary(e.target.checked)} /> Summary instead of every row</label>
          <label>Save these settings as (optional)<input value={saveAs} onChange={(e) => setSaveAs(e.target.value)} placeholder="e.g. Weekly failed payments" /></label>
        </div>
        <p className="small"><Link to={`/walker/reports/${key}/columns`}>Export Columns</Link>: choose and rename the columns in the file.</p>
      </Panel>
      <div className="row"><button className="btn" disabled={run.isPending}>Run Report</button></div>
    </form>
  );
}

export function ReportColumns() {
  const { key = '' } = useParams();
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['walker', 'columns', key], queryFn: () => api<{ available: string[]; columns: { key: string; title: string }[] | null }>(`/api/walker/reports/${key}/columns`) });
  const [map, setMap] = useState<{ key: string; title: string }[]>([]);
  useEffect(() => { if (q.data) setMap(q.data.columns ?? q.data.available.map((a) => ({ key: a, title: a }))); }, [q.data]);
  const save = useMutation({ mutationFn: (m: typeof map) => put(`/api/walker/reports/${key}/columns`, m), onSuccess: () => qc.invalidateQueries({ queryKey: ['walker', 'columns', key] }) });
  if (!q.data) return <Skeleton rows={6} />;
  const move = (i: number, d: number) => setMap((m) => { const n = [...m]; const [x] = n.splice(i, 1); n.splice(i + d, 0, x); return n; });
  return (
    <>
      <p className="crumbs"><Link to={`/walker/reports/${key}`}>Report</Link> › Export Columns</p>
      <h1>Export Columns</h1>
      <Panel>
        <ErrorBox error={save.error} />{save.isSuccess && <div className="alert ok">Saved.</div>}
        <table className="table compact grid-edit"><thead><tr><th>Column</th><th>Title in the file</th><th /></tr></thead>
          <tbody>{map.map((c, i) => <tr key={c.key}><td>{c.key}</td><td><input value={c.title} onChange={(e) => setMap((m) => m.map((x, k) => (k === i ? { ...x, title: e.target.value } : x)))} /></td>
            <td className="row" style={{ margin: 0 }}><button className="btn outline small" disabled={i === 0} onClick={() => move(i, -1)}>↑</button><button className="btn outline small" disabled={i === map.length - 1} onClick={() => move(i, 1)}>↓</button><button className="btn outline small danger" onClick={() => setMap((m) => m.filter((_, k) => k !== i))}>Remove</button></td></tr>)}</tbody></table>
        <div className="row">
          <select onChange={(e) => { if (e.target.value) setMap((m) => [...m, { key: e.target.value, title: e.target.value }]); e.target.value = ''; }} aria-label="Add a column"><option value="">+ Add a column</option>{q.data.available.filter((a) => !map.some((m) => m.key === a)).map((a) => <option key={a}>{a}</option>)}</select>
          <button className="btn" onClick={() => save.mutate(map)}>Save</button>
          <button className="btn outline" onClick={() => save.mutate([])}>Back to the standard columns</button>
        </div>
      </Panel>
    </>
  );
}

interface RunDetail { id: number; title: string; report: string; status: string; rows: number; format: string; summary: boolean; params: Record<string, unknown>; preview: { columns: string[]; rows: (string | null)[][] } | null; error: string | null; file: string | null; fileSize: number | null; createdAt: string; startedAt: string | null; finishedAt: string | null; durationMs: number | null; who: string; rerunOf: number | null }

export function RunPage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const q = useQuery({ queryKey: ['walker', 'run', id], queryFn: () => api<RunDetail>(`/api/walker/runs/${id}`), refetchInterval: (query) => (['queued', 'running'].includes(query.state.data?.status ?? '') ? 2000 : false) });
  const rerun = useMutation({ mutationFn: () => post<{ id: number }>(`/api/walker/runs/${id}/rerun`), onSuccess: (r) => navigate(`/walker/runs/${r.id}`) });
  if (!q.data) return q.error ? <ErrorBox error={q.error} /> : <Skeleton rows={8} />;
  const r = q.data;
  return (
    <>
      <p className="crumbs"><Link to="/walker">Report Status</Link> › {r.title}</p>
      <div className="page-head">
        <div><h1>{r.title} <Pill tone={statusTone(r.status)}>{r.status}</Pill></h1><p className="muted">Run by {r.who} · {dateTime(r.createdAt)}{r.rerunOf && <> · rerun of <Link to={`/walker/runs/${r.rerunOf}`}>#{r.rerunOf}</Link></>}</p></div>
        <div className="row">
          {r.status === 'completed' && <a className="btn" href={`/api/walker/runs/${r.id}/download`}>Download {r.format.toUpperCase()}</a>}
          <button className="btn outline" onClick={() => rerun.mutate()}>Rerun</button>
        </div>
      </div>
      {r.error && <div className="alert bad">{r.error}</div>}
      <div className="grid-2">
        <Panel title="File"><dl className="facts"><dt>Rows</dt><dd>{num(r.rows)}</dd><dt>Format</dt><dd>{r.format}{r.summary && ' (summary)'}</dd><dt>Size</dt><dd>{r.fileSize ? `${Math.round(r.fileSize / 1024)} KB` : '—'}</dd><dt>Took</dt><dd>{r.durationMs != null ? `${(r.durationMs / 1000).toFixed(1)} s` : '—'}</dd></dl></Panel>
        <Panel title="Settings"><dl className="facts">{Object.entries(r.params ?? {}).map(([k, v]) => <div key={k} style={{ display: 'contents' }}><dt>{k}</dt><dd>{String(v)}</dd></div>)}</dl></Panel>
      </div>
      {r.preview && (
        <Panel title="First rows">
          <div className="table-wrap"><table className="table compact"><thead><tr>{r.preview.columns.map((c) => <th key={c}>{c}</th>)}</tr></thead>
            <tbody>{r.preview.rows.map((row, i) => <tr key={i}>{row.map((v, j) => <td key={j}>{v}</td>)}</tr>)}</tbody></table></div>
        </Panel>
      )}
    </>
  );
}

export function Upload() {
  const navigate = useNavigate();
  const [f, setF] = useState({ title: '', category: 'etl', description: '' });
  const [file, setFile] = useState<File | null>(null);
  const up = useMutation({
    mutationFn: async () => {
      const fd = new FormData(); fd.append('file', file!); Object.entries(f).forEach(([k, v]) => fd.append(k, v));
      const res = await fetch('/api/walker/uploads', { method: 'POST', body: fd, headers: { 'X-RCB': '1' }, credentials: 'same-origin' });
      const body = await res.json(); if (!res.ok) throw new Error(body.message ?? 'Upload failed'); return body as { id: number };
    },
    onSuccess: (r) => navigate(`/walker/r/report-uploads/${r.id}`),
  });
  const folders: Record<string, string> = { etl: 'ETL', legal: 'Legal Documents', templates: 'Customer Templates', finance: 'Finance', ercot: 'Utility / ERCOT', vendor: 'Vendor' };
  return (
    <>
      <h1>Upload a File</h1>
      <Panel>
        <ErrorBox error={up.error} />
        <div className="form-grid">
          <label>File<input type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} /></label>
          <label>Title<input value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} /></label>
          <label>Folder<select value={f.category} onChange={(e) => setF({ ...f, category: e.target.value })}>{Object.entries(folders).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
          <label className="span-all">Description<textarea rows={3} value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} /></label>
        </div>
        <div className="row"><button className="btn" disabled={!file || up.isPending} onClick={() => up.mutate()}>Upload</button></div>
      </Panel>
    </>
  );
}

export function Etl() {
  const q = useQuery({ queryKey: ['walker', 'etl'], queryFn: () => api<{ feeds: { key: string; name: string; direction: string; party: string; schedule: string; format: string; file: string; target: string; status: string }[]; uploads: { id: number; title: string; originalName: string; size: number; createdAt: string }[] }>('/api/walker/etl') });
  return (
    <>
      <h1>ETL Feeds</h1>
      {!q.data ? <Skeleton rows={8} /> : (
        <>
          <Panel><table className="table"><thead><tr><th>Feed</th><th>Direction</th><th>Schedule</th><th>File</th><th>Goes to</th><th>Status</th></tr></thead>
            <tbody>{q.data.feeds.map((f) => <tr key={f.key}><td>{f.name}<div className="muted small">{f.party}</div></td><td>{f.direction}</td><td>{f.schedule}</td><td className="mono small">{f.file}<div className="muted">{f.format}</div></td><td>{f.target}</td><td><Pill tone={f.status.startsWith('Skipped') ? 'warn' : 'info'}>{f.status}</Pill></td></tr>)}</tbody></table></Panel>
          <Panel title="Recent files"><table className="table compact"><tbody>{q.data.uploads.map((u) => <tr key={u.id}><td><Link to={`/walker/r/report-uploads/${u.id}`}>{u.title}</Link></td><td className="mono small">{u.originalName}</td><td>{dateTime(u.createdAt)}</td></tr>)}</tbody></table></Panel>
        </>
      )}
    </>
  );
}

// ---------------------------------------------------------------- Caboose

export function CabooseDashboard() {
  const q = useQuery({ queryKey: ['caboose', 'dashboard'], queryFn: () => api<{ collected30: number; payments30: number; failed30: number; reversed30: number; byMethod: { method: string; amount: number; n: number }[]; byDay: { day: string; amount: number }[]; receivables: number; credits: number; pastDue: number; depositsHeld: number; depositsDue: number; pendingLedger: number; refundsRequested: number; refundsApproved: number; canDecide: boolean }>('/api/caboose/dashboard') });
  const d = q.data;
  return (
    <>
      <h1>Finance</h1>
      {!d ? <Skeleton rows={6} /> : (
        <>
          <Stats>
            <Stat label="Collected, 30 days" value={money(d.collected30)} to="/caboose/r/caboose-payments" />
            <Stat label="Receivables" value={money(d.receivables)} to="/caboose/aging" />
            <Stat label="Past due" value={money(d.pastDue)} tone={d.pastDue ? 'bad' : undefined} to="/caboose/aging" />
            <Stat label="Credit balances" value={money(d.credits)} />
            <Stat label="Deposits held" value={money(d.depositsHeld)} to="/caboose/deposits" />
            <Stat label="Deposits due" value={money(d.depositsDue)} to="/caboose/deposits" />
          </Stats>
          <Stats>
            <Stat label="Credits & debits to apply" value={d.pendingLedger} to="/caboose/r/pending-ledger?f.status=pending" tone={d.pendingLedger ? 'warn' : undefined} />
            <Stat label="Refunds to decide" value={d.refundsRequested} to="/caboose/r/refunds?f.status=requested" tone={d.refundsRequested ? 'warn' : undefined} />
            <Stat label="Refunds to pay" value={d.refundsApproved} to="/caboose/r/refunds?f.status=approved" />
            <Stat label="Failed / reversed, 30 days" value={`${d.failed30} / ${d.reversed30}`} />
          </Stats>
          {!d.canDecide && <p className="muted small">Approving refunds and reversing payments need the Refunds right.</p>}
          <div className="grid-2">
            <Panel title="Payments by day"><Columns rows={d.byDay.map((x) => ({ label: String(x.day).slice(5), value: x.amount }))} format={money} /></Panel>
            <Panel title="By method"><Bars rows={d.byMethod.map((m) => ({ label: m.method, value: m.amount, hint: `${m.n} payments` }))} format={money} /></Panel>
          </div>
        </>
      )}
    </>
  );
}

export function Deposits() {
  const q = useQuery({ queryKey: ['caboose', 'deposits'], queryFn: () => api<{ id: number; account: string; name: string; status: string; depositDue: number; depositHeld: number; tecScore: number | null; createdAt: string }[]>('/api/caboose/deposits') });
  return (
    <>
      <h1>Deposits</h1>
      <p className="muted">Deposits held and due. Thresholds and caps are in <Link to="/caboose/data/deposit-thresholds">Deposit Thresholds</Link> and <Link to="/caboose/data/max-deposits">Max Deposits</Link>.</p>
      <Panel>{q.isPending ? <Skeleton rows={8} /> : <table className="table"><thead><tr><th>Account</th><th>Status</th><th className="num">Credit score</th><th className="num">Due</th><th className="num">Held</th></tr></thead>
        <tbody>{q.data?.map((c) => <tr key={c.id}><td><Link to={`/corral/customers/${c.id}`}>{c.account}</Link> {c.name}</td><td>{c.status}</td><td className="num">{c.tecScore ?? '—'}</td><td className="num">{c.depositDue ? <strong>{money(c.depositDue)}</strong> : '—'}</td><td className="num">{money(c.depositHeld)}</td></tr>)}</tbody></table>}</Panel>
    </>
  );
}

export function Aging() {
  const q = useQuery({ queryKey: ['caboose', 'aging'], queryFn: () => api<{ buckets: { bucket: string; accounts: number; balance: number }[]; accounts: { id: number; account: string; name: string; status: string; market: string; balance: number; dueDate: string; days: number; bucket: string }[] }>('/api/caboose/aging') });
  return (
    <>
      <div className="page-head"><h1>Receivables Aging</h1><Link className="btn outline" to="/walker/reports/receivables">Export</Link></div>
      {!q.data ? <Skeleton rows={8} /> : (
        <>
          <Stats>{q.data.buckets.map((b) => <Stat key={b.bucket} label={`${b.bucket} · ${b.accounts} accounts`} value={money(b.balance)} tone={b.bucket === '90+' && b.balance ? 'bad' : undefined} />)}</Stats>
          <Panel title="Oldest first"><table className="table compact"><thead><tr><th>Account</th><th>Status</th><th>Utility</th><th className="num">Balance</th><th>Due</th><th className="num">Days</th><th>Aging</th></tr></thead>
            <tbody>{q.data.accounts.map((a) => <tr key={a.id}><td><Link to={`/corral/customers/${a.id}`}>{a.account}</Link> {a.name}</td><td>{a.status}</td><td>{a.market}</td><td className="num">{money(a.balance)}</td><td>{date(a.dueDate)}</td><td className="num">{a.days}</td><td><Pill tone={a.bucket === 'Current' ? 'ok' : a.bucket === '90+' ? 'bad' : 'warn'}>{a.bucket}</Pill></td></tr>)}</tbody></table></Panel>
        </>
      )}
    </>
  );
}

export function Journal() {
  const today = new Date().toISOString().slice(0, 10);
  const [range, setRange] = useState({ from: new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10), to: today });
  const q = useQuery({ queryKey: ['caboose', 'journal', range], queryFn: () => api<{ columns: string[]; lines: (string | number)[][]; debits: number; credits: number }>(`/api/caboose/journal${qs(range)}`) });
  return (
    <>
      <div className="page-head">
        <h1>Journal</h1>
        <div className="row"><a className="btn outline" href={`/api/caboose/journal${qs({ ...range, format: 'csv' })}`}>CSV</a><a className="btn outline" href={`/api/caboose/journal${qs({ ...range, format: 'xlsx' })}`}>Excel</a></div>
      </div>
      <Panel>
        <div className="filters"><label className="inline">From <input type="date" value={range.from} onChange={(e) => setRange({ ...range, from: e.target.value })} /></label><label className="inline">To <input type="date" value={range.to} onChange={(e) => setRange({ ...range, to: e.target.value })} /></label></div>
        {!q.data ? <Skeleton rows={10} /> : (
          <>
            <p className="small">Debits {money(q.data.debits)} · Credits {money(q.data.credits)} {Math.abs(q.data.debits - q.data.credits) < 0.01 ? <Pill tone="ok">balanced</Pill> : <Pill tone="bad">out of balance</Pill>}</p>
            <div className="table-wrap"><table className="table compact"><thead><tr>{q.data.columns.map((c) => <th key={c}>{c}</th>)}</tr></thead>
              <tbody>{q.data.lines.slice(0, 500).map((l, i) => <tr key={i}><td>{date(String(l[0]))}</td><td>{l[1]}</td><td className="num">{Number(l[2]) ? money(Number(l[2])) : ''}</td><td className="num">{Number(l[3]) ? money(Number(l[3])) : ''}</td><td className="muted">{l[4]}</td></tr>)}</tbody></table></div>
          </>
        )}
      </Panel>
    </>
  );
}

// ---------------------------------------------------------------- Bounty

export function BountyDashboard() {
  const q = useQuery({ queryKey: ['bounty', 'dashboard'], queryFn: () => api<{ outstanding: number; members: number; earned30: number; spent30: number; pendingRedemptions: number; byReason: { reason: string; stars: number }[]; top: { id: number; account: string; name: string; stars: number }[]; offers: { id: number; name: string; stars: number; effect: string }[] }>('/api/bounty/dashboard') });
  const d = q.data;
  return (
    <>
      <h1>Rewards</h1>
      {!d ? <Skeleton rows={6} /> : (
        <>
          <Stats><Stat label="Stars outstanding" value={num(d.outstanding)} /><Stat label="Members with stars" value={num(d.members)} to="/bounty/r/members" /><Stat label="Earned, 30 days" value={num(d.earned30)} /><Stat label="Spent, 30 days" value={num(d.spent30)} /><Stat label="Redemptions to fulfil" value={d.pendingRedemptions} to="/bounty/r/redemptions?f.fulfillment=pending" tone={d.pendingRedemptions ? 'warn' : undefined} /></Stats>
          <div className="grid-2">
            <Panel title="How stars were earned (30 days)"><Bars rows={d.byReason.map((r) => ({ label: r.reason, value: r.stars }))} /></Panel>
            <Panel title="Top members"><table className="table compact"><tbody>{d.top.map((m) => <tr key={m.id}><td><Link to={`/bounty/r/members/${m.id}`}>{m.account}</Link> {m.name}</td><td className="num">{num(m.stars)}</td></tr>)}</tbody></table></Panel>
          </div>
          <Panel title="Offers"><table className="table compact"><tbody>{d.offers.map((o) => <tr key={o.id}><td><Link to={`/bounty/r/reward-offers/${o.id}`}>{o.name}</Link></td><td>{o.effect}</td><td className="num">{num(o.stars)} stars</td></tr>)}</tbody></table></Panel>
        </>
      )}
    </>
  );
}

export function Drawing() {
  const qc = useQueryClient();
  const thisMonth = new Date().toISOString().slice(0, 7);
  const [month, setMonth] = useState(thisMonth);
  const [prize, setPrize] = useState('$100 bill credit');
  const q = useQuery({ queryKey: ['bounty', 'drawing', month], queryFn: () => api<{ month: string; entries: { id: number; customerId: number; account: string; name: string; createdAt: string }[]; winners: string[][]; drawn: boolean }>(`/api/bounty/drawing${qs({ month })}`) });
  const draw = useMutation({ mutationFn: () => post<{ winner: { id: number; account: string; name: string }; entries: number }>('/api/bounty/drawing', { month, prize }), onSuccess: () => qc.invalidateQueries({ queryKey: ['bounty'] }) });
  return (
    <>
      <h1>Monthly Drawing</h1>
      <Panel>
        <div className="filters">
          <label className="inline">Month <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} /></label>
          <label className="inline">Prize <input value={prize} onChange={(e) => setPrize(e.target.value)} /></label>
          <button className="btn" disabled={!q.data?.entries.length || q.data?.drawn || draw.isPending} onClick={() => confirm(`Draw a winner from ${q.data?.entries.length} entries?`) && draw.mutate()}>Draw a Winner</button>
        </div>
        <ErrorBox error={draw.error} />
        {draw.data && <div className="alert ok">🎉 {draw.data.winner.name} ({draw.data.winner.account}) won, out of {draw.data.entries} entries.</div>}
        {q.data?.drawn && !draw.data && <p className="muted">This month has been drawn.</p>}
      </Panel>
      <div className="grid-2">
        <Panel title={`Entries for ${month} (${q.data?.entries.length ?? '…'})`}><table className="table compact"><tbody>{q.data?.entries.map((e) => <tr key={e.id}><td><Link to={`/corral/customers/${e.customerId}`}>{e.account}</Link> {e.name}</td><td>{date(e.createdAt)}</td></tr>)}</tbody></table></Panel>
        <Panel title="Winners" actions={<Link to="/bounty/data/monthly-drawing">Edit</Link>}><table className="table compact"><thead><tr><th>Month</th><th>Prize</th><th>Account</th><th>Drawn</th></tr></thead><tbody>{q.data?.winners.map((w, i) => <tr key={i}>{w.map((c, j) => <td key={j}>{c}</td>)}</tr>)}</tbody></table></Panel>
      </div>
    </>
  );
}
