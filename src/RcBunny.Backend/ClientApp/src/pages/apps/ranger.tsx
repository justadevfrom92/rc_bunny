import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { api, post, put, qs } from '../../api';
import { date, dateTime, ErrorBox, money, Panel, pct, Pill, Refreshing, Skeleton, Stat, Stats, Bars, Columns } from '../../ui';

const tone = (p: string) => (p === 'urgent' ? 'bad' : p === 'high' ? 'warn' : p === 'low' ? '' : 'info');

interface Dash { open: number; unassigned: number; urgent: number; mine: number; waiting: number; managerRequests: number; solved30: number; avgFirstResponseMinutes: number; approval: number | null; byTopic: { topic: string; n: number }[]; isManager: boolean; messages: number }

export function Dashboard() {
  const q = useQuery({ queryKey: ['ranger', 'dashboard'], queryFn: () => api<Dash>('/api/ranger/dashboard'), refetchInterval: 30_000 });
  const d = q.data;
  return (
    <>
      <h1>Ranger</h1>
      {!d ? <Skeleton rows={4} /> : (
        <>
          <Stats>
            <Stat label="Open tickets" value={d.open} to="/ranger/tickets" />
            <Stat label="Unassigned" value={d.unassigned} to="/ranger/tickets?view=unassigned" tone={d.unassigned ? 'warn' : undefined} />
            <Stat label="Urgent / high" value={d.urgent} to="/ranger/tickets?view=urgent" tone={d.urgent ? 'bad' : undefined} />
            <Stat label="Mine" value={d.mine} to="/ranger/tickets?view=mine" />
            <Stat label="Manager requests" value={d.managerRequests} to="/ranger/tickets?view=manager" />
            <Stat label="Web messages" value={d.messages} to="/ranger/r/contact-messages" />
          </Stats>
          <div className="grid-2">
            <Panel title="Last 30 days">
              <dl className="facts">
                <dt>Solved</dt><dd>{d.solved30}</dd>
                <dt>First reply</dt><dd>{d.avgFirstResponseMinutes < 120 ? `${d.avgFirstResponseMinutes} min` : `${Math.round(d.avgFirstResponseMinutes / 60)} h`} on average</dd>
                <dt>Approval</dt><dd><Link to="/ranger/satisfaction">{pct(d.approval)}</Link></dd>
                <dt>Waiting on customer</dt><dd>{d.waiting}</dd>
              </dl>
            </Panel>
            <Panel title="Open by topic"><Bars rows={d.byTopic.map((t) => ({ label: t.topic, value: t.n }))} /></Panel>
          </div>
        </>
      )}
    </>
  );
}

interface TicketRow { id: number; subject: string; name: string; status: string; priority: string; channel: string; topic: string; lastActivityAt: string; createdAt: string; assignee: string | null; manager: boolean; rating: string | null }

const VIEWS = [['open', 'Open'], ['mine', 'Mine'], ['unassigned', 'Unassigned'], ['urgent', 'Urgent'], ['joined', 'Joined'], ['manager', 'Manager requests'], ['solved', 'Solved'], ['all', 'All']];

export function Tickets() {
  const [params, setParams] = useSearchParams();
  const view = params.get('view') ?? 'open';
  const [q, setQ] = useState('');
  const list = useQuery({ queryKey: ['ranger', 'tickets', view, q], queryFn: () => api<{ rows: TicketRow[]; total: number }>(`/api/ranger/tickets${qs({ view, q })}`), refetchInterval: 30_000 });
  return (
    <>
      <h1>Tickets</h1>
      <div className="tabs">{VIEWS.map(([k, l]) => <button key={k} className={view === k ? 'on' : ''} onClick={() => setParams({ view: k })}>{l}</button>)}</div>
      <Panel actions={<Refreshing on={list.isFetching && !list.isPending} />}>
        <div className="filters"><input type="search" placeholder="Subject, name, email…" value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search tickets" /></div>
        {list.isPending ? <Skeleton rows={10} cols={5} /> : list.data!.rows.length === 0 ? <p className="muted">No tickets here.</p> : (
          <table className="table">
            <thead><tr><th>Subject</th><th>Customer</th><th>Status</th><th>Priority</th><th>Assigned</th><th>Last activity</th></tr></thead>
            <tbody>
              {list.data!.rows.map((t) => (
                <tr key={t.id}>
                  <td><Link to={`/ranger/tickets/${t.id}`}>{t.subject}</Link><div className="muted small">#{t.id} · {t.channel} · {t.topic}{t.manager && <> · <Pill tone="warn">manager asked</Pill></>}</div></td>
                  <td>{t.name}</td>
                  <td><Pill tone={t.status === 'new' ? 'info' : t.status === 'solved' ? 'ok' : ''}>{t.status}</Pill></td>
                  <td><Pill tone={tone(t.priority)}>{t.priority}</Pill></td>
                  <td>{t.assignee ?? <span className="muted">—</span>}</td>
                  <td>{dateTime(t.lastActivityAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {list.data && <p className="muted small">{list.data.total} tickets</p>}
      </Panel>
    </>
  );
}

interface TicketDetail {
  ticket: { id: number; subject: string; name: string; email: string; status: string; priority: string; topic: string; channel: string; assignedTo: number | null; createdAt: string; firstResponseAt: string | null; solvedAt: string | null; rating: string | null; ratingComment: string | null; managerRequestedAt: string | null; managerRequestNote: string | null };
  customer: { id: number; account: string; name: string; status: string; balance: number; plan: string | null; phone: string | null; email: string | null } | null;
  timeline: { id: number; author: string; body: string; via: string | null; direction: string | null; duration: number | null; createdAt: string; who: string | null; kind: string }[];
  files: { id: number; name: string; mime: string; size: number; fromCustomer: boolean; createdAt: string }[];
  watchers: { userId: number; name: string }[];
  team: { userId: number; name: string; team: string; level: string; company: string | null }[];
  can: { manage: boolean; joined: boolean; mine: boolean };
}

export function Ticket() {
  const id = Number(useParams().id);
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['ranger', 'ticket', id], queryFn: () => api<TicketDetail>(`/api/ranger/tickets/${id}`), refetchInterval: 20_000 });
  const [reply, setReply] = useState('');
  const [note, setNote] = useState('');
  const [mode, setMode] = useState<'reply' | 'note'>('reply');
  const done = () => qc.invalidateQueries({ queryKey: ['ranger'] });
  const act = useMutation({ mutationFn: ({ path, body, method }: { path: string; body?: unknown; method?: string }) => method === 'PATCH' ? api(`/api/ranger/tickets/${id}${path}`, { method: 'PATCH', json: body }) : post(`/api/ranger/tickets/${id}${path}`, body), onSuccess: done });

  if (q.isPending) return <div className="panel"><Skeleton rows={10} /></div>;
  if (q.error || !q.data) return <ErrorBox error={q.error} />;
  const { ticket: t, customer: c, timeline, files, watchers, team, can } = q.data;

  const send = (e: FormEvent, status?: string) => {
    e.preventDefault();
    if (mode === 'reply') act.mutate({ path: '/reply', body: { body: reply, status } }, { onSuccess: () => setReply('') });
    else act.mutate({ path: '/notes', body: { body: note } }, { onSuccess: () => setNote('') });
  };

  return (
    <>
      <p className="crumbs"><Link to="/ranger/tickets">Tickets</Link> › #{t.id}</p>
      <div className="page-head">
        <div>
          <h1>{t.subject}</h1>
          <p className="muted">{t.name} · {t.email} · {t.channel} · opened {dateTime(t.createdAt)}</p>
        </div>
        <div className="row">
          {can.manage && !can.mine && <button className="btn outline" onClick={() => act.mutate({ path: '/take-over' })}>Take Over</button>}
          {can.manage && !can.joined && <button className="btn outline" onClick={() => act.mutate({ path: '/join' })}>Join In</button>}
          {can.joined && <button className="btn outline" onClick={() => act.mutate({ path: '/leave' })}>Leave</button>}
          {!can.manage && !t.managerRequestedAt && <button className="btn outline" onClick={() => { const n = prompt('Why do you need a manager?'); if (n !== null) act.mutate({ path: '/ask-manager', body: { note: n } }); }}>Ask a Manager</button>}
          {t.managerRequestedAt && <button className="btn outline" onClick={() => act.mutate({ path: '/cancel-manager' })}>Cancel Manager Request</button>}
        </div>
      </div>
      <ErrorBox error={act.error} />
      {t.managerRequestedAt && <div className="alert warn">Manager requested {dateTime(t.managerRequestedAt)}{t.managerRequestNote && <>: {t.managerRequestNote}</>}</div>}
      <div className="grid-2" style={{ gridTemplateColumns: '2fr 1fr' }}>
        <Panel title="Conversation">
          <div className="chat">
            {timeline.map((m) => (
              <div key={`${m.kind}${m.id}`} className={`bubble ${m.kind === 'note' ? 'note' : m.author === 'system' ? 'system' : m.author === 'customer' ? '' : 'me'}`}>
                {m.author !== 'system' && <small>{m.kind === 'note' ? `Internal note · ${m.who}` : m.author === 'customer' ? t.name : m.who} · {dateTime(m.createdAt)}{m.via === 'phone' && ` · call ${m.direction === 'in' ? 'in' : 'out'} ${Math.round((m.duration ?? 0) / 60)} min`}{m.via === 'email' && ' · email'}</small>}
                {m.body}
              </div>
            ))}
          </div>
          <form className="note-form" style={{ marginTop: 16 }} onSubmit={(e) => send(e)}>
            <div className="tabs">
              <button type="button" className={mode === 'reply' ? 'on' : ''} onClick={() => setMode('reply')}>Reply to customer</button>
              <button type="button" className={mode === 'note' ? 'on' : ''} onClick={() => setMode('note')}>Internal note</button>
            </div>
            {mode === 'reply'
              ? <textarea rows={4} value={reply} onChange={(e) => setReply(e.target.value)} placeholder="The customer sees this." required />
              : <textarea rows={3} value={note} onChange={(e) => setNote(e.target.value)} placeholder="Only staff see this (also on the account)." required />}
            <div className="row">
              <button className="btn" disabled={act.isPending}>{mode === 'reply' ? 'Send' : 'Add note'}</button>
              {mode === 'reply' && <button type="button" className="btn outline" onClick={(e) => send(e, 'waiting')}>Send & wait for customer</button>}
              {mode === 'reply' && <button type="button" className="btn outline" onClick={(e) => send(e, 'solved')}>Send & solve</button>}
            </div>
          </form>
        </Panel>
        <div>
          <Panel title="Ticket">
            <dl className="facts">
              <dt>Status</dt>
              <dd><select value={t.status} onChange={(e) => act.mutate({ path: '', method: 'PATCH', body: { status: e.target.value } })} aria-label="Status">{['new', 'open', 'waiting', 'solved', 'closed'].map((s) => <option key={s}>{s}</option>)}</select></dd>
              <dt>Priority</dt>
              <dd><select value={t.priority} onChange={(e) => act.mutate({ path: '', method: 'PATCH', body: { priority: e.target.value } })} aria-label="Priority">{['urgent', 'high', 'normal', 'low'].map((s) => <option key={s}>{s}</option>)}</select></dd>
              <dt>Assigned</dt>
              <dd>
                {can.manage ? (
                  <select value={t.assignedTo ?? ''} onChange={(e) => e.target.value && act.mutate({ path: '', method: 'PATCH', body: { assignedTo: Number(e.target.value) } })} aria-label="Assigned to">
                    <option value="">Nobody</option>
                    {team.map((m) => <option key={m.userId} value={m.userId}>{m.name}{m.company ? ` (${m.company})` : ''}</option>)}
                  </select>
                ) : team.find((m) => m.userId === t.assignedTo)?.name ?? 'Nobody'}
              </dd>
              <dt>Joined in</dt><dd>{watchers.map((w) => w.name).join(', ') || '—'}</dd>
              <dt>First reply</dt><dd>{dateTime(t.firstResponseAt)}</dd>
              {t.rating && <><dt>Rating</dt><dd>{t.rating === 'good' ? '👍' : '👎'} {t.ratingComment}</dd></>}
            </dl>
          </Panel>
          {c && (
            <Panel title="Account">
              <dl className="facts">
                <dt>Account</dt><dd><Link to={`/corral/customers/${c.id}`}>{c.account}</Link></dd>
                <dt>Status</dt><dd>{c.status}</dd>
                <dt>Plan</dt><dd>{c.plan ?? '—'}</dd>
                <dt>Balance</dt><dd>{money(c.balance)}</dd>
                <dt>Phone</dt><dd>{c.phone ?? '—'}</dd>
              </dl>
            </Panel>
          )}
          {files.length > 0 && (
            <Panel title="Files">
              <ul className="notes">{files.map((f) => <li key={f.id}><span>{f.name}</span><span className="muted small">{f.fromCustomer ? 'from customer' : 'from staff'} · {Math.round(f.size / 1024)} KB · {date(f.createdAt)}</span></li>)}</ul>
            </Panel>
          )}
        </div>
      </div>
    </>
  );
}

interface Sat { office: number; thirdParty: number; rated: number; byWeek: { week: string; office: number; thirdParty: number; n: number }[]; byPerson: { userId: number; name: string; team: string; rated: number; approval: number }[]; recent: { id: number; rating: string; ratingComment: string; name: string; ratedAt: string }[] }

export function Satisfaction() {
  const q = useQuery({ queryKey: ['ranger', 'satisfaction'], queryFn: () => api<Sat>('/api/ranger/satisfaction') });
  const d = q.data;
  return (
    <>
      <h1>Approval Ratings</h1>
      <p className="muted">👍 out of rated tickets, last 12 weeks.</p>
      {!d ? <Skeleton rows={6} /> : (
        <>
          <Stats><Stat label="In-office" value={pct(d.office)} /><Stat label="3rd party" value={pct(d.thirdParty)} /><Stat label="Rated tickets" value={d.rated} /></Stats>
          <div className="grid-2">
            <Panel title="In-office by week"><Columns rows={d.byWeek.map((w) => ({ label: w.week.slice(5), value: w.office }))} format={(n) => `${n}%`} /></Panel>
            <Panel title="3rd party by week"><Columns rows={d.byWeek.map((w) => ({ label: w.week.slice(5), value: w.thirdParty }))} format={(n) => `${n}%`} /></Panel>
          </div>
          <Panel title="By person">
            <table className="table compact"><thead><tr><th>Person</th><th>Team</th><th className="num">Rated</th><th className="num">Approval</th></tr></thead>
              <tbody>{d.byPerson.map((p) => <tr key={p.userId}><td>{p.name}</td><td>{p.team}</td><td className="num">{p.rated}</td><td className="num">{p.approval}%</td></tr>)}</tbody></table>
          </Panel>
          <Panel title="Recent comments">
            <ul className="notes">{d.recent.map((r) => <li key={r.id}><span className="muted small">{r.rating === 'good' ? '👍' : '👎'} {r.name} · {date(r.ratedAt)} · <Link to={`/ranger/tickets/${r.id}`}>#{r.id}</Link></span><p>{r.ratingComment}</p></li>)}</ul>
          </Panel>
        </>
      )}
    </>
  );
}

export function Employees() {
  const q = useQuery({ queryKey: ['ranger', 'employees'], queryFn: () => api<{ id: number; name: string; title: string | null; email: string; company: string | null; role: string | null; onTeam: boolean; level: string | null; openTickets: number; lastLoginAt: string | null }[]>('/api/ranger/employees') });
  return (
    <>
      <h1>Employees</h1>
      <Panel>
        {q.isPending ? <Skeleton rows={8} /> : (
          <table className="table"><thead><tr><th>Name</th><th>Works for</th><th>Ticket team</th><th className="num">Open tickets</th><th>Last sign-in</th></tr></thead>
            <tbody>{q.data?.map((u) => <tr key={u.id}><td>{u.name}<div className="muted small">{u.title} · {u.email}</div></td><td>{u.company ?? 'In-office'}<div className="muted small">{u.role}</div></td><td>{u.onTeam ? <Pill tone="ok">{u.level}</Pill> : <span className="muted">—</span>}</td><td className="num">{u.openTickets}</td><td>{date(u.lastLoginAt)}</td></tr>)}</tbody></table>
        )}
      </Panel>
    </>
  );
}

// Keep unused helper imports referenced for type-only builds
void put;
