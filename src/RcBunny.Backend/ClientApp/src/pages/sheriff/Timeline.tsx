import { type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { qs, useLoad } from '../../api';
import { DateInput } from '../../fields';
import { PageHead, PagerBar, Panel, secondsTime } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const kinds: Record<string, string> = { job: 'Cron Jobs', automation: 'Automations', user: 'People' };
const kindLabel: Record<string, string> = { job: 'Cron Job', automation: 'Automation', user: 'Person' };
const statuses: Record<string, string> = { ok: 'Worked', warn: 'Warnings', bad: 'Errors' };
const tone: Record<string, string> = { ok: 'ok', warn: 'warn', bad: 'bad', running: 'info' };

/** Sheriff → Timeline (ebunny's sheriff/timeline): everything that ran or happened, newest first. */
export default function Timeline() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['q', 'from', 'to', 'kind', 'status', 'user'].map((k) => [k, params.get(k) ?? '']));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/sheriff/timeline?${qs({ ...f, page })}`);
  const navigate = useNavigate();
  const set = (changes: Record<string, string>) => setParams(qs({ ...f, page: 1, ...changes }));
  const search = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const d = new FormData(e.currentTarget);
    set({ q: String(d.get('q') ?? ''), from: String(d.get('from') ?? ''), to: String(d.get('to') ?? ''), kind: d.getAll('kind').join(',') });
  };
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const person = data.person;
  const max = Math.max(1, ...data.days.map((d: any) => d.n));
  const all = Object.values(data.counts as Record<string, number>).reduce((a, b) => a + b, 0);
  const link = (e: any) => (e.src === 'history_entry' ? `/sheriff/records/history_entry/${e.seq}` : e.src === 'job_run' ? '/sheriff/jobs' : e.src === 'automation_run' ? `/sheriff/records/automation_run/${e.seq}` : null);

  return (
    <>
      <PageHead title={person ? `${person.name} — Timeline` : 'Timeline'}
        sub={person ? `Everything ${person.name} did or ran, newest first.` : 'Everything that ran or happened, newest first: cron jobs, heartbeat controls, sign-ins and what people did. Search the backlog for errors.'}
        actions={person ? <><Link className="btn ghost" to={`/sheriff/users/${person.id}`}>Profile</Link><Link className="btn ghost" to="/sheriff/timeline">Everyone</Link></> : undefined} />

      <Panel title="Last 14 Days" right={<span className="help">Bar height = events; red = errors, amber = warnings. Click a day to see it.</span>}>
        <div className="panel-body tl-days">
          {data.days.map((d: any) => (
            <a key={d.date} className="tl-day" href="#" onClick={(e) => { e.preventDefault(); set({ from: d.date, to: d.date }); }}
              data-tip={`${new Date(d.date + 'T00:00').toLocaleDateString('en-US', { weekday: 'short', month: 'numeric', day: 'numeric' })}: ${d.n} events, ${d.bad} errors, ${d.warn} warnings`}>
              <span className="bar" style={{ height: Math.max(2, Math.round((d.n / max) * 70)) }}>
                {d.bad > 0 && <span className="bad" style={{ height: `${Math.max(4, Math.round((d.bad / Math.max(1, d.n)) * 100))}%` }} />}
              </span>
              <small>{new Date(d.date + 'T00:00').toLocaleDateString('en-US', { month: 'numeric', day: 'numeric' })}</small>{d.bad > 0 && <b>{d.bad}</b>}
            </a>
          ))}
        </div>
      </Panel>

      <form className="panel" onSubmit={search} key={params.toString()}>
        <div className="panel-body tl-filters">
          <div className="field"><label htmlFor="tl-q">Search</label><input id="tl-q" name="q" type="search" defaultValue={f.q} placeholder="e.g. timeout, AutoPay, a name" /></div>
          <div className="field"><label htmlFor="tl-from">From</label><DateInput id="tl-from" name="from" defaultValue={f.from} /></div>
          <div className="field"><label htmlFor="tl-to">To</label><DateInput id="tl-to" name="to" defaultValue={f.to} /></div>
          <div className="field"><label>What</label><div className="chips small">{Object.entries(kinds).map(([k, l]) => (
            <label key={k}><input type="checkbox" name="kind" value={k} defaultChecked={f.kind.split(',').includes(k)} /><span>{l}</span></label>))}</div></div>
          <div className="actions"><button className="btn cyan">Search</button>
            {(f.q || f.from || f.to || f.kind || f.status) && <Link className="btn ghost" to={`/sheriff/timeline${person ? `?user=${person.id}` : ''}`}>Clear</Link>}</div>
        </div>
      </form>

      <Panel title={`${data.total.toLocaleString()} ${data.total === 1 ? 'event' : 'events'}`} right={
        <div className="chips small">
          <a className={`chip-link${!f.status ? ' on' : ''}`} href="#" onClick={(e) => { e.preventDefault(); set({ status: '' }); }}>All · {all.toLocaleString()}</a>
          {Object.entries(statuses).map(([s, l]) => <a key={s} className={`chip-link${f.status === s ? ' on' : ''}`} href="#" onClick={(e) => { e.preventDefault(); set({ status: s }); }}>{l} · {(data.counts[s] ?? 0).toLocaleString()}</a>)}
        </div>}>
        <div className="table-wrap"><table className="tl-table">
          <thead><tr><th>ID</th><th>When</th><th>What</th><th>Status</th><th>Event</th><th>Who</th></tr></thead>
          <tbody>
            {data.events.map((e: any) => (
              <tr key={e.src + e.seq} className="click" onClick={() => { const l = link(e); if (l) navigate(l); }}>
                <td className="nowrap"><b>#{e.seq}</b></td><td className="nowrap">{secondsTime(e.at)}</td><td className="nowrap">{kindLabel[e.kind] ?? e.kind}</td>
                <td><span className={`pill ${tone[e.status] ?? ''}`}>{statuses[e.status]?.replace(/s$/, '') ?? e.status}</span></td>
                <td className="wrap"><b>{e.title}</b>{e.detail && <div className="help">{e.detail.length > 160 ? e.detail.slice(0, 160) + '...' : e.detail}</div>}</td>
                <td className="nowrap">{e.userId ? <Link to={`/sheriff/timeline?user=${e.userId}`} onClick={(ev) => ev.stopPropagation()}>{e.who}</Link> : <span className="help">System</span>}</td>
              </tr>
            ))}
            {data.events.length === 0 && <tr><td colSpan={6} className="empty">Nothing matches. Try fewer filters.</td></tr>}
          </tbody>
        </table></div>
        <PagerBar page={page} total={data.total} size={25} onPage={(p) => setParams(qs({ ...f, page: p }))} />
      </Panel>
    </>
  );
}
