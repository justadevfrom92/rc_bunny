import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, qs, useLoad } from '../../api';
import { DateInput } from '../../fields';
import { BarChart, EmptyRow, PageHead, PagerBar, age, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

// ebunny's CrmLead::STAGES (label, chance, tone)
const stages: Record<string, [string, number, string]> = {
  new: ['New', 10, 'info'], contacted: ['Contacted', 20, 'info'], qualified: ['Qualified', 40, 'warn'], proposal: ['Proposal Sent', 70, 'warn'], won: ['Won', 100, 'ok'], lost: ['Lost', 0, 'bad'],
};
const types: Record<string, string> = { call: 'Call', email: 'Email', meeting: 'Meeting', note: 'Note', task: 'Follow-up', stage: 'Stage' };
const StagePill = ({ stage }: { stage: string }) => <span className={`pill ${stages[stage]?.[2] ?? ''}`}>{stages[stage]?.[0] ?? stage}</span>;
const $0 = (v: number) => '$' + Math.round(v).toLocaleString('en-US');
const d = (s: string) => new Date(s.length === 10 ? s + 'T00:00' : s).toLocaleDateString('en-US');
const today = () => new Date().toLocaleDateString('en-CA');
const shown = (l: any) => l.company ?? l.name;
const FollowUp = ({ on, overdue }: { on: string | null; overdue?: boolean }) =>
  on ? <span className={overdue ?? on < today() ? 'bad-text' : undefined}>{d(on)}</span> : <span className="muted">None set</span>;
export const commissionLabel = (p: any) => p.commissionBasis === 'kwh' ? `$${Number(p.commissionRate).toFixed(4)} / kWh` : `${Math.round(p.commissionRate * 10000) / 100}% of first-year revenue`;

/** CRM home (ebunny's crm/pipeline): the open pipeline by stage, what it's worth, wins and where leads come from. */
export function Pipeline() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const mine = params.get('owner') === 'me';
  const { data, error } = useLoad<any>(`/api/crm/pipeline?mine=${mine}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const s = data.stats;
  const sum = (list: any[], k: string) => list.reduce((t, l) => t + l[k], 0);
  return (
    <>
      <PageHead title="Sales Pipeline" sub="Homes and businesses that might sign up, by stage. Weighted value counts each lead at its stage's chance of winning."
        actions={<><Link className="btn ghost" to="/corral/crm/leads">All Leads</Link><Link className="btn cyan" to="/corral/crm/leads/new">New Lead</Link></>} />
      <div className="chips small" style={{ marginBottom: 14 }}><Link to="/corral/crm" className={`chip-link${mine ? '' : ' on'}`}>Everyone</Link><Link to="/corral/crm?owner=me" className={`chip-link${mine ? ' on' : ''}`}>My leads</Link></div>
      <div className="grid-4 stat-row">
        <Link className="stat" to="/corral/crm/leads?stage=open"><span>Open Pipeline</span><strong>{$0(s.value)}</strong><small>{s.open} open {s.open === 1 ? 'lead' : 'leads'} · yearly value</small></Link>
        <div className="stat"><span>Weighted</span><strong>{$0(s.weighted)}</strong><small>value × chance of winning</small></div>
        <Link className="stat" to="/corral/crm/leads?stage=won"><span>Won This Month</span><strong>{s.wonMonth}</strong><small>{$0(s.wonValue)} a year · {s.winRate}% win rate (90 days)</small></Link>
        <Link className="stat" to="/corral/crm/follow-ups"><span>Follow-ups Overdue</span><strong>{s.overdue}</strong><small>leads past their follow-up date</small></Link>
      </div>
      <div className="panel"><div className="panel-head"><h2>Stages</h2></div><div className="table-wrap"><table>
        <thead><tr><th>Stage</th><th className="num">Leads</th><th className="num">Yearly Value</th><th className="num">Chance</th><th className="num">Weighted</th></tr></thead>
        <tbody>{data.stages.map((st: any) => (
          <tr key={st.key} className="click" onClick={() => navigate(`/corral/crm/leads?stage=${st.key}`)}><td><Link to={`/corral/crm/leads?stage=${st.key}`}>{st.label}</Link></td><td className="num">{st.leads.length}</td>
            <td className="num">{$0(sum(st.leads, 'estValue'))}</td><td className="num">{st.chance}%</td><td className="num">{$0(sum(st.leads, 'weighted'))}</td></tr>
        ))}</tbody>
      </table></div></div>
      {data.stages.map((st: any) => (
        <div key={st.key} className="panel"><div className="panel-head"><h2>{st.label}</h2><span className="muted">{st.leads.length} · {$0(sum(st.leads, 'estValue'))}</span></div><div className="table-wrap"><table>
          <thead><tr><th>Lead</th><th>Kind</th><th className="num">Yearly Value</th><th>Owner</th><th>Source</th><th>Next Follow-up</th></tr></thead>
          <tbody>
            {st.leads.map((l: any) => (
              <tr key={l.id} className="click" onClick={() => navigate(`/corral/crm/leads/${l.id}`)}>
                <td><Link to={`/corral/crm/leads/${l.id}`}><b>{shown(l)}</b></Link>{l.company && <div className="help">{l.name}</div>}</td>
                <td>{l.kind}</td><td className="num">{$0(l.estValue)}</td><td>{l.owner ?? '—'}</td><td>{l.source}{l.partner && <div className="help">{l.partner}</div>}</td>
                <td className="nowrap"><FollowUp on={l.nextFollowUpOn} overdue={l.overdue} /></td></tr>
            ))}
            {st.leads.length === 0 && <EmptyRow cols={6}>No leads at this stage.</EmptyRow>}
          </tbody>
        </table></div></div>
      ))}
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>New Leads by Source</h2><span className="muted">last 90 days</span></div><div className="panel-body">
          <BarChart unit="leads" bars={data.bySource.map((b: any) => ({ label: b.source, value: b.count }))} /></div></div>
        <div className="panel"><div className="panel-head"><h2>Recent Wins</h2></div><div className="table-wrap"><table>
          <thead><tr><th>Won</th><th>Lead</th><th className="num">Yearly Value</th><th>Owner</th></tr></thead>
          <tbody>
            {data.recentWins.map((l: any) => (
              <tr key={l.id} className="click" onClick={() => navigate(`/corral/crm/leads/${l.id}`)}><td>{l.closedAt && d(l.closedAt)}</td><td><Link to={`/corral/crm/leads/${l.id}`}>{shown(l)}</Link></td><td className="num">{$0(l.estValue)}</td><td>{l.owner}</td></tr>
            ))}
            {data.recentWins.length === 0 && <EmptyRow cols={4}>No wins yet.</EmptyRow>}
          </tbody>
        </table></div></div>
      </div>
    </>
  );
}

/** Corral → Leads (ebunny's crm/leads): filter chips that clear when picked again. */
export function Leads() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const f: Record<string, string> = Object.fromEntries(['stage', 'kind', 'source', 'owner', 'q'].map((k) => [k, params.get(k) ?? '']).filter(([, v]) => v));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/crm/leads?${qs({ stage: f.stage || 'all', kind: f.kind, source: f.source, mine: f.owner === 'me', q: f.q, page })}`);
  const { data: options } = useLoad<any>('/api/crm/options');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data || !options) return <div className="empty">Loading…</div>;
  const url = (set: Record<string, string | number | null>) => `/corral/crm/leads?${new URLSearchParams(Object.entries({ ...f, ...set }).filter(([, v]) => v !== null && v !== '') as [string, string][])}`;
  const chip = (key: string, val: string, label: string) => <Link key={val} to={url({ [key]: f[key] === val ? null : val, page: null })} className={`chip-link${f[key] === val ? ' on' : ''}`}>{label}</Link>;
  const count = (s: string) => data.counts.find((c: any) => c.stage === s)?.n ?? 0;
  return (
    <>
      <PageHead title="Leads" sub="Everyone in the pipeline. Pick filters below; pick one again to clear it."
        actions={<><Link className="btn ghost" to="/corral/crm">Pipeline</Link><Link className="btn cyan" to="/corral/crm/leads/new">New Lead</Link></>} />
      <div className="panel"><div className="panel-body stack">
        <form className="inline-search" onSubmit={(e) => { e.preventDefault(); navigate(url({ q: String(new FormData(e.currentTarget).get('q') ?? ''), page: null })); }} key={f.q}>
          <input name="q" defaultValue={f.q} placeholder="Search name, business, email or phone" aria-label="Search leads" /><button className="btn">Search</button></form>
        <div className="field"><label>Stage</label><div className="chips small">{chip('stage', 'open', 'All open')}{Object.entries(stages).map(([s, [label]]) => chip('stage', s, `${label} · ${count(s)}`))}</div></div>
        <div className="field"><label>Kind</label><div className="chips small">{options.kinds.map((k: string) => chip('kind', k, k))}</div></div>
        <div className="field"><label>Source</label><div className="chips small">{options.sources.map((k: string) => chip('source', k, k))}</div></div>
        <div className="field"><label>Owner</label><div className="chips small">{chip('owner', 'me', 'Mine')}</div></div>
      </div></div>
      <div className="panel"><div className="panel-head"><h2>{data.total.toLocaleString()} {data.total === 1 ? 'lead' : 'leads'}</h2></div><div className="table-wrap"><table>
        <thead><tr><th>Lead</th><th>Kind</th><th>Stage</th><th className="num">Yearly Value</th><th>Owner</th><th>Source</th><th>Next Follow-up</th></tr></thead>
        <tbody>
          {data.leads.map((l: any) => (
            <tr key={l.id} className="click" onClick={() => navigate(`/corral/crm/leads/${l.id}`)}>
              <td><Link to={`/corral/crm/leads/${l.id}`}><b>{shown(l)}</b></Link><div className="help">{l.company ? `${l.name} · ` : ''}{l.city}</div></td>
              <td>{l.kind}</td><td><StagePill stage={l.stage} /></td><td className="num">{$0(l.estValue)}</td><td>{l.owner ?? '—'}</td><td>{l.source}{l.partner && <div className="help">{l.partner}</div>}</td>
              <td className="nowrap">{l.stage === 'won' || l.stage === 'lost' ? <span className="muted">{l.closedAt && d(l.closedAt)}</span> : <FollowUp on={l.nextFollowUpOn} />}</td></tr>
          ))}
          {data.leads.length === 0 && <EmptyRow cols={7}>No leads match.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={page} total={data.total} size={30} onPage={(p) => navigate(url({ page: p }))} /></div>
    </>
  );
}

/** Log Activity: a call, email, meeting or note, or set a follow-up (ebunny's form on a lead or partner). */
function LogActivity({ url, run, sub, rows = 3 }: { url: string; run: (p: Promise<unknown>, done?: string) => Promise<void>; sub?: string; rows?: number }) {
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    run(api.post(url, { type: f.get('type'), subject: f.get('subject'), body: f.get('body'), dueOn: f.get('due_on') || null }).then(() => form.reset()), 'Logged.');
  };
  return (
    <form className="panel" onSubmit={submit}>
      <div className="panel-head"><h2>Log Activity</h2>{sub && <span className="muted">{sub}</span>}</div>
      <div className="panel-body stack">
        <select name="type" defaultValue="call" style={{ maxWidth: 320 }} aria-label="Type">{['call', 'email', 'meeting', 'note', 'task'].map((t) => <option key={t} value={t}>{types[t]}</option>)}</select>
        <div className="grid-2"><div className="field"><label htmlFor="subject">What happened</label><input id="subject" name="subject" required maxLength={200} placeholder="e.g. Sent the 24-month business quote" /></div>
          <div className="field"><label htmlFor="due_on">Follow up on</label><DateInput id="due_on" name="due_on" /><div className="help">Needed for a follow-up.</div></div></div>
        <div className="field"><label htmlFor="body">Details</label><textarea id="body" name="body" rows={rows} maxLength={2000} /></div>
        <div><button className="btn cyan">Log It</button></div>
      </div>
    </form>
  );
}

/** One activity row (Timeline / Activity). */
const ActivityRow = ({ a, navigate, stagePill }: { a: any; navigate: (to: string) => void; stagePill?: boolean }) => (
  <tr className="click" onClick={() => navigate(`/corral/records/crm_activity/${a.id}`)}><td className="nowrap"><Link to={`/corral/records/crm_activity/${a.id}`}><b>#{a.id}</b></Link></td><td className="nowrap">{dateTime(a.createdAt)}</td>
    <td>{stagePill ? <span className={`pill ${a.type === 'stage' ? 'info' : ''}`}>{types[a.type] ?? a.type}</span> : types[a.type] ?? a.type}</td>
    <td className="wrap"><b>{a.subject}</b>{a.body && <div className="help">{a.body}</div>}{a.dueOn && <div className="help">Due {d(a.dueOn)}{a.doneAt ? ` · done ${d(a.doneAt)}` : ''}</div>}</td><td>{a.user ?? 'System'}</td></tr>
);

/** One lead (ebunny's crm/lead): stage, contact, opportunity, ownership, follow-ups, Log Activity and the timeline. */
export function Lead() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/crm/leads/${id}`);
  const { data: options } = useLoad<any>('/api/crm/options');
  const [run, bar] = useRun(reload);
  const [stage, setStage] = useState<string | null>(null);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data || !options) return <div className="empty">Loading…</div>;
  const l = data.lead, now = stage ?? l.stage;
  const saveStage = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post(`/api/crm/leads/${l.id}/stage`, { stage: now, lostReason: f.get('lost_other') || f.get('lost_reason'), account: f.get('account') }), `Moved to ${stages[now][0]}.`).then(() => setStage(null));
  };
  const contractFuture = l.contractEndsOn && l.contractEndsOn > today();
  return (
    <>
      <PageHead title={shown(l)} sub={<>{l.kind}{l.company && ` · ${l.name}${l.title ? `, ${l.title}` : ''}`} · <StagePill stage={l.stage} /></>}
        actions={<><Link className="btn ghost" to="/corral/crm/leads">All Leads</Link><Link className="btn cyan" to={`/corral/crm/leads/${l.id}/edit`}>Edit Lead</Link></>} />
      {bar}
      <form className="panel" id="stage-form" onSubmit={saveStage} key={l.stage}>
        <div className="panel-head"><h2>Stage</h2><span className="muted">since {d(l.stageChangedAt ?? l.createdAt)}</span></div>
        <div className="panel-body stack">
          <div className="field" style={{ maxWidth: 320 }}><label htmlFor="stage">Stage</label><select id="stage" name="stage" value={now} onChange={(e) => setStage(e.target.value)}>
            {Object.entries(stages).map(([s, [label, chance]]) => <option key={s} value={s}>{label} ({s === 'lost' ? 'closed' : `${chance}% chance`})</option>)}</select></div>
          <div className="grid-2" hidden={now !== 'won' && now !== 'lost'}>
            <div className="field" hidden={now !== 'lost'}><label htmlFor="lost_reason">Why it was lost</label><select id="lost_reason" name="lost_reason" defaultValue={options.lostReasons.includes(l.lostReason) ? l.lostReason : ''}>
              <option value="">Pick a reason</option>{options.lostReasons.map((r: string) => <option key={r}>{r}</option>)}</select>
              <input name="lost_other" maxLength={200} defaultValue={l.lostReason && !options.lostReasons.includes(l.lostReason) ? l.lostReason : ''} placeholder="Or type another reason" aria-label="Other reason" style={{ marginTop: 8 }} /></div>
            <div className="field" hidden={now !== 'won'}><label htmlFor="account">New account number (optional)</label><input id="account" name="account" className="mono" maxLength={20} defaultValue={l.customer?.account ?? ''} placeholder="1219…" /></div>
          </div>
          <div><button className="btn">Save Stage</button></div>
        </div>
      </form>
      <div className="grid-3">
        <div className="panel"><div className="panel-head"><h2>Contact</h2></div><div className="panel-body"><dl className="kv">
          <dt>Name</dt><dd>{l.name}{l.title ? `, ${l.title}` : ''}</dd>
          {l.company && <><dt>Business</dt><dd>{l.company}</dd></>}
          <dt>Email</dt><dd>{l.email ? <a href={`mailto:${l.email}`}>{l.email}</a> : '—'}</dd>
          <dt>Phone</dt><dd>{l.phone ? <a href={`tel:${l.phone.replace(/[^0-9+]/g, '')}`}>{l.phone}</a> : '—'}</dd>
          <dt>City</dt><dd>{l.city ?? '—'}{l.market ? ` · ${l.market}` : ''}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Opportunity</h2></div><div className="panel-body"><dl className="kv">
          <dt>Yearly Value</dt><dd><b>{$0(l.estValue)}</b> <span className="muted">· weighted {$0(l.weighted)}</span></dd>
          <dt>Usage</dt><dd>{Number(l.annualKwh ?? 0).toLocaleString()} kWh a year{l.sites > 1 ? ` across ${l.sites} sites` : ''}</dd>
          <dt>Current Provider</dt><dd>{l.currentProvider ?? '—'}</dd>
          <dt>Their Contract Ends</dt><dd>{l.contractEndsOn ? d(l.contractEndsOn) : '—'}{contractFuture && <> <span className="muted">(in {age(new Date().toISOString(), l.contractEndsOn)})</span></>}</dd>
          {l.stage === 'lost' && <><dt>Lost Because</dt><dd>{l.lostReason}</dd></>}
          {l.customer && <><dt>Account</dt><dd><Link to={`/corral/customers/${l.customer.id}`}>{l.customer.account}</Link></dd></>}
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Ownership</h2></div><div className="panel-body"><dl className="kv">
          <dt>Owner</dt><dd>{l.owner ?? 'Unassigned'}</dd>
          <dt>Source</dt><dd>{l.source}</dd>
          {l.partner && <><dt>Partner</dt><dd><Link to={`/corral/crm/partners/${l.partnerId}`}>{l.partner}</Link>{l.stage === 'won' && l.commission != null && <div className="help">earns ${Number(l.commission).toFixed(2)}</div>}</dd></>}
          <dt>Added</dt><dd>{d(l.createdAt)} ({age(l.createdAt)} ago)</dd>
          <dt>Next Follow-up</dt><dd>{l.nextFollowUpOn ? <span className={l.nextFollowUpOn < today() ? 'bad-text' : undefined}>{new Date(l.nextFollowUpOn + 'T00:00').toLocaleDateString('en-US', { weekday: 'long' })}, {d(l.nextFollowUpOn)}</span> : '—'}</dd>
        </dl></div></div>
      </div>
      {l.notes && <div className="panel"><div className="panel-head"><h2>Notes</h2></div><div className="panel-body" style={{ whiteSpace: 'pre-line' }}>{l.notes}</div></div>}
      {data.openTasks.length > 0 && (
        <div className="panel"><div className="panel-head"><h2>Open Follow-ups</h2><span className="muted">{data.openTasks.length}</span></div><div className="table-wrap"><table>
          <thead><tr><th>Due</th><th>Follow-up</th><th>Set By</th><th /></tr></thead>
          <tbody>{data.openTasks.map((t: any) => (
            <tr key={t.id}><td className="nowrap"><span className={t.dueOn < today() ? 'bad-text' : undefined}>{new Date(t.dueOn + 'T00:00').toLocaleDateString('en-US', { weekday: 'short' })} {d(t.dueOn)}</span></td>
              <td className="wrap"><b>{t.subject}</b>{t.body && <div className="help">{t.body}</div>}</td><td>{t.user}</td>
              <td className="actions"><button className="btn sm" onClick={() => run(api.post(`/api/crm/follow-ups/${t.id}`), 'Follow-up done.')}>Done</button></td></tr>
          ))}</tbody>
        </table></div></div>
      )}
      <LogActivity url={`/api/crm/leads/${l.id}/activities`} run={run} sub="a call, email, meeting or note, or set a follow-up" />
      <div className="panel"><div className="panel-head"><h2>Timeline</h2><span className="muted">{data.activities.length}</span></div><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>When</th><th>Type</th><th>What</th><th>By</th></tr></thead>
        <tbody>{data.activities.map((a: any) => <ActivityRow key={a.id} a={a} navigate={navigate} stagePill />)}</tbody>
      </table></div></div>
      {data.similar.length > 0 && (
        <div className="panel"><div className="panel-head"><h2>Existing Accounts With This Name</h2><span className="muted">check before creating an order</span></div><div className="table-wrap"><table>
          <tbody>{data.similar.map((c: any) => <tr key={c.id} className="click" onClick={() => navigate(`/corral/customers/${c.id}`)}><td><Link to={`/corral/customers/${c.id}`} className="mono">{c.account}</Link></td><td>{c.name}</td><td>{c.status}</td></tr>)}</tbody>
        </table></div></div>
      )}
    </>
  );
}

/** A pick list of radio rows (ebunny's .pick-list). */
const PickList = ({ name, rows, value }: { name: string; rows: { id: number | ''; label: string; help?: string; side?: string }[]; value: any }) => (
  <div className="table-wrap pick-list"><table className="table"><tbody>
    {rows.map((r) => (
      <tr key={r.id}><td style={{ width: 32 }}><input type="radio" id={`${name}${r.id || 0}`} name={name} value={r.id} defaultChecked={String(value ?? '') === String(r.id)} /></td>
        <td><label htmlFor={`${name}${r.id || 0}`}>{r.id === '' ? r.label : <b>{r.label}</b>}</label>{r.help && <div className="help">{r.help}</div>}</td><td className="help">{r.side}</td></tr>
    ))}
  </tbody></table></div>
);

/** New Lead / Edit (ebunny's crm/lead-form). */
export function LeadForm() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data: options } = useLoad<any>('/api/crm/options');
  const { data } = useLoad<any>(id ? `/api/crm/leads/${id}` : null);
  const [run, bar] = useRun();
  if (!options || (id && !data)) return <div className="empty">Loading…</div>;
  const l = data?.lead ?? { kind: 'Residential', source: params.get('partner') ? 'Broker' : 'Website', partnerId: Number(params.get('partner')) || null, sites: 1 };
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) || null, n = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
    const body = { name: v('name'), company: v('company'), title: v('title'), kind: v('kind'), email: v('email'), phone: v('phone'), city: v('city'), marketId: n('market_id'), source: v('source'),
      partnerId: n('partner_id'), annualKwh: n('annual_kwh'), estValue: n('est_value'), sites: n('sites'), currentProvider: v('current_provider'), contractEndsOn: v('contract_ends_on'),
      ownerId: n('owner_id'), nextFollowUpOn: v('next_follow_up_on'), notes: v('notes') };
    run((id ? api.put(`/api/crm/leads/${id}`, body).then(() => ({ id: Number(id) })) : api.post<{ id: number }>('/api/crm/leads', body))
      .then((r: any) => { navigate(`/corral/crm/leads/${r.id}`); return r; }));
  };
  return (
    <>
      <PageHead title={id ? `Edit ${shown(l)}` : 'New Lead'} sub="A home or business that might sign up." />
      {bar}
      <form className="stack" style={{ maxWidth: 980 }} onSubmit={submit}>
        <div className="panel"><div className="panel-head"><h2>Who</h2></div><div className="panel-body stack">
          <div className="field"><label htmlFor="sel-kind">Kind</label><select name="kind" id="sel-kind" style={{ maxWidth: 320 }} defaultValue={l.kind}>{options.kinds.map((k: string) => <option key={k}>{k}</option>)}</select></div>
          <div className="grid-2">
            <div className="field"><label htmlFor="name">Contact name</label><input id="name" name="name" required maxLength={150} defaultValue={l.name} /></div>
            <div className="field"><label htmlFor="company">Business name</label><input id="company" name="company" maxLength={150} defaultValue={l.company ?? ''} /><div className="help">Leave empty for a home.</div></div>
            <div className="field"><label htmlFor="title">Job title</label><input id="title" name="title" maxLength={100} defaultValue={l.title ?? ''} /></div>
            <div className="field"><label htmlFor="email">Email</label><input id="email" name="email" type="email" maxLength={200} defaultValue={l.email ?? ''} /></div>
            <div className="field"><label htmlFor="phone">Phone</label><input id="phone" name="phone" maxLength={30} defaultValue={l.phone ?? ''} /></div>
            <div className="field"><label htmlFor="city">City</label><input id="city" name="city" maxLength={80} defaultValue={l.city ?? ''} /></div>
          </div>
          <div className="field"><label htmlFor="sel-market_id">Utility area</label><select name="market_id" id="sel-market_id" style={{ maxWidth: 320 }} defaultValue={l.marketId ?? ''}><option value="">Not sure</option>
            {options.markets.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select></div>
        </div></div>
        <div className="panel"><div className="panel-head"><h2>Opportunity</h2></div><div className="panel-body stack">
          <div className="grid-2">
            <div className="field"><label htmlFor="annual_kwh">Yearly usage (kWh)</label><input id="annual_kwh" name="annual_kwh" type="number" min={0} defaultValue={l.annualKwh ?? ''} /></div>
            <div className="field"><label htmlFor="est_value">Yearly value ($)</label><input id="est_value" name="est_value" type="number" min={0} step="0.01" defaultValue={l.estValue || ''} /><div className="help">Empty: estimated at about 14¢ per kWh.</div></div>
            <div className="field"><label htmlFor="sites">Sites / meters</label><input id="sites" name="sites" type="number" min={1} defaultValue={l.sites || 1} /></div>
            <div className="field"><label htmlFor="current_provider">Current provider</label><input id="current_provider" name="current_provider" maxLength={80} defaultValue={l.currentProvider ?? ''} /></div>
            <div className="field"><label htmlFor="contract_ends_on">Their contract ends</label><DateInput id="contract_ends_on" name="contract_ends_on" defaultValue={l.contractEndsOn ?? ''} /></div>
            <div className="field"><label htmlFor="next_follow_up_on">Next follow-up</label><DateInput id="next_follow_up_on" name="next_follow_up_on" defaultValue={l.nextFollowUpOn ?? ''} /></div>
          </div>
          <div className="field"><label htmlFor="notes">Notes</label><textarea id="notes" name="notes" rows={3} maxLength={2000} defaultValue={l.notes ?? ''} /></div>
        </div></div>
        <div className="panel"><div className="panel-head"><h2>Source &amp; Owner</h2></div><div className="panel-body stack">
          <div className="field"><label htmlFor="sel-source">Source</label><select name="source" id="sel-source" style={{ maxWidth: 320 }} defaultValue={l.source}>{options.sources.map((s: string) => <option key={s}>{s}</option>)}</select></div>
          <div className="field"><label>Broker or partner <span className="help">(for Broker and Partner leads)</span></label>
            <PickList name="partner_id" value={l.partnerId ?? ''} rows={[{ id: '', label: 'None' }, ...options.partners.map((p: any) => ({ id: p.id, label: p.name, help: p.contactName, side: p.kind }))]} /></div>
          <div className="field"><label>Owner</label>
            <PickList name="owner_id" value={l.ownerId} rows={options.owners.map((o: any) => ({ id: o.id, label: o.name, side: `${o.title ?? ''}${o.company ? ` · ${o.company}` : ''}` }))} /></div>
        </div></div>
        <div className="actions"><button className="btn cyan">{id ? 'Save Lead' : 'Add Lead'}</button><Link className="btn ghost" to={id ? `/corral/crm/leads/${id}` : '/corral/crm/leads'}>Cancel</Link></div>
      </form>
    </>
  );
}

/** Corral → Follow-ups (ebunny's crm/tasks): overdue, due today, coming up, and done in the last 2 weeks. */
export function FollowUps() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const who = params.get('who');
  const { data, error, reload } = useLoad<any>(`/api/crm/follow-ups${who ? `?who=${who}` : ''}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const link = (t: any) => (t.leadId ? `/corral/crm/leads/${t.leadId}` : t.partnerId ? `/corral/crm/partners/${t.partnerId}` : null);
  const toggle = (t: any) => run(api.post(`/api/crm/follow-ups/${t.id}`), t.doneAt ? 'Reopened.' : 'Follow-up done.');
  return (
    <>
      <PageHead title="Follow-ups" sub="Calls, emails and visits promised to leads and partners. Done takes it off the list; the lead's next follow-up moves to the next one." />
      {bar}
      <div className="chips small" style={{ marginBottom: 14 }}><Link to="/corral/crm/follow-ups?who=me" className={`chip-link${data.everyone ? '' : ' on'}`}>Mine</Link><Link to="/corral/crm/follow-ups?who=all" className={`chip-link${data.everyone ? ' on' : ''}`}>Everyone</Link></div>
      {([['Overdue', data.overdue], ['Due Today', data.today], ['Coming Up', data.upcoming]] as [string, any[]][]).map(([title, list]) => (
        <div key={title} className="panel"><div className="panel-head"><h2>{title}</h2><span className="muted">{list.length}</span></div><div className="table-wrap"><table>
          <thead><tr><th>Due</th><th>Follow-up</th><th>Lead or Partner</th><th>Set By</th><th /></tr></thead>
          <tbody>
            {list.map((t) => {
              const url = link(t);
              return (
                <tr key={t.id} className={url ? 'click' : undefined} onClick={() => url && navigate(url)}>
                  <td className="nowrap"><span className={title === 'Overdue' ? 'bad-text' : undefined}>{new Date(t.dueOn + 'T00:00').toLocaleDateString('en-US', { weekday: 'short' })} {new Date(t.dueOn + 'T00:00').toLocaleDateString('en-US', { month: 'numeric', day: 'numeric' })}</span>
                    {title === 'Overdue' && <div className="help">{age(t.dueOn + 'T00:00')} ago</div>}</td>
                  <td className="wrap"><b>{t.subject}</b>{t.body && <div className="help">{t.body.length > 120 ? t.body.slice(0, 117) + '...' : t.body}</div>}</td>
                  <td>{url && <Link to={url} onClick={(e) => e.stopPropagation()}>{t.lead ?? t.partner}</Link>}{t.leadStage && <div className="help">{stages[t.leadStage]?.[0]}</div>}</td>
                  <td>{t.user}</td>
                  <td className="actions" onClick={(e) => e.stopPropagation()}><button className="btn sm" onClick={() => toggle(t)}>Done</button></td></tr>
              );
            })}
            {list.length === 0 && <EmptyRow cols={5}>{title === 'Overdue' ? 'Nothing overdue.' : 'Nothing here.'}</EmptyRow>}
          </tbody>
        </table></div></div>
      ))}
      <div className="panel"><div className="panel-head"><h2>Done in the Last 2 Weeks</h2></div><div className="table-wrap"><table>
        <thead><tr><th>Done</th><th>Follow-up</th><th>Lead or Partner</th><th>By</th><th /></tr></thead>
        <tbody>
          {data.done.map((t: any) => {
            const url = link(t);
            return (
              <tr key={t.id} className={url ? 'click' : undefined} onClick={() => url && navigate(url)}><td className="nowrap">{new Date(t.doneAt).toLocaleString('en-US', { month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '')}</td>
                <td className="wrap">{t.subject}</td><td>{url && <Link to={url} onClick={(e) => e.stopPropagation()}>{t.lead ?? t.partner}</Link>}</td><td>{t.user}</td>
                <td className="actions" onClick={(e) => e.stopPropagation()}><button className="btn sm ghost" onClick={() => toggle(t)}>Reopen</button></td></tr>
            );
          })}
          {data.done.length === 0 && <EmptyRow cols={5}>Nothing done yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** Corral → Partners & Brokers (ebunny's crm/partners). */
export function Partners() {
  const { data, error } = useLoad<any[]>('/api/crm/partners');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Partners & Brokers" sub="Brokers, referral partners, property managers and builders who send us leads, what they've sent and what they've earned."
        actions={<Link className="btn cyan" to="/corral/crm/partners/new">New Partner</Link>} />
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Partner</th><th>Kind</th><th>Contact</th><th className="num">Leads</th><th className="num">Open</th><th className="num">Won</th><th className="num">Commission Earned</th><th>Commission</th><th>Status</th></tr></thead>
        <tbody>{data.map((p) => (
          <tr key={p.id} className="click" onClick={() => navigate(`/corral/crm/partners/${p.id}`)}>
            <td><Link to={`/corral/crm/partners/${p.id}`}><b>{p.name}</b></Link><div className="help">{p.city}</div></td><td>{p.kind}</td>
            <td>{p.contactName}<div className="help">{p.phone}</div></td>
            <td className="num">{p.leads}</td><td className="num">{p.open}</td><td className="num">{p.won}</td>
            <td className="num">${Number(p.earned).toFixed(2)}</td><td className="help">{commissionLabel(p)}</td>
            <td><span className={`pill ${({ Active: 'ok', Paused: 'warn' } as Record<string, string>)[p.status] ?? ''}`}>{p.status}</span></td></tr>
        ))}</tbody>
      </table></div></div>
    </>
  );
}

/** One partner (ebunny's crm/partner): numbers, contact, agreement, their leads, Log Activity and activity. */
export function Partner() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/crm/partners/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const p = data.partner, leads = data.leads as any[];
  const closed = leads.filter((l) => l.stage === 'won' || l.stage === 'lost').length;
  return (
    <>
      <PageHead title={p.name} sub={<>{p.kind} · <span className={`pill ${({ Active: 'ok', Paused: 'warn' } as Record<string, string>)[p.status] ?? ''}`}>{p.status}</span></>}
        actions={<><Link className="btn ghost" to="/corral/crm/partners">All Partners</Link><Link className="btn ghost" to={`/corral/crm/leads/new?partner=${p.id}`}>Add a Lead</Link><Link className="btn cyan" to={`/corral/crm/partners/${p.id}/edit`}>Edit</Link></>} />
      {bar}
      <div className="grid-4 stat-row">
        <div className="stat"><span>Leads Sent</span><strong>{leads.length}</strong><small>{leads.filter((l) => l.stage !== 'won' && l.stage !== 'lost').length} still open</small></div>
        <div className="stat"><span>Won</span><strong>{data.won}</strong><small>{closed ? Math.round((100 * data.won) / closed) : 0}% of closed leads</small></div>
        <div className="stat"><span>Open Pipeline</span><strong>{$0(data.pipeline)}</strong><small>yearly value</small></div>
        <div className="stat"><span>Commission Earned</span><strong>${Number(data.earned).toFixed(2)}</strong><small>{commissionLabel(p)}</small></div>
      </div>
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Contact</h2></div><div className="panel-body"><dl className="kv">
          <dt>Contact</dt><dd>{p.contactName ?? '—'}</dd>
          <dt>Email</dt><dd>{p.email ? <a href={`mailto:${p.email}`}>{p.email}</a> : '—'}</dd>
          <dt>Phone</dt><dd>{p.phone ?? '—'}</dd>
          <dt>City</dt><dd>{p.city ?? '—'}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Agreement</h2></div><div className="panel-body"><dl className="kv">
          <dt>Commission</dt><dd>{commissionLabel(p)}</dd>
          <dt>Agreement Ends</dt><dd>{p.agreementEndsOn ? d(p.agreementEndsOn) : 'No end date'}</dd>
          <dt>Our Contact</dt><dd>{p.owner ?? '—'}</dd>
          <dt>Notes</dt><dd>{p.notes ?? '—'}</dd>
        </dl></div></div>
      </div>
      <div className="panel"><div className="panel-head"><h2>Leads From {p.name}</h2><span className="muted">{leads.length}</span></div><div className="table-wrap"><table>
        <thead><tr><th>Lead</th><th>Stage</th><th className="num">Yearly Value</th><th className="num">kWh / Year</th><th className="num">Commission</th><th>Owner</th><th>Added</th></tr></thead>
        <tbody>
          {leads.map((l) => (
            <tr key={l.id} className="click" onClick={() => navigate(`/corral/crm/leads/${l.id}`)}><td><Link to={`/corral/crm/leads/${l.id}`}>{shown(l)}</Link></td><td><StagePill stage={l.stage} /></td>
              <td className="num">{$0(l.estValue)}</td><td className="num">{Number(l.annualKwh ?? 0).toLocaleString()}</td><td className="num">{l.commission != null ? `$${Number(l.commission).toFixed(2)}` : '—'}</td><td>{l.owner}</td><td>{d(l.createdAt)}</td></tr>
          ))}
          {leads.length === 0 && <EmptyRow cols={7}>No leads yet.</EmptyRow>}
        </tbody>
      </table></div></div>
      <LogActivity url={`/api/crm/partners/${p.id}/activities`} run={run} rows={2} />
      <div className="panel"><div className="panel-head"><h2>Activity</h2></div><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>When</th><th>Type</th><th>What</th><th>By</th></tr></thead>
        <tbody>
          {data.activities.map((a: any) => <ActivityRow key={a.id} a={a} navigate={navigate} />)}
          {data.activities.length === 0 && <EmptyRow cols={5}>No activity yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** New Partner / Edit (ebunny's crm/partner-form). */
export function PartnerForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data: options } = useLoad<any>('/api/crm/options');
  const { data } = useLoad<any>(id ? `/api/crm/partners/${id}` : null);
  const [run, bar] = useRun();
  if (!options || (id && !data)) return <div className="empty">Loading…</div>;
  const p = data?.partner ?? { kind: 'Broker', status: 'Active', commissionBasis: 'kwh', commissionRate: 0.004 };
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) || null;
    const body = { name: v('name'), kind: v('kind'), contactName: v('contact_name'), email: v('email'), phone: v('phone'), city: v('city'), commissionBasis: v('commission_basis'),
      commissionRate: Number(f.get('commission_rate')), status: v('status'), ownerId: f.get('owner_id') ? Number(f.get('owner_id')) : null, agreementEndsOn: v('agreement_ends_on'), notes: v('notes') };
    run((id ? api.put(`/api/crm/partners/${id}`, body).then(() => ({ id: Number(id) })) : api.post<{ id: number }>('/api/crm/partners', body))
      .then((r: any) => { navigate(`/corral/crm/partners/${r.id}`); return r; }));
  };
  return (
    <>
      <PageHead title={id ? `Edit ${p.name}` : 'New Partner'} sub="A broker or referral partner who sends leads." />
      {bar}
      <form className="stack" style={{ maxWidth: 980 }} onSubmit={submit}>
        <div className="panel"><div className="panel-body stack">
          <div className="field"><label htmlFor="sel-kind">Kind</label><select name="kind" id="sel-kind" style={{ maxWidth: 320 }} defaultValue={p.kind}>{options.partnerKinds.map((k: string) => <option key={k}>{k}</option>)}</select></div>
          <div className="grid-2">
            <div className="field"><label htmlFor="name">Name</label><input id="name" name="name" required maxLength={150} defaultValue={p.name} /></div>
            <div className="field"><label htmlFor="contact_name">Contact</label><input id="contact_name" name="contact_name" maxLength={120} defaultValue={p.contactName ?? ''} /></div>
            <div className="field"><label htmlFor="email">Email</label><input id="email" name="email" type="email" maxLength={200} defaultValue={p.email ?? ''} /></div>
            <div className="field"><label htmlFor="phone">Phone</label><input id="phone" name="phone" maxLength={30} defaultValue={p.phone ?? ''} /></div>
            <div className="field"><label htmlFor="city">City</label><input id="city" name="city" maxLength={80} defaultValue={p.city ?? ''} /></div>
            <div className="field"><label htmlFor="agreement_ends_on">Agreement ends</label><DateInput id="agreement_ends_on" name="agreement_ends_on" defaultValue={p.agreementEndsOn ?? ''} /></div>
          </div>
          <div className="field"><label>Commission paid</label><div className="chips small">
            <label><input type="radio" name="commission_basis" value="kwh" defaultChecked={p.commissionBasis === 'kwh'} /><span>Per kWh</span></label>
            <label><input type="radio" name="commission_basis" value="flat" defaultChecked={p.commissionBasis === 'flat'} /><span>Share of first-year revenue</span></label></div></div>
          <div className="field"><label htmlFor="commission_rate">Rate</label><input id="commission_rate" name="commission_rate" type="number" step="0.0001" min={0} max={1} required defaultValue={p.commissionRate} style={{ maxWidth: '16ch' }} />
            <div className="help">Per kWh in dollars (0.004 = 0.4¢), or a share (0.05 = 5%).</div></div>
          <div className="field"><label htmlFor="p-status">Status</label><select id="p-status" name="status" style={{ maxWidth: 240 }} defaultValue={p.status}>{options.partnerStatuses.map((s: string) => <option key={s}>{s}</option>)}</select></div>
          <div className="field"><label>Our contact</label><PickList name="owner_id" value={p.ownerId} rows={options.owners.map((o: any) => ({ id: o.id, label: o.name, side: o.title ?? '' }))} /></div>
          <div className="field"><label htmlFor="notes">Notes</label><textarea id="notes" name="notes" rows={3} maxLength={2000} defaultValue={p.notes ?? ''} /></div>
        </div></div>
        <div className="actions"><button className="btn cyan">{id ? 'Save Partner' : 'Add Partner'}</button><Link className="btn ghost" to={id ? `/corral/crm/partners/${id}` : '/corral/crm/partners'}>Cancel</Link></div>
      </form>
    </>
  );
}
