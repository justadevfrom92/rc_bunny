import { useDeferredValue, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, post, put, qs } from '../../api';
import { signOut } from '../../auth';
import { Bars, date, dateTime, ErrorBox, num, Panel, pct, Pill, Refreshing, Skeleton, Stat, Stats } from '../../ui';

// ---------------------------------------------------------------- Rodeo

export function RodeoDashboard() {
  const q = useQuery({ queryKey: ['rodeo', 'dashboard'], queryFn: () => api<{ emails30: number; texts30: number; openRate: number; clickRate: number; bounceRate: number; campaigns: { id: number; name: string; channel: string; status: string; recipients: number; sentAt: string | null; scheduledAt: string | null }[]; surveys: { id: number; title: string; responses: number }[]; suppressed: number }>('/api/rodeo/dashboard') });
  const d = q.data;
  return (
    <>
      <div className="page-head"><h1>Marketing</h1><Link className="btn" to="/rodeo/campaigns/new">+ New Campaign</Link></div>
      {!d ? <Skeleton rows={6} /> : (
        <>
          <Stats><Stat label="Emails, 30 days" value={num(d.emails30)} to="/rodeo/r/sent-emails" /><Stat label="Texts, 30 days" value={num(d.texts30)} /><Stat label="Open rate" value={pct(d.openRate)} /><Stat label="Click rate" value={pct(d.clickRate)} /><Stat label="Bounce rate" value={pct(d.bounceRate)} /><Stat label="Suppressed" value={d.suppressed} to="/rodeo/r/email-suppressions" /></Stats>
          <div className="grid-2">
            <Panel title="Campaigns"><table className="table compact"><tbody>{d.campaigns.map((c) => <tr key={c.id}><td><Link to={`/rodeo/r/campaigns/${c.id}`}>{c.name}</Link><div className="muted small">{c.channel}</div></td><td><Pill tone={c.status === 'sent' ? 'ok' : c.status === 'scheduled' ? 'info' : ''}>{c.status}</Pill></td><td className="num">{c.status === 'sent' ? `${num(c.recipients)} sent` : c.scheduledAt ? dateTime(c.scheduledAt) : ''}</td></tr>)}</tbody></table></Panel>
            <Panel title="Active surveys"><table className="table compact"><tbody>{d.surveys.map((s) => <tr key={s.id}><td><Link to={`/rodeo/surveys/${s.id}/results`}>{s.title}</Link></td><td className="num">{s.responses} responses</td></tr>)}</tbody></table></Panel>
          </div>
        </>
      )}
    </>
  );
}

interface AudienceOpts { statuses: string[]; types: string[]; phoneTypes: string[]; markets: string[]; products: string[]; templates: { id: number; name: string }[] }

function Multi({ label, options, value, onChange }: { label: string; options: string[]; value: string[]; onChange: (v: string[]) => void }) {
  return (
    <fieldset className="multi"><legend>{label}</legend>
      {options.map((o) => <label key={o} className="check"><input type="checkbox" checked={value.includes(o)} onChange={(e) => onChange(e.target.checked ? [...value, o] : value.filter((x) => x !== o))} /> {o}</label>)}
    </fieldset>
  );
}

/** New Campaign: audience builder with a live count, then the message. */
export function NewCampaign() {
  const navigate = useNavigate();
  const opts = useQuery({ queryKey: ['rodeo', 'audience-options'], queryFn: () => api<AudienceOpts>('/api/rodeo/audience-options'), staleTime: 10 * 60_000 });
  const [c, setC] = useState({ name: '', channel: 'Email', emailTemplateId: '', message: '', goal: 'engagement', capDays: 7, promoCode: '', msid: '' });
  const [aud, setAud] = useState<{ statuses: string[]; types: string[]; phone_types: string[]; markets: string[]; without_product: string; with_product: string }>({ statuses: ['Good - On Flow'], types: [], phone_types: [], markets: [], without_product: '', with_product: '' });
  const audience = useDeferredValue(aud);
  const count = useQuery({ queryKey: ['rodeo', 'count', c.channel, audience], queryFn: () => post<{ count: number; sample: { account: string; name: string }[] }>('/api/rodeo/audience-count', { channel: c.channel, audience }) });
  const save = useMutation({
    mutationFn: () => post<{ _id: string }>('/api/r/campaigns', { name: c.name, channel: c.channel, emailTemplateId: c.channel === 'Email' ? c.emailTemplateId || null : null, message: c.channel === 'SMS' ? c.message : null, goal: c.goal, capDays: c.capDays, promoCode: c.promoCode || null, msid: c.msid || null, audience: aud }),
    onSuccess: (r) => navigate(`/rodeo/r/campaigns/${r._id}`),
  });
  if (!opts.data) return <Skeleton rows={8} />;
  const o = opts.data;
  return (
    <>
      <h1>New Campaign</h1>
      <ErrorBox error={save.error} />
      <div className="grid-2" style={{ gridTemplateColumns: '2fr 1fr' }}>
        <div>
          <Panel title="Message">
            <div className="form-grid">
              <label>Name<input value={c.name} onChange={(e) => setC({ ...c, name: e.target.value })} required /></label>
              <label>Channel<select value={c.channel} onChange={(e) => setC({ ...c, channel: e.target.value })}><option>Email</option><option>SMS</option></select></label>
              {c.channel === 'Email'
                ? <label>Template<select value={c.emailTemplateId} onChange={(e) => setC({ ...c, emailTemplateId: e.target.value })}><option value="">Pick a template</option>{o.templates.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select></label>
                : <label className="span-all">Text message<textarea rows={3} maxLength={320} value={c.message} onChange={(e) => setC({ ...c, message: e.target.value })} placeholder="Hi {{first_name}}, … Reply STOP to opt out." /></label>}
              <label>Goal<select value={c.goal} onChange={(e) => setC({ ...c, goal: e.target.value })}>{['renewals', 'product', 'winback', 'engagement', 'survey'].map((g) => <option key={g}>{g}</option>)}</select></label>
              <label>Don't message anyone messaged in the last (days)<input type="number" min={0} value={c.capDays} onChange={(e) => setC({ ...c, capDays: Number(e.target.value) })} /></label>
              <label>Promo code<input value={c.promoCode} onChange={(e) => setC({ ...c, promoCode: e.target.value })} /></label>
            </div>
          </Panel>
          <Panel title="Audience">
            <div className="form-grid">
              <Multi label="Statuses" options={o.statuses} value={aud.statuses} onChange={(v) => setAud({ ...aud, statuses: v })} />
              <Multi label="Customer type" options={o.types} value={aud.types} onChange={(v) => setAud({ ...aud, types: v })} />
              <Multi label="Utility" options={o.markets} value={aud.markets} onChange={(v) => setAud({ ...aud, markets: v })} />
              {c.channel === 'SMS' && <Multi label="Phone type" options={o.phoneTypes} value={aud.phone_types} onChange={(v) => setAud({ ...aud, phone_types: v })} />}
              <label>Has product<select value={aud.with_product} onChange={(e) => setAud({ ...aud, with_product: e.target.value })}><option value="">Any</option>{o.products.map((p) => <option key={p}>{p}</option>)}</select></label>
              <label>Doesn't have product<select value={aud.without_product} onChange={(e) => setAud({ ...aud, without_product: e.target.value })}><option value="">Any</option>{o.products.map((p) => <option key={p}>{p}</option>)}</select></label>
            </div>
          </Panel>
        </div>
        <Panel title="Reaches">
          <p className="big-num">{count.data ? num(count.data.count) : '…'} <span className="muted small">customers</span> <Refreshing on={count.isFetching} /></p>
          <p className="muted small">Customers who opted out of marketing and suppressed addresses are always left out.</p>
          <ul className="notes">{count.data?.sample.map((s) => <li key={s.account}>{s.account} · {s.name}</li>)}</ul>
          <button className="btn block" disabled={!c.name || save.isPending} onClick={() => save.mutate()}>Save as Draft</button>
          <p className="muted small">Send or schedule it from the campaign page.</p>
        </Panel>
      </div>
    </>
  );
}

export function EmailOverview() {
  const q = useQuery({ queryKey: ['rodeo', 'emails'], queryFn: () => api<{ byTemplate: { template: string; sent: number; opened: number; clicked: number; bounced: number }[]; suppressions: { reason: string; n: number }[]; mail: string }>('/api/rodeo/emails') });
  return (
    <>
      <h1>Emails</h1>
      {!q.data ? <Skeleton rows={8} /> : (
        <>
          <p className="muted">{q.data.mail}</p>
          <div className="grid-2" style={{ gridTemplateColumns: '2fr 1fr' }}>
            <Panel title="Last 90 days by template"><table className="table compact"><thead><tr><th>Template</th><th className="num">Sent</th><th className="num">Opened</th><th className="num">Clicked</th><th className="num">Bounced</th></tr></thead>
              <tbody>{q.data.byTemplate.map((t) => <tr key={t.template}><td>{t.template}</td><td className="num">{t.sent}</td><td className="num">{Math.round((100 * t.opened) / Math.max(1, t.sent))}%</td><td className="num">{Math.round((100 * t.clicked) / Math.max(1, t.sent))}%</td><td className="num">{t.bounced}</td></tr>)}</tbody></table></Panel>
            <Panel title="Suppression list"><Bars rows={q.data.suppressions.map((s) => ({ label: s.reason, value: s.n }))} /><Link to="/rodeo/r/email-suppressions">See the list</Link></Panel>
          </div>
        </>
      )}
    </>
  );
}

export function SurveyEmailsPage() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['rodeo', 'survey-emails'], queryFn: () => api<{ ninetyDay: { id: number; account: string; name: string }[]; farewell: { id: number; account: string; name: string; status: string }[]; sent: { template: string; sentAt: string; account: string; name: string }[] }>('/api/rodeo/survey-emails') });
  const send = useMutation({ mutationFn: () => post<{ message: string }>('/api/rodeo/survey-emails/send'), onSuccess: () => qc.invalidateQueries({ queryKey: ['rodeo'] }) });
  return (
    <>
      <div className="page-head"><h1>Survey Emails</h1><button className="btn" disabled={send.isPending || !q.data || q.data.ninetyDay.length + q.data.farewell.length === 0} onClick={() => send.mutate()}>Send Now</button></div>
      <p className="muted">The et:survey-emails job sends these every morning: the 90-Day Check-In to customers about three months in, and the farewell survey to customers who left. Each link is signed so the answer is tied to the account.</p>
      {send.data && <div className="alert ok">{send.data.message}</div>}
      {!q.data ? <Skeleton rows={6} /> : (
        <div className="grid-2">
          <Panel title={`90-Day Check-In (${q.data.ninetyDay.length} due)`}><ul className="notes">{q.data.ninetyDay.map((c) => <li key={c.id}>{c.account} · {c.name}</li>)}</ul></Panel>
          <Panel title={`Farewell (${q.data.farewell.length} due)`}><ul className="notes">{q.data.farewell.map((c) => <li key={c.id}>{c.account} · {c.name} <span className="muted small">{c.status}</span></li>)}</ul></Panel>
          <Panel title="Recently sent"><table className="table compact"><tbody>{q.data.sent.map((s, i) => <tr key={i}><td>{s.template}</td><td>{s.account} {s.name}</td><td>{dateTime(s.sentAt)}</td></tr>)}</tbody></table></Panel>
        </div>
      )}
    </>
  );
}

interface QResult { id: number; question: string; type: string; category: string | null; answered: number; nps?: number | null; average?: number; counts?: { label: string; n: number }[]; texts?: string[] }

export function SurveyResults() {
  const id = Number(useParams().id);
  const q = useQuery({ queryKey: ['rodeo', 'results', id], queryFn: () => api<{ survey: { id: number; title: string; slug: string; status: string }; responses: number; questions: QResult[] }>(`/api/rodeo/surveys/${id}/results`) });
  if (!q.data) return q.error ? <ErrorBox error={q.error} /> : <Skeleton rows={8} />;
  const cats = [...new Set(q.data.questions.map((x) => x.category ?? 'Other'))];
  return (
    <>
      <p className="crumbs"><Link to="/rodeo/r/surveys">Surveys</Link> › <Link to={`/rodeo/r/surveys/${id}`}>{q.data.survey.title}</Link> › Results</p>
      <div className="page-head"><h1>{q.data.survey.title}: {q.data.responses} responses</h1><div className="row"><a className="btn outline" href={`/api/rodeo/surveys/${id}/export`}>Export CSV</a><a className="btn outline" href={`${import.meta.env.VITE_WEBSITE_URL ?? 'http://localhost:5100'}/survey/${q.data.survey.slug}`} target="_blank" rel="noreferrer">Open survey ↗</a></div></div>
      {cats.map((cat) => (
        <section key={cat}>
          <h2 style={{ margin: '12px 0' }}>{cat}</h2>
          <div className="grid-2">
            {q.data.questions.filter((x) => (x.category ?? 'Other') === cat).map((x) => (
              <Panel key={x.id} title={x.question}>
                <p className="muted small">{x.answered} answered{x.nps != null && <> · <strong>NPS {x.nps}</strong></>}{x.average ? <> · average {x.average}</> : null}</p>
                {x.counts && <Bars rows={x.counts.map((c) => ({ label: c.label, value: c.n }))} />}
                {x.texts && <ul className="notes">{x.texts.map((t, i) => <li key={i}>“{t}”</li>)}</ul>}
              </Panel>
            ))}
          </div>
        </section>
      ))}
    </>
  );
}

export function TemplateTest() {
  const id = Number(useParams().id);
  const [account, setAccount] = useState('');
  const [acct, setAcct] = useState<string | undefined>();
  const preview = useQuery({ queryKey: ['rodeo', 'preview', id, acct], queryFn: () => api<{ subject: string; html: string; to: string; account: string }>(`/api/rodeo/templates/${id}/preview${qs({ account: acct })}`) });
  const [t, setT] = useState({ mode: 'me', emails: '', accounts: '', note: '', subjectPrefix: '[TEST] ' });
  const [width, setWidth] = useState(640);
  const send = useMutation({ mutationFn: () => post<{ message: string }>(`/api/rodeo/templates/${id}/test`, t) });
  return (
    <>
      <p className="crumbs"><Link to="/rodeo/r/email-templates">Templates</Link> › <Link to={`/rodeo/r/email-templates/${id}`}>Template</Link> › Send Test</p>
      <h1>Send Test</h1>
      <div className="grid-2" style={{ gridTemplateColumns: '1fr 2fr' }}>
        <Panel title="Who gets it">
          <div className="form-grid" style={{ gridTemplateColumns: '1fr' }}>
            <label>Send to<select value={t.mode} onChange={(e) => setT({ ...t, mode: e.target.value })}><option value="me">Just me</option><option value="emails">These addresses</option><option value="accounts">These accounts</option><option value="bookmarks">My Corral bookmarks</option></select></label>
            {t.mode === 'emails' && <label>Emails<textarea rows={3} value={t.emails} onChange={(e) => setT({ ...t, emails: e.target.value })} /></label>}
            {t.mode === 'accounts' && <label>Account numbers<textarea rows={3} value={t.accounts} onChange={(e) => setT({ ...t, accounts: e.target.value })} /></label>}
            <label>Subject prefix<input value={t.subjectPrefix} onChange={(e) => setT({ ...t, subjectPrefix: e.target.value })} /></label>
            <label>Note to testers<textarea rows={2} value={t.note} onChange={(e) => setT({ ...t, note: e.target.value })} /></label>
          </div>
          <ErrorBox error={send.error} />
          {send.data && <div className="alert ok">{send.data.message}</div>}
          <button className="btn" onClick={() => send.mutate()} disabled={send.isPending}>Send Test (up to 25)</button>
          <p className="small"><Link to={`/rodeo/r/email-test-sends?f.emailTemplateId=${id}`}>Test history</Link></p>
        </Panel>
        <Panel title={preview.data ? `Subject: ${preview.data.subject}` : 'Preview'} actions={<div className="tab-row"><button className={`btn small ${width === 640 ? '' : 'outline'}`} onClick={() => setWidth(640)}>Desktop</button><button className={`btn small ${width === 375 ? '' : 'outline'}`} onClick={() => setWidth(375)}>Phone</button></div>}>
          <form className="filters" onSubmit={(e) => { e.preventDefault(); setAcct(account || undefined); }}><input placeholder="Preview as account #" value={account} onChange={(e) => setAccount(e.target.value)} /><button className="btn outline small">Preview</button></form>
          <ErrorBox error={preview.error} />
          {preview.data && <><p className="muted small">As account {preview.data.account} · to {preview.data.to}</p><iframe title="Email preview" className="preview-frame" style={{ maxWidth: width }} sandbox="" srcDoc={preview.data.html} /></>}
        </Panel>
      </div>
    </>
  );
}

// ---------------------------------------------------------------- Deputy

export function DeputyDashboard() {
  const q = useQuery({ queryKey: ['deputy', 'dashboard'], queryFn: () => api<{ conversations: number; waiting: number; live: number; resolvedRate: number; handoffRate: number; tokens: number; byOutcome: { outcome: string; n: number }[]; byTopic: { topic: string; n: number; handedOff: number }[]; agents: { id: number; name: string; activity: string; active: boolean; schedule: string | null; model: string; modelStatus: string; conversations: number }[]; runs: { id: number; agent: string; status: string; checked: number; fixed: number; flagged: number; startedAt: string; summary: string }[] }>('/api/deputy/dashboard'), refetchInterval: 30_000 });
  const d = q.data;
  return (
    <>
      <h1>AI Agents</h1>
      {!d ? <Skeleton rows={6} /> : (
        <>
          <Stats><Stat label="Waiting for a person" value={d.waiting} to="/deputy/r/ai-conversations?f.status=waiting" tone={d.waiting ? 'bad' : undefined} /><Stat label="Live now" value={d.live} /><Stat label="Conversations, 30 days" value={num(d.conversations)} to="/deputy/r/ai-conversations" /><Stat label="Resolved by the bot" value={pct(d.resolvedRate)} /><Stat label="Handed to a person" value={pct(d.handoffRate)} /><Stat label="Tokens" value={num(d.tokens)} /></Stats>
          <div className="grid-2">
            <Panel title="By topic"><Bars rows={d.byTopic.map((t) => ({ label: t.topic, value: t.n, hint: `${t.handedOff} handed off` }))} /></Panel>
            <Panel title="Outcomes"><Bars rows={d.byOutcome.map((o) => ({ label: o.outcome, value: o.n }))} /></Panel>
          </div>
          <Panel title="Agents"><table className="table compact"><tbody>{d.agents.map((a) => <tr key={a.id}><td><Link to={`/deputy/r/ai-agents/${a.id}`}>{a.name}</Link><div className="muted small">{a.activity}{a.schedule && ` · ${a.schedule}`}</div></td><td>{a.model} <Pill tone={a.modelStatus === 'available' ? 'ok' : 'warn'}>{a.modelStatus}</Pill></td><td>{a.active ? <Pill tone="ok">on</Pill> : <Pill>off</Pill>}</td><td className="num">{a.conversations || ''}</td></tr>)}</tbody></table></Panel>
          <Panel title="Recent automation runs"><table className="table compact"><tbody>{d.runs.map((r) => <tr key={r.id}><td>{dateTime(r.startedAt)}</td><td>{r.agent}</td><td><Pill tone={r.status === 'completed' ? 'ok' : r.status === 'failed' ? 'bad' : 'warn'}>{r.status}</Pill></td><td className="muted">{r.summary}</td></tr>)}</tbody></table></Panel>
        </>
      )}
    </>
  );
}

// ---------------------------------------------------------------- Sheriff

interface HbResult { status: string; message: string; ms: number }

export function Heartbeat() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['sheriff', 'heartbeat'], queryFn: () => api<{ latest: { checkedAt: string; status: string; results: Record<string, HbResult>; durationMs: number } | null; history: { checkedAt: string; status: string }[]; controls: { key: string; state: string; reason: string | null; changedAt: string; who: string | null }[]; pulses: { key: string; beatAt: string }[] }>('/api/sheriff/heartbeat'), refetchInterval: 30_000 });
  const check = useMutation({ mutationFn: () => post('/api/sheriff/heartbeat/check'), onSuccess: () => qc.invalidateQueries({ queryKey: ['sheriff', 'heartbeat'] }) });
  const control = useMutation({ mutationFn: ({ key, action, reason }: { key: string; action: string; reason?: string }) => post<{ message: string }>(`/api/sheriff/heartbeat/${key}/${action}`, { reason }), onSuccess: () => qc.invalidateQueries({ queryKey: ['sheriff'] }) });
  const d = q.data;
  const websiteDown = d?.controls.find((c) => c.key === 'website')?.state === 'down';
  return (
    <>
      <div className="page-head"><h1>Heartbeat</h1><button className="btn" disabled={check.isPending} onClick={() => check.mutate()}>{check.isPending ? 'Checking…' : 'Check Now'}</button></div>
      {control.data && <div className="alert ok">{control.data.message}</div>}
      <ErrorBox error={check.error ?? control.error} />
      {!d ? <Skeleton rows={8} /> : (
        <>
          {d.latest && <p className="muted">Last check {dateTime(d.latest.checkedAt)} · {d.latest.durationMs} ms · overall <Pill tone={d.latest.status === 'ok' ? 'ok' : d.latest.status === 'warn' ? 'warn' : 'bad'}>{d.latest.status}</Pill></p>}
          <div className="cards">
            {Object.entries(d.latest?.results ?? {}).map(([k, r]) => (
              <div key={k} className={`panel health-${r.status}`}><h3>{k.replace('_', ' ')}</h3><p className="small">{r.message}</p><p className="muted small">{r.ms} ms</p></div>
            ))}
          </div>
          <Panel title="Controls">
            <div className="row">
              <button className={`btn ${websiteDown ? '' : 'outline danger'}`} onClick={() => { if (websiteDown) control.mutate({ key: 'website', action: 'up' }); else { const r = prompt('Why is the website going into maintenance?'); if (r !== null) control.mutate({ key: 'website', action: 'down', reason: r }); } }}>{websiteDown ? 'Bring the Website Back' : 'Website Maintenance Mode'}</button>
              <button className="btn outline" onClick={() => control.mutate({ key: 'queue', action: 'run_queue' })}>Run Queued Reports</button>
              <button className="btn outline" onClick={() => control.mutate({ key: 'cache', action: 'clear_cache' })}>Clear Caches</button>
              <button className="btn outline" onClick={() => control.mutate({ key: 'mail', action: 'send_test' })}>Send Test Email</button>
            </div>
            <table className="table compact" style={{ marginTop: 12 }}><tbody>{d.controls.map((c) => <tr key={c.key}><td>{c.key}</td><td><Pill tone={c.state === 'down' ? 'bad' : 'ok'}>{c.state}</Pill></td><td>{c.reason}</td><td className="muted">{c.who} · {dateTime(c.changedAt)}</td></tr>)}
              {d.pulses.map((p) => <tr key={p.key}><td>{p.key} beat</td><td colSpan={3}>{dateTime(p.beatAt)}</td></tr>)}</tbody></table>
          </Panel>
          <Panel title="Last day"><div className="tab-row">{d.history.slice().reverse().map((h, i) => <span key={i} title={`${dateTime(h.checkedAt)}: ${h.status}`} className={`swatch`} style={{ background: h.status === 'ok' ? 'var(--ok)' : h.status === 'warn' ? 'var(--gold)' : 'var(--bad)', width: 12 }} />)}</div></Panel>
        </>
      )}
    </>
  );
}

export function Jobs() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['sheriff', 'jobs'], queryFn: () => api<{ command: string; name: string; schedule: string; failures: number; last: { status: string; startedAt: string; durationMs: number | null; message: string | null } | null }[]>('/api/sheriff/jobs'), refetchInterval: 30_000 });
  const run = useMutation({ mutationFn: (cmd: string) => post<{ status: string; message: string }>(`/api/sheriff/jobs/${encodeURIComponent(cmd)}/run`), onSuccess: () => qc.invalidateQueries({ queryKey: ['sheriff'] }) });
  return (
    <>
      <h1>Crons</h1>
      <p className="muted">Scheduled jobs. The Backend runs them on their schedule (Scheduler); Run Now runs one immediately.</p>
      {run.data && <div className={`alert ${run.data.status === 'Failed' ? 'bad' : 'ok'}`}>{run.data.status}: {run.data.message}</div>}
      <ErrorBox error={run.error} />
      <Panel>{!q.data ? <Skeleton rows={10} /> : (
        <table className="table"><thead><tr><th>Job</th><th>Schedule</th><th>Last run</th><th>Status</th><th /></tr></thead>
          <tbody>{q.data.map((j) => <tr key={j.command}><td>{j.name}<div className="mono small muted">{j.command}</div></td><td className="mono">{j.schedule}</td><td>{dateTime(j.last?.startedAt)}<div className="muted small">{j.last?.message}</div></td>
            <td>{j.last && <Pill tone={j.last.status === 'OK' ? 'ok' : j.last.status === 'Failed' ? 'bad' : 'warn'}>{j.last.status}</Pill>}{j.failures > 0 && <span className="muted small"> {j.failures} failed recently</span>}</td>
            <td><button className="btn outline small" disabled={run.isPending} onClick={() => run.mutate(j.command)}>Run Now</button> <Link className="small" to={`/sheriff/r/job-runs?f.command=${encodeURIComponent(j.command)}`}>history</Link></td></tr>)}</tbody></table>
      )}</Panel>
    </>
  );
}

export function Apis() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['sheriff', 'apis'], queryFn: () => api<{ key: string; name: string; purpose: string; settings: { name: string; set: boolean }[]; last: { ok: boolean; message: string; createdAt: string } | null }[]>('/api/sheriff/apis') });
  const test = useMutation({ mutationFn: (key: string) => post(`/api/sheriff/apis/${key}/test`), onSuccess: () => qc.invalidateQueries({ queryKey: ['sheriff', 'apis'] }) });
  const all = useMutation({ mutationFn: () => post<{ message: string }>('/api/sheriff/apis/test-all'), onSuccess: () => qc.invalidateQueries({ queryKey: ['sheriff', 'apis'] }) });
  return (
    <>
      <div className="page-head"><h1>Integrations</h1><button className="btn" onClick={() => all.mutate()} disabled={all.isPending}>Test All</button></div>
      <p className="muted">Keys go in environment variables (or appsettings) only; this page shows whether each is set, never the value.</p>
      <Panel>{!q.data ? <Skeleton rows={10} /> : (
        <table className="table"><thead><tr><th>Integration</th><th>Settings</th><th>Last test</th><th /></tr></thead>
          <tbody>{q.data.map((a) => <tr key={a.key}><td>{a.name}<div className="muted small">{a.purpose}</div></td><td>{a.settings.map((s) => <div key={s.name} className="mono small">{s.set ? '✓' : '✗'} {s.name}</div>)}</td>
            <td>{a.last ? <><Pill tone={a.last.ok ? 'ok' : 'bad'}>{a.last.ok ? 'OK' : 'Failed'}</Pill> <span className="small">{a.last.message}</span><div className="muted small">{dateTime(a.last.createdAt)}</div></> : '—'}</td>
            <td><button className="btn outline small" onClick={() => test.mutate(a.key)}>Test Connection</button></td></tr>)}</tbody></table>
      )}</Panel>
    </>
  );
}

export function TimelineAll() {
  const [f, setF] = useState({ q: '', kind: '', days: '7' });
  const deferred = useDeferredValue(f);
  const q = useQuery({ queryKey: ['sheriff', 'timeline-all', deferred], queryFn: () => api<{ at: string; kind: string; who: string; what: string; detail: string | null; link: string | null }[]>(`/api/sheriff/timeline/all${qs(deferred)}`), refetchInterval: 30_000 });
  return (
    <>
      <h1>Timeline</h1>
      <Panel>
        <div className="filters">
          <input type="search" placeholder="Search…" value={f.q} onChange={(e) => setF({ ...f, q: e.target.value })} aria-label="Search" />
          <select value={f.kind} onChange={(e) => setF({ ...f, kind: e.target.value })} aria-label="Kind"><option value="">Everything</option><option value="admin">What people did</option><option value="job">Cron jobs</option><option value="report">Reports</option><option value="history">Record changes</option><option value="api">API calls</option></select>
          <select value={f.days} onChange={(e) => setF({ ...f, days: e.target.value })} aria-label="Days"><option value="1">Today</option><option value="7">7 days</option><option value="30">30 days</option><option value="90">90 days</option></select>
        </div>
        {!q.data ? <Skeleton rows={12} /> : (
          <table className="table compact"><tbody>{q.data.map((i, n) => <tr key={n}><td>{dateTime(i.at)}</td><td><Pill>{i.kind}</Pill></td><td>{i.who}</td><td>{i.link ? <Link to={i.link}>{i.what}</Link> : i.what}</td><td className="muted small">{i.detail}</td></tr>)}</tbody></table>
        )}
      </Panel>
    </>
  );
}

// ---------------------------------------------------------------- Account (person menu)

interface Notif { id: number; kind: string; title: string; body: string | null; url: string | null; readAt: string | null; createdAt: string }

export function Notifications() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const q = useQuery({ queryKey: ['account', 'notifications'], queryFn: () => api<{ unread: number; rows: Notif[] }>('/api/account/notifications') });
  const read = useMutation({ mutationFn: (id: number) => post(`/api/account/notifications/${id}/read`), onSuccess: () => qc.invalidateQueries({ queryKey: ['account'] }) });
  const all = useMutation({ mutationFn: () => post('/api/account/notifications/read-all'), onSuccess: () => qc.invalidateQueries({ queryKey: ['account'] }) });
  return (
    <>
      <div className="page-head"><h1>Notifications</h1><button className="btn outline" onClick={() => all.mutate()}>Mark all read</button></div>
      <Panel>{!q.data ? <Skeleton rows={8} /> : q.data.rows.length === 0 ? <p className="muted">Nothing yet.</p> : (
        <table className="table"><tbody>{q.data.rows.map((n) => (
          <tr key={n.id} className={`clickable ${n.readAt ? '' : 'unread'}`} onClick={() => { if (!n.readAt) read.mutate(n.id); if (n.url) navigate(n.url); }}>
            <td>{n.title}<div className="muted small">{n.body}</div></td><td><Pill>{n.kind}</Pill></td><td>{dateTime(n.createdAt)}</td>
          </tr>
        ))}</tbody></table>
      )}</Panel>
    </>
  );
}

export function MyFiles() {
  const q = useQuery({ queryKey: ['account', 'files'], queryFn: () => api<{ id: number; title: string; status: string; rows: number; format: string; fileSize: number | null; createdAt: string; seenAt: string | null }[]>('/api/account/files'), refetchInterval: 10_000 });
  return (
    <>
      <h1>My Files</h1>
      <p className="muted">Every report you've run, from any app.</p>
      <Panel>{!q.data ? <Skeleton rows={8} /> : (
        <table className="table"><tbody>{q.data.map((r) => <tr key={r.id} className={r.seenAt ? '' : 'unread'}><td><Link to={`/walker/runs/${r.id}`}>{r.title}</Link></td><td><Pill tone={r.status === 'completed' ? 'ok' : r.status === 'failed' ? 'bad' : 'info'}>{r.status}</Pill></td><td className="num">{num(r.rows)} rows</td><td>{dateTime(r.createdAt)}</td><td>{r.status === 'completed' && <a href={`/api/walker/runs/${r.id}/download`}>Download {r.format}</a>}</td></tr>)}</tbody></table>
      )}</Panel>
    </>
  );
}

export function Settings() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['account', 'settings'], queryFn: () => api<{ kinds: string[]; prefs: Record<string, boolean>; name: string; email: string }>('/api/account/settings') });
  const [prefs, setPrefs] = useState<Record<string, boolean>>({});
  useEffect(() => { if (q.data) setPrefs(q.data.prefs); }, [q.data]);
  const save = useMutation({ mutationFn: () => put('/api/account/settings', { prefs }), onSuccess: () => qc.invalidateQueries({ queryKey: ['account'] }) });
  const [pw, setPw] = useState({ current: '', new: '' });
  const change = useMutation({ mutationFn: () => put<{ message: string }>('/api/account/password', pw), onSuccess: () => setTimeout(() => void signOut(), 1500) });
  if (!q.data) return <Skeleton rows={6} />;
  return (
    <>
      <h1>Settings</h1>
      <div className="grid-2">
        <Panel title="Notify me about">
          {q.data.kinds.map((k) => <label key={k} className="check inline"><input type="checkbox" checked={prefs[k] ?? true} onChange={(e) => setPrefs({ ...prefs, [k]: e.target.checked })} /> {k}</label>)}
          {save.isSuccess && <div className="alert ok">Saved.</div>}
          <button className="btn" onClick={() => save.mutate()}>Save</button>
        </Panel>
        <Panel title="Password">
          <form className="form-grid" style={{ gridTemplateColumns: '1fr' }} onSubmit={(e) => { e.preventDefault(); change.mutate(); }}>
            <label>Current password<input type="password" autoComplete="current-password" value={pw.current} onChange={(e) => setPw({ ...pw, current: e.target.value })} required /></label>
            <label>New password (12+ characters)<input type="password" autoComplete="new-password" minLength={12} value={pw.new} onChange={(e) => setPw({ ...pw, new: e.target.value })} required /></label>
            <ErrorBox error={change.error} />
            {change.data && <div className="alert ok">{change.data.message}</div>}
            <button className="btn">Change Password</button>
          </form>
        </Panel>
      </div>
      <p className="muted small">Signed in as {q.data.name} ({q.data.email}). <a href="#" onClick={(e) => { e.preventDefault(); void signOut(); }}>Sign out</a></p>
    </>
  );
}

void date;
