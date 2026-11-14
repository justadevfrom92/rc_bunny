import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, useLoad } from '../../api';
import { EmptyRow, PageHead, PagerBar, Pill, useRun } from '../../ui';
import { ModelName } from '../deputy/Deputy';
import { when } from '../walker/shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const short = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');
const OnPill = ({ on }: { on: boolean }) => (on ? <Pill tone="ok">On</Pill> : <Pill>Paused</Pill>);
const StatusPill = ({ status }: { status: string }) => (status === 'ok' ? <Pill tone="ok">OK</Pill> : <Pill tone="bad">Failed</Pill>);
const n = (v?: number) => (v ?? 0).toLocaleString();

/** Sheriff → Cron Automations (ebunny's sheriff/automations/index): AI agents that run on a schedule in the background. */
export default function Automations() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any>('/api/sheriff/automations');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Cron Automations" sub="AI agents that don't talk to customers. Each runs on its cron schedule to fix issues in the background, and leaves what it can't fix for a person."
        actions={<Link className="btn cyan" to="/sheriff/automations/new">New Automation</Link>} />
      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head"><h2>Automations</h2><span className="muted">last 7 days</span></div>
        <div className="table-wrap"><table className="table">
          <thead><tr><th>Automation</th><th>Schedule</th><th>Model</th><th>Last Run</th><th>Next Run</th><th className="num">Runs</th><th className="num">Fixed</th><th className="num">For a Person</th><th className="num">Failed</th></tr></thead>
          <tbody>
            {data.agents.map((a: any) => (
              <tr key={a.id} className="click" onClick={() => navigate(`/sheriff/automations/${a.id}`)}>
                <td><Link to={`/sheriff/automations/${a.id}`}><b>{a.name}</b></Link> <OnPill on={a.active} /><div className="help">{a.description}</div></td>
                <td><span className="mono">{a.schedule ?? '—'}</span>{a.schedule && <div className="help">{a.describe}</div>}</td>
                <td><ModelName m={a.model} /></td>
                <td className="nowrap">{a.last ? <>{short(a.last.startedAt)}<br /><StatusPill status={a.last.status} /></> : '—'}</td>
                <td className="nowrap">{a.next ? short(a.next) : '—'}</td>
                <td className="num">{n(a.week?.n)}</td><td className="num">{n(a.week?.fixed)}</td><td className="num">{n(a.week?.flagged)}</td>
                <td className="num">{a.week?.failed ? n(a.week.failed) : '—'}</td></tr>
            ))}
            {data.agents.length === 0 && <EmptyRow cols={9}>No automations yet.</EmptyRow>}
          </tbody>
        </table></div>
      </div>
      <div className="panel">
        <div className="panel-head"><h2>Latest Runs</h2><span className="muted">all automations</span></div>
        <div className="table-wrap"><table className="table">
          <thead><tr><th>ID</th><th>Started</th><th>Automation</th><th>What It Did</th><th className="num">Checked</th><th className="num">Fixed</th><th className="num">For a Person</th><th>Status</th></tr></thead>
          <tbody>
            {data.runs.map((r: any) => (
              <tr key={r.id} className="click" onClick={() => navigate(`/sheriff/automations/${r.aiAgentId}`)}><td className="nowrap"><Link to={`/sheriff/automations/${r.aiAgentId}`}><b>#{r.id}</b></Link></td>
                <td className="nowrap">{when(r.startedAt)}</td><td>{r.agent}</td><td style={{ whiteSpace: 'normal' }}>{r.summary}</td>
                <td className="num">{n(r.checked)}</td><td className="num">{n(r.fixed)}</td><td className="num">{n(r.flagged)}</td><td><StatusPill status={r.status} /></td></tr>
            ))}
            {data.runs.length === 0 && <EmptyRow cols={8}>No runs yet.</EmptyRow>}
          </tbody>
        </table></div>
      </div>
    </>
  );
}

/** One cron automation (ebunny's sheriff/automations/show): what it does, its schedule and model, how it has done, every run. */
export function Automation() {
  const { id } = useParams();
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/sheriff/automations/${id}?page=${sp.get('page') ?? 1}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const a = data.agent, w = data.week, m = data.month;
  const next = a.next && new Date(a.next).toLocaleString('en-US', { weekday: 'long', month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(/,/g, '');
  return (
    <>
      <PageHead title={a.name} sub={<>{a.description || a.activityLabel} · <OnPill on={a.active} /></>}
        actions={<><button className="btn ghost" onClick={() => run(api.post(`/api/sheriff/automations/${id}/toggle`))}>{a.active ? 'Pause' : 'Resume'}</button><Link className="btn cyan" to={`/sheriff/automations/${id}/edit`}>Edit</Link></>} />
      {bar}
      <div className="grid-4 stat-row">
        <div className="stat"><span>Runs</span><strong>{n(w?.n)}</strong><small>last 7 days · {n(m?.n)} in 30</small></div>
        <div className="stat"><span>Fixed</span><strong>{n(w?.fixed)}</strong><small>of {n(w?.checked)} checked</small></div>
        <div className="stat"><span>For a Person</span><strong>{n(w?.flagged)}</strong><small>left for the team</small></div>
        <div className="stat"><span>Failed Runs</span><strong>{n(w?.failed)}</strong><small>{w?.avgSec ? `avg ${Math.round(w.avgSec)}s a run` : 'no runs'}</small></div>
      </div>
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Schedule</h2></div><div className="panel-body"><dl className="kv">
          <dt>Cron</dt><dd className="mono">{a.schedule}</dd>
          <dt>Runs</dt><dd>{a.describe}</dd>
          <dt>Next Run</dt><dd>{a.active ? next ?? '—' : 'Paused'}</dd>
          <dt>Does</dt><dd>{a.activityLabel}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Model</h2></div><div className="panel-body"><dl className="kv">
          <dt>Model</dt><dd><ModelName m={a.model} /></dd>
          <dt>Fallback</dt><dd><ModelName m={a.fallback} /></dd>
          <dt>Temperature</dt><dd>{String(Number(Number(a.temperature).toFixed(2)))}</dd>
          <dt>Tokens</dt><dd>{n(m?.tokens)} in 30 days</dd>
        </dl></div></div>
      </div>
      <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>Instructions</h2></div><div className="panel-body"><div style={{ whiteSpace: 'pre-wrap' }}>{a.systemPrompt}</div></div></div>
      <div className="panel">
        <div className="panel-head"><h2>Runs</h2><span className="muted">{n(data.total)}</span></div>
        <div className="table-wrap"><table className="table" data-page="0">
          <thead><tr><th>ID</th><th>Started</th><th>What It Did</th><th className="num">Checked</th><th className="num">Fixed</th><th className="num">For a Person</th><th className="num">Length</th><th>Status</th></tr></thead>
          <tbody>
            {data.runs.map((r: any) => (
              <tr key={r.id} className="click" onClick={() => navigate(`/sheriff/records/automation_run/${r.id}`)}><td className="nowrap"><Link to={`/sheriff/records/automation_run/${r.id}`}><b>#{r.id}</b></Link></td>
                <td className="nowrap">{when(r.startedAt)}{r.model && r.aiModelId !== a.aiModelId && <div className="help">on {r.model}</div>}</td>
                <td style={{ whiteSpace: 'normal' }}>{r.summary}</td><td className="num">{n(r.checked)}</td><td className="num">{n(r.fixed)}</td><td className="num">{n(r.flagged)}</td>
                <td className="num">{r.durationSec}s</td><td><StatusPill status={r.status} /></td></tr>
            ))}
            {data.runs.length === 0 && <EmptyRow cols={8}>Hasn't run yet. It runs on its schedule once it's on.</EmptyRow>}
          </tbody>
        </table></div>
        <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => navigate(`/sheriff/automations/${id}?page=${p}`)} />
      </div>
    </>
  );
}

/** New Automation / Edit (ebunny's sheriff/automations/form). */
export function AutomationForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/sheriff/automations/${id ? `${id}/edit` : 'new'}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const a = data.agent, models: any[] = data.options.models;
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = {
      name: f.get('name'), activity: f.get('activity'), description: f.get('description'), schedule: f.get('schedule'), aiModelId: f.get('ai_model_id') ? Number(f.get('ai_model_id')) : null,
      fallbackModelId: f.get('fallback_model_id') ? Number(f.get('fallback_model_id')) : null, temperature: Number(f.get('temperature')), maxTokens: Number(f.get('max_tokens')),
      systemPrompt: f.get('system_prompt'), active: f.get('active') === 'on',
    };
    run((id ? api.put<any>(`/api/sheriff/automations/${id}`, body) : api.post<any>('/api/sheriff/automations', body)).then((r) => { navigate(`/sheriff/automations/${r.id}`); return r; }));
  };
  // Clicking a row picks its radio (ebunny's pick-table)
  const pickRow = (e: React.MouseEvent<HTMLTableRowElement>) => { if ((e.target as HTMLElement).tagName !== 'INPUT') { const i = e.currentTarget.querySelector('input'); if (i) i.checked = true; } };
  return (
    <>
      <PageHead title={id ? `Edit ${a.name}` : 'New Automation'} sub="An AI agent that runs on a cron schedule in the background. Models are added in Deputy." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={80} defaultValue={a.name} />
          <span className="label">Does</span>
          <div className="table-wrap"><table className="table pick-table" data-page="0"><tbody>{Object.entries(data.options.activities as Record<string, string>).map(([v, l]) => (
            <tr key={v} onClick={pickRow}><td><input type="radio" id={`activity-${v}`} name="activity" value={v} required defaultChecked={a.activity === v} /></td><td><label htmlFor={`activity-${v}`}>{l}</label></td></tr>
          ))}</tbody></table></div>
          <label htmlFor="schedule">Schedule</label><div><input id="schedule" name="schedule" required maxLength={60} className="mono" defaultValue={a.schedule ?? ''} placeholder="*/15 * * * *" style={{ maxWidth: '20ch' }} />
            <div className="help">Cron: */15 * * * * is every 15 minutes, 0 * * * * every hour, 30 6 * * * daily at 6:30am.</div></div>
          <label htmlFor="description">Description</label><input id="description" name="description" maxLength={250} defaultValue={a.description ?? ''} />
          <span className="label">Model</span>
          <div className="table-wrap"><table className="table pick-table" data-page="0"><thead><tr><th></th><th>Model</th><th>Where</th><th>Status</th></tr></thead><tbody>{models.map((m) => (
            <tr key={m.id} onClick={pickRow}><td><input type="radio" id={`model-${m.id}`} name="ai_model_id" value={m.id} required defaultChecked={a.aiModelId === m.id} /></td>
              <td><label htmlFor={`model-${m.id}`}>{m.name}</label> <span className="mono help">{m.modelId}</span></td><td>{m.providerLabel}</td><td>{m.status.charAt(0).toUpperCase() + m.status.slice(1)}</td></tr>
          ))}</tbody></table></div>
          <span className="label">Fallback model</span>
          <div className="table-wrap"><table className="table pick-table" data-page="0"><tbody>
            <tr onClick={pickRow}><td><input type="radio" id="fallback-none" name="fallback_model_id" value="" defaultChecked={!a.fallbackModelId} /></td><td colSpan={2}><label htmlFor="fallback-none">None</label></td></tr>
            {models.map((m) => (
              <tr key={m.id} onClick={pickRow}><td><input type="radio" id={`fallback-${m.id}`} name="fallback_model_id" value={m.id} defaultChecked={a.fallbackModelId === m.id} /></td>
                <td><label htmlFor={`fallback-${m.id}`}>{m.name}</label> <span className="mono help">{m.modelId}</span></td><td>{m.providerLabel}</td></tr>
            ))}
          </tbody></table></div>
          <label htmlFor="temperature">Temperature</label><div><input id="temperature" name="temperature" type="number" step="0.05" min="0" max="2" required defaultValue={a.temperature} style={{ maxWidth: '10ch' }} />
            <div className="help">Keep it low (0 to 0.3) so background fixes are consistent.</div></div>
          <label htmlFor="max_tokens">Max tokens</label><input id="max_tokens" name="max_tokens" type="number" min="256" max="64000" required defaultValue={a.maxTokens} style={{ maxWidth: '14ch' }} />
          <label htmlFor="system_prompt">Instructions</label><textarea id="system_prompt" name="system_prompt" required maxLength={20000} style={{ minHeight: 220, fontFamily: 'inherit' }} defaultValue={a.systemPrompt} />
          <label htmlFor="active">On</label><label className="check"><input id="active" type="checkbox" name="active" defaultChecked={a.active} /> Runs on its schedule</label>
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save Automation' : 'Add Automation'}</button><Link className="btn ghost" to={id ? `/sheriff/automations/${id}` : '/sheriff/automations'}>Cancel</Link></div>
      </form>
    </>
  );
}
