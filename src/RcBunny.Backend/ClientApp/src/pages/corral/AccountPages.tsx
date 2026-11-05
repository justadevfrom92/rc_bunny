import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, money, useLoad } from '../../api';
import { EmptyRow, PageHead, Panel, Pill, RecordLink, ResolutionPill, duration, secondsTime, shortTime, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */
type Any = any;

const balanceTone: Record<string, string> = { Applied: 'ok', applied: 'ok', Success: 'ok', Pending: 'warn', pending: 'warn', Rejected: 'bad', rejected: 'bad', Failed: 'bad', Reversed: 'warn' };
const sentTone: Record<string, string> = { sent: 'ok', queued: 'info', received: 'ok', logged: 'info' };
const moneyParens = (v: number) => (v < 0 ? `(${money(-v)})` : money(v));
const Loading = () => <div className="empty">Loading…</div>;

/** One account action (ebunny's corral/account/action): its fields, or a confirmation when there's nothing to fill in. */
export function ActionForm() {
  const { id, key } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<Any>(`/api/customers/${id}/actions/${key}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  const { action, customer: c } = data;
  const danger = ['delete-order', 'disconnect', 'cancel-order'].includes(action.key);

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const values = Object.fromEntries(new FormData(e.currentTarget).entries());
    run(api.post<{ message: string; goTo?: string }>(`/api/customers/${c.id}/actions/${key}`, values).then((r) => {
      navigate(r.goTo ?? `/corral/customers/${c.id}#actions`, { state: { message: r.message } });
      return r;
    }));
  };
  const autopay = data.methods.find((m: Any) => m.autopay)?.id;

  return (
    <>
      <PageHead title={action.label} sub={<>{c.name} · Account <span className="mono">{c.account}</span> · Balance {money(c.balance)}</>}
        actions={<Link className="btn ghost" to={`/corral/customers/${c.id}#actions`}>Back to account</Link>} />
      {bar}
      <div className="panel"><div className="panel-body">
        <form className="form-grid" onSubmit={submit}>
          {action.fields.map((f: Any) => {
            const fid = `f-${f.name}`;
            return [
              <label key={f.name + 'l'} htmlFor={fid}>{f.label}</label>,
              f.type === 'money' ? <input key={f.name} id={fid} name={f.name} type="number" step="0.01" min="0.01" required defaultValue={f.name === 'amount' && action.key === 'make-payment' && c.balance > 0 ? c.balance.toFixed(2) : ''} />
                : f.type === 'number' ? <input key={f.name} id={fid} name={f.name} type="number" step="1" min="1" required />
                : f.type === 'date' ? <input key={f.name} id={fid} name={f.name} type="date" required defaultValue={new Date().toISOString().slice(0, 10)} />
                : f.type === 'textarea' ? <textarea key={f.name} id={fid} name={f.name} maxLength={2000} required style={{ fontFamily: 'inherit', minHeight: 140 }} />
                : f.type === 'method' ? <select key={f.name} id={fid} name={f.name} required defaultValue={autopay ?? ''}><option value="">-- Payment method --</option>
                    {data.methods.map((m: Any) => <option key={m.id} value={m.id}>{m.nickname ?? m.type} ending {m.last4}{m.autopay ? ' (AutoPay)' : ''}</option>)}</select>
                : f.type === 'payment' ? <select key={f.name} id={fid} name={f.name} required><option value="">-- Payment --</option>
                    {data.payments.map((p: Any) => <option key={p.id} value={p.id}>{date(p.paidOn)} · {money(p.amount)} · {p.method} · {p.status}</option>)}</select>
                : f.type === 'template' ? <select key={f.name} id={fid} name={f.name} required><option value="">-- Template --</option>{data.templates.map((t: string) => <option key={t}>{t}</option>)}</select>
                : <input key={f.name} id={fid} name={f.name} maxLength={200} required />,
            ];
          })}
          {action.fields.length === 0 && <p style={{ gridColumn: '1/-1' }}>Run <b>{action.label}</b> on {c.name}'s account? A note is added to the account with your name.</p>}
          {action.key === 'make-payment' && data.methods.length === 0 && <p className="help" style={{ gridColumn: '1/-1' }}>No payment methods on file. Add one through MyAccount or the customer's next payment.</p>}
          <div className="actions" style={{ gridColumn: '1/-1' }}>
            <button className={`btn${danger ? ' red' : ''}`}>{action.fields.length ? 'Submit' : `Yes, ${action.label.includes(' - ') ? action.label.split(' - ').slice(1).join(' - ') : action.label}`}</button>
            <Link className="btn ghost" to={`/corral/customers/${c.id}#actions`}>Cancel</Link>
          </div>
        </form>
      </div></div>
    </>
  );
}

/** The Log button on the Balances, Actions and Flags panels (ebunny's corral/customers/panel-log). */
export function PanelLog() {
  const { id, panel } = useParams();
  const { data, error } = useLoad<Any>(`/api/customers/${id}/panel-log/${panel}`);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  const c = data.customer;
  const title = { balances: 'Balance Log', actions: 'Action Log', flags: 'Flag Log' }[panel as string];
  const back = panel === 'balances' ? 'balances' : panel === 'flags' ? 'flags' : 'actions';
  const n = data.rows.length;
  return (
    <>
      <PageHead title={title} sub={<>{c.name} · Account <Link className="mono" to={`/corral/customers/${c.id}`}>{c.account}</Link></>}
        actions={<Link className="btn ghost btn-back" to={`/corral/customers/${c.id}#${back}`}>Back to Account</Link>} />
      <div className="chips small" style={{ marginBottom: 14 }}>
        {Object.entries(data.panels).map(([k, l]) => <Link key={k} className={`chip-link${k === panel ? ' on' : ''}`} to={`/corral/customers/${c.id}/log/${k}`}>{l as string}</Link>)}
      </div>
      <Panel title={`${n.toLocaleString()} ${n === 1 ? 'entry' : 'entries'}`}>
        <div className="table-wrap"><table>
          {panel === 'balances' && <>
            <thead><tr><th>ID</th><th>When</th><th>Type</th><th className="num">Amount</th><th>Description</th><th>Status</th><th>By</th></tr></thead>
            <tbody>
              {data.rows.map((r: Any) => <tr key={r.record + r.id} className="click" onClick={() => navigate(`/records/${r.record}/${r.id}`)}>
                <td className="nowrap"><RecordLink type={r.record} id={r.id} /></td><td className="nowrap">{dateTime(r.at)}</td><td>{r.type}</td><td className="num">{moneyParens(r.amount)}</td>
                <td className="wrap">{r.description}</td><td><span className={`pill ${balanceTone[r.status] ?? ''}`}>{r.status}</span></td><td>{r.by ?? '—'}</td></tr>)}
              {n === 0 && <EmptyRow cols={7}>No credits, debits or payments yet.</EmptyRow>}
            </tbody>
          </>}
          {panel === 'actions' && <>
            <thead><tr><th>ID</th><th>When</th><th>Action</th><th>Result</th><th>By</th></tr></thead>
            <tbody>
              {data.rows.map((x: Any) => <tr key={x.id} className="click" onClick={() => navigate(`/records/note/${x.id}`)}>
                <td className="nowrap"><RecordLink type="note" id={x.id} /></td><td className="nowrap">{dateTime(x.createdAt)}</td><td><b>{x.action}</b></td>
                <td className="wrap">{String(x.body).startsWith(`${x.action}: `) ? x.body.slice(x.action.length + 2) : x.body}</td><td>{x.author ?? '—'}</td></tr>)}
              {n === 0 && <EmptyRow cols={5}>No actions run on this account yet.</EmptyRow>}
            </tbody>
          </>}
          {panel === 'flags' && <>
            <thead><tr><th>ID</th><th>Flag</th><th>Added</th><th>By</th><th>Removed</th><th>Status</th></tr></thead>
            <tbody>
              {data.rows.map((f: Any) => <tr key={f.id} className="click" onClick={() => navigate(`/records/customer_flag/${f.id}`)}>
                <td className="nowrap"><RecordLink type="customer_flag" id={f.id} /></td><td><b>{f.flag}</b></td><td className="nowrap">{dateTime(f.createdAt)}</td><td>{f.author ?? 'System'}</td>
                <td className="nowrap">{f.removedAt ? dateTime(f.removedAt) : '—'}</td><td><span className={`pill ${f.removedAt ? '' : 'warn'}`}>{f.removedAt ? 'Removed' : 'On the account'}</span></td></tr>)}
              {n === 0 && <EmptyRow cols={6}>No flags yet.</EmptyRow>}
            </tbody>
          </>}
        </table></div>
      </Panel>
    </>
  );
}

/** Contact Log (ebunny's corral/account/contact-log): marketing opt-in, every email and text, and the calls. */
export function ContactLog() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<Any>(`/api/customers/${id}/contact-log`);
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  const c = data.customer;
  const smsOrRecord = (l: Any) => (l.channel === 'SMS' ? `/corral/sms/${l.id}` : `/records/contact_log/${l.id}`);
  return (
    <>
      <PageHead title="Contact Log" sub={<>{c.name} · Account <span className="mono">{c.account}</span> · {c.email} · {c.phone}</>}
        actions={<><Link className="btn ghost" to={`/corral/customers/${c.id}#general`}>Back to account</Link><Link className="btn cyan" to={`/corral/contacts/new?account=${c.account}`}>New Contact</Link></>} />
      {bar}
      <Panel title="Marketing Emails" right={<button className={`btn sm${c.marketingOptIn ? ' ghost' : ''}`} onClick={() => run(api.post(`/api/customers/${c.id}/contact-log/marketing`))}>{c.marketingOptIn ? 'Opt Out' : 'Opt In'}</button>}>
        <div className="panel-body">Customer is opted <b>{c.marketingOptIn ? 'IN' : 'OUT'}</b> of marketing emails. Billing and service messages are always sent.</div>
      </Panel>
      <Panel title={`Emails & Texts (${data.logs.length})`} right={<span className="actions">
        <Link className="btn sm ghost" to={`/corral/customers/${c.id}/actions/send-email`}>Send Email</Link><Link className="btn sm ghost" to={`/corral/customers/${c.id}/actions/send-text`}>Send Text</Link></span>}>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Created</th><th>Template</th><th>Status</th><th>Sent</th><th>Opened</th><th>Clicked</th><th>Dropped</th><th>Resolution</th></tr></thead>
          <tbody>
            {data.logs.map((l: Any) => (
              <tr key={l.id} className="click" onClick={() => navigate(smsOrRecord(l))}>
                <td className="nowrap"><Link to={smsOrRecord(l)}><b>#{l.id}</b></Link></td>
                <td>{dateTime(l.createdAt)}<br /><span className="help">{l.channel === 'SMS' ? `Text ${l.direction === 'in' ? 'from customer' : 'to customer'}` : 'Email'}</span></td>
                <td className="wrap">{l.template}{l.body && <><br /><span className="help">{l.body.length > 120 ? l.body.slice(0, 120) + '...' : l.body}</span></>}</td>
                <td><span className={`pill ${sentTone[l.status] ?? 'bad'}`}>{l.status}</span></td>
                <td className="nowrap">{l.sentAt ? shortTime(l.sentAt) : '—'}</td><td className="nowrap">—</td><td className="nowrap">—</td><td className="nowrap">—</td>
                <td><ResolutionPill value={l.resolution} /></td>
              </tr>
            ))}
            {data.logs.length === 0 && <EmptyRow cols={9}>No emails or texts sent.</EmptyRow>}
          </tbody>
        </table></div>
      </Panel>
      <Panel title={`Phone Calls (${data.calls.length})`}>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Started</th><th>Direction</th><th>Phone</th><th>Agent</th><th className="num">Duration</th><th>Disposition</th><th>Resolution</th></tr></thead>
          <tbody>
            {data.calls.map((p: Any) => (
              <tr key={p.id} className="click" onClick={() => navigate(`/corral/calls/${p.id}`)}>
                <td className="nowrap"><Link to={`/corral/calls/${p.id}`}><b>#{p.id}</b></Link></td><td>{dateTime(p.startedAt)}</td><td>{p.direction?.[0]?.toUpperCase()}{p.direction?.slice(1)}</td>
                <td className="mono">{p.phone}</td><td>{p.answeredBy ?? '—'}</td><td className="num">{duration(p.durationSec)}</td><td>{p.issue ?? '—'}</td><td><ResolutionPill value={p.resolution} /></td>
              </tr>
            ))}
            {data.calls.length === 0 && <EmptyRow cols={8}>No phone calls.</EmptyRow>}
          </tbody>
        </table></div>
      </Panel>
    </>
  );
}

/** API Log (ebunny's corral/account/api-log): calls made to outside systems for this account. */
export function ApiLog() {
  const { id } = useParams();
  const { data, error } = useLoad<Any>(`/api/customers/${id}/api-log`);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  const c = data.customer;
  return (
    <>
      <PageHead title="API Log" sub={<>{c.name} · Account <span className="mono">{c.account}</span> · calls made to outside systems for this account. Request bodies and keys are never stored here.</>}
        actions={<Link className="btn ghost" to={`/corral/customers/${c.id}#tech`}>Back to account</Link>} />
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>Time</th><th>API</th><th>Action</th><th>Status</th><th className="num">Response</th></tr></thead>
        <tbody>
          {data.logs.map((l: Any) => (
            <tr key={l.id} className="click" onClick={() => navigate(`/records/api_log/${l.id}`)}>
              <td className="nowrap"><RecordLink type="api_log" id={l.id} /></td><td>{secondsTime(l.createdAt)}</td><td>{l.api}</td><td className="mono">{l.action}</td>
              <td><span className={`pill ${l.status === 'ok' || String(l.status).startsWith('2') ? 'ok' : 'bad'}`}>{l.status}</span></td>
              <td className="num">{l.milliseconds != null ? `${l.milliseconds.toLocaleString()} ms` : '—'}</td>
            </tr>
          ))}
          {data.logs.length === 0 && <EmptyRow cols={6}>No API calls logged for this account.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** Spend Stars (ebunny's corral/account/stars): the reward offers and the star history. */
export function Stars() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<Any>(`/api/customers/${id}/stars`);
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  return (
    <>
      <PageHead title="Spend Stars" sub={<>{data.name} · Account <span className="mono">{data.account}</span> · <b>{data.stars.toLocaleString()}</b> stars available</>}
        actions={<Link className="btn ghost" to={`/corral/customers/${data.id}#stars`}>Back to account</Link>} />
      {bar}
      <Panel title="Rangler Rewards"><div className="panel-body">
        <div className="offers">
          {data.offers.map((o: Any) => {
            const locked = o.stars > data.stars;
            return (
              <form key={o.id} className={`offer${locked ? ' locked' : ''}`} onSubmit={(e) => {
                e.preventDefault();
                if (confirm(`Redeem ${o.name}?\n${o.stars} stars will be taken from the account.`)) run(api.post(`/api/customers/${data.id}/stars`, { offerId: o.id }), `Redeemed ${o.name}.`);
              }}>
                <b className="stars">{o.stars ? `${o.stars.toLocaleString()} ★` : 'Free'}</b>
                <b>{o.name}</b>
                <span className="help">{o.description}</span>
                <button className="btn sm" disabled={locked}>{locked ? `Needs ${(o.stars - data.stars).toLocaleString()} more` : 'Redeem'}</button>
              </form>
            );
          })}
        </div>
      </div></Panel>
      <Panel title="Star History">
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Date</th><th>Reason</th><th className="num">Stars</th></tr></thead>
          <tbody>
            {data.history.map((s: Any) => (
              <tr key={s.record + s.id} className={`click${s.drawing ? ' drawing-row' : ''}`} onClick={() => navigate(`/records/${s.record}/${s.id}`)}>
                <td className="nowrap"><RecordLink type={s.record} id={s.id} /></td><td>{dateTime(s.at)}</td>
                <td>{s.reason}{s.drawing && <> <span className="pill info">Drawing</span></>}</td>
                <td className="num">{s.drawing ? '—' : `${s.stars >= 0 ? '+' : ''}${s.stars}`}</td>
              </tr>
            ))}
            {data.history.length === 0 && <EmptyRow cols={4}>No stars earned yet.</EmptyRow>}
          </tbody>
        </table></div>
      </Panel>
    </>
  );
}

/** ebunny's 'F jS, Y @ g:i:s A': October 26th, 2026 @ 9:04:05 PM */
function ticketTime(s: string) {
  const d = new Date(s), day = d.getDate();
  const th = day % 10 === 1 && day !== 11 ? 'st' : day % 10 === 2 && day !== 12 ? 'nd' : day % 10 === 3 && day !== 13 ? 'rd' : 'th';
  return `${d.toLocaleString('en-US', { month: 'long' })} ${day}${th}, ${d.getFullYear()} @ ${d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', second: '2-digit' })}`;
}
const stamp = (s: string) => s.replace('T', ' ').slice(0, 19);

/** The account ticket, one of its logs, or a bill's ticket (ebunny's history/ticket): by item type, or one timeline. */
export function History() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const log = params.get('log'), bill = params.get('bill');
  const [view, setView] = useState<'grouped' | 'timeline'>('grouped');
  const { data, error } = useLoad<Any>(`/api/customers/${id}/history?${log ? `log=${log}` : ''}${bill ? `bill=${bill}` : ''}`);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  const c = data.customer;
  const items: Any[] = data.groups.flatMap((g: Any) => g.items);
  const timeline = [...items].sort((x, y) => y.createdAt.localeCompare(x.createdAt));
  const days = timeline.reduce((acc: Record<string, Any[]>, e) => {
    const day = new Date(e.createdAt).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
    (acc[day] ??= []).push(e);
    return acc;
  }, {});
  const current = (data.queues ?? []).filter((q: Any) => !q.exitedAt);
  const open = (h: Any) => navigate(`/records/history_entry/${h.id}`);

  return (
    <>
      <PageHead title={data.title} sub={<>created: {ticketTime(c.createdAt)} · id <span className="mono">{c.ticket}</span> · {c.name}</>}
        actions={<><Link className="btn ghost" to={`/corral/customers/${c.id}`}>Back to account</Link>
          {(log || bill) && <Link className="btn ghost" to={`/corral/customers/${c.id}/history`}>Account ticket</Link>}</>} />

      <div className="chips small" role="tablist" aria-label="Ticket view">
        <button type="button" className={`chip-link${view === 'grouped' ? ' on' : ''}`} role="tab" aria-selected={view === 'grouped'} onClick={() => setView('grouped')}>By Item Type</button>
        <button type="button" className={`chip-link${view === 'timeline' ? ' on' : ''}`} role="tab" aria-selected={view === 'timeline'} onClick={() => setView('timeline')}>Timeline</button>
      </div>

      {view === 'timeline' ? (
        <Panel title="Timeline" right={<span className="muted">{timeline.length} items, newest first</span>}>
          <div className="table-wrap"><table className="hist tl-ticket"><tbody>
            {Object.entries(days).map(([day, events]) => [
              <tr key={day} className="tl-daterow"><th colSpan={4}>{day}</th></tr>,
              ...(events as Any[]).map((e) => (
                <tr key={e.id} className="click" onClick={() => open(e)}>
                  <td style={{ width: '10ch' }} className="nowrap">{new Date(e.createdAt).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', second: '2-digit' })}</td>
                  <td style={{ width: '22ch' }}><Link className="mono" to={`/records/history_entry/${e.id}`}>{e.model}</Link></td><td className="wrap">{e.summary}</td>
                  <td className="muted" style={{ width: '14ch' }}>{e.who || 'System'}</td>
                </tr>
              )),
            ])}
            {timeline.length === 0 && <tr><td className="empty">No items on this ticket yet.</td></tr>}
          </tbody></table></div>
        </Panel>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          {data.groups.map((g: Any) => (
            <div key={g.model} className="panel">
              <div className="panel-head"><h2><span className="muted" style={{ fontWeight: 400 }}>{g.items.length}</span> <span className="mono">{g.model}</span></h2></div>
              <div className="table-wrap"><table className="hist"><tbody>
                {g.items.map((h: Any) => (
                  <tr key={h.id} className="click" onClick={() => open(h)}>
                    <td className="mono" style={{ width: '9ch' }}><Link to={`/records/history_entry/${h.id}`}>{h.id}</Link></td>
                    <td style={{ width: '19ch', whiteSpace: 'nowrap' }}>{stamp(h.createdAt)}</td>
                    <td>{h.summary}</td>
                    <td className="muted" style={{ width: '14ch' }}>{h.who || 'System'}</td>
                  </tr>
                ))}
              </tbody></table></div>
            </div>
          ))}
          {data.groups.length === 0 && <div className="panel empty">No items on this ticket yet.</div>}
        </div>
      )}

      {data.logs && data.logs.length > 0 && (
        <Panel title="Logs"><div className="panel-body actions">
          {data.logs.map((l: Any) => <Link key={l.key} className="btn ghost" to={`/corral/customers/${c.id}/history?log=${l.key}`}>{l.title} <span className="count-pill">{l.count}</span></Link>)}
        </div></Panel>
      )}

      <div className="grid-2">
        <Panel title="Actions"><div className="panel-body actions">
          <Link className="btn ghost" to={`/corral/customers/${c.id}`}>Open Account</Link>
          <Link className="btn ghost" to={`/corral/customers/${c.id}/contact-log`}>Contact Log</Link>
          <Link className="btn ghost" to={`/corral/customers/${c.id}/api-log`}>API Log</Link>
        </div></Panel>
        <Panel title="Queues"><div className="panel-body">
          {current.length ? current.map((q: Any) => <span key={q.id}><span className="pill info mono">{q.queue}</span> </span>)
            : <span className="muted">{log || bill ? 'Log tickets are not queued.' : 'Not in any queue.'}</span>}
        </div></Panel>
      </div>
    </>
  );
}
