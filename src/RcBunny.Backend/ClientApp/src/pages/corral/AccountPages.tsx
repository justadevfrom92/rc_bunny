import { type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, money, useLoad } from '../../api';
import { Pill, RecordLink, Status, duration, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */
type Any = any;

const Back = ({ c }: { c: { id: number; account: string; name: string } }) => <p><Link to={`/corral/customers/${c.id}`}>← {c.name} #{c.account}</Link></p>;

/** An account action's own page: its form, or a confirmation when there's nothing to fill in. */
export function ActionForm() {
  const { id, key } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<Any>(`/api/customers/${id}/actions/${key}`);
  const [run, bar] = useRun();
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { action, customer: c } = data;

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const values = Object.fromEntries(new FormData(e.currentTarget).entries());
    run(api.post<{ message: string; goTo?: string }>(`/api/customers/${c.id}/actions/${key}`, values).then((r) => {
      navigate(r.goTo ?? `/corral/customers/${c.id}#actions`, { state: { message: r.message } });
      return r;
    }));
  };

  return (
    <>
      <Back c={c} />
      <h1>{action.label}</h1>
      {bar}
      <form className="card" onSubmit={submit}>
        {action.fields.length === 0 && <p>Run <b>{action.label}</b> on {c.name}'s account?</p>}
        {action.fields.map((f: Any) => (
          <label key={f.name}>{f.label}
            {f.type === 'textarea' ? <textarea name={f.name} rows={4} required />
              : f.type === 'method' ? <select name={f.name} required>{data.methods.length === 0 && <option value="">No payment methods on the account</option>}{data.methods.map((m: Any) => <option key={m.id} value={m.id}>{m.nickname ?? m.type} ending {m.last4}</option>)}</select>
              : f.type === 'payment' ? <select name={f.name} required>{data.payments.map((p: Any) => <option key={p.id} value={p.id}>{p.reference} · {date(p.paidOn)} · {money(p.amount)} · {p.status}</option>)}</select>
              : f.type === 'template' ? <select name={f.name} required>{data.templates.map((t: string) => <option key={t}>{t}</option>)}</select>
              : <input name={f.name} required type={f.type === 'date' ? 'date' : f.type === 'money' || f.type === 'number' ? 'number' : 'text'} step={f.type === 'money' ? '0.01' : undefined} min={f.type === 'money' ? '0.01' : f.type === 'number' ? '1' : undefined} />}
          </label>
        ))}
        <div className="row"><button className="primary">{action.label}</button><Link to={`/corral/customers/${c.id}`}>Cancel</Link></div>
      </form>
    </>
  );
}

/** The Log button on the Balances, Actions and Flags panels. */
export function PanelLog() {
  const { id, panel } = useParams();
  const { data, error } = useLoad<Any>(`/api/customers/${id}/panel-log/${panel}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const title = { balances: 'Balance Log', actions: 'Action Log', flags: 'Flag Log' }[panel as string];
  return (
    <>
      <Back c={data.customer} />
      <h1>{title}</h1>
      <table>
        {panel === 'balances' && <>
          <thead><tr><th>ID</th><th>When</th><th>Type</th><th className="num">Amount</th><th>Description</th><th>Status</th><th>By</th></tr></thead>
          <tbody>{data.rows.map((r: Any) => <tr key={r.record + r.id}><td><RecordLink type={r.record} id={r.id} /></td><td>{dateTime(r.at)}</td><td>{r.type}</td><td className="num">{money(r.amount)}</td><td>{r.description}</td><td><Status value={r.status} /></td><td>{r.by}</td></tr>)}</tbody>
        </>}
        {panel === 'actions' && <>
          <thead><tr><th>ID</th><th>When</th><th>Action</th><th>By</th><th>Result</th></tr></thead>
          <tbody>{data.rows.map((n: Any) => <tr key={n.id}><td><RecordLink type="note" id={n.id} /></td><td>{dateTime(n.createdAt)}</td><td>{n.action}</td><td>{n.author}</td><td>{n.body}</td></tr>)}</tbody>
        </>}
        {panel === 'flags' && <>
          <thead><tr><th>ID</th><th>Added</th><th>Flag</th><th>By</th><th>Removed</th></tr></thead>
          <tbody>{data.rows.map((f: Any) => <tr key={f.id}><td><RecordLink type="customer_flag" id={f.id} /></td><td>{dateTime(f.createdAt)}</td><td>{f.flag}</td><td>{f.author}</td><td>{f.removedAt ? dateTime(f.removedAt) : '—'}</td></tr>)}</tbody>
        </>}
      </table>
    </>
  );
}

/** Every email and text to and from the customer, and their calls; and the marketing opt-in. */
export function ContactLog() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<Any>(`/api/customers/${id}/contact-log`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const c = data.customer;
  return (
    <>
      <Back c={c} />
      <div className="row spread"><h1>Contact Log</h1><Link className="button" to={`/corral/contacts/new?account=${c.account}`}>New Contact</Link></div>
      {bar}
      <p>Marketing emails: <b>{c.marketingOptIn ? 'opted IN' : 'opted OUT'}</b> <button onClick={() => run(api.post(`/api/customers/${c.id}/contact-log/marketing`))}>Switch</button></p>
      <section className="card"><h2>Emails and texts</h2>
        <table>
          <thead><tr><th>ID</th><th>When</th><th>Channel</th><th>Template</th><th>Direction</th><th>Status</th><th>Resolution</th><th>By</th></tr></thead>
          <tbody>{data.logs.map((l: Any) => <tr key={l.id}><td><RecordLink type="contact_log" id={l.id} /></td><td>{dateTime(l.createdAt)}</td><td>{l.channel}</td><td>{l.template}{l.body && <div className="muted">{l.body}</div>}</td>
            <td>{l.direction === 'in' ? 'From customer' : 'To customer'}</td><td>{l.status}</td><td><Status value={l.resolution} /></td><td>{l.who}</td></tr>)}</tbody>
        </table>
      </section>
      <section className="card"><h2>Phone calls</h2>
        <table>
          <thead><tr><th>ID</th><th>When</th><th>Direction</th><th>Issue</th><th className="num">Duration</th><th>Answered by</th><th>Resolution</th></tr></thead>
          <tbody>{data.calls.map((p: Any) => <tr key={p.id}><td><Link to={`/corral/calls/${p.id}`}><b>#{p.id}</b></Link></td><td>{dateTime(p.startedAt)}</td><td>{p.direction}</td><td>{p.issue}</td>
            <td className="num">{duration(p.durationSec)}</td><td>{p.answeredBy}</td><td><Status value={p.resolution} /></td></tr>)}</tbody>
        </table>
      </section>
    </>
  );
}

/** Calls to outside services for this account (Stripe, Experian…), and the ones that weren't made. */
export function ApiLog() {
  const { id } = useParams();
  const { data, error } = useLoad<Any>(`/api/customers/${id}/api-log`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  return (
    <>
      <Back c={data.customer} />
      <h1>API Log</h1>
      {data.logs.length === 0 ? <p className="muted">No API calls for this account.</p> : (
        <table>
          <thead><tr><th>ID</th><th>When</th><th>Service</th><th>Action</th><th>Status</th><th>Request</th><th>Response</th></tr></thead>
          <tbody>{data.logs.map((l: Any) => <tr key={l.id}><td><RecordLink type="api_log" id={l.id} /></td><td>{dateTime(l.createdAt)}</td><td>{l.api}</td><td className="mono">{l.action}</td>
            <td><Pill tone={l.status === 'ok' ? 'ok' : l.status === 'failed' ? 'bad' : 'warn'}>{l.status}</Pill></td><td className="mono">{l.request}</td><td>{l.response}</td></tr>)}</tbody>
        </table>
      )}
    </>
  );
}

/** Spend Stars: redeem an offer for the customer on the phone. */
export function Stars() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<Any>(`/api/customers/${id}/stars`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  return (
    <>
      <Back c={data} />
      <h1>Spend Stars</h1>
      <p>{data.name} has <b>{data.stars.toLocaleString()}</b> stars.</p>
      {bar}
      <div className="tiles">
        {data.offers.map((o: Any) => (
          <div key={o.id} className="card">
            <h2>{o.name}</h2><p>{o.description}</p><p><b>{o.stars.toLocaleString()}</b> stars</p>
            <button className="primary" disabled={data.stars < o.stars} onClick={() => run(api.post(`/api/customers/${data.id}/stars`, { offerId: o.id }), `Redeemed ${o.name}.`)}>Redeem</button>
          </div>
        ))}
      </div>
    </>
  );
}

/** The account ticket (every history entry), one log, or a bill's ticket. */
export function History() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const log = params.get('log'), bill = params.get('bill');
  const { data, error } = useLoad<Any>(`/api/customers/${id}/history?${log ? `log=${log}` : ''}${bill ? `bill=${bill}` : ''}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const c = data.customer;
  return (
    <>
      <Back c={c} />
      <h1>{data.title}</h1>
      <p className="muted">Ticket <span className="mono">{c.ticket}</span> · opened {dateTime(c.createdAt)}{(log || bill) && <> · <Link to={`/corral/customers/${c.id}/history`}>Account ticket</Link></>}</p>
      {data.logs && data.logs.length > 0 && (
        <section className="card"><h2>Logs</h2>
          <div className="row">{data.logs.map((l: Any) => <Link key={l.key} className="button" to={`/corral/customers/${c.id}/history?log=${l.key}`}>{l.title} <Pill>{l.count}</Pill></Link>)}</div>
        </section>
      )}
      {data.queues && data.queues.length > 0 && (
        <section className="card"><h2>Process log</h2>
          <table><tbody>{data.queues.map((q: Any) => <tr key={q.id}><td>{dateTime(q.enteredAt)}</td><td className="mono">{q.queue}</td><td>{q.exitedAt ? `left ${dateTime(q.exitedAt)}` : 'current'}</td><td>{q.note}</td><td>{q.who}</td></tr>)}</tbody></table>
        </section>
      )}
      {data.groups.length === 0 && <p className="muted">Nothing recorded yet. Changes made from now on show up here.</p>}
      {data.groups.map((g: Any) => (
        <section key={g.model} className="card"><h2 className="mono">{g.model} <span className="muted">({g.items.length})</span></h2>
          <table>
            <thead><tr><th>ID</th><th>When</th><th>Who</th><th>What</th><th>Summary</th></tr></thead>
            <tbody>{g.items.map((h: Any) => <tr key={h.id}><td><RecordLink type="history_entry" id={h.id} /></td><td>{dateTime(h.createdAt)}</td><td>{h.who}</td><td>{h.action}</td><td>{h.summary}</td></tr>)}</tbody>
          </table>
        </section>
      ))}
    </>
  );
}
