import { type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, dateTime, useLoad } from '../../api';
import { useAuth } from '../../auth';
import { useRun } from '../../ui';

interface Data {
  message: { id: number; name: string; email: string; phone: string | null; topic: string; message: string; createdAt: string;
    handledAt: string | null; handledBy: string | null; reply: string | null; repliedAt: string | null; repliedBy: string | null };
  customer: { id: number; account: string; name: string; email: string | null; phone: string | null; status: string } | null;
  others: { id: number; topic: string; message: string; createdAt: string; handledAt: string | null }[];
  tickets: { id: number; number: string; subject: string; status: string }[] | null;
}

/** One web message: the account it matches, earlier messages from them, a reply box, and Mark Handled / Reopen. */
export default function Message() {
  const { id } = useParams();
  const { can } = useAuth();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<Data>(`/api/messages/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { message: m, customer: c } = data;

  const reply = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post(`/api/messages/${m.id}/reply`, { reply: f.get('reply'), handled: f.get('handled') === 'on' }));
  };
  const makeTicket = () => api.post<{ id: number }>(`/api/messages/${m.id}/ticket`).then((r) => navigate(`/ranger/tickets/${r.id}`));

  return (
    <>
      <p><Link to="/ranger/messages">← Web Messages</Link></p>
      <div className="row spread">
        <h1>{m.topic} · {m.name}</h1>
        <div className="row">
          <button onClick={makeTicket}>Make a Ticket</button>
          <button className={m.handledAt ? '' : 'primary'} onClick={() => run(api.post(`/api/messages/${m.id}/handled`))}>{m.handledAt ? 'Reopen' : 'Mark Handled'}</button>
        </div>
      </div>
      <p className="muted"><a href={`mailto:${m.email}`}>{m.email}</a>{m.phone && ` · ${m.phone}`} · sent {dateTime(m.createdAt)}
        {m.handledAt && ` · handled ${dateTime(m.handledAt)} by ${m.handledBy}`}</p>
      {bar}
      <div className="grid" style={{ gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div>
          <section className="card ticket-msg customer"><p>{m.message}</p></section>
          {m.reply && <section className="card ticket-msg agent"><small>Reply by {m.repliedBy} · {m.repliedAt && dateTime(m.repliedAt)}</small><p>{m.reply}</p></section>}
          <form className="card" onSubmit={reply}>
            <h2>{m.reply ? 'Reply Again' : 'Reply'}</h2>
            <textarea name="reply" required maxLength={5000} rows={6} placeholder={`Hi ${m.name.split(' ')[0]}, …`} />
            <div className="row">
              <label style={{ flexDirection: 'row', margin: 0 }}><input type="checkbox" name="handled" defaultChecked={!m.handledAt} /> Mark handled</label>
              <button className="primary">Send Reply</button>
              <span className="muted">The reply is emailed to {m.email}{c && ' and logged on the account'}.</span>
            </div>
          </form>
        </div>
        <div>
          <section className="card">
            <h2>Account</h2>
            {c ? (
              <dl>
                <dt>Name</dt><dd>{can('corral') ? <Link to={`/corral/customers/${c.id}`}>{c.name}</Link> : c.name}</dd>
                <dt>Account</dt><dd className="mono">#{c.account}</dd><dt>Status</dt><dd>{c.status}</dd>
                <dt>Email</dt><dd>{c.email ?? '—'}</dd><dt>Phone</dt><dd>{c.phone ?? '—'}</dd>
              </dl>
            ) : <p className="muted">No account has this email or phone number.</p>}
            {data.tickets && data.tickets.length > 0 && <>
              <h3>Their tickets</h3>
              {data.tickets.map((t) => <p key={t.id}><Link to={`/ranger/tickets/${t.id}`}><span className="mono">{t.number}</span> {t.subject}</Link> <span className="muted">{t.status}</span></p>)}
            </>}
          </section>
          <section className="card">
            <h2>Earlier Messages</h2>
            {data.others.length === 0 ? <p className="muted">None.</p> : data.others.map((o) => (
              <p key={o.id}><Link to={`/ranger/messages/${o.id}`}>{o.topic}</Link> <span className="muted">{dateTime(o.createdAt)}{o.handledAt ? ' · handled' : ''}</span><br />{o.message.slice(0, 100)}</p>
            ))}
          </section>
        </div>
      </div>
    </>
  );
}
