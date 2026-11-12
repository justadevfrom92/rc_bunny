import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, useLoad } from '../../api';
import { ago } from '../../Sidebar';
import { AccountNo, BarChart, EmptyRow, PageHead, PagerBar, Pill, Stat, age, toast, useRun } from '../../ui';
import { DateInput } from '../../fields';
import { when } from '../walker/shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const stateTones: Record<string, string> = { open: 'ok', scheduled: 'info', ended: '', closed: '' };
const day = (s: string) => new Date(s).toLocaleDateString('en-US');

/** Rodeo → Dashboard (ebunny's rodeo/dashboard). */
export function Dashboard() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any>('/api/rodeo/dashboard');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Marketing" sub="Campaigns, customer surveys and where sign-ups come from." actions={<Link className="btn cyan" to="/rodeo/campaigns/new">New Campaign</Link>} />
      <div className="grid-4 stat-row">
        <Stat label="Opted In to Marketing" value={data.optedIn.toLocaleString()} note={`of ${data.customers.toLocaleString()} customers`} />
        <Stat label="Campaigns Sent" value={data.campaigns} note={`${data.messages.toLocaleString()} messages`} />
        <Stat label="Email Open / Click Rate" value={`${data.openRate}% / ${data.clickRate}%`} />
        <Link className="stat" to="/rodeo/surveys"><span>Survey Responses</span><strong>{data.responses.toLocaleString()}</strong></Link>
      </div>
      <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>Surveys</h2><span className="muted">{data.responses30.toLocaleString()} responses in the last 30 days · <Link to="/rodeo/surveys">All Surveys</Link></span></div>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Survey</th><th>Status</th><th className="num">Last 30 Days</th><th className="num">All Responses</th><th className="num">Score (NPS)</th><th>Last Response</th></tr></thead>
          <tbody>
            {data.surveys.map((s: any) => (
              <tr key={s.id} className="click" onClick={() => navigate(`/rodeo/surveys/${s.id}/results`)}><td className="nowrap"><Link to={`/rodeo/surveys/${s.id}/results`}><b>#{s.id}</b></Link></td><td><b>{s.title}</b></td>
                <td><Pill tone={stateTones[s.state] ?? ''}>{s.stateLabel}</Pill></td><td className="num">{s.recent.toLocaleString()}</td><td className="num">{s.responses.toLocaleString()}</td>
                <td className="num">{s.score != null ? <b style={{ color: s.score >= 30 ? 'var(--ok)' : s.score < 0 ? 'var(--bad)' : undefined }}>{s.score > 0 ? '+' : ''}{s.score}</b> : '—'}</td>
                <td className="nowrap">{s.lastAt ? day(s.lastAt) : '—'}</td></tr>
            ))}
            {data.surveys.length === 0 && <EmptyRow cols={7}>No surveys yet.</EmptyRow>}
          </tbody>
        </table></div></div>
      <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>Latest Survey Responses</h2><Link className="btn sm ghost" to="/rodeo/surveys/responses">All Responses</Link></div>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>When</th><th>Survey</th><th>Customer</th><th className="num">Score</th></tr></thead>
          <tbody>
            {data.latestResponses.map((r: any) => (
              <tr key={r.id} className="click" onClick={() => navigate(`/rodeo/surveys/responses/${r.id}`)}><td className="nowrap"><Link to={`/rodeo/surveys/responses/${r.id}`}><b>#{r.id}</b></Link></td>
                <td className="nowrap">{when(r.createdAt)}</td><td>{r.survey}</td>
                <td>{r.customer ? <>{r.customer.name}<div className="help" onClick={(e) => e.stopPropagation()}><AccountNo id={r.customer.id} account={r.customer.account} /></div></> : <span className="muted">Anonymous</span>}</td>
                <td className="num">{r.score ?? '—'}</td></tr>
            ))}
            {data.latestResponses.length === 0 && <EmptyRow cols={5}>No responses yet.</EmptyRow>}
          </tbody>
        </table></div></div>
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Sign-ups by MSID</h2><span className="muted">last 90 days</span></div><div className="table-wrap"><table>
          <thead><tr><th>MSID</th><th>Channel</th><th className="num">Orders</th></tr></thead>
          <tbody>{data.byChannel.map((r: any, i: number) => <tr key={i}><td className="mono">{r.msid}</td><td>{r.name}</td><td className="num">{r.n}</td></tr>)}
            {data.byChannel.length === 0 && <EmptyRow cols={3}>No orders.</EmptyRow>}</tbody>
        </table></div></div>
        <div className="panel"><div className="panel-head"><h2>Recent Campaigns</h2><Link className="btn sm ghost" to="/rodeo/campaigns">All</Link></div><div className="table-wrap"><table>
          <tbody>{data.recent.map((c: any) => (
            <tr key={c.id} className="click" onClick={() => navigate(`/rodeo/campaigns/${c.id}`)}><td><Link to={`/rodeo/campaigns/${c.id}`}>{c.name}</Link></td><td>{c.channel}</td>
              <td><Pill tone={c.status === 'sent' ? 'ok' : 'info'}>{c.status.charAt(0).toUpperCase() + c.status.slice(1)}</Pill></td></tr>
          ))}{data.recent.length === 0 && <tr><td className="empty">No campaigns yet.</td></tr>}</tbody>
        </table></div></div>
      </div>
    </>
  );
}

/** Rodeo → Campaigns (ebunny's rodeo/campaigns): Scheduled, Drafts and Sent with results. */
export function Campaigns() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any>('/api/rodeo/campaigns');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const conv = data.sent.reduce((n: number, c: any) => n + c.converted, 0);
  const msgs = Math.max(1, data.sent.reduce((n: number, c: any) => n + c.messages, 0));
  const next = data.scheduled[0];
  return (
    <>
      <PageHead title="Campaigns" sub="Emails and texts to groups of customers, each with a goal so you can see what it achieved. Only customers who opted in to marketing are included."
        actions={<Link className="btn cyan" to="/rodeo/campaigns/new">New Campaign</Link>} />
      <div className="grid-4 stat-row">
        <Stat label="Scheduled" value={data.scheduled.length} note={next ? `next ${new Date(next.scheduledAt).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '')}` : 'nothing queued'} />
        <Stat label="Drafts" value={data.drafts.length} note="not sent yet" />
        <Stat label="Sent" value={data.sent.length} note={`${data.sent30.toLocaleString()} messages in 30 days`} />
        <Stat label="Conversions" value={conv.toLocaleString()} note={`${(Math.round((1000 * conv) / msgs) / 10).toString()}% of recipients reached the goal`} />
      </div>
      {([['Scheduled', data.scheduled], ['Drafts', data.drafts]] as const).map(([heading, list]) => (
        <div key={heading} className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>{heading} <span className="count-pill">{list.length}</span></h2></div>
          <div className="table-wrap"><table className="table">
            <thead><tr><th>Campaign</th><th>Goal</th><th>Channel</th><th className="num">Audience</th><th>{heading === 'Scheduled' ? 'Sends' : 'Last Edited'}</th></tr></thead>
            <tbody>
              {list.map((c: any) => (
                <tr key={c.id} className="click" onClick={() => navigate(`/rodeo/campaigns/${c.id}`)}><td><Link to={`/rodeo/campaigns/${c.id}`}><b>{c.name}</b></Link>{c.description && <><br /><span className="help">{c.description.length > 70 ? c.description.slice(0, 67) + '...' : c.description}</span></>}</td>
                  <td>{c.goalLabel}{c.product && <><br /><span className="help">{c.product}</span></>}</td><td>{c.channel === 'SMS' ? 'Text' : 'Email'}</td>
                  <td className="num">{c.audience.toLocaleString()}</td>
                  <td className="nowrap">{heading === 'Scheduled' ? new Date(c.scheduledAt).toLocaleString('en-US', { weekday: 'short', month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(/,/g, '') : `${ago(c.updatedAt)}`}</td></tr>
              ))}
              {list.length === 0 && <EmptyRow cols={5}>{heading === 'Scheduled' ? 'Nothing scheduled.' : 'No drafts.'}</EmptyRow>}
            </tbody>
          </table></div>
        </div>
      ))}
      <div className="panel"><div className="panel-head"><h2>Sent <span className="count-pill">{data.sent.length}</span></h2><span className="muted">conversions count for {data.window} days after sending</span></div>
        <div className="table-wrap"><table className="table">
          <thead><tr><th>Campaign</th><th>Goal</th><th>Sent</th><th className="num">Recipients</th><th className="num">Opened</th><th className="num">Clicked</th><th className="num">Converted</th></tr></thead>
          <tbody>
            {data.sent.map((c: any) => {
              const pct = (n: number) => (c.messages ? `${Math.round((100 * n) / c.messages)}%` : '—');
              return (
                <tr key={c.id} className="click" onClick={() => navigate(`/rodeo/campaigns/${c.id}`)}><td><Link to={`/rodeo/campaigns/${c.id}`}><b>{c.name}</b></Link><br /><span className="help">{c.channel === 'SMS' ? 'Text' : c.template ?? 'Email'}</span></td>
                  <td>{c.goalLabel}</td><td className="nowrap">{c.sentAt && day(c.sentAt)}</td><td className="num">{c.messages.toLocaleString()}</td>
                  <td className="num">{c.channel === 'Email' ? pct(c.opened) : '—'}</td><td className="num">{c.channel === 'Email' ? pct(c.clicked) : '—'}</td>
                  <td className="num">{c.goal === 'announcement' ? '—' : <><b>{c.converted.toLocaleString()}</b> <span className="help">{pct(c.converted)}</span></>}</td></tr>
              );
            })}
            {data.sent.length === 0 && <EmptyRow cols={7}>Nothing sent yet.</EmptyRow>}
          </tbody>
        </table></div>
      </div>
    </>
  );
}

/** A campaign: the builder while it's a draft or scheduled; results once sent. */
export function Campaign() {
  const { id } = useParams();
  const [sp] = useSearchParams();
  const { data, error, reload } = useLoad<any>(id ? `/api/rodeo/campaigns/${id}?page=${sp.get('page') ?? 1}` : '/api/rodeo/campaigns/new');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return data.campaign.status === 'sent' ? <Results data={data} /> : <Builder data={data} id={id} reload={reload} key={id ?? 'new'} />;
}

type Audience = { statuses?: string[]; type?: string; market_ids?: number[]; product?: string; without_product?: string; autopay?: string; balance?: string; contract_ends_within?: number | null };

function Builder({ data, id, reload }: { data: any; id?: string; reload: () => void }) {
  const navigate = useNavigate();
  const c = data.campaign, o = data.options;
  const [run, bar] = useRun(reload);
  const [f, setF] = useState(() => ({
    name: c.name ?? '', goal: c.goal ?? 'renewal', description: c.description ?? '', product: c.product ?? '', channel: c.channel ?? 'Email', emailTemplateId: c.emailTemplateId ?? null,
    message: c.message ?? '', msid: c.msid ?? '', promoCode: c.promoCode ?? '', capDays: c.capDays ?? 7, audience: (c.audience ?? {}) as Audience,
  }));
  const set = (k: string, v: unknown) => setF((x) => ({ ...x, [k]: v }));
  const aud = (k: keyof Audience, v: unknown) => setF((x) => ({ ...x, audience: { ...x.audience, [k]: v } }));
  const toggle = (k: 'statuses' | 'market_ids', v: any) => aud(k, ((f.audience[k] ?? []) as any[]).includes(v) ? (f.audience[k] as any[]).filter((x) => x !== v) : [...((f.audience[k] ?? []) as any[]), v]);
  // Live audience count (nothing is saved)
  const [live, setLive] = useState<any>({ count: data.count, excluded: data.excluded, sample: [] });
  const t = useRef(0);
  const count = useCallback(() => api.post<any>('/api/rodeo/campaigns/count', { channel: f.channel, capDays: f.capDays, audience: f.audience }).then(setLive, () => {}), [f.channel, f.capDays, f.audience]);
  useEffect(() => { clearTimeout(t.current); t.current = window.setTimeout(count, 250); }, [count]);
  const save = (e: React.FormEvent) => {
    e.preventDefault();
    const body = { ...f, emailTemplateId: f.channel === 'Email' ? f.emailTemplateId : null, capDays: Number(f.capDays), audience: { ...f.audience, contract_ends_within: f.audience.contract_ends_within ? Number(f.audience.contract_ends_within) : null } };
    run((id ? api.put<any>(`/api/rodeo/campaigns/${id}`, body) : api.post<any>('/api/rodeo/campaigns', body)).then((r) => { if (!id) navigate(`/rodeo/campaigns/${r.id}`); return r; }));
  };
  const scheduled = c.status === 'scheduled';
  const [sched, setSched] = useState({ date: new Date(Date.now() + 86400000).toLocaleDateString('en-CA'), time: '10:00' });
  const radio = (name: string, value: string, checked: boolean, onChange: () => void, label: React.ReactNode) => <label key={name + value}><input type="radio" name={name} value={value} checked={checked} onChange={onChange} /><span>{label}</span></label>;
  const at = c.scheduledAt && new Date(c.scheduledAt);
  return (
    <>
      <PageHead title={id ? c.name : 'New Campaign'}
        sub={scheduled ? `Scheduled to send ${at.toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric' })} at ${at.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}.` : id ? 'Draft. Check the audience and message, then send it or schedule it.' : 'Set the goal, who gets it and what they get. Save to see the preview.'}
        actions={id ? <button className="btn ghost" onClick={() => run(api.post<any>(`/api/rodeo/campaigns/${id}/duplicate`).then((r) => { navigate(`/rodeo/campaigns/${r.id}`); return r; }))}>Duplicate</button> : undefined} />
      {bar}
      <div className="banner-note" style={{ marginBottom: 14 }}>{f.channel === 'SMS' ? 'Twilio' : 'Email sending'} isn't connected yet, so sending logs each message on the account without delivering it (Sheriff → APIs).</div>
      <div className="camp-grid">
        <form id="camp-form" className="camp-steps" onSubmit={save}>
          <section className="panel"><div className="panel-head"><h2>1. Goal</h2><span className="muted">what counts as success</span></div><div className="panel-body">
            <div className="field"><label htmlFor="name">Name</label><input id="name" required maxLength={120} value={f.name} onChange={(e) => set('name', e.target.value)} placeholder="Fall renewal reminder" /></div>
            <div className="chips" role="radiogroup" aria-label="Goal">{o.goals.map((g: any) => radio('goal', g.key, f.goal === g.key, () => set('goal', g.key), <><b>{g.label}</b><small>{g.converts ? `Converted: ${g.converts}` : 'No conversion tracking'}</small></>))}</div>
            <div className="field goal-product" hidden={f.goal !== 'product'}><label>Product it promotes</label>
              <div className="chips small">{o.products.map((p: string) => radio('product', p, f.product === p, () => set('product', p), p))}</div></div>
            <div className="field"><label htmlFor="description">Notes</label><input id="description" maxLength={500} value={f.description} onChange={(e) => set('description', e.target.value)} placeholder="Why we're sending it, offer details…" /></div>
          </div></section>
          <section className="panel"><div className="panel-head"><h2>2. Audience</h2><span className="muted">opted-in customers only</span></div><div className="panel-body">
            <div className="field"><label>Account status</label><div className="chips small">{o.statuses.map((s: string) => <label key={s}><input type="checkbox" checked={(f.audience.statuses ?? []).includes(s)} onChange={() => toggle('statuses', s)} /><span>{s}</span></label>)}</div></div>
            <div className="field"><label>Customer type</label><div className="chips small">{[['', 'Any'], ['Residential', 'Residential'], ['Small Business', 'Small Business']].map(([k, l]) => radio('a-type', k, (f.audience.type ?? '') === k, () => aud('type', k), l))}</div></div>
            <div className="field"><label>Utility area</label><div className="chips small">{o.markets.map((m: any) => <label key={m.id}><input type="checkbox" checked={(f.audience.market_ids ?? []).includes(m.id)} onChange={() => toggle('market_ids', m.id)} /><span>{m.short}</span></label>)}</div><small className="help">None picked = every area.</small></div>
            <div className="grid-2">
              <div className="field"><label>AutoPay</label><div className="chips small">{[['', 'Any'], ['yes', 'On'], ['no', 'Off']].map(([k, l]) => radio('a-autopay', k, (f.audience.autopay ?? '') === k, () => aud('autopay', k), l))}</div></div>
              <div className="field"><label>Balance</label><div className="chips small">{[['', 'Any'], ['owes', 'Owes money'], ['paid', 'Paid up']].map(([k, l]) => radio('a-balance', k, (f.audience.balance ?? '') === k, () => aud('balance', k), l))}</div></div>
            </div>
            <details className="field" open={!!(f.audience.product || f.audience.without_product) || undefined}><summary>Products they have or don't have</summary>
              <div className="grid-2" style={{ marginTop: 8 }}>
                <div><label>Has</label><div className="chips small">{radio('a-product', '', !f.audience.product, () => aud('product', ''), 'Any')}{o.products.map((p: string) => radio('a-product', p, f.audience.product === p, () => aud('product', p), p))}</div></div>
                <div><label>Doesn't have</label><div className="chips small">{radio('a-without', '', !f.audience.without_product, () => aud('without_product', ''), '—')}{o.products.map((p: string) => radio('a-without', p, f.audience.without_product === p, () => aud('without_product', p), p))}</div></div>
              </div></details>
            <div className="grid-2">
              <div className="field"><label htmlFor="a-ends">Contract ends within (days)</label><input id="a-ends" type="number" min="1" max="365" value={f.audience.contract_ends_within ?? ''} onChange={(e) => aud('contract_ends_within', e.target.value ? Number(e.target.value) : null)} placeholder="Any" /></div>
              <div className="field"><label htmlFor="cap_days">Skip anyone who got a campaign in the last (days)</label><input id="cap_days" type="number" min="0" max="90" value={f.capDays} onChange={(e) => set('capDays', e.target.value)} /><small className="help">0 = no limit</small></div>
            </div>
          </div></section>
          <section className="panel"><div className="panel-head"><h2>3. Message</h2></div><div className="panel-body">
            <div className="chips" role="radiogroup" aria-label="Channel">{[['Email', 'Email'], ['SMS', 'Text message']].map(([k, l]) => radio('channel', k, f.channel === k, () => set('channel', k), <b>{l}</b>))}</div>
            <div className="ch-email field" hidden={f.channel !== 'Email'}><label>Email template</label>
              <div className="table-wrap pick-list"><table className="table" data-page="0"><tbody>{o.templates.map((t: any) => (
                <tr key={t.id}><td style={{ width: 32 }}><input type="radio" id={`t${t.id}`} name="email_template_id" checked={f.emailTemplateId === t.id} onChange={() => set('emailTemplateId', t.id)} /></td>
                  <td><label htmlFor={`t${t.id}`}><b>{t.name}</b></label><br /><span className="help">{t.subject}</span></td><td className="help">{t.category}</td></tr>
              ))}</tbody></table></div></div>
            <div className="ch-sms field" hidden={f.channel !== 'SMS'}><label htmlFor="message">Text message</label><textarea id="message" maxLength={320} rows={3} placeholder="Hi {{first_name}}, …" value={f.message} onChange={(e) => set('message', e.target.value)} />
              <small className="help"><span id="sms-len">{f.message.length}</span>/320 characters · <span id="sms-seg">{Math.max(1, Math.ceil(f.message.length / 160))}</span> text(s) · use {'{{first_name}}'} and {'{{promo_code}}'} · "Reply STOP to opt out" is added by Twilio</small></div>
          </div></section>
          <section className="panel"><div className="panel-head"><h2>4. Tracking</h2><span className="muted">so sign-ups from it are counted</span></div><div className="panel-body grid-2">
            <div className="field"><label>MSID (sales channel)</label><div className="chips small">{radio('msid', '', !f.msid, () => set('msid', ''), 'None')}{o.channels.map((ch: any) => radio('msid', ch.msid, f.msid === ch.msid, () => set('msid', ch.msid), `${ch.msid} · ${ch.name}`))}</div></div>
            <div className="field"><label>Promo code</label><div className="chips small">{radio('promo', '', !f.promoCode, () => set('promoCode', ''), 'None')}{o.promos.map((p: any) => radio('promo', p.code, f.promoCode === p.code, () => set('promoCode', p.code), `${p.code} · $${Math.round(p.credit)}`))}</div></div>
          </div></section>
          <div className="actions"><button className="btn cyan">{id ? 'Save Changes' : 'Save Draft'}</button><Link className="btn ghost" to="/rodeo/campaigns">Back to Campaigns</Link></div>
        </form>
        <aside className="camp-side">
          <section className="panel"><div className="panel-head"><h2>Audience</h2></div><div className="panel-body">
            <p className="camp-count"><strong id="aud-count">{live.count != null ? live.count.toLocaleString() : '—'}</strong> customers will get it</p>
            <dl className="kv camp-excl"><dt>Not opted in</dt><dd id="x-opt">{live.excluded?.notOptedIn ?? '—'}</dd><dt>Suppressed (email)</dt><dd id="x-sup">{live.excluded?.suppressed ?? '—'}</dd><dt>Contacted recently</dt><dd id="x-rec">{live.excluded?.recent ?? '—'}</dd></dl>
            <p className="help" style={{ margin: '6px 0 4px' }}>For example:</p>
            <ul className="camp-sample" id="aud-sample">{live.sample?.map((s: string) => <li key={s}>{s}</li>)}{live.sample && live.sample.length === 0 && <li className="muted">No one matches.</li>}</ul>
            <p className="help" style={{ margin: 0 }}>Suppressed and recently contacted customers are skipped when it sends.</p>
          </div></section>
          {id && (
            <>
              <section className="panel"><div className="panel-head"><h2>Preview</h2><span className="muted">{data.sample ? `as ${data.sample}` : 'no one matches'}</span></div><div className="panel-body">
                {!data.preview ? <p className="muted">Pick an audience that matches someone to see the preview.</p>
                  : c.channel === 'Email' ? <><p className="help" style={{ marginTop: 0 }}><b>Subject:</b> {data.preview.subject}</p><iframe className="camp-prev" sandbox="" title="Email preview" srcDoc={data.preview.html} /></>
                    : <p className="camp-sms">{data.preview.text}</p>}
              </div></section>
              <section className="panel"><div className="panel-head"><h2>Send</h2></div><div className="panel-body" style={{ display: 'grid', gap: 12 }}>
                {scheduled ? (
                  <>
                    <p style={{ margin: 0 }}>Sends <b>{at.toLocaleString('en-US', { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}</b> (in {age(new Date().toISOString(), c.scheduledAt)}).</p>
                    <button className="btn ghost" onClick={() => run(api.post(`/api/rodeo/campaigns/${id}/unschedule`))}>Cancel Schedule</button>
                  </>
                ) : (
                  <form className="camp-sched" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/rodeo/campaigns/${id}/schedule`, sched)); }}>
                    <div className="field"><label htmlFor="s-date">Date</label><DateInput id="s-date" value={sched.date} onChange={(v: string) => setSched({ ...sched, date: v })} /></div>
                    <div className="field"><label htmlFor="s-time">Time</label><input id="s-time" type="time" value={sched.time} onChange={(e) => setSched({ ...sched, time: e.target.value })} /></div>
                    <button className="btn">Schedule</button>
                  </form>
                )}
                {data.count ? <button className="btn cyan" style={{ width: '100%' }} onClick={() => confirm(`Send this campaign now?\nIt goes to up to ${data.count.toLocaleString()} customers right away.`) && run(api.post(`/api/rodeo/campaigns/${id}/send`))}>Send Now to {data.count.toLocaleString()}</button>
                  : <p className="muted" style={{ margin: 0 }}>No one matches this audience yet.</p>}
                <p className="help" style={{ margin: 0 }}>Save your changes first: sending uses the saved version.</p>
              </div></section>
            </>
          )}
        </aside>
      </div>
    </>
  );
}

function Results({ data }: { data: any }) {
  const navigate = useNavigate();
  const c = data.campaign, r = data.results, a = c.audience ?? {};
  const base = Math.max(1, r.sent);
  const steps = ([['Sent', r.sent], ['Delivered', r.delivered], ['Opened', r.opened], ['Clicked', r.clicked], ['Converted', c.goal === 'announcement' ? null : r.converted]] as [string, number | null][]).filter(([, v]) => v != null) as [string, number][];
  const pct = (n: number, d = 0) => `${(Math.round((10 ** d * 100 * n) / base) / 10 ** d).toString()}%`;
  const audience = [a.statuses?.join(', ') || null, a.type, a.autopay ? `AutoPay ${a.autopay === 'yes' ? 'on' : 'off'}` : null, a.balance ? (a.balance === 'owes' ? 'owes money' : 'paid up') : null,
    data.markets.length ? data.markets.join(', ') : null, a.product ? `has ${a.product}` : null, a.without_product ? `no ${a.without_product}` : null,
    a.contract_ends_within ? `contract ends within ${a.contract_ends_within} days` : null].filter(Boolean).join(' · ');
  const sentAt = new Date(c.sentAt).toLocaleString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(' at ', ' ');
  return (
    <>
      <PageHead title={c.name} sub={`Sent ${sentAt} · ${c.goalLabel} · ${c.channel === 'SMS' ? 'Text message' : `Email: ${c.template ?? '—'}`}`}
        actions={<><Link className="btn ghost" to="/rodeo/campaigns">All Campaigns</Link><Link className="btn ghost" to={`/rodeo/campaigns/${c.id}/heatmap`}>Click Heat Map</Link>
          <button className="btn cyan" onClick={() => api.post<any>(`/api/rodeo/campaigns/${c.id}/duplicate`).then((x) => { toast(x.message); navigate(`/rodeo/campaigns/${x.id}`); })}>Duplicate</button></>} />
      <div className="grid-4 stat-row">
        <Stat label="Recipients" value={r.sent.toLocaleString()} note={`${c.skipped.toLocaleString()} skipped (suppressed or contacted recently)`} />
        <Stat label="Delivered" value={pct(r.delivered)} note={`${r.delivered.toLocaleString()} of ${r.sent.toLocaleString()}`} />
        <Stat label={c.channel === 'Email' ? 'Opened' : 'Channel'} value={c.channel === 'Email' ? pct(r.opened) : 'Text'} note={c.channel === 'Email' ? `${r.clicked.toLocaleString()} clicked (${pct(r.clicked)})` : 'texts have no open tracking'} />
        <Stat label="Converted" value={data.converts ? pct(r.converted, 1) : '—'} note={data.converts ? `${r.converted.toLocaleString()} ${data.converts} within ${data.window} days` : 'announcements have no goal to count'} />
      </div>
      <div className="grid-2" style={{ alignItems: 'start', marginBottom: 20 }}>
        <div className="panel"><div className="panel-head"><h2>Funnel</h2><span className="muted">share of everyone it was sent to</span></div><div className="panel-body">
          <div className="sv-bars">{steps.map(([label, n]) => (
            <div key={label} className="sv-bar" tabIndex={0} data-tip={`${label}: ${n.toLocaleString()} (${pct(n, 1)})`}><span>{label}</span><span className="track"><span className="fill" style={{ width: pct(n) }} /></span><span className="n">{n.toLocaleString()}</span></div>
          ))}</div>
        </div></div>
        <div className="panel"><div className="panel-head"><h2>Details</h2></div><div className="panel-body"><dl className="kv">
          <dt>Goal</dt><dd>{c.goalLabel}{c.product && `: ${c.product}`}</dd>
          {c.description && <><dt>Notes</dt><dd>{c.description}</dd></>}
          <dt>Audience</dt><dd>{audience || 'Every opted-in customer'}</dd>
          <dt>Tracking</dt><dd>{c.msid ? `MSID ${c.msid}` : 'No MSID'} · {c.promoCode ? `promo ${c.promoCode}` : 'no promo code'}</dd>
          <dt>Contact limit</dt><dd>{c.capDays ? `skipped anyone contacted in the previous ${c.capDays} days` : 'none'}</dd>
        </dl></div></div>
      </div>
      {data.daily.length > 0 && (
        <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>Opens by Day</h2></div><div className="panel-body">
          <BarChart bars={data.daily.map((d: any) => { const t = new Date(d.day); return { label: `${t.getMonth() + 1}/${t.getDate()}`, value: d.n, tip: t.toLocaleDateString('en-US', { weekday: 'short', month: 'numeric', day: 'numeric' }).replace(',', '') }; })} unit="opens" height={150} />
        </div></div>
      )}
      <div className="panel"><div className="panel-head"><h2>Recipients</h2><span className="muted">{data.total.toLocaleString()}</span></div>
        <div className="table-wrap"><table className="table" data-page="0">
          <thead><tr><th>Account</th><th>Status</th>{c.channel === 'Email' && <><th>Opened</th><th>Clicked</th></>}<th>Result</th></tr></thead>
          <tbody>{data.recipients.map((m: any) => {
            const t = (s: string | null) => (s ? new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '') : '—');
            return (
              <tr key={m.id}><td>{m.customer && <><AccountNo id={m.customer.id} account={m.customer.account} /><br /><span className="help">{m.customer.name}</span></>}</td>
                <td>{m.status.charAt(0).toUpperCase() + m.status.slice(1)}</td>
                {c.channel === 'Email' && <><td>{t(m.openedAt)}</td><td>{t(m.clickedAt)}</td></>}
                <td>{m.converted ? <Pill tone="ok">Converted</Pill> : <span className="muted">—</span>}</td></tr>
            );
          })}</tbody>
        </table></div>
        <PagerBar page={data.page} total={data.total} size={50} onPage={(p) => navigate(`/rodeo/campaigns/${c.id}?page=${p}`)} />
      </div>
    </>
  );
}

/** Click heat map (ebunny's rodeo/heatmap): the email as sent, each link shaded by how often it was clicked. */
export function Heatmap({ kind }: { kind: 'campaigns' | 'templates' }) {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/rodeo/${kind}/${id}/heatmap`);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!data || !box.current) return;
    const counts = Object.fromEntries(data.links.map((l: any) => [l.key, l.n]));
    const total = Math.max(1, data.total);
    box.current.querySelectorAll<HTMLAnchorElement>('a[data-link]').forEach((a) => {
      const n = counts[a.dataset.link!] || 0;
      a.classList.add('hm-link');
      a.style.setProperty('--h', (n / data.max).toFixed(2));
      a.setAttribute('data-tip', `${n} ${n === 1 ? 'click' : 'clicks'} · ${Math.round((100 * n) / total)}%`);
      a.addEventListener('click', (e) => e.preventDefault());
      const b = document.createElement('span'); b.className = 'hm-count'; b.textContent = String(n); a.appendChild(b);
    });
  }, [data]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const total = data.total;
  const linksWithClicks = data.links.filter((l: any) => l.n > 0).length;
  return (
    <>
      <PageHead title="Click Heat Map" sub={`${data.name} · ${data.sent.toLocaleString()} sent · ${data.opened.toLocaleString()} opened · ${data.clickers.toLocaleString()} clicked`}
        actions={<Link className="btn ghost btn-back" to={data.back}>Back</Link>} />
      <div className="grid-4 stat-row">
        <Stat label="Clicks" value={total.toLocaleString()} note={`on ${linksWithClicks} ${linksWithClicks === 1 ? 'link' : 'links'}`} />
        <Stat label="Click Rate" value={`${data.sent ? Math.round((1000 * data.clickers) / data.sent) / 10 : 0}%`} note="of emails sent" />
        <Stat label="Click-to-Open" value={`${data.opened ? Math.round((1000 * data.clickers) / data.opened) / 10 : 0}%`} note="of emails opened" />
        <div className="stat"><span>Devices</span><strong style={{ fontSize: '1rem' }}>{data.devices.length ? data.devices.map((d: any) => `${d.device} ${total ? Math.round((100 * d.n) / total) : 0}%`).join(' · ') : '—'}</strong></div>
      </div>
      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0,3fr) minmax(0,2fr)', alignItems: 'start' }}>
        <div className="panel"><div className="panel-head"><h2>Where People Click</h2><span className="hm-legend"><i style={{ ['--h' as string]: 0.15 }} /> few <i style={{ ['--h' as string]: 0.55 }} /><i style={{ ['--h' as string]: 1 }} /> most</span></div>
          <div className="panel-body hm-wrap" id="heatmap" ref={box} dangerouslySetInnerHTML={{ __html: data.html }} />
        </div>
        <div className="panel"><div className="panel-head"><h2>Clicks by Link</h2></div><div className="table-wrap"><table data-page="0">
          <thead><tr><th>Link</th><th className="num">Clicks</th><th className="num">People</th><th className="num">%</th></tr></thead>
          <tbody>{data.links.map((l: any) => {
            const p = total ? Math.round((100 * l.n) / total) : 0;
            return (
              <tr key={l.key} data-hm-row={l.key}><td className="wrap" style={{ minWidth: 0 }}><b>{l.label}</b>{l.url && <div className="help mono">{l.url}</div>}</td><td className="num">{l.n.toLocaleString()}</td>
                <td className="num">{l.people.toLocaleString()}</td><td className="num"><span className="hm-bar"><span style={{ width: `${p}%` }} /></span> {p}%</td></tr>
            );
          })}</tbody>
        </table></div></div>
      </div>
    </>
  );
}
