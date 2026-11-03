import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { api, post, put, qs } from '../../api';
import { Bars, Columns, date, ErrorBox, Panel, Pill, Skeleton, Stat, Stats } from '../../ui';
import tokens from '../../tokens.css?raw';

// ---------------------------------------------------------------- Lando

interface LandoDash { onlineNow: number; visitors24h: number; views24h: number; views7d: number; visitors7d: number; blocked7d: number; topPages: { path: string; views: number }[]; referrers: { host: string; visits: number }[]; byDay: { day: string; views: number; visitors: number }[]; consents: Record<string, number>; pages: { status: string; n: number }[]; blocks: number }

export function LandoDashboard() {
  const q = useQuery({ queryKey: ['lando', 'dashboard'], queryFn: () => api<LandoDash>('/api/lando/dashboard'), refetchInterval: 60_000 });
  const d = q.data;
  return (
    <>
      <h1>Website</h1>
      {!d ? <Skeleton rows={6} /> : (
        <>
          <Stats>
            <Stat label="On the site now" value={d.onlineNow} />
            <Stat label="Visitors, 24 h" value={d.visitors24h} />
            <Stat label="Page views, 7 days" value={d.views7d} to="/lando/r/site-visits" />
            <Stat label="Blocked visits, 7 days" value={d.blocked7d} to="/lando/r/blocked-ips" />
            <Stat label="Active blocks" value={d.blocks} />
          </Stats>
          <Panel title="Views by day"><Columns rows={d.byDay.map((x) => ({ label: x.day.slice(5), value: x.views }))} /></Panel>
          <div className="grid-2">
            <Panel title="Top pages"><Bars rows={d.topPages.map((p) => ({ label: p.path || '/', value: p.views }))} /></Panel>
            <Panel title="Where visitors come from"><Bars rows={d.referrers.map((r) => ({ label: r.host, value: r.visits }))} /></Panel>
            <Panel title="Cookie choices (30 days)"><Bars rows={Object.entries(d.consents).map(([k, v]) => ({ label: k, value: v }))} /></Panel>
            <Panel title="Pages"><Bars rows={d.pages.map((p) => ({ label: p.status, value: p.n }))} /></Panel>
          </div>
        </>
      )}
    </>
  );
}

export function SiteHealth() {
  const q = useQuery({ queryKey: ['lando', 'health'], queryFn: () => api<{ pages: number; published: number; unusedBlocks: number; issues: { id: number; path: string; problem: string; severity: string }[] }>('/api/lando/health') });
  return (
    <>
      <h1>Site Health</h1>
      {!q.data ? <Skeleton rows={6} /> : (
        <>
          <Stats><Stat label="Pages" value={q.data.pages} /><Stat label="Published" value={q.data.published} /><Stat label="Problems" value={q.data.issues.length} tone={q.data.issues.length ? 'warn' : 'ok'} /><Stat label="Unused content blocks" value={q.data.unusedBlocks} /></Stats>
          <Panel title="Problems">
            {q.data.issues.length === 0 ? <p className="ok-text">Nothing to fix.</p> : (
              <table className="table compact"><tbody>{q.data.issues.map((i, n) => <tr key={n}><td><Pill tone={i.severity}>{i.severity === 'bad' ? 'Fix' : 'Check'}</Pill></td><td><Link to={`/lando/r/pages/${i.id}`}>/{i.path}</Link></td><td>{i.problem}</td></tr>)}</tbody></table>
            )}
          </Panel>
        </>
      )}
    </>
  );
}

export function Sitemap() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['lando', 'sitemap'], queryFn: () => api<{ path: string; title: string; updatedAt: string }[]>('/api/lando/sitemap') });
  const rebuild = useMutation({ mutationFn: () => post<{ message: string }>('/api/lando/sitemap'), onSuccess: () => qc.invalidateQueries({ queryKey: ['lando'] }) });
  return (
    <>
      <div className="page-head"><h1>Update Sitemap</h1><button className="btn" onClick={() => rebuild.mutate()}>Rebuild Sitemap</button></div>
      {rebuild.data && <div className="alert ok">{rebuild.data.message}</div>}
      <ErrorBox error={rebuild.error} />
      <Panel title={`${q.data?.length ?? '…'} pages in /sitemap.xml`}>
        {q.isPending ? <Skeleton rows={8} /> : <table className="table compact"><tbody>{q.data?.map((p) => <tr key={p.path}><td className="mono">/{p.path}</td><td>{p.title}</td><td>{date(p.updatedAt)}</td></tr>)}</tbody></table>}
      </Panel>
    </>
  );
}

/** Style Guide: every color token from the shared stylesheet, with live pickers and the changed CSS to copy. */
export function StyleGuide() {
  const base = useMemo(() => [...tokens.matchAll(/--([a-z0-9-]+):\s*(#[0-9a-fA-F]{3,8})/g)].map((m) => ({ name: m[1], value: m[2] })), []);
  const [vals, setVals] = useState<Record<string, string>>({});
  const changed = base.filter((t) => vals[t.name] && vals[t.name].toLowerCase() !== t.value.toLowerCase());
  useEffect(() => {
    for (const t of base) document.documentElement.style.setProperty(`--${t.name}`, vals[t.name] ?? t.value);
  }, [vals, base]);
  useEffect(() => () => { for (const t of base) document.documentElement.style.removeProperty(`--${t.name}`); }, [base]);
  return (
    <>
      <h1>Style Guide</h1>
      <p className="muted">Colors shared by the website and the admin (tokens.css). Changing one here previews it across this admin; copy the CSS below into tokens.css in both projects to keep it.</p>
      <div className="grid-2">
        <Panel title="Colors">
          <table className="table compact"><tbody>
            {base.map((t) => (
              <tr key={t.name}><td><span className="swatch" style={{ background: vals[t.name] ?? t.value }} /></td><td className="mono">--{t.name}</td>
                <td><input type="color" value={(vals[t.name] ?? t.value).slice(0, 7)} onChange={(e) => setVals((v) => ({ ...v, [t.name]: e.target.value }))} aria-label={t.name} /></td><td className="mono">{vals[t.name] ?? t.value}</td></tr>
            ))}
          </tbody></table>
        </Panel>
        <div>
          <Panel title="Components">
            <div className="row"><button className="btn">Primary</button><button className="btn outline">Outline</button><button className="btn danger">Danger</button></div>
            <div className="row"><Pill tone="ok">ok</Pill><Pill tone="warn">warn</Pill><Pill tone="bad">bad</Pill><Pill tone="info">info</Pill></div>
            <div className="alert ok" style={{ marginTop: 12 }}>Saved.</div><div className="alert bad">Example: this is how an error looks.</div>
          </Panel>
          <Panel title="Changed CSS">
            {changed.length === 0 ? <p className="muted">Pick a color to see the CSS.</p> : <pre className="json">{`:root {\n${changed.map((t) => `  --${t.name}: ${vals[t.name]};`).join('\n')}\n}`}</pre>}
            {changed.length > 0 && <button className="btn outline small" onClick={() => setVals({})}>Reset</button>}
          </Panel>
        </div>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- Reference data tables

export function DataIndex() {
  const q = useQuery({ queryKey: ['sheriff', 'data'], queryFn: () => api<{ key: string; name: string; app: string; columns: number }[]>('/api/data') });
  return (
    <>
      <h1>Reference Data</h1>
      <Panel>{q.isPending ? <Skeleton rows={8} /> : <table className="table"><tbody>{q.data?.map((t) => <tr key={t.key}><td><Link to={`/${t.app}/data/${t.key}`}>{t.name}</Link></td><td className="muted">{t.app}</td><td className="num">{t.columns} columns</td></tr>)}</tbody></table>}</Panel>
    </>
  );
}

export function DataTable() {
  const { key = '' } = useParams();
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['data', key], queryFn: () => api<{ key: string; name: string; columns: string[]; types: Record<string, string> | null; rows: string[][] }>(`/api/data/${key}`) });
  const [rows, setRows] = useState<string[][]>([]);
  useEffect(() => { if (q.data) setRows(q.data.rows.map((r) => [...r])); }, [q.data]);
  const save = useMutation({ mutationFn: () => put<{ saved: number }>(`/api/data/${key}`, rows), onSuccess: () => qc.invalidateQueries({ queryKey: ['data', key] }) });
  if (!q.data) return q.error ? <ErrorBox error={q.error} /> : <Skeleton rows={6} />;
  const cols = q.data.columns;
  return (
    <>
      <h1>{q.data.name}</h1>
      <Panel>
        <ErrorBox error={save.error} />
        {save.data && <div className="alert ok">Saved {save.data.saved} rows.</div>}
        <table className="table compact grid-edit">
          <thead><tr>{cols.map((c) => <th key={c}>{c}</th>)}<th /></tr></thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={i}>
                {cols.map((c, j) => <td key={c}><input type={q.data.types?.[String(j)] === 'date' ? 'date' : 'text'} value={r[j] ?? ''} aria-label={c} onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? Object.assign([...x], { [j]: e.target.value }) : x)))} /></td>)}
                <td><button className="btn outline small danger" onClick={() => setRows((rs) => rs.filter((_, k) => k !== i))}>Remove</button></td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="row">
          <button className="btn outline" onClick={() => setRows((rs) => [...rs, cols.map(() => '')])}>+ Add row</button>
          <button className="btn" disabled={save.isPending} onClick={() => save.mutate()}>Save</button>
        </div>
      </Panel>
    </>
  );
}

// ---------------------------------------------------------------- Astro

interface RateRow { planId: number; plan: string; code: string; type: string; term: number; marketId: number; market: string; energy: number | null; effective: string | null; next: number | null; nextOn: string | null; avg500: number; avg1000: number; avg2000: number }
interface RatesResp { rows: RateRow[]; markets: { id: number; short: string; name: string }[] }

export function Rates() {
  const [f, setF] = useState({ marketId: '', type: 'Resi', q: '' });
  const q = useQuery({ queryKey: ['astro', 'rates', f], queryFn: () => api<RatesResp>(`/api/astro/rates${qs(f)}`) });
  return (
    <>
      <div className="page-head"><h1>Rates</h1><Link className="btn" to="/astro/rates/edit">Update Rates</Link></div>
      <Panel>
        <div className="filters">
          <input type="search" placeholder="Plan or code" value={f.q} onChange={(e) => setF({ ...f, q: e.target.value })} aria-label="Search" />
          <select value={f.marketId} onChange={(e) => setF({ ...f, marketId: e.target.value })} aria-label="Market"><option value="">All utilities</option>{q.data?.markets.map((m) => <option key={m.id} value={m.id}>{m.short}</option>)}</select>
          <select value={f.type} onChange={(e) => setF({ ...f, type: e.target.value })} aria-label="Type"><option value="">Resi & Biz</option><option>Resi</option><option>Biz</option></select>
        </div>
        {q.isPending ? <Skeleton rows={10} cols={8} /> : (
          <div className="table-wrap"><table className="table compact">
            <thead><tr><th>Plan</th><th>Utility</th><th className="num">Energy ¢</th><th>Since</th><th className="num">Next</th><th className="num">500 kWh</th><th className="num">1,000 kWh</th><th className="num">2,000 kWh</th></tr></thead>
            <tbody>{q.data?.rows.map((r) => <tr key={`${r.planId}-${r.marketId}`}><td>{r.plan} <span className="muted mono small">{r.code}</span></td><td>{r.market}</td><td className="num">{r.energy?.toFixed(3) ?? '—'}</td><td>{date(r.effective)}</td><td className="num">{r.next ? <>{r.next.toFixed(3)} <span className="muted small">{date(r.nextOn)}</span></> : ''}</td><td className="num">{r.avg500.toFixed(1)}¢</td><td className="num"><strong>{r.avg1000.toFixed(1)}¢</strong></td><td className="num">{r.avg2000.toFixed(1)}¢</td></tr>)}</tbody>
          </table></div>
        )}
        <p className="muted small">Average prices as the Electricity Facts Label shows them: energy + TDSP delivery + base charge.</p>
      </Panel>
    </>
  );
}

export function RateEditor() {
  const qc = useQueryClient();
  const [type, setType] = useState('Resi');
  const q = useQuery({ queryKey: ['astro', 'rates', 'edit', type], queryFn: () => api<RatesResp>(`/api/astro/rates${qs({ type })}`) });
  const [edits, setEdits] = useState<Record<string, string>>({});
  const tomorrow = new Date(Date.now() + 86400000).toISOString().slice(0, 10);
  const [effectiveOn, setEffectiveOn] = useState(tomorrow);
  const plans = useMemo(() => [...new Map((q.data?.rows ?? []).map((r) => [r.planId, r])).values()], [q.data]);
  const cell = (planId: number, marketId: number) => q.data?.rows.find((r) => r.planId === planId && r.marketId === marketId);
  const save = useMutation({
    mutationFn: () => put('/api/astro/rates', { effectiveOn, changes: Object.entries(edits).filter(([, v]) => v !== '').map(([k, v]) => { const [planId, marketId] = k.split('-').map(Number); return { planId, marketId, energy: Number(v) }; }) }),
    onSuccess: () => { setEdits({}); void qc.invalidateQueries({ queryKey: ['astro'] }); },
  });
  const [file, setFile] = useState<File | null>(null);
  const upload = useMutation({
    mutationFn: async () => {
      const fd = new FormData(); fd.append('file', file!); fd.append('effectiveOn', effectiveOn);
      const res = await fetch('/api/astro/rates/import', { method: 'POST', body: fd, headers: { 'X-RCB': '1' }, credentials: 'same-origin' });
      const body = await res.json(); if (!res.ok) throw new Error(body.message ?? 'Upload failed'); return body as { id: number; status: string; changes: number; problems: number; error: string | null };
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['astro'] }),
  });
  const changed = Object.values(edits).filter((v) => v !== '').length;
  return (
    <>
      <h1>Update Rates</h1>
      <p className="muted">New rates are added with an effective date; the website switches on that date and the history is kept.</p>
      <Panel>
        <div className="filters">
          <select value={type} onChange={(e) => { setType(e.target.value); setEdits({}); }} aria-label="Type"><option>Resi</option><option>Biz</option></select>
          <label className="inline">Effective on <input type="date" value={effectiveOn} min={new Date().toISOString().slice(0, 10)} onChange={(e) => setEffectiveOn(e.target.value)} /></label>
          <button className="btn" disabled={!changed || save.isPending} onClick={() => save.mutate()}>Save {changed || ''} change{changed === 1 ? '' : 's'}</button>
        </div>
        <ErrorBox error={save.error} />
        {save.isSuccess && <div className="alert ok">Saved.</div>}
        {q.isPending ? <Skeleton rows={10} cols={6} /> : (
          <div className="table-wrap"><table className="table compact grid-edit">
            <thead><tr><th>Plan</th>{q.data!.markets.map((m) => <th key={m.id}>{m.short}</th>)}</tr></thead>
            <tbody>{plans.map((p) => (
              <tr key={p.planId}><td>{p.plan} <span className="muted small">{p.code}</span></td>
                {q.data!.markets.map((m) => { const c = cell(p.planId, m.id); const k = `${p.planId}-${m.id}`; return (
                  <td key={m.id}><input inputMode="decimal" placeholder={c?.energy?.toFixed(3) ?? '—'} value={edits[k] ?? ''} onChange={(e) => setEdits((x) => ({ ...x, [k]: e.target.value }))} aria-label={`${p.code} ${m.short}`} /></td>); })}
              </tr>
            ))}</tbody>
          </table></div>
        )}
      </Panel>
      <Panel title="Or upload a rate sheet">
        <p className="muted small">CSV with Plan, Market, Energy columns (<a href="/api/astro/rates/template">template</a>). You'll see the changes before anything goes live.</p>
        <div className="row"><input type="file" accept=".csv" onChange={(e) => setFile(e.target.files?.[0] ?? null)} /><button className="btn outline" disabled={!file || upload.isPending} onClick={() => upload.mutate()}>Upload</button></div>
        <ErrorBox error={upload.error} />
        {upload.data && <div className={`alert ${upload.data.status === 'ready' ? 'ok' : 'bad'}`}>{upload.data.status === 'ready' ? <>{upload.data.changes} changes, {upload.data.problems} problems. <Link to={`/astro/r/rate-imports/${upload.data.id}`}>Review and confirm</Link></> : upload.data.error}</div>}
      </Panel>
    </>
  );
}

export function Terms() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['astro', 'terms'], queryFn: () => api<{ regions: string[]; rows: { term: number; discounts: Record<string, number>; etf: number }[] }>('/api/astro/terms') });
  const [rows, setRows] = useState<{ term: number; discounts: Record<string, number>; etf: number }[]>([]);
  useEffect(() => { if (q.data) setRows(structuredClone(q.data.rows)); }, [q.data]);
  const save = useMutation({ mutationFn: () => put('/api/astro/terms', rows), onSuccess: () => qc.invalidateQueries({ queryKey: ['astro'] }) });
  if (!q.data) return <Skeleton rows={10} />;
  return (
    <>
      <h1>Term Modifiers</h1>
      <p className="muted">Discount in ¢/kWh by plan length and region (positive lowers the rate), and the early termination fee for each length. Build Your Own Plan uses these.</p>
      <Panel>
        <ErrorBox error={save.error} />
        {save.isSuccess && <div className="alert ok">Saved.</div>}
        <div className="table-wrap"><table className="table compact grid-edit">
          <thead><tr><th>Months</th>{q.data.regions.map((r) => <th key={r}>{r}</th>)}<th>ETF $</th></tr></thead>
          <tbody>{rows.map((r, i) => (
            <tr key={r.term}><td>{r.term}</td>
              {q.data.regions.map((g) => <td key={g}><input inputMode="decimal" value={r.discounts[g] ?? 0} aria-label={`${r.term} ${g}`} onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, discounts: { ...x.discounts, [g]: Number(e.target.value) } } : x)))} /></td>)}
              <td><input inputMode="numeric" value={r.etf} aria-label={`${r.term} ETF`} onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, etf: Number(e.target.value) } : x)))} /></td>
            </tr>
          ))}</tbody>
        </table></div>
        <div className="row"><button className="btn" onClick={() => save.mutate()} disabled={save.isPending}>Save</button></div>
      </Panel>
    </>
  );
}
