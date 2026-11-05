import { type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, dateTime, money, useLoad } from '../../api';
import { useAuth } from '../../auth';
import { PageHead, ResolutionForm, age, longTime, statusTone, useRun } from '../../ui';

interface Data {
  message: { id: number; name: string; email: string; phone: string | null; topic: string; topicLabel: string; message: string; ip: string | null; createdAt: string;
    handledAt: string | null; handledBy: string | null; reply: string | null; repliedAt: string | null; repliedBy: string | null; resolution: string | null; resolvedBy: string | null };
  customer: { id: number; account: string; name: string; status: string; balance: number; plan: string | null; byEmail: boolean } | null;
  others: { id: number; topicLabel: string; createdAt: string; handledAt: string | null }[];
}

/** One web message (ebunny's corral/message): the message, the reply box, who it's from and the account it matches. */
export default function Message() {
  const { id } = useParams();
  const { me } = useAuth();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<Data>(`/api/messages/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const { message: m, customer: c } = data;
  const first = m.name.split(' ')[0];

  const reply = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post(`/api/messages/${m.id}/reply`, { reply: f.get('reply'), handled: f.get('handled') === 'on' }));
  };
  const text = { whiteSpace: 'pre-line', margin: 0, lineHeight: 1.6 } as const;

  return (
    <>
      <PageHead title={`${m.topicLabel}: ${m.name}`} sub={`Received ${longTime(m.createdAt)} (${age(m.createdAt)} ago)`}
        actions={<><Link className="btn ghost" to="/ranger/messages">All Messages</Link>
          <button className={`btn ${m.handledAt ? 'ghost' : 'cyan'}`} onClick={() => run(api.post(`/api/messages/${m.id}/handled`))}>{m.handledAt ? 'Reopen' : 'Mark Handled'}</button></>} />
      {bar}
      <div className="grid-2" style={{ alignItems: 'start', gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)' }}>
        <div style={{ display: 'grid', gap: 20, minWidth: 0 }}>
          <div className="panel"><div className="panel-head"><h2>Message</h2>{m.handledAt ? <span className="pill ok">Handled</span> : <span className="pill warn">Open</span>}</div>
            <div className="panel-body"><p style={text}>{m.message}</p></div></div>
          {m.reply && <div className="panel"><div className="panel-head"><h2>Reply sent</h2><span className="muted">{m.repliedBy} · {m.repliedAt && dateTime(m.repliedAt)}</span></div>
            <div className="panel-body"><p style={text}>{m.reply}</p></div></div>}
          <form className="panel" onSubmit={reply} key={m.repliedAt ?? ''}>
            <div className="panel-head"><h2>{m.reply ? 'Send another reply' : 'Reply'}</h2><span className="muted">emails {m.email}</span></div>
            <div className="panel-body" style={{ display: 'grid', gap: 10 }}>
              <label htmlFor="reply" className="sr-only">Reply</label>
              <textarea id="reply" name="reply" rows={6} required maxLength={5000} style={{ width: '100%', font: 'inherit', padding: 10, border: '1px solid var(--line)', borderRadius: 6 }}
                defaultValue={`Hi ${first},\n\nThanks for reaching out. \n\n${me?.name ?? ''}\nEnergy Texas Customer Care`} />
              <label className="check"><input type="checkbox" name="handled" defaultChecked={!m.handledAt} /> Mark handled when sent</label>
              <div><button className="btn cyan">Send Reply</button></div>
            </div>
          </form>
        </div>
        <div style={{ display: 'grid', gap: 20, minWidth: 0 }}>
          <div className="panel"><div className="panel-head"><h2>From</h2></div>
            <div className="panel-body"><dl className="kv">
              <dt>Name</dt><dd>{m.name}</dd>
              <dt>Email</dt><dd><a href={`mailto:${m.email}`}>{m.email}</a></dd>
              <dt>Phone</dt><dd>{m.phone || '—'}</dd>
              <dt>Topic</dt><dd>{m.topicLabel}</dd>
              <dt>IP</dt><dd className="mono">{m.ip || '—'}</dd>
              {m.handledAt && <><dt>Handled</dt><dd>{dateTime(m.handledAt)}<br /><span className="help">{m.handledBy}</span></dd></>}
            </dl></div>
            <div className="panel-body" style={{ borderTop: '1px solid var(--line)' }}><h3>Resolution</h3>
              <ResolutionForm value={m.resolution} by={m.resolvedBy} onSave={(r) => run(api.post(`/api/corral/resolution/message/${m.id}`, { resolution: r }), 'Resolution saved.')} /></div>
          </div>
          <div className="panel"><div className="panel-head"><h2>Account</h2></div><div className="panel-body">
            {c ? <>
              <dl className="kv"><dt>Account</dt><dd><Link className="mono" to={`/corral/customers/${c.id}`}>{c.account}</Link></dd><dt>Name</dt><dd>{c.name}</dd>
                <dt>Status</dt><dd><span className={`pill ${statusTone(c.status)}`}>{c.status}</span></dd><dt>Balance</dt><dd>{money(c.balance)}</dd><dt>Plan</dt><dd>{c.plan ?? '—'}</dd></dl>
              <p className="help" style={{ margin: '8px 0 0' }}>Matched by {c.byEmail ? 'email' : 'phone number'}. Replies are logged on this account.</p>
            </> : <p className="muted" style={{ margin: 0 }}>No account matches this email or phone. Likely a prospect: <Link to="/corral/orders/create">start an order</Link> if they want service.</p>}
          </div></div>
          {data.others.length > 0 && <div className="panel"><div className="panel-head"><h2>Other messages from them</h2></div><div className="table-wrap"><table><tbody>
            {data.others.map((o) => (
              <tr key={o.id} className="click" onClick={() => navigate(`/ranger/messages/${o.id}`)}><td className="nowrap"><Link to={`/ranger/messages/${o.id}`}><b>#{o.id}</b></Link></td>
                <td className="nowrap">{new Date(o.createdAt).toLocaleDateString('en-US')}</td><td>{o.topicLabel}</td><td>{o.handledAt ? 'Handled' : 'Open'}</td></tr>
            ))}
          </tbody></table></div></div>}
        </div>
      </div>
    </>
  );
}
