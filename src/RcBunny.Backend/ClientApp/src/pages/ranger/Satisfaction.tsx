import { Link, useNavigate, useSearchParams } from 'react-router';
import { useLoad } from '../../api';
import { BarChart, EmptyRow, PageHead, PagerBar } from '../../ui';
import { ApprovalBar, ApprovalCard, TeamPill, dayChoices, daysLabel, type Approval, type Lists } from './shared';

interface Data {
  days: number; overall: Approval; teams: { team: string; label: string; approval: Approval }[];
  weeks: ({ week: string } & Record<string, Approval>)[];
  people: { id: number; name: string; team: string; company?: string | null; approval: Approval }[];
  page: number; total: number; comments: { id: number; number: string; rating: string; ratingComment: string; ratedAt: string; agent: string | null }[];
  lists: Lists;
}

/** Ranger → Approval Ratings (ebunny's ranger/satisfaction). */
export default function Satisfaction() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<Data>(`/api/ranger/satisfaction?${params}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const period = data.days === 365 ? 'last year' : `last ${data.days} days`;
  return (
    <>
      <PageHead title="Approval Ratings" sub={`How customers rated the help they got, ${period}.`} />
      <div className="chips small" style={{ marginBottom: 12 }}>{dayChoices.map((d) => <Link key={d} className={`chip-link${data.days === d ? ' on' : ''}`} to={`/ranger/satisfaction?days=${d}`}>{daysLabel(d)}</Link>)}</div>
      <div className="panel"><div className="panel-head"><h2>How Approval Works</h2></div><div className="panel-body ap-explain">
        <div><b>1. Ticket is solved</b>The customer sees “Did we help?” on the ticket in My Account.</div>
        <div><b>2. They rate it</b>👍 Helped or 👎 Didn't help, with an optional comment.</div>
        <div><b>3. Approval %</b>👍 ÷ (👍 + 👎). Tickets nobody rated don't count either way.</div>
        <div><b>Colors</b><span className="approval ok">90%+ good</span> · <span className="approval warn">75–89% watch</span> · <span className="approval bad">under 75% needs work</span></div>
      </div></div>
      <div className="grid-3">
        <ApprovalCard title="Everyone" a={data.overall} />
        {data.teams.map((t) => <ApprovalCard key={t.team} title={t.label} a={t.approval} />)}
      </div>
      <div className="grid-2" style={{ alignItems: 'start' }}>
        {data.teams.map((t) => (
          <div key={t.team} className="panel"><div className="panel-head"><h2>{t.label} — Week by Week</h2><span className="help">approval %, last 12 weeks</span></div><div className="panel-body">
            <BarChart unit="%" height={150} bars={data.weeks.filter((w) => w[t.team].rated).map((w) => ({ label: new Date(w.week).toLocaleDateString('en-US', { month: 'numeric', day: 'numeric' }), value: w[t.team].pct ?? 0,
              tip: `Week of ${new Date(w.week).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} (${w[t.team].good} 👍, ${w[t.team].rated - w[t.team].good} 👎)` }))} />
          </div></div>
        ))}
      </div>
      <div className="panel"><div className="panel-head"><h2>By Team Member</h2><span className="help">highest approval first</span></div><div className="table-wrap"><table>
        <thead><tr><th>Name</th><th>Team</th><th className="num">👍 Helped</th><th className="num">👎 Didn't</th><th className="num">Rated</th><th>Approval</th></tr></thead>
        <tbody>
          {data.people.map((p) => (
            <tr key={p.id} className="click" onClick={() => navigate(`/ranger/team/${p.id}`)}><td><Link to={`/ranger/team/${p.id}`}><b>{p.name}</b></Link>{p.company && <div className="help">{p.company}</div>}</td>
              <td><TeamPill lists={data.lists} team={p.team} /></td>
              <td className="num">{p.approval.good}</td><td className="num">{p.approval.rated - p.approval.good}</td><td className="num">{p.approval.rated}</td>
              <td className="nowrap"><ApprovalBar a={p.approval} /></td></tr>
          ))}
          {data.people.length === 0 && <EmptyRow cols={6}>No ratings in this period.</EmptyRow>}
        </tbody>
      </table></div></div>
      <div className="panel"><div className="panel-head"><h2>What Customers Said</h2></div><div className="table-wrap"><table>
        <thead><tr><th>Rated</th><th>Ticket</th><th>Rating</th><th>Comment</th><th>Handled By</th></tr></thead>
        <tbody>
          {data.comments.map((t) => (
            <tr key={t.id} className="click" onClick={() => navigate(`/ranger/tickets/${t.id}`)}><td className="nowrap">{new Date(t.ratedAt).toLocaleDateString('en-US')}</td><td><Link className="mono" to={`/ranger/tickets/${t.id}`}>{t.number}</Link></td>
              <td className="nowrap">{t.rating === 'good' ? '👍 Helped' : "👎 Didn't help"}</td><td className="wrap">{t.ratingComment}</td><td>{t.agent ?? '—'}</td></tr>
          ))}
          {data.comments.length === 0 && <EmptyRow cols={5}>No comments in this period.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={data.page} total={data.total} size={10} onPage={(p) => navigate(`/ranger/satisfaction?days=${data.days}&page=${p}`)} />
      </div>
    </>
  );
}
