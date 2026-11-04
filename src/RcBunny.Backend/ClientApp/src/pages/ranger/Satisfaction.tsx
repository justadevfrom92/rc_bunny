import { Link, useSearchParams } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { Pager, Pill } from '../../ui';
import { ApprovalCard, ApprovalText, type Approval, type Lists } from './shared';

interface Data {
  days: number; overall: Approval; teams: { team: string; label: string; approval: Approval }[];
  weeks: ({ week: string } & Record<string, Approval>)[];
  people: { id: number; name: string; team: string; approval: Approval }[];
  page: number; total: number;
  comments: { id: number; number: string; subject: string; name: string; rating: string; ratingComment: string; ratedAt: string; agent: string | null }[];
  lists: Lists;
}

/** Approval %: in-office vs 3rd party, each person, the weekly trend, and what customers said. */
export default function Satisfaction() {
  const [params, setParams] = useSearchParams();
  const { data, error } = useLoad<Data>(`/api/ranger/satisfaction?${params}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const teams = Object.entries(data.lists.teams);

  return (
    <>
      <h1>Satisfaction</h1>
      <div className="row">
        <label>For<select value={data.days} onChange={(e) => setParams({ days: e.target.value })}><option value="30">Last 30 days</option><option value="90">Last 90 days</option><option value="365">Last year</option></select></label>
      </div>
      <div className="stats">
        <ApprovalCard label="Overall" a={data.overall} />
        {data.teams.map((t) => <ApprovalCard key={t.team} label={t.label} a={t.approval} />)}
      </div>
      <div className="grid">
        <section className="card">
          <h2>By Person</h2>
          {data.people.length === 0 ? <p className="muted">No ratings in this period.</p> : (
            <table>
              <thead><tr><th>Name</th><th>Team</th><th>Approval</th></tr></thead>
              <tbody>{data.people.map((p) => <tr key={p.id}><td><Link to={`/ranger/team/${p.id}`}>{p.name}</Link></td><td>{data.lists.teams[p.team]}</td><td><ApprovalText a={p.approval} /></td></tr>)}</tbody>
            </table>
          )}
        </section>
        <section className="card">
          <h2>By Week</h2>
          <table>
            <thead><tr><th>Week of</th>{teams.map(([k, v]) => <th key={k}>{v}</th>)}</tr></thead>
            <tbody>{data.weeks.map((w) => <tr key={w.week}><td className="nowrap">{new Date(w.week).toLocaleDateString('en-US')}</td>{teams.map(([k]) => <td key={k}><ApprovalText a={w[k]} /></td>)}</tr>)}</tbody>
          </table>
        </section>
      </div>
      <section className="card">
        <h2>What Customers Said</h2>
        {data.comments.length === 0 ? <p className="muted">No comments in this period.</p> : (
          <table>
            <thead><tr><th>Rating</th><th>Comment</th><th>Ticket</th><th>Owner</th><th>When</th></tr></thead>
            <tbody>{data.comments.map((c) => (
              <tr key={c.id}><td><Pill tone={c.rating === 'good' ? 'ok' : 'bad'}>{c.rating === 'good' ? '👍 Good' : '👎 Bad'}</Pill></td><td>“{c.ratingComment}”<div className="muted">{c.name}</div></td>
                <td><Link to={`/ranger/tickets/${c.id}`}><span className="mono">{c.number}</span> {c.subject}</Link></td><td>{c.agent ?? '—'}</td><td className="nowrap">{dateTime(c.ratedAt)}</td></tr>
            ))}</tbody>
          </table>
        )}
        <Pager page={data.page} total={data.total} size={10} onPage={(p) => setParams({ days: String(data.days), page: String(p) })} />
      </section>
    </>
  );
}
