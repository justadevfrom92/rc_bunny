import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, dateTime, money, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, age, duration, shortTime, statusTone, useRun } from '../../ui';
import { StatusPill, type Lists } from './shared';

interface File { id: number; name: string; mime: string; size: number; isImage: boolean; isPdf: boolean; createdAt: string }
interface Message {
  id: number; kind: string; author: string; userId: number | null; via: string; direction: string | null; durationSec: number | null; contact: string | null;
  body: string; createdAt: string; files: File[];
}
interface Note { id: number; createdAt: string; author: string; category: string | null; action: string | null; priority: string | null; body: string }
interface Data {
  id: number; number: string; subject: string; name: string; email: string; topic: string; topicLabel: string; channel: string; status: string; priority: string;
  createdAt: string; firstResponseAt: string | null; solvedAt: string | null; rating: string | null; ratingComment: string | null;
  assignedToId: number | null; assignedTo: string | null; assignedCompany: string | null;
  managerRequestedAt: string | null;
  customer: { id: number; account: string; name: string; email: string | null; phone: string | null; status: string; plan: string | null; balance: number } | null;
  history: number; messages: Message[]; notes: Note[]; attachments: { file: File; by: string }[];
  watchers: { userId: number; name: string }[]; teamOf: Record<string, string>;
  me: number; manager: boolean; agents: { userId: number; name: string; team: string; level: string }[];
  noteCategories: Record<string, string[]>; priorities: string[]; lists: Lists;
}

export const size = (n: number) => (n >= 1048576 ? `${(n / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`);
export const icon = (f: { isImage: boolean; isPdf: boolean }) => (f.isImage ? '🖼' : f.isPdf ? '📄' : '📎');
const dt = (s: string) => dateTime(s);

/**
 * One ticket (ebunny's ranger/ticket): its activity (My Account messages, emails and phone calls, with attachments),
 * the reply box, the notes, and on the side the ticket, Assign To for managers, the customer and the attachments.
 */
export default function Ticket() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data: t, error, reload } = useLoad<Data>(`/api/ranger/tickets/${id}`);
  const [run, bar] = useRun(reload);
  const [category, setCategory] = useState('Ticket');
  const [find, setFind] = useState('');
  if (error) return <div className="flash bad">{error}</div>;
  if (!t) return <div className="empty">Loading…</div>;

  const l = t.lists;
  const owner = t.assignedToId === t.me;
  const joined = t.watchers.some((w) => w.userId === t.me);
  const canWork = !t.assignedToId || owner || joined || t.manager;
  const done = t.status === 'solved' || t.status === 'closed';
  const url = `/api/ranger/tickets/${t.id}`;
  const post = (path: string, body?: unknown) => run(api.post(`${url}/${path}`, body));
  const team = (userId: number | null) => (userId !== null ? t.teamOf[String(userId)] : undefined);
  const data = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); return { el: e.currentTarget, f: new FormData(e.currentTarget) }; };

  const reply = (e: FormEvent<HTMLFormElement>) => {
    const { el, f } = data(e);
    run(api.post(`${url}/reply`, { body: f.get('body'), via: f.get('via'), direction: f.get('direction'), minutes: Number(f.get('minutes') || 0), status: f.get('status') || null }).then((r) => { el.reset(); return r; }));
  };
  const note = (e: FormEvent<HTMLFormElement>) => {
    const { el, f } = data(e);
    run(api.post(`${url}/notes`, { body: f.get('body'), category: f.get('category'), action: f.get('action'), priority: f.get('priority') }).then((r) => { el.reset(); return r; }));
  };
  const ask = (e: FormEvent<HTMLFormElement>) => { const { f } = data(e); window.location.hash = ''; post('ask-manager', { reason: f.get('reason') }); };
  const save = (e: FormEvent<HTMLFormElement>) => { const { f } = data(e); run(api.put(url, { status: f.get('status'), priority: f.get('priority') })); };
  const assign = (e: FormEvent<HTMLFormElement>) => { const { f } = data(e); if (f.get('assigned_to')) run(api.put(url, { assignedTo: Number(f.get('assigned_to')) })); };

  const entries = t.messages.filter((m) => m.kind !== 'system');
  const calls = entries.filter((m) => m.via === 'phone').length, emails = entries.filter((m) => m.via === 'email').length, msgs = entries.length - calls - emails;
  const plural = (n: number, w: string) => `${n} ${w}${n === 1 ? '' : 's'}`;
  const counts = [msgs && plural(msgs, 'message'), emails && plural(emails, 'email'), calls && plural(calls, 'call')].filter(Boolean).join(' · ');
  const firstReply = !t.firstResponseAt ? 'Not yet' : new Date(t.firstResponseAt).getTime() - new Date(t.createdAt).getTime() < 60000 ? 'Answered live on the call' : `${age(t.createdAt, t.firstResponseAt)} later`;

  return (
    <>
      <PageHead title={t.subject}
        sub={<><span className="mono">{t.number}</span> · <StatusPill lists={l} value={t.status} /> · {t.topicLabel} · opened {dt(t.createdAt)} from {l.channels[t.channel] ?? ''} {t.channel}</>}
        actions={(t.manager && !owner) || joined || !t.assignedToId || (!t.manager && canWork && !done) ? <>
          {t.manager && !owner && <button className="btn cyan" onClick={() => confirm(`Take over ${t.number}?\nIt becomes yours${t.assignedTo ? `; ${t.assignedTo} stays on it as a watcher` : ''}.`) && post('take-over')}>Take Over</button>}
          {t.manager && !owner && !joined && t.assignedToId && <button className="btn ghost" onClick={() => post('join')}>Join In</button>}
          {joined && <button className="btn ghost" onClick={() => post('leave')}>Leave</button>}
          {!t.assignedToId && <button className="btn cyan" onClick={() => run(api.put(url, { assignedTo: t.me }))}>Pick Up</button>}
          {!t.manager && canWork && !done && (t.managerRequestedAt
            ? <span className="inline"><span className="pill warn">Manager requested</span> <button className="btn ghost" onClick={() => post('cancel-manager')}>No Longer Needed</button></span>
            : <a className="btn cyan" href="#need-help">Need Help?</a>)}
        </> : undefined} />
      {bar}

      {!t.manager && canWork && !done && !t.managerRequestedAt && (
        <form className="panel need-help" id="need-help" onSubmit={ask}>
          <div className="panel-head"><h2>Ask for a Manager</h2><a className="btn sm ghost" href="#">Cancel</a></div>
          <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <span className="help">A manager can assign this ticket to someone else, take it over, or join in. Your reason is saved as a note.</span>
            <input name="reason" maxLength={500} aria-label="Why" placeholder="Why (optional), e.g. customer wants a refund over $100" />
            <div><button className="btn cyan">Ask for a Manager</button></div>
          </div>
        </form>
      )}

      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20, minWidth: 0 }}>
          <div className="panel"><div className="panel-head"><h2>Activity</h2><span className="muted">{counts || 'Nothing yet'}</span></div>
            <div className="panel-body rt-thread">
              {t.messages.map((m) => m.kind === 'system' ? <p key={m.id} className="rt-system">{m.body} <span>{shortTime(m.createdAt)}</span></p>
                : m.via === 'phone' ? (
                  <div key={m.id} className="rt-msg rt-call">
                    <div className="rt-who"><b>📞 {m.direction === 'in' ? 'Inbound call' : 'Outbound call'}</b><span>{m.durationSec ? duration(m.durationSec) : '—'}</span>{m.contact && <span className="help">{m.contact}</span>}
                      <span className="help">{m.author || 'Support'}{team(m.userId) ? ` · ${team(m.userId)}` : ''} · {dt(m.createdAt)}</span></div>
                    <div className="rt-body" style={{ whiteSpace: 'pre-line' }}>{m.body}</div>
                  </div>
                ) : (
                  <div key={m.id} className={`rt-msg rt-${m.kind}`}>
                    <div className="rt-who"><b>{m.kind === 'customer' ? t.name : m.author || 'Support'}</b>
                      {m.kind === 'note' ? <span className="pill warn">Note</span>
                        : <span className="help">{({ portal: '💬', email: '✉', phone: '📞' } as Record<string, string>)[m.via] ?? ''} {m.via === 'email' ? `${m.kind === 'customer' ? 'Email from ' : 'Email to '}${m.contact ?? t.email}` : 'My Account'}</span>}
                      {m.kind === 'agent' && team(m.userId) && <span className="help">{team(m.userId)}</span>}
                      <span className="help">{dt(m.createdAt)}</span></div>
                    <div className="rt-body" style={{ whiteSpace: 'pre-line' }}>{m.body}</div>
                    {m.files.length > 0 && <div className="rt-files">{m.files.map((a) => <Link key={a.id} to={`/ranger/tickets/${t.id}/files/${a.id}`}>{icon(a)} {a.name} <span className="help">{size(a.size)}</span></Link>)}</div>}
                  </div>
                ))}
            </div>
          </div>

          {canWork ? (
            <form className="panel rt-reply" onSubmit={reply}>
              <div className="panel-head"><h2>Reply to the Customer</h2></div>
              <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <div className="field rt-agent-only"><label>Send by</label><div className="chips small">
                  <label><input type="radio" name="via" value="portal" defaultChecked={t.channel !== 'Email' && t.channel !== 'Phone'} /><span>💬 My Account</span></label>
                  <label><input type="radio" name="via" value="email" defaultChecked={t.channel === 'Email'} /><span>✉ Email to {t.email}</span></label>
                  <label><input type="radio" name="via" value="phone" defaultChecked={t.channel === 'Phone'} /><span>📞 Log a phone call</span></label></div></div>
                <div className="rt-phone-only" style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                  <div className="field"><label>Call</label><div className="chips small"><label><input type="radio" name="direction" value="out" defaultChecked /><span>We called them</span></label><label><input type="radio" name="direction" value="in" /><span>They called us</span></label></div></div>
                  <div className="field"><label htmlFor="rt-min">Minutes</label><input id="rt-min" name="minutes" type="number" min={0} max={600} defaultValue={5} style={{ width: 90 }} /></div>
                </div>
                <textarea name="body" required maxLength={5000} style={{ minHeight: 120 }} aria-label="Message" placeholder={`Hi ${t.name.split(' ')[0]}, …`} />
                <div className="field" style={{ maxWidth: 280 }}><label htmlFor="rt-then">Then set the ticket to</label><select id="rt-then" name="status" defaultValue=""><option value="">Leave as is</option><option value="waiting">Waiting on Customer</option><option value="solved">Solved</option></select></div>
                <div><button className="btn cyan">Save</button> <span className="help">Messages and emails reach the customer; call notes stay with the team.</span></div>
              </div>
            </form>
          ) : <div className="alert info"><b>{t.assignedTo} has this ticket.</b> A manager can join you in or take it over.</div>}

          <div className="panel" id="notes"><div className="panel-head"><h2>Notes</h2><span className="muted">{t.notes.length} · staff only, also on the account's notes</span></div>
            {t.customer && (
              <form className="panel-body rt-note-form" style={{ display: 'flex', flexDirection: 'column', gap: 12 }} onSubmit={note}>
                <div className="form-row note-selects">
                  <div className="field"><label htmlFor="rt-cat">Category</label><select id="rt-cat" name="category" required value={category} onChange={(e) => setCategory(e.target.value)}>{Object.keys(t.noteCategories).map((c) => <option key={c}>{c}</option>)}</select></div>
                  <div className="field"><label htmlFor="rt-act">Action</label><select id="rt-act" name="action" required key={category} defaultValue={category === 'Ticket' ? 'Internal Note' : undefined}>
                    <optgroup label={category}>{t.noteCategories[category].map((a) => <option key={a}>{a}</option>)}</optgroup></select></div>
                  <div className="field"><label htmlFor="rt-pri">Priority</label><select id="rt-pri" name="priority" required defaultValue="Low">{t.priorities.map((p) => <option key={p}>{p}</option>)}</select></div>
                </div>
                <div className="field"><label htmlFor="rt-note">Note</label><textarea id="rt-note" name="body" required maxLength={5000} style={{ minHeight: 90 }} placeholder="What the team should know about this ticket" /></div>
                <div><button className="btn">Add Note</button></div>
              </form>
            )}
            <div className="table-wrap"><table data-page="10">
              <thead><tr><th>ID</th><th>Added</th><th>By</th><th>Category / Action</th><th>Priority</th><th>Note</th></tr></thead>
              <tbody>
                {t.notes.map((n) => (
                  <tr key={n.id} className="click" onClick={() => navigate(`/ranger/records/note/${n.id}`)}><td className="nowrap"><Link to={`/ranger/records/note/${n.id}`}><b>#{n.id}</b></Link></td><td className="nowrap">{dt(n.createdAt)}</td><td>{n.author}</td>
                    <td>{n.category ?? '—'}{n.action && <div className="help">{n.action}</div>}</td>
                    <td>{n.priority ? <span className={`pill ${({ High: 'bad', Medium: 'warn' } as Record<string, string>)[n.priority] ?? ''}`}>{n.priority}</span> : '—'}</td>
                    <td className="wrap" style={{ minWidth: 160 }}>{n.body.length > 200 ? n.body.slice(0, 197) + '...' : n.body}</td></tr>
                ))}
                {t.notes.length === 0 && <EmptyRow cols={6}>No notes yet.</EmptyRow>}
              </tbody>
            </table></div>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 20, minWidth: 0 }}>
          <div className="panel"><div className="panel-head"><h2>Ticket</h2></div><div className="panel-body">
            <dl className="kv">
              <dt>Came in by</dt><dd>{l.channels[t.channel] ?? ''} {t.channel}</dd>
              <dt>Owner</dt><dd>{t.assignedTo ?? 'Unassigned'}{t.assignedToId && team(t.assignedToId) && <div className="help">{team(t.assignedToId)}{t.assignedCompany && ` · ${t.assignedCompany}`}</div>}</dd>
              <dt>Joined</dt><dd>{t.watchers.map((w) => w.name).join(', ') || '—'}</dd>
              <dt>First reply</dt><dd>{firstReply}</dd>
              {t.solvedAt && <><dt>Solved</dt><dd>{dt(t.solvedAt)}</dd></>}
              <dt>Rating</dt><dd>{t.rating ? <>{t.rating === 'good' ? '👍 Helped' : "👎 Didn't help"}{t.ratingComment && <div className="help">“{t.ratingComment}”</div>}</> : <span className="muted">{done ? 'Not rated yet' : 'Asked when solved'}</span>}</dd>
            </dl>
          </div>
            <form className="panel-body" style={{ borderTop: '1px solid var(--line)', display: 'flex', flexDirection: 'column', gap: 12 }} onSubmit={save} key={`${t.status}${t.priority}`}>
              <div className="field"><label htmlFor="rt-status">Status</label><select id="rt-status" name="status" defaultValue={t.status}>{Object.entries(l.statuses).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></div>
              <div className="field"><label htmlFor="rt-priority">Priority</label><select id="rt-priority" name="priority" defaultValue={t.priority}>{Object.entries(l.priorities).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></div>
              <div><button className="btn">Save</button></div>
            </form>
          </div>

          {t.manager && (
            <form className="panel" onSubmit={assign} key={t.assignedToId ?? 0}>
              <div className="panel-head"><h2>Assign To</h2><span className="help">managers &amp; admins</span></div>
              <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <input type="search" className="search-box" placeholder="Find someone" aria-label="Find a team member" value={find} onChange={(e) => setFind(e.target.value)} />
                <div className="table-wrap pick-list" style={{ maxHeight: 220 }} id="rt-assign"><table className="table"><tbody>
                  {t.agents.map((a) => (
                    <tr key={a.userId} data-filter-row={`${a.name} ${l.teams[a.team]}`.toLowerCase()} hidden={!!find && !`${a.name} ${l.teams[a.team]}`.toLowerCase().includes(find.toLowerCase())}>
                      <td style={{ width: 32 }}><input type="radio" id={`as${a.userId}`} name="assigned_to" value={a.userId} defaultChecked={t.assignedToId === a.userId} /></td>
                      <td><label htmlFor={`as${a.userId}`}><b>{a.name}</b></label> <span className="help">{l.teams[a.team]}{a.level === 'manager' ? ' · Manager' : ''}</span></td></tr>
                  ))}
                </tbody></table></div>
                <div><button className="btn">Assign</button></div>
              </div>
            </form>
          )}

          <div className="panel"><div className="panel-head"><h2>Customer</h2>{t.customer && <Link className="btn sm ghost" to={`/ranger/tickets?view=all&customer=${t.customer.id}`}>Ticket History · {t.history}</Link>}</div><div className="panel-body"><dl className="kv">
            <dt>Name</dt><dd>{t.name}</dd>
            <dt>Email</dt><dd>{t.email}</dd>
            {t.customer && <>
              <dt>Account</dt><dd><AccountNo id={t.customer.id} account={t.customer.account} /></dd>
              <dt>Phone</dt><dd>{t.customer.phone ?? '—'}</dd>
              <dt>Status</dt><dd><span className={`pill ${statusTone(t.customer.status)}`}>{t.customer.status}</span></dd>
              <dt>Plan</dt><dd>{t.customer.plan ?? '—'}</dd>
              <dt>Balance</dt><dd>{money(t.customer.balance)}</dd>
            </>}
          </dl></div></div>

          {t.attachments.length > 0 && (
            <div className="panel"><div className="panel-head"><h2>Attachments</h2><span className="muted">{t.attachments.length}</span></div><div className="table-wrap"><table data-page="10"><tbody>
              {t.attachments.map(({ file: a, by }) => (
                <tr key={a.id} className="click" onClick={() => navigate(`/ranger/tickets/${t.id}/files/${a.id}`)}><td className="wrap"><Link to={`/ranger/tickets/${t.id}/files/${a.id}`}>{icon(a)} {a.name}</Link>
                  <div className="help">{size(a.size)} · {by === t.name ? `from ${t.name}` : `sent by ${by}`} · {new Date(a.createdAt).toLocaleDateString('en-US')}</div></td></tr>
              ))}
            </tbody></table></div></div>
          )}
        </div>
      </div>
    </>
  );
}
