import { type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, qs, useLoad } from '../../api';
import { Pager, Pill, Status, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const notReady = <p className="alert info">No text provider (Twilio) is set up, so texts are saved here but not delivered.</p>;

/** Corral → SMS: texts in and out, with filters. "Waiting" = customers whose last text came from them. */
export function Sms() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['q', 'account', 'direction', 'status', 'start', 'end', 'waiting'].map((k) => [k, params.get(k) ?? '']));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/sms?${qs({ ...f, page })}`);
  const filter = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs(Object.fromEntries(new FormData(e.currentTarget).entries()) as any)); };

  return (
    <>
      <div className="row spread"><h1>SMS</h1><Link className="button" to="/corral/sms/new">Send a Text</Link></div>
      {data && !data.ready && notReady}
      <form className="row" onSubmit={filter} key={params.toString()}>
        <input name="q" placeholder="Message, name or phone" defaultValue={f.q} />
        <input name="account" placeholder="Account" defaultValue={f.account} style={{ maxWidth: 140 }} />
        <select name="direction" defaultValue={f.direction}><option value="">In and out</option><option value="in">From customers</option><option value="out">To customers</option></select>
        <select name="status" defaultValue={f.status}><option value="">Any status</option>{data?.statuses.map((s: string) => <option key={s}>{s}</option>)}</select>
        <input name="start" type="date" defaultValue={f.start} /><input name="end" type="date" defaultValue={f.end} />
        <label style={{ flexDirection: 'row', margin: 0 }}><input type="checkbox" name="waiting" value="true" defaultChecked={f.waiting === 'true'} /> Waiting on a reply</label>
        <button className="primary">Show</button>
      </form>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <p className="muted">{data.total} texts</p>
          <table>
            <thead><tr><th>ID</th><th>When</th><th>Customer</th><th>Phone</th><th>Direction</th><th>Message</th><th>Status</th><th>Resolution</th></tr></thead>
            <tbody>{data.rows.map((m: any) => (
              <tr key={m.id}><td><Link to={`/corral/sms/${m.id}`}><b>#{m.id}</b></Link></td><td>{dateTime(m.createdAt)}</td>
                <td>{m.customerId ? <Link to={`/corral/customers/${m.customerId}`}>{m.customer}</Link> : '—'}{data.waitingIds.includes(m.customerId) && <> <Pill tone="warn">waiting</Pill></>}</td>
                <td>{m.phone}</td><td>{m.direction === 'in' ? 'From customer' : 'To customer'}</td><td>{m.body}</td><td>{m.status}</td><td><Status value={m.resolution} /></td></tr>
            ))}</tbody>
          </table>
          <Pager page={page} total={data.total} size={50} onPage={(p) => setParams(qs({ ...f, page: p }))} />
        </>
      )}
    </>
  );
}

/** One text, with the whole conversation, a reply box and the resolution. */
export function SmsMessage() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/sms/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { message: m, customer: c } = data;
  return (
    <>
      <p><Link to="/corral/sms">← SMS</Link></p>
      <h1>Text #{m.id}</h1>
      {c && <p><Link to={`/corral/customers/${c.id}`}>{c.name} #{c.account}</Link> · {c.phone}</p>}
      {bar}
      <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/corral/resolution/contact/${m.id}`, { resolution: new FormData(e.currentTarget).get('resolution') }), 'Saved.'); }}>
        <label style={{ flexDirection: 'row', margin: 0 }}>Resolution <select name="resolution" defaultValue={m.resolution ?? ''}>{data.resolutions.map((r: string) => <option key={r}>{r}</option>)}</select></label>
        <button>Save</button>{m.resolvedBy && <small>by {m.resolvedBy}</small>}
      </form>
      <section className="card">
        <h2>Conversation</h2>
        {data.thread.map((t: any) => (
          <div key={t.id} className={`card ticket-msg ${t.direction === 'in' ? 'customer' : ''}`} style={t.id === m.id ? { outline: '2px solid var(--cyan)' } : undefined}>
            <small>{t.direction === 'in' ? 'Customer' : t.who ?? 'Energy Texas'} · {dateTime(t.createdAt)} · {t.status}</small><p>{t.body}</p>
          </div>
        ))}
      </section>
      {c && <ReplyBox account={c.account} after={reload} />}
    </>
  );
}

function ReplyBox({ account, after }: { account: string; after: () => void }) {
  const [run, bar] = useRun(after);
  return (
    <form className="card" onSubmit={(e) => { e.preventDefault(); const form = e.currentTarget; run(api.post('/api/corral/sms', { account, message: new FormData(form).get('message') }).then((r) => { form.reset(); return r; })); }}>
      {bar}
      <textarea name="message" rows={3} maxLength={480} placeholder="Reply" required />
      <button className="primary">Send</button>
    </form>
  );
}

/** Send a Text: to an account, with its last few texts. */
export function SmsNew() {
  const [params, setParams] = useSearchParams();
  const account = params.get('account') ?? '';
  const { data } = useLoad<any>(`/api/corral/sms/new?${qs({ account })}`);
  const navigate = useNavigate();
  const [run, bar] = useRun();
  if (!data) return <p>Loading…</p>;
  const c = data.customer;
  return (
    <>
      <p><Link to="/corral/sms">← SMS</Link></p>
      <h1>Send a Text</h1>
      {!data.ready && notReady}
      {bar}
      {!c ? (
        <form className="row" onSubmit={(e) => { e.preventDefault(); setParams({ account: String(new FormData(e.currentTarget).get('account')).trim() }); }}>
          <input name="account" placeholder="Account number" required /><button className="primary">Find</button>
          {account && <span className="error">No account {account}.</span>}
        </form>
      ) : (
        <>
          <p>To <Link to={`/corral/customers/${c.id}`}>{c.name} #{c.account}</Link> at {c.phone ?? <b>no phone number</b>}</p>
          {data.recent.map((t: any) => <div key={t.id} className={`card ticket-msg ${t.direction === 'in' ? 'customer' : ''}`}><small>{dateTime(t.createdAt)}</small><p>{t.body}</p></div>)}
          <form className="card" onSubmit={(e) => { e.preventDefault(); run(api.post('/api/corral/sms', { account: c.account, message: new FormData(e.currentTarget).get('message') }).then((r) => { navigate('/corral/sms'); return r; })); }}>
            <textarea name="message" rows={3} maxLength={480} required placeholder="Message" />
            <button className="primary" disabled={!c.phone}>Send</button>
          </form>
        </>
      )}
    </>
  );
}
