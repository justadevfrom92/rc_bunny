import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { api, date, useLoad } from '../../api';
import { useAuth } from '../../auth';

interface Message { id: number; author: string; fromCustomer: boolean; internal: boolean; body: string; createdAt: string }
interface TicketData {
  id: number; subject: string; name: string; email: string; topic: string; status: string; priority: string; createdAt: string;
  rating: string | null; ratingComment: string | null; assignedToId: number | null; assignedTo: string | null;
  customer: { id: number; account: string; name: string } | null;
  messages: Message[]; statuses: string[]; isManager: boolean; team: { userId: number; name: string }[];
}

export default function Ticket() {
  const { id } = useParams();
  const { me, can } = useAuth();
  const { data: t, error, reload } = useLoad<TicketData>(`/api/ranger/tickets/${id}`);
  const [body, setBody] = useState('');
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!t) return <p>Loading…</p>;

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); }, (e: Error) => setProblem(e.message));
  const send = (internal: boolean) => (e: FormEvent) => {
    e.preventDefault();
    run(api.post(`/api/ranger/tickets/${t.id}/messages`, { body, internal }).then(() => setBody('')));
  };
  const update = (changes: Partial<Pick<TicketData, 'status' | 'priority'>>) => run(api.put(`/api/ranger/tickets/${t.id}`, { status: t.status, priority: t.priority, ...changes }));
  const assign = (userId?: number) => run(api.post(`/api/ranger/tickets/${t.id}/assign${userId ? `?userId=${userId}` : ''}`));

  return (
    <>
      <p><Link to="/ranger/tickets">← Tickets</Link></p>
      <h1>#{t.id} {t.subject}</h1>
      <p className="muted">
        {t.name} · <a href={`mailto:${t.email}`}>{t.email}</a> · {t.topic} · opened {date(t.createdAt)}
        {t.customer && <> · {can('corral') ? <Link to={`/corral/customers/${t.customer.id}`}>account #{t.customer.account}</Link> : `account #${t.customer.account}`}</>}
      </p>
      {problem && <p className="error">{problem}</p>}

      <div className="card row">
        <label>Status<select value={t.status} onChange={(e) => update({ status: e.target.value })}>{t.statuses.map((s) => <option key={s}>{s}</option>)}</select></label>
        <label>Priority<select value={t.priority} onChange={(e) => update({ priority: e.target.value })}><option>normal</option><option>high</option></select></label>
        <span>Assigned to <b>{t.assignedTo ?? 'nobody'}</b></span>
        {t.assignedToId === null && <button onClick={() => assign()}>Pick up</button>}
        {t.isManager && t.assignedToId !== null && t.assignedToId !== me!.id && <button onClick={() => assign()}>Take over</button>}
        {t.isManager && (
          <select value="" onChange={(e) => assign(Number(e.target.value))}>
            <option value="">Assign to…</option>
            {t.team.map((a) => <option key={a.userId} value={a.userId}>{a.name}</option>)}
          </select>
        )}
      </div>

      {t.rating && <p className="card">Customer rated this <b>{t.rating}</b>{t.ratingComment && `: "${t.ratingComment}"`}</p>}

      {t.messages.map((m) => (
        <div key={m.id} className={`card ticket-msg ${m.internal ? 'internal' : m.fromCustomer ? 'customer' : ''}`}>
          <small>{m.internal ? 'Internal note · ' : ''}{m.author} · {new Date(m.createdAt).toLocaleString('en-US')}</small>
          <p>{m.body}</p>
        </div>
      ))}

      <form className="card" onSubmit={send(false)}>
        <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={4} placeholder="Write a reply to the customer, or an internal note" />
        <div className="row">
          <button className="primary" disabled={!body.trim()}>Send reply</button>
          <button type="button" disabled={!body.trim()} onClick={send(true)}>Add internal note</button>
        </div>
      </form>
    </>
  );
}
