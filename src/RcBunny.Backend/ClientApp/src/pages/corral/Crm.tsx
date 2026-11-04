import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, money, qs, useLoad } from '../../api';
import { Pager, Pill, Status, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const stageLabels: Record<string, string> = { new: 'New', contacted: 'Contacted', qualified: 'Qualified', proposal: 'Proposal Sent', won: 'Won', lost: 'Lost' };
const StagePill = ({ stage }: { stage: string }) => <Pill tone={stage === 'won' ? 'ok' : stage === 'lost' ? 'bad' : stage === 'proposal' || stage === 'qualified' ? 'warn' : 'info'}>{stageLabels[stage]}</Pill>;
const today = () => new Date().toISOString().slice(0, 10);

/** Pipeline: open leads by stage, value and weighted value, win rate, leads by source, and recent wins. */
export function Pipeline() {
  const [mine, setMine] = useState(false);
  const { data, error } = useLoad<any>(`/api/crm/pipeline?mine=${mine}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const s = data.stats;
  return (
    <>
      <div className="row spread">
        <h1>Pipeline</h1>
        <div className="row"><button className={mine ? '' : 'primary'} onClick={() => setMine(false)}>Everyone</button><button className={mine ? 'primary' : ''} onClick={() => setMine(true)}>Mine</button>
          <Link className="button" to="/corral/crm/leads/new">Add a lead</Link></div>
      </div>
      <div className="stats">
        <div className="card"><span className="muted">Open leads</span><strong>{s.open}</strong></div>
        <div className="card"><span className="muted">Yearly value</span><strong>{money(s.value)}</strong></div>
        <div className="card"><span className="muted">Weighted</span><strong>{money(s.weighted)}</strong></div>
        <div className="card"><span className="muted">Won this month</span><strong>{s.wonMonth}</strong><small>{money(s.wonValue)}</small></div>
        <div className="card"><span className="muted">Win rate (90 days)</span><strong>{s.winRate}%</strong></div>
        <Link className="card" to="/corral/crm/follow-ups"><span className="muted">Overdue follow-ups</span><strong className={s.overdue ? 'overdue' : ''}>{s.overdue}</strong></Link>
      </div>
      <div className="board">
        {data.stages.map((st: any) => (
          <div key={st.key} className="card">
            <h2>{st.label} <span className="muted">({st.leads.length})</span></h2>
            {st.leads.map((l: any) => (
              <Link key={l.id} to={`/corral/crm/leads/${l.id}`} className="lead">
                <strong>{l.company ?? l.name}</strong>
                <div className="muted">{l.kind} · {money(l.estValue)}/yr</div>
                {l.nextFollowUpOn && <div className={l.overdue ? 'overdue' : 'muted'}>Follow up {date(l.nextFollowUpOn)}</div>}
              </Link>
            ))}
          </div>
        ))}
      </div>
      <div className="grid">
        <section className="card"><h2>Leads by source (90 days)</h2>
          <table><tbody>{data.bySource.map((b: any) => <tr key={b.source}><td>{b.source}</td><td className="num">{b.count}</td><td className="muted">{b.won} won</td></tr>)}</tbody></table>
        </section>
        <section className="card"><h2>Recent wins</h2>
          <table><tbody>{data.recentWins.map((w: any) => <tr key={w.id}><td><Link to={`/corral/crm/leads/${w.id}`}>{w.company ?? w.name}</Link></td><td className="num">{money(w.estValue)}</td><td>{w.closedAt && date(w.closedAt)}</td><td>{w.owner}</td></tr>)}</tbody></table>
        </section>
      </div>
    </>
  );
}

/** Leads, with filters by stage, kind, source and owner. */
export function Leads() {
  const [params, setParams] = useSearchParams();
  const f = { stage: params.get('stage') ?? 'open', kind: params.get('kind') ?? '', source: params.get('source') ?? '', mine: params.get('mine') === 'true', q: params.get('q') ?? '' };
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/crm/leads?${qs({ ...f, page })}`);
  const { data: options } = useLoad<any>('/api/crm/options');
  const set = (changes: Record<string, any>) => setParams(qs({ ...f, page: 1, ...changes }));
  const count = (s: string) => data?.counts.find((c: any) => c.stage === s)?.n ?? 0;

  return (
    <>
      <div className="row spread"><h1>Leads</h1><Link className="button" to="/corral/crm/leads/new">Add a lead</Link></div>
      <div className="row">
        {['open', 'new', 'contacted', 'qualified', 'proposal', 'won', 'lost', 'all'].map((s) => (
          <button key={s} className={f.stage === s ? 'primary' : ''} onClick={() => set({ stage: s })}>{s === 'open' ? 'Open' : s === 'all' ? 'All' : `${stageLabels[s]} (${count(s)})`}</button>
        ))}
      </div>
      <form className="row" onSubmit={(e) => { e.preventDefault(); set({ q: new FormData(e.currentTarget).get('q') }); }}>
        <input name="q" placeholder="Name, company, email or phone" defaultValue={f.q} />
        <select value={f.kind} onChange={(e) => set({ kind: e.target.value })}><option value="">Any kind</option>{options?.kinds.map((k: string) => <option key={k}>{k}</option>)}</select>
        <select value={f.source} onChange={(e) => set({ source: e.target.value })}><option value="">Any source</option>{options?.sources.map((k: string) => <option key={k}>{k}</option>)}</select>
        <label style={{ flexDirection: 'row', margin: 0 }}><input type="checkbox" checked={f.mine} onChange={(e) => set({ mine: e.target.checked })} /> Mine</label>
        <button className="primary">Search</button>
      </form>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <table>
            <thead><tr><th>Lead</th><th>Kind</th><th>Source</th><th>Stage</th><th className="num">Value / yr</th><th>Follow up</th><th>Owner</th></tr></thead>
            <tbody>{data.leads.map((l: any) => (
              <tr key={l.id}><td><Link to={`/corral/crm/leads/${l.id}`}>{l.company ?? l.name}</Link>{l.company && <div className="muted">{l.name}</div>}</td><td>{l.kind}</td>
                <td>{l.source}{l.partner && <div className="muted">{l.partner}</div>}</td><td><StagePill stage={l.stage} /></td><td className="num">{money(l.estValue)}</td>
                <td className={l.nextFollowUpOn && l.nextFollowUpOn < today() ? 'overdue' : ''}>{l.nextFollowUpOn ? date(l.nextFollowUpOn) : '—'}</td><td>{l.owner ?? '—'}</td></tr>
            ))}</tbody>
          </table>
          <Pager page={page} total={data.total} size={30} onPage={(p) => setParams(qs({ ...f, page: p }))} />
        </>
      )}
    </>
  );
}

function ActivityForm({ url, run }: { url: string; run: (p: Promise<unknown>, done?: string) => Promise<void> }) {
  const [type, setType] = useState('call');
  return (
    <form className="card" onSubmit={(e) => { e.preventDefault(); const form = e.currentTarget, f = new FormData(form);
      run(api.post(url, { type, subject: f.get('subject'), body: f.get('body'), dueOn: f.get('dueOn') || null }).then(() => form.reset()), type === 'task' ? 'Follow-up set.' : 'Logged.'); }}>
      <h2>Log activity</h2>
      <div className="row">
        <select value={type} onChange={(e) => setType(e.target.value)}><option value="call">Call</option><option value="email">Email</option><option value="meeting">Meeting</option><option value="note">Note</option><option value="task">Follow-up</option></select>
        <input name="subject" placeholder={type === 'task' ? 'What to do' : 'Subject'} required />
        {type === 'task' && <input name="dueOn" type="date" required style={{ maxWidth: 170 }} />}
      </div>
      <textarea name="body" rows={2} placeholder="Details (optional)" />
      <button className="primary">Save</button>
    </form>
  );
}

const Activities = ({ rows }: { rows: any[] }) => (
  <section className="card"><h2>Activity</h2>
    <div className="note-feed">{rows.map((a) => (
      <article key={a.id}><div className="note-meta"><b>{a.type === 'stage' ? 'Stage' : a.type === 'task' ? 'Follow-up' : a.type}</b><span>{dateTime(a.createdAt)}</span><span>{a.user}</span>
        {a.dueOn && <span className={!a.doneAt && a.dueOn < today() ? 'overdue' : ''}>due {date(a.dueOn)}{a.doneAt && ' · done'}</span>}</div>
        <p><b>{a.subject}</b>{a.body && <><br />{a.body}</>}</p></article>
    ))}</div>
  </section>
);

/** One lead: details, move it through the stages, log calls/emails/meetings, follow-ups, and similar existing customers. */
export function Lead() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/crm/leads/${id}`);
  const { data: options } = useLoad<any>('/api/crm/options');
  const [run, bar] = useRun(reload);
  const [stage, setStage] = useState('');
  if (error) return <p className="error">{error}</p>;
  if (!data || !options) return <p>Loading…</p>;
  const l = data.lead;
  const next = stage || l.stage;

  return (
    <>
      <p><Link to="/corral/crm/leads">← Leads</Link></p>
      <div className="row spread"><h1>{l.company ?? l.name} <StagePill stage={l.stage} /></h1><Link className="button" to={`/corral/crm/leads/${l.id}/edit`}>Edit</Link></div>
      <p className="muted">Added {date(l.createdAt)} · stage since {date(l.stageChangedAt)}{l.closedAt && ` · closed ${date(l.closedAt)}`}{l.lostReason && ` · lost: ${l.lostReason}`}</p>
      {bar}
      <div className="grid">
        <section className="card"><h2>Details</h2>
          <dl>
            <dt>Contact</dt><dd>{l.name}{l.title && `, ${l.title}`}</dd><dt>Email</dt><dd>{l.email ? <a href={`mailto:${l.email}`}>{l.email}</a> : '—'}</dd><dt>Phone</dt><dd>{l.phone ?? '—'}</dd>
            <dt>Kind</dt><dd>{l.kind}{l.sites > 1 && ` · ${l.sites} sites`}</dd><dt>City / market</dt><dd>{l.city ?? '—'}{l.market && ` · ${l.market}`}</dd>
            <dt>Source</dt><dd>{l.source}{l.partner && <> · <Link to={`/corral/crm/partners/${l.partnerId}`}>{l.partner}</Link></>}</dd>
            <dt>Usage</dt><dd>{l.annualKwh.toLocaleString()} kWh / year</dd><dt>Value</dt><dd>{money(l.estValue)} / year · weighted {money(l.weighted)}{l.commission != null && ` · commission ${money(l.commission)}`}</dd>
            <dt>Now with</dt><dd>{l.currentProvider ?? '—'}{l.contractEndsOn && `, contract ends ${date(l.contractEndsOn)}`}</dd><dt>Owner</dt><dd>{l.owner ?? '—'}</dd>
            <dt>Next follow-up</dt><dd>{l.nextFollowUpOn ? date(l.nextFollowUpOn) : '—'}</dd>
            {l.customer && <><dt>Account</dt><dd><Link to={`/corral/customers/${l.customer.id}`}>{l.customer.name} #{l.customer.account}</Link></dd></>}
          </dl>
          {l.notes && <p className="wrap-text">{l.notes}</p>}
        </section>
        <section className="card"><h2>Move stage</h2>
          <form onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget);
            run(api.post(`/api/crm/leads/${l.id}/stage`, { stage: next, lostReason: f.get('lostOther') || f.get('lostReason'), account: f.get('account') }), `Moved to ${stageLabels[next]}.`).then(() => setStage('')); }}>
            <div className="row">{Object.keys(stageLabels).map((s) => <label key={s} style={{ flexDirection: 'row', margin: 0 }}><input type="radio" checked={next === s} onChange={() => setStage(s)} /> {stageLabels[s]}</label>)}</div>
            {next === 'lost' && <div className="row"><select name="lostReason">{options.lostReasons.map((r: string) => <option key={r}>{r}</option>)}</select><input name="lostOther" placeholder="or another reason" /></div>}
            {next === 'won' && <input name="account" placeholder="New account number (optional)" />}
            <button className="primary" disabled={next === l.stage}>Move</button>
          </form>
          <h2>Follow-ups</h2>
          {data.openTasks.length === 0 ? <p className="muted">None open.</p> : data.openTasks.map((t: any) => (
            <div key={t.id} className="row-line"><span className={t.dueOn < today() ? 'overdue' : ''}>{date(t.dueOn)} · {t.subject}</span><button onClick={() => run(api.post(`/api/crm/follow-ups/${t.id}`), 'Follow-up done.')}>Done</button></div>
          ))}
          {data.similar.length > 0 && <><h2>Already a customer?</h2>{data.similar.map((c: any) => <div key={c.id}><Link to={`/corral/customers/${c.id}`}>{c.name} #{c.account}</Link> · {c.status}</div>)}</>}
        </section>
      </div>
      <ActivityForm url={`/api/crm/leads/${l.id}/activities`} run={run} />
      <Activities rows={data.activities} />
    </>
  );
}

/** Add or edit a lead. */
export function LeadForm() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data: options } = useLoad<any>('/api/crm/options');
  const { data } = useLoad<any>(id ? `/api/crm/leads/${id}` : null);
  const [source, setSource] = useState<string | null>(null);
  const [run, bar] = useRun();
  if (!options || (id && !data)) return <p>Loading…</p>;
  const l = data?.lead ?? { kind: 'Small Business', source: params.get('partner') ? 'Broker' : 'Website', partnerId: Number(params.get('partner')) || null, sites: 1 };
  const src = source ?? l.source;

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) || null, n = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
    const body = { name: v('name'), company: v('company'), title: v('title'), kind: v('kind'), email: v('email'), phone: v('phone'), city: v('city'), marketId: n('marketId'), source: src,
      partnerId: n('partnerId'), annualKwh: n('annualKwh'), estValue: n('estValue'), sites: n('sites'), currentProvider: v('currentProvider'), contractEndsOn: v('contractEndsOn'),
      ownerId: n('ownerId'), nextFollowUpOn: v('nextFollowUpOn'), notes: v('notes') };
    run((id ? api.put<{ id: number }>(`/api/crm/leads/${id}`, body).then(() => ({ id: Number(id) })) : api.post<{ id: number }>('/api/crm/leads', body))
      .then((r) => { navigate(`/corral/crm/leads/${r.id}`); return r; }));
  };

  return (
    <>
      <h1>{id ? 'Edit lead' : 'Add a lead'}</h1>
      {bar}
      <form className="card" onSubmit={submit}>
        <div className="row">
          <label>Contact name<input name="name" defaultValue={l.name} required /></label>
          <label>Company<input name="company" defaultValue={l.company ?? ''} /></label>
          <label>Title<input name="title" defaultValue={l.title ?? ''} /></label>
        </div>
        <div className="row">
          <label>Kind<select name="kind" defaultValue={l.kind}>{options.kinds.map((k: string) => <option key={k}>{k}</option>)}</select></label>
          <label>Email<input name="email" type="email" defaultValue={l.email ?? ''} /></label>
          <label>Phone<input name="phone" defaultValue={l.phone ?? ''} /></label>
          <label>City<input name="city" defaultValue={l.city ?? ''} /></label>
          <label>Market<select name="marketId" defaultValue={l.marketId ?? ''}><option value="">—</option>{options.markets.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select></label>
        </div>
        <div className="row">
          <label>Source<select value={src} onChange={(e) => setSource(e.target.value)}>{options.sources.map((s: string) => <option key={s}>{s}</option>)}</select></label>
          {(src === 'Broker' || src === 'Partner') && <label>Broker / partner<select name="partnerId" defaultValue={l.partnerId ?? ''} required><option value="">—</option>{options.partners.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>}
          <label>Owner<select name="ownerId" defaultValue={l.ownerId ?? ''}><option value="">—</option>{options.owners.map((o: any) => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>
        </div>
        <div className="row">
          <label>kWh a year<input name="annualKwh" type="number" min="0" defaultValue={l.annualKwh ?? ''} /></label>
          <label>Value a year ($, blank = estimate)<input name="estValue" type="number" step="0.01" min="0" defaultValue={id ? l.estValue : ''} /></label>
          <label>Sites<input name="sites" type="number" min="1" defaultValue={l.sites} /></label>
        </div>
        <div className="row">
          <label>Current provider<input name="currentProvider" defaultValue={l.currentProvider ?? ''} /></label>
          <label>Their contract ends<input name="contractEndsOn" type="date" defaultValue={l.contractEndsOn ?? ''} /></label>
          <label>Next follow-up<input name="nextFollowUpOn" type="date" defaultValue={l.nextFollowUpOn ?? ''} /></label>
        </div>
        <label>Notes<textarea name="notes" rows={3} defaultValue={l.notes ?? ''} /></label>
        <button className="primary">Save</button>
      </form>
    </>
  );
}

/** Follow-ups: overdue, today, upcoming, and recently done. Yours, or everyone's. */
export function FollowUps() {
  const [who, setWho] = useState<string | null>(null);
  const { data, error, reload } = useLoad<any>(`/api/crm/follow-ups${who ? `?who=${who}` : ''}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const list = (title: string, rows: any[], tone?: string) => (
    <section className="card"><h2 className={tone}>{title} ({rows.length})</h2>
      {rows.length === 0 ? <p className="muted">None.</p> : (
        <table><tbody>{rows.map((t) => (
          <tr key={t.id}><td className={tone}>{t.dueOn && date(t.dueOn)}</td>
            <td>{t.leadId ? <Link to={`/corral/crm/leads/${t.leadId}`}>{t.lead}</Link> : t.partnerId ? <Link to={`/corral/crm/partners/${t.partnerId}`}>{t.partner}</Link> : '—'}</td>
            <td>{t.subject}{t.body && <div className="muted">{t.body}</div>}</td><td>{t.user}</td>
            <td><button onClick={() => run(api.post(`/api/crm/follow-ups/${t.id}`))}>{t.doneAt ? 'Reopen' : 'Done'}</button></td></tr>
        ))}</tbody></table>
      )}
    </section>
  );
  return (
    <>
      <div className="row spread"><h1>Follow-ups</h1>
        <div className="row"><button className={data.everyone ? '' : 'primary'} onClick={() => setWho('mine')}>Mine</button><button className={data.everyone ? 'primary' : ''} onClick={() => setWho('all')}>Everyone's</button></div></div>
      {bar}
      {list('Overdue', data.overdue, 'overdue')}{list('Today', data.today)}{list('Coming up', data.upcoming)}{list('Done in the last 14 days', data.done)}
    </>
  );
}

/** Partners & Brokers: leads sent, won, open, and commission earned. */
export function Partners() {
  const { data, error } = useLoad<any[]>('/api/crm/partners');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  return (
    <>
      <div className="row spread"><h1>Partners & Brokers</h1><Link className="button" to="/corral/crm/partners/new">Add a partner</Link></div>
      <table>
        <thead><tr><th>Name</th><th>Kind</th><th>Contact</th><th>Commission</th><th className="num">Leads</th><th className="num">Open</th><th className="num">Won</th><th className="num">Earned</th><th>Status</th></tr></thead>
        <tbody>{data.map((p) => (
          <tr key={p.id} className={p.status === 'Ended' ? 'inactive' : ''}><td><Link to={`/corral/crm/partners/${p.id}`}>{p.name}</Link>{p.city && <div className="muted">{p.city}</div>}</td><td>{p.kind}</td>
            <td>{p.contactName}{p.email && <div><a href={`mailto:${p.email}`}>{p.email}</a></div>}</td>
            <td>{p.commissionBasis === 'kwh' ? `${(p.commissionRate * 100).toFixed(2)}¢ / kWh` : `${(p.commissionRate * 100).toFixed(1)}% of value`}</td>
            <td className="num">{p.leads}</td><td className="num">{p.open}</td><td className="num">{p.won}</td><td className="num">{money(p.earned)}</td><td><Pill tone={p.status === 'Active' ? 'ok' : p.status === 'Paused' ? 'warn' : ''}>{p.status}</Pill></td></tr>
        ))}</tbody>
      </table>
    </>
  );
}

/** One partner: their leads, commission earned, pipeline, and activity. */
export function Partner() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/crm/partners/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const p = data.partner;
  return (
    <>
      <p><Link to="/corral/crm/partners">← Partners & Brokers</Link></p>
      <div className="row spread"><h1>{p.name} <span className="muted">{p.kind} · {p.status}</span></h1>
        <div className="row"><Link className="button" to={`/corral/crm/leads/new?partner=${p.id}`}>Add a lead from them</Link><Link className="button" to={`/corral/crm/partners/${p.id}/edit`}>Edit</Link></div></div>
      {bar}
      <div className="stats">
        <div className="card"><span className="muted">Leads won</span><strong>{data.won}</strong></div>
        <div className="card"><span className="muted">Commission earned</span><strong>{money(data.earned)}</strong></div>
        <div className="card"><span className="muted">Open pipeline</span><strong>{money(data.pipeline)}</strong></div>
      </div>
      <section className="card"><h2>Details</h2>
        <dl><dt>Contact</dt><dd>{p.contactName ?? '—'}</dd><dt>Email</dt><dd>{p.email ?? '—'}</dd><dt>Phone</dt><dd>{p.phone ?? '—'}</dd><dt>City</dt><dd>{p.city ?? '—'}</dd>
          <dt>Commission</dt><dd>{p.commissionBasis === 'kwh' ? `${p.commissionRate} $/kWh × yearly kWh` : `${p.commissionRate} × yearly value`}</dd>
          <dt>Owner</dt><dd>{p.owner ?? '—'}</dd><dt>Agreement ends</dt><dd>{p.agreementEndsOn ? date(p.agreementEndsOn) : '—'}</dd></dl>
        {p.notes && <p className="wrap-text">{p.notes}</p>}
      </section>
      <section className="card"><h2>Leads</h2>
        <table><thead><tr><th>Lead</th><th>Stage</th><th className="num">Value</th><th className="num">Commission</th><th>Owner</th><th>Added</th></tr></thead>
          <tbody>{data.leads.map((l: any) => <tr key={l.id}><td><Link to={`/corral/crm/leads/${l.id}`}>{l.company ?? l.name}</Link></td><td><StagePill stage={l.stage} /></td><td className="num">{money(l.estValue)}</td>
            <td className="num">{l.commission != null ? money(l.commission) : '—'}</td><td>{l.owner}</td><td>{date(l.createdAt)}</td></tr>)}</tbody></table>
      </section>
      <ActivityForm url={`/api/crm/partners/${p.id}/activities`} run={run} />
      <Activities rows={data.activities} />
    </>
  );
}

/** Add or edit a partner. */
export function PartnerForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data: options } = useLoad<any>('/api/crm/options');
  const { data } = useLoad<any>(id ? `/api/crm/partners/${id}` : null);
  const [run, bar] = useRun();
  if (!options || (id && !data)) return <p>Loading…</p>;
  const p = data?.partner ?? { kind: 'Broker', status: 'Active', commissionBasis: 'kwh', commissionRate: 0.004 };
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) || null;
    const body = { name: v('name'), kind: v('kind'), contactName: v('contactName'), email: v('email'), phone: v('phone'), city: v('city'), commissionBasis: v('commissionBasis'),
      commissionRate: Number(f.get('commissionRate')), status: v('status'), ownerId: f.get('ownerId') ? Number(f.get('ownerId')) : null, agreementEndsOn: v('agreementEndsOn'), notes: v('notes') };
    run((id ? api.put(`/api/crm/partners/${id}`, body).then(() => ({ id: Number(id) })) : api.post<{ id: number }>('/api/crm/partners', body))
      .then((r: any) => { navigate(`/corral/crm/partners/${r.id}`); return r; }));
  };
  return (
    <>
      <h1>{id ? 'Edit partner' : 'Add a partner'}</h1>
      {bar}
      <form className="card" onSubmit={submit}>
        <div className="row">
          <label>Name<input name="name" defaultValue={p.name} required /></label>
          <label>Kind<select name="kind" defaultValue={p.kind}>{options.partnerKinds.map((k: string) => <option key={k}>{k}</option>)}</select></label>
          <label>Status<select name="status" defaultValue={p.status}>{options.partnerStatuses.map((k: string) => <option key={k}>{k}</option>)}</select></label>
        </div>
        <div className="row">
          <label>Contact<input name="contactName" defaultValue={p.contactName ?? ''} /></label>
          <label>Email<input name="email" type="email" defaultValue={p.email ?? ''} /></label>
          <label>Phone<input name="phone" defaultValue={p.phone ?? ''} /></label>
          <label>City<input name="city" defaultValue={p.city ?? ''} /></label>
        </div>
        <div className="row">
          <label>Commission based on<select name="commissionBasis" defaultValue={p.commissionBasis}><option value="kwh">Yearly kWh ($ per kWh)</option><option value="flat">Yearly value (share)</option></select></label>
          <label>Rate<input name="commissionRate" type="number" step="0.0001" min="0" max="1" defaultValue={p.commissionRate} required /></label>
          <label>Owner<select name="ownerId" defaultValue={p.ownerId ?? ''}><option value="">—</option>{options.owners.map((o: any) => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>
          <label>Agreement ends<input name="agreementEndsOn" type="date" defaultValue={p.agreementEndsOn ?? ''} /></label>
        </div>
        <label>Notes<textarea name="notes" rows={3} defaultValue={p.notes ?? ''} /></label>
        <button className="primary">Save</button>
      </form>
    </>
  );
}
