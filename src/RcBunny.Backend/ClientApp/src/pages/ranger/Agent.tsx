import { type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { api, dateTime, useLoad } from '../../api';
import { Pill, useRun } from '../../ui';
import { ApprovalCard, TicketTable, WeekBars, type Approval, type Lists, type Row } from './shared';

interface Data {
  agent: { id: number; userId: number; name: string; email: string; company: string | null; role: string; team: string; level: string; active: boolean; addedBy: string | null; createdAt: string };
  approval: Record<string, Approval>; firstReplyMinutes: number | null; weeks: { week: string; approval: Approval }[];
  open: Row[]; solved: number; ratings: { id: number; number: string; subject: string; name: string; rating: string; ratingComment: string | null; ratedAt: string }[];
  canManage: boolean; company: string | null; lists: Lists;
}

/** One team member: who they are, their numbers, open tickets, approval by week and what customers said. */
export default function Agent() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<Data>(`/api/ranger/team/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { agent: a, lists: l } = data;

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.put(`/api/ranger/team/${a.id}`, { team: f.get('team'), level: f.get('level'), active: f.get('active') === 'on' }));
  };

  return (
    <>
      <p><Link to="/ranger/team">← Ticket Team</Link></p>
      <h1>{a.name} {!a.active && <Pill>Off tickets</Pill>}</h1>
      <p className="muted">
        {l.levels[a.level]} · {l.teams[a.team]}{a.company && ` · ${a.company}`} · {a.role} · {a.email} · on the team since {dateTime(a.createdAt)}{a.addedBy && ` (added by ${a.addedBy})`}
        {' · '}<Link to={`/ranger/employees/${a.userId}`}>Every ticket they handled</Link>
      </p>
      {bar}
      <div className="stats">
        <ApprovalCard label="Approval · 30 days" a={data.approval['30']} />
        <ApprovalCard label="90 days" a={data.approval['90']} />
        <ApprovalCard label="Year" a={data.approval['365']} />
        <div className="card"><span className="muted">Open</span><strong>{data.open.length}</strong></div>
        <div className="card"><span className="muted">Solved · 90 days</span><strong>{data.solved}</strong></div>
        <div className="card"><span className="muted">Avg first reply</span><strong>{data.firstReplyMinutes === null ? '—' : data.firstReplyMinutes < 120 ? `${data.firstReplyMinutes} min` : `${Math.round(data.firstReplyMinutes / 60)} h`}</strong></div>
      </div>

      {data.canManage && (
        <form className="card row" onSubmit={save} key={`${a.team}${a.level}${a.active}`}>
          <strong>Change</strong>
          {data.company === null && <label>Team<select name="team" defaultValue={a.team}>{Object.entries(l.teams).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>}
          <label>Level<select name="level" defaultValue={a.level}>{Object.entries(l.levels).filter(([k]) => data.company === null || k !== 'company_admin').map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
          <label style={{ flexDirection: 'row', margin: 0 }}><input type="checkbox" name="active" defaultChecked={a.active} /> Works tickets</label>
          <button className="primary">Save</button>
          <span className="muted">Taking someone off tickets puts their open tickets back in the queue.</span>
        </form>
      )}

      <section className="card"><h2>Open Tickets</h2><TicketTable rows={data.open} lists={l} empty="No open tickets." /></section>
      <div className="grid">
        <section className="card"><h2>Approval by Week</h2><WeekBars weeks={data.weeks} /></section>
        <section className="card">
          <h2>What Customers Said</h2>
          {data.ratings.length === 0 ? <p className="muted">No ratings yet.</p> : data.ratings.map((r) => (
            <div key={r.id} className="row-line">
              <span><Pill tone={r.rating === 'good' ? 'ok' : 'bad'}>{r.rating === 'good' ? '👍' : '👎'}</Pill> <Link to={`/ranger/tickets/${r.id}`}>{r.number}</Link> {r.ratingComment ?? <span className="muted">No comment</span>}</span>
              <span className="muted nowrap">{dateTime(r.ratedAt)}</span>
            </div>
          ))}
        </section>
      </div>
    </>
  );
}
