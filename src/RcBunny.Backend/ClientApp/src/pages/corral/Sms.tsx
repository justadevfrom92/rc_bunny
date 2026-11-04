import { type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, qs, useLoad } from '../../api';
import { AccountNo, EmptyRow, Field, PageHead, PagerBar, Panel, PhoneActions, ResolutionForm, ResolutionPill, longTime, secondsTime, shortTime, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const NotReady = () => <div className="alert info" style={{ marginBottom: 16 }}><b>Texts are logged, not sent.</b> Add the Twilio settings (see Sheriff → APIs) to send texts.</div>;

/** Corral → SMS (ebunny's corral/sms): search on the left, recent messages on the right. */
export function Sms() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['q', 'account', 'direction', 'status', 'start', 'end', 'waiting'].map((k) => [k, params.get(k) ?? '']));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/sms?${qs({ ...f, page })}`);
  const navigate = useNavigate();
  const filter = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs(Object.fromEntries(new FormData(e.currentTarget).entries()) as any)); };
  const waiting = data ? new Set<number>(data.waitingIds) : new Set<number>();
  const filtered = Object.values(f).some(Boolean);

  return (
    <>
      <PageHead title="SMS"
        sub={!data ? '' : waiting.size ? <><b>{waiting.size}</b> {waiting.size === 1 ? 'customer' : 'customers'} waiting on a reply.</> : 'Recent text messages to and from customers. Nobody is waiting on a reply.'}
        actions={<><Link className="btn ghost" to={`/corral/contacts/new?${qs({ account: f.account, channel: 'sms' })}`}>New Contact</Link><Link className="btn cyan" to="/corral/sms/new">Send a Text</Link></>} />
      {data && !data.ready && <NotReady />}
      {error && <div className="flash bad">{error}</div>}
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={filter} key={params.toString()}><div className="panel-head"><h2>Search Messages</h2></div><div className="panel-body">
            <Field label="Name, phone or text" htmlFor="q"><input id="q" name="q" defaultValue={f.q} /></Field>
            <Field label="Account Number" htmlFor="account"><input id="account" name="account" defaultValue={f.account} inputMode="numeric" /></Field>
            <Field label="Direction" htmlFor="direction"><select id="direction" name="direction" defaultValue={f.direction}><option value="">Both</option><option value="in">From customer</option><option value="out">To customer</option></select></Field>
            <Field label="Status" htmlFor="status"><select id="status" name="status" defaultValue={f.status}><option value="">Any</option>{data?.statuses.map((s: string) => <option key={s}>{s}</option>)}</select></Field>
            <Field label="Start" htmlFor="start"><input id="start" name="start" type="date" defaultValue={f.start} /></Field>
            <Field label="End" htmlFor="end"><input id="end" name="end" type="date" defaultValue={f.end} /></Field>
            <label className="check"><input type="checkbox" name="waiting" value="true" defaultChecked={f.waiting === 'true'} /> Waiting on a reply</label>
            <button className="btn">Search</button>{filtered && <Link className="btn ghost" to="/corral/sms">Clear</Link>}
          </div></form>
        </aside>
        <div className="search-main">
          <Panel title="Recent Messages" right={<span className="muted">{data?.total.toLocaleString()}</span>}>
            <div className="table-wrap"><table>
              <thead><tr><th>ID</th><th>Sent</th><th>Account</th><th>Message</th><th>Status</th><th>Resolution</th></tr></thead>
              <tbody>
                {data?.rows.map((m: any) => (
                  <tr key={m.id} className={`click${m.direction === 'in' && waiting.has(m.customerId) ? ' row-alert' : ''}`} onClick={() => navigate(`/corral/sms/${m.id}`)}>
                    <td className="nowrap"><Link to={`/corral/sms/${m.id}`}><b>#{m.id}</b></Link></td>
                    <td className="nowrap">{dateTime(m.createdAt)}<br /><span className="help">{m.direction === 'in' ? 'From customer' : 'To customer'}</span></td>
                    <td>{m.customerId && <><Link to={`/corral/customers/${m.customerId}`}>{m.account}</Link><br /><span className="help">{m.customer}</span><br /></>}<span className="help mono">{m.phone ?? m.customerPhone}</span></td>
                    <td className="wrap">{m.body}</td>
                    <td>{m.status}{m.direction !== 'in' && <><br /><span className="help">by {m.who ?? 'System'}</span></>}</td>
                    <td><ResolutionPill value={m.resolution} /></td>
                  </tr>
                ))}
                {data?.rows.length === 0 && <EmptyRow cols={6}>No messages match.</EmptyRow>}
              </tbody>
            </table></div>
            {data && <PagerBar page={page} total={data.total} size={50} onPage={(p) => setParams(qs({ ...f, page: p }))} />}
          </Panel>
        </div>
      </div>
    </>
  );
}

const statusTone: Record<string, string> = { sent: 'ok', queued: 'info', received: 'ok', logged: 'info' };

/** One text (ebunny's corral/sms-show): the message and how it ended, and the whole conversation. */
export function SmsMessage() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/sms/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const { message: m, customer: c } = data;
  const phone = m.phone ?? c?.phone;
  return (
    <>
      <PageHead title={`Text Message${c ? `: ${c.name}` : ''}`}
        sub={<>{longTime(m.createdAt)} · {m.direction === 'in' ? 'From the customer' : 'To the customer'} · <span className="mono">{phone}</span></>}
        actions={<>
          <Link className="btn ghost" to="/corral/sms">Back to SMS</Link>
          {c && <><Link className="btn ghost" to={`/corral/customers/${c.id}/contact-log`}>Contact Log</Link><Link className="btn ghost" to={`/corral/customers/${c.id}`}>Back to account</Link>
            <Link className="btn ghost" to={`/corral/contacts/new?account=${c.account}&channel=sms`}>New Contact</Link><Link className="btn cyan" to={`/corral/sms/new?account=${c.account}`}>Reply</Link></>}
        </>} />
      {bar}
      <div className="grid-2" style={{ alignItems: 'start', gridTemplateColumns: 'minmax(0,1fr) minmax(0,2fr)' }}>
        <div className="panel"><div className="panel-head"><h2>Message</h2><span className={`pill ${statusTone[m.status] ?? 'bad'}`}>{m.status}</span></div>
          <div className="panel-body"><dl className="kv">
            <dt>Account</dt><dd><AccountNo id={c?.id} account={c?.account} /><br /><span className="help">{c?.name}</span></dd>
            <dt>Phone</dt><dd className="mono"><PhoneActions phone={phone} account={c?.account} /></dd>
            <dt>Direction</dt><dd>{m.direction === 'in' ? 'From the customer' : 'To the customer'}</dd>
            <dt>Template</dt><dd>{m.template}</dd>
            <dt>Sent by</dt><dd>{m.direction === 'in' ? '—' : m.who ?? 'System'}</dd>
            <dt>Created</dt><dd>{secondsTime(m.createdAt)}</dd>
            <dt>Delivered</dt><dd>{m.sentAt ? secondsTime(m.sentAt) : '—'}</dd>
            <dt>Text</dt><dd>{m.body}</dd>
            <dt>Resolution</dt><dd><ResolutionPill value={m.resolution} /></dd>
          </dl></div>
          <div className="panel-body" style={{ borderTop: '1px solid var(--line)' }}><h3>How did it end?</h3>
            <ResolutionForm value={m.resolution} by={m.resolvedBy} onSave={(r) => run(api.post(`/api/corral/resolution/contact/${m.id}`, { resolution: r }), 'Resolution saved')} />
          </div>
        </div>
        <Panel title="Conversation" right={<span className="muted">{data.thread.length} {data.thread.length === 1 ? 'message' : 'messages'}</span>}>
          <div className="panel-body transcript">
            {data.thread.map((t: any) => (
              <p key={t.id} className={`${t.direction !== 'in' ? 'agent' : ''}${t.id === m.id ? ' current' : ''}`}>
                <b>{t.direction === 'in' ? c?.firstName ?? 'Customer' : t.who ?? 'Us'}</b>{' '}
                {t.id !== m.id ? <Link to={`/corral/sms/${t.id}`} className="muted" style={{ fontSize: '.75rem' }}>{shortTime(t.createdAt)}</Link>
                  : <span className="muted" style={{ fontSize: '.75rem' }}>{shortTime(t.createdAt)} · this message</span>}<br />{t.body}
              </p>
            ))}
          </div>
        </Panel>
      </div>
    </>
  );
}

/** Send a Text (ebunny's corral/sms-form). */
export function SmsNew() {
  const [params] = useSearchParams();
  const account = params.get('account') ?? '';
  const { data } = useLoad<any>(`/api/corral/sms/new?${qs({ account })}`);
  const navigate = useNavigate();
  const [run, bar] = useRun();
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.customer;
  const send = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/corral/sms', { account: f.get('account'), message: f.get('message') }).then((r) => { navigate('/corral/sms', { state: r }); return r; }));
  };
  return (
    <>
      <PageHead title="Send a Text" sub={c ? <>To {c.name} at <span className="mono">{c.phone}</span></> : 'Texts go to the phone number on the account.'} />
      {!data.ready && <NotReady />}
      {bar}
      {data.recent.length > 0 && (
        <div className="panel simple-form" style={{ marginBottom: 16 }}><div className="panel-head"><h2>Last messages</h2></div><div className="table-wrap"><table><tbody>
          {data.recent.map((m: any) => <tr key={m.id}><td className="nowrap">{shortTime(m.createdAt)}</td><td>{m.direction === 'in' ? 'From customer' : 'To customer'}</td><td className="wrap">{m.body}</td></tr>)}
        </tbody></table></div></div>
      )}
      <form className="panel simple-form" onSubmit={send}>
        <div className="panel-body form-grid">
          <label htmlFor="account">Account Number</label><input id="account" name="account" required defaultValue={c?.account ?? account} inputMode="numeric" style={{ maxWidth: '16ch' }} />
          <label htmlFor="message">Message</label><textarea id="message" name="message" required maxLength={480} style={{ minHeight: 110, fontFamily: 'inherit' }} />
        </div>
        <div className="panel-body actions"><button className="btn cyan">Send Text</button><Link className="btn ghost" to="/corral/sms">Cancel</Link></div>
      </form>
    </>
  );
}
