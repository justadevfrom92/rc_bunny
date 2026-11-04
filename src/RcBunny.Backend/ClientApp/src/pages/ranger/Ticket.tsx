import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { api, dateTime, useLoad } from '../../api';
import { useAuth } from '../../auth';
import { Pill, RecordLink, duration, useRun } from '../../ui';
import { PriorityPill, StatusPill, type Lists } from './shared';

interface File { id: number; name: string; size: number; isImage: boolean; isPdf: boolean }
interface Message {
  id: number; kind: string; author: string; userId: number | null; via: string; direction: string | null; durationSec: number | null; contact: string | null;
  body: string; createdAt: string; files: File[];
}
interface Note { id: number; createdAt: string; author: string; category: string | null; action: string | null; priority: string | null; body: string }
interface Data {
  id: number; number: string; subject: string; name: string; email: string; topic: string; topicLabel: string; channel: string; status: string; priority: string;
  createdAt: string; firstResponseAt: string | null; solvedAt: string | null; rating: string | null; ratingComment: string | null;
  assignedToId: number | null; assignedTo: string | null; assignedCompany: string | null;
  managerRequestedAt: string | null; managerRequestedBy: string | null; managerRequestNote: string | null;
  customer: { id: number; account: string; name: string; email: string | null; phone: string | null; status: string; plan: string | null; address: string } | null;
  otherTickets: number; messages: Message[]; notes: Note[]; attachments: (File & { messageId: number | null; createdAt: string })[];
  watchers: { userId: number; name: string; joinedAt: string }[]; teamOf: Record<string, string>;
  me: number; manager: boolean; agents: { userId: number; name: string; team: string }[];
  noteCategories: Record<string, string[]>; priorities: string[]; lists: Lists;
}

export const size = (n: number) => (n >= 1048576 ? `${(n / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`);

/**
 * One ticket: its activity (My Account messages, emails and phone calls, with attachments), the reply box, the notes,
 * and on the side the ticket's status, owner, who joined, and the customer.
 */
export default function Ticket() {
  const { id } = useParams();
  const { can } = useAuth();
  const { data: t, error, reload } = useLoad<Data>(`/api/ranger/tickets/${id}`);
  const [run, bar] = useRun(reload);
  const [via, setVia] = useState<string | null>(null);
  const [category, setCategory] = useState('Ticket');
  const [asking, setAsking] = useState(false);

  if (error) return <p className="error">{error}</p>;
  if (!t) return <p>Loading…</p>;

  const l = t.lists;
  const owner = t.assignedToId === t.me;
  const joined = t.watchers.some((w) => w.userId === t.me);
  const canWork = !t.assignedToId || owner || joined || t.manager;
  const done = t.status === 'solved' || t.status === 'closed';
  const sendBy = via ?? ({ Email: 'email', Phone: 'phone' } as Record<string, string>)[t.channel] ?? 'portal';
  const url = `/api/ranger/tickets/${t.id}`;
  const post = (path: string, body?: unknown) => run(api.post(`${url}/${path}`, body));

  const form = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); return { el: e.currentTarget, f: new FormData(e.currentTarget) }; };
  const reply = (e: FormEvent<HTMLFormElement>) => {
    const { el, f } = form(e);
    run(api.post(`${url}/reply`, { body: f.get('body'), via: sendBy, direction: f.get('direction'), minutes: Number(f.get('minutes') || 0), status: f.get('status') || null })
      .then((r) => { el.reset(); return r; }));
  };
  const note = (e: FormEvent<HTMLFormElement>) => {
    const { el, f } = form(e);
    run(api.post(`${url}/notes`, { body: f.get('body'), category: f.get('category'), action: f.get('action'), priority: f.get('priority') }).then((r) => { el.reset(); return r; }));
  };
  const ask = (e: FormEvent<HTMLFormElement>) => { const { f } = form(e); setAsking(false); post('ask-manager', { reason: f.get('reason') }); };
  const update = (changes: { status?: string; priority?: string; assignedTo?: number }) => run(api.put(url, changes));

  const entries = t.messages.filter((m) => m.kind !== 'system');
  const calls = entries.filter((m) => m.via === 'phone').length, emails = entries.filter((m) => m.via === 'email').length;
  const team = (userId: number | null) => (userId !== null ? t.teamOf[String(userId)] : undefined);

  return (
    <>
      <p><Link to="/ranger/tickets">← Tickets</Link></p>
      <div className="row spread">
        <div>
          <h1>{t.subject}</h1>
          <p className="muted">
            <span className="mono">{t.number}</span> · <StatusPill lists={l} value={t.status} /> · {t.topicLabel} · opened {dateTime(t.createdAt)} from {l.channels[t.channel]} {t.channel}
          </p>
        </div>
        <div className="row">
          {t.manager && !owner && <button className="primary" onClick={() => confirm(`Take over ${t.number}? It becomes yours${t.assignedTo ? `; ${t.assignedTo} stays on it as a watcher` : ''}.`) && post('take-over')}>Take Over</button>}
          {t.manager && !owner && !joined && t.assignedToId && <button onClick={() => post('join')}>Join In</button>}
          {joined && <button onClick={() => post('leave')}>Leave</button>}
          {!t.assignedToId && <button className="primary" onClick={() => update({ assignedTo: t.me })}>Pick Up</button>}
          {!t.manager && canWork && !done && (t.managerRequestedAt
            ? <><Pill tone="warn">Manager requested</Pill><button onClick={() => post('cancel-manager')}>No Longer Needed</button></>
            : <button onClick={() => setAsking(true)}>Need Help?</button>)}
        </div>
      </div>
      {bar}

      {asking && (
        <form className="card" onSubmit={ask}>
          <h2>Ask for a Manager</h2>
          <p className="muted">A manager can assign this ticket to someone else, take it over, or join in. Your reason is saved as a note.</p>
          <input name="reason" maxLength={500} aria-label="Why" placeholder="Why (optional), e.g. customer wants a refund over $100" />
          <div className="row"><button className="primary">Ask for a Manager</button><button type="button" onClick={() => setAsking(false)}>Cancel</button></div>
        </form>
      )}

      <div className="grid" style={{ gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div>
          <section className="card">
            <div className="row spread"><h2>Activity</h2>
              <span className="muted">{[entries.length - calls - emails && `${entries.length - calls - emails} messages`, emails && `${emails} emails`, calls && `${calls} calls`].filter(Boolean).join(' · ') || 'Nothing yet'}</span></div>
            {t.messages.map((m) => m.kind === 'system'
              ? <p key={m.id} className="ticket-system">{m.body} · {dateTime(m.createdAt)}</p>
              : (
                <div key={m.id} className={`card ticket-msg ${m.via === 'phone' ? 'call' : m.kind}`}>
                  <small>
                    {m.via === 'phone'
                      ? <>📞 <b>{m.direction === 'in' ? 'Inbound call' : 'Outbound call'}</b> · {m.durationSec ? duration(m.durationSec) : '—'}{m.contact && ` · ${m.contact}`} · {m.kind === 'customer' ? t.name : m.author}</>
                      : <><b>{m.kind === 'customer' ? t.name : m.author}</b>{' · '}
                        {m.kind === 'note' ? <Pill tone="warn">Note</Pill> : m.via === 'email' ? `✉ ${m.kind === 'customer' ? 'Email from' : 'Email to'} ${m.contact ?? t.email}` : '💬 My Account'}</>}
                    {m.kind === 'agent' && team(m.userId) && ` · ${team(m.userId)}`} · {dateTime(m.createdAt)}
                  </small>
                  <p>{m.body}</p>
                  {m.files.length > 0 && <p className="files">{m.files.map((a) => <Link key={a.id} to={`/ranger/tickets/${t.id}/files/${a.id}`}>{a.isImage ? '🖼' : a.isPdf ? '📄' : '📎'} {a.name} <span className="muted">{size(a.size)}</span></Link>)}</p>}
                </div>
              ))}
          </section>

          {canWork ? (
            <form className="card" onSubmit={reply}>
              <h2>Reply to the Customer</h2>
              <div className="row">Send by
                {[['portal', '💬 My Account'], ['email', `✉ Email to ${t.email}`], ['phone', '📞 Log a phone call']].map(([k, label]) => (
                  <label key={k} style={{ flexDirection: 'row', margin: 0 }}><input type="radio" name="via" value={k} checked={sendBy === k} onChange={() => setVia(k)} /> {label}</label>
                ))}
              </div>
              {sendBy === 'phone' && (
                <div className="row">
                  <label style={{ flexDirection: 'row', margin: 0 }}><input type="radio" name="direction" value="out" defaultChecked /> We called them</label>
                  <label style={{ flexDirection: 'row', margin: 0 }}><input type="radio" name="direction" value="in" /> They called us</label>
                  <label>Minutes<input name="minutes" type="number" min={0} max={600} defaultValue={5} style={{ width: 90 }} /></label>
                </div>
              )}
              <textarea name="body" required maxLength={5000} rows={5} aria-label="Message" placeholder={`Hi ${t.name.split(' ')[0]}, …`} />
              <div className="row">
                <label>Then set the ticket to<select name="status" defaultValue=""><option value="">Leave as is</option><option value="waiting">Waiting on Customer</option><option value="solved">Solved</option></select></label>
                <button className="primary">Save</button>
                <span className="muted">Messages and emails reach the customer; call notes stay with the team.</span>
              </div>
            </form>
          ) : <p className="notice"><b>{t.assignedTo} has this ticket.</b> A manager can join you in or take it over.</p>}

          <section className="card" id="notes">
            <div className="row spread"><h2>Notes</h2><span className="muted">{t.notes.length} · staff only{t.customer && ', also on the account\'s notes'}</span></div>
            {t.customer && (
              <form onSubmit={note}>
                <div className="row">
                  <label>Category<select name="category" value={category} onChange={(e) => setCategory(e.target.value)}>{Object.keys(t.noteCategories).map((c) => <option key={c}>{c}</option>)}</select></label>
                  <label>Action<select name="action" key={category} defaultValue={category === 'Ticket' ? 'Internal Note' : undefined}>{t.noteCategories[category].map((a) => <option key={a}>{a}</option>)}</select></label>
                  <label>Priority<select name="priority" defaultValue="Low">{t.priorities.map((p) => <option key={p}>{p}</option>)}</select></label>
                </div>
                <textarea name="body" required maxLength={5000} rows={3} aria-label="Note" placeholder="What the team should know about this ticket" />
                <button>Add Note</button>
              </form>
            )}
            {t.notes.length === 0 ? <p className="muted">No notes yet.</p> : (
              <table>
                <thead><tr><th>ID</th><th>Added</th><th>By</th><th>Category / Action</th><th>Priority</th><th>Note</th></tr></thead>
                <tbody>{t.notes.map((n) => (
                  <tr key={n.id}><td><RecordLink type="note" id={n.id} /></td><td className="nowrap">{dateTime(n.createdAt)}</td><td>{n.author}</td>
                    <td>{n.category ?? '—'}{n.action && <div className="muted">{n.action}</div>}</td>
                    <td>{n.priority ? <Pill tone={({ High: 'bad', Medium: 'warn' } as Record<string, string>)[n.priority]}>{n.priority}</Pill> : '—'}</td>
                    <td className="wrap-text">{n.body}</td></tr>
                ))}</tbody>
              </table>
            )}
          </section>
        </div>

        <div>
          <section className="card">
            <h2>Ticket</h2>
            <dl>
              <dt>Came in by</dt><dd>{l.channels[t.channel]} {t.channel}</dd>
              <dt>Owner</dt><dd>{t.assignedTo ?? 'Unassigned'}{team(t.assignedToId) && <div className="muted">{team(t.assignedToId)}{t.assignedCompany && ` · ${t.assignedCompany}`}</div>}</dd>
              <dt>Joined</dt><dd>{t.watchers.map((w) => w.name).join(', ') || '—'}</dd>
              <dt>Priority</dt><dd><PriorityPill lists={l} value={t.priority} /></dd>
              <dt>First reply</dt><dd>{t.firstResponseAt ? dateTime(t.firstResponseAt) : '—'}</dd>
              {t.solvedAt && <><dt>Solved</dt><dd>{dateTime(t.solvedAt)}</dd></>}
              {t.rating && <><dt>Rating</dt><dd><Pill tone={t.rating === 'good' ? 'ok' : 'bad'}>{t.rating === 'good' ? '👍 Good' : '👎 Bad'}</Pill>{t.ratingComment && <div>“{t.ratingComment}”</div>}</dd></>}
              {t.managerRequestedAt && <><dt>Manager</dt><dd><Pill tone="warn">Requested</Pill> by {t.managerRequestedBy}, {dateTime(t.managerRequestedAt)}{t.managerRequestNote && <div className="muted">{t.managerRequestNote}</div>}</dd></>}
            </dl>
            {canWork && (
              <div className="row">
                <label>Status<select value={t.status} onChange={(e) => update({ status: e.target.value })}>{Object.entries(l.statuses).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></label>
                <label>Priority<select value={t.priority} onChange={(e) => update({ priority: e.target.value })}>{Object.entries(l.priorities).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></label>
                {t.manager && (
                  <label>Owner<select value={t.assignedToId ?? ''} onChange={(e) => e.target.value && update({ assignedTo: Number(e.target.value) })}>
                    <option value="">Unassigned</option>
                    {t.agents.map((a) => <option key={a.userId} value={a.userId}>{a.name} ({l.teams[a.team]})</option>)}
                  </select></label>
                )}
              </div>
            )}
          </section>

          <section className="card">
            <h2>Customer</h2>
            {t.customer ? (
              <dl>
                <dt>Name</dt><dd>{can('corral') ? <Link to={`/corral/customers/${t.customer.id}`}>{t.customer.name}</Link> : t.customer.name}</dd>
                <dt>Account</dt><dd className="mono">#{t.customer.account}</dd>
                <dt>Status</dt><dd>{t.customer.status}</dd>
                <dt>Plan</dt><dd>{t.customer.plan ?? '—'}</dd>
                <dt>Email</dt><dd>{t.customer.email ?? '—'}</dd>
                <dt>Phone</dt><dd>{t.customer.phone ?? '—'}</dd>
                <dt>Address</dt><dd>{t.customer.address}</dd>
                <dt>Tickets</dt><dd><Link to={`/ranger/tickets?view=all&customer=${t.customer.id}`}>{t.otherTickets} in all</Link></dd>
              </dl>
            ) : <p>{t.name}<br /><a href={`mailto:${t.email}`}>{t.email}</a><br /><span className="muted">No account matched.</span></p>}
          </section>

          <section className="card">
            <h2>Attachments</h2>
            {t.attachments.length === 0 ? <p className="muted">None.</p> : t.attachments.map((a) => (
              <p key={a.id}><Link to={`/ranger/tickets/${t.id}/files/${a.id}`}>{a.isImage ? '🖼' : a.isPdf ? '📄' : '📎'} {a.name}</Link> <span className="muted">{size(a.size)} · {dateTime(a.createdAt)}</span></p>
            ))}
          </section>
        </div>
      </div>
    </>
  );
}
