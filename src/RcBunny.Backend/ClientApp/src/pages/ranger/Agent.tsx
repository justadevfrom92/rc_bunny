import { type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, useLoad } from '../../api';
import { BarChart, EmptyRow, PageHead, useRun } from '../../ui';
import { ApprovalCard, StatusPill, TeamPill, daysLabel, type Approval, type Lists, type Row } from './shared';

interface Data {
  agent: { id: number; userId: number; name: string; email: string; company: string | null; title: string | null; department: string | null; role: string; team: string; level: string; active: boolean; addedBy: string | null; createdAt: string };
  approval: Record<string, Approval>; firstReplyMinutes: number | null; weeks: { week: string; approval: Approval }[];
  open: Row[]; solved: number; ratings: { id: number; number: string; rating: string; ratingComment: string | null; ratedAt: string }[];
  canManage: boolean; company: string | null; lists: Lists;
}

const minutes = (m: number | null) => m === null ? '—' : m < 60 ? `${Math.round(m)} min` : m < 1440 ? `${Math.round(m / 6) / 10} hrs` : `${Math.round(m / 144) / 10} days`;
const md = (s: string) => new Date(s).toLocaleDateString('en-US', { month: 'numeric', day: 'numeric' });

/** One ticket team member (ebunny's ranger/team-show): numbers, approval, profile, open tickets and what customers said. */
export default function Agent() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<Data>(`/api/ranger/team/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const { agent: a, lists: l } = data;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.put(`/api/ranger/team/${a.id}`, { team: f.get('team'), level: f.get('level'), active: f.get('active') === '1' }));
  };
  return (
    <>
      <PageHead title={a.name} sub={<><TeamPill lists={l} team={a.team} /> · {l.levels[a.level]}{!a.active && <> · <b>Off tickets</b></>}</>}
        actions={<><Link className="btn ghost" to="/ranger/team">Ticket Team</Link><Link className="btn ghost" to={`/ranger/employees/${a.userId}`}>Tickets Handled</Link></>} />
      {bar}
      <div className="grid-4 stat-row">
        <Link className="stat" to={`/ranger/tickets?view=active&owner=${a.userId}`}><span>Open Tickets</span><strong>{data.open.length}</strong><small>new, open or waiting</small></Link>
        <div className="stat"><span>Solved</span><strong>{data.solved}</strong><small>last 90 days</small></div>
        <div className="stat"><span>Avg First Reply</span><strong>{minutes(data.firstReplyMinutes)}</strong><small>last 90 days</small></div>
        <div className="stat"><span>Rated</span><strong>{data.approval['90'].rated}</strong><small>tickets, last 90 days</small></div>
      </div>
      <div className="grid-3">{['30', '90', '365'].map((d) => <ApprovalCard key={d} title={`Approval — ${daysLabel(Number(d))}`} a={data.approval[d]} />)}</div>
      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div className="panel"><div className="panel-head"><h2>Approval by Week</h2><span className="help">% of each week's ratings that were 👍 (weeks with no ratings left out)</span></div><div className="panel-body">
          <BarChart unit="%" bars={data.weeks.filter((w) => w.approval.rated).map((w) => ({ label: md(w.week), value: w.approval.pct ?? 0,
            tip: `Week of ${new Date(w.week).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} (${w.approval.good} 👍, ${w.approval.rated - w.approval.good} 👎)` }))} />
        </div></div>
        <div className="panel"><div className="panel-head"><h2>Profile</h2></div><div className="panel-body"><dl className="kv">
          <dt>Email</dt><dd>{a.email}</dd>
          <dt>Title</dt><dd>{a.title ?? '—'}</dd>
          <dt>{a.team === 'office' ? 'Department' : 'Company'}</dt><dd>{(a.team === 'office' ? a.department : a.company) ?? '—'}</dd>
          <dt>Role</dt><dd>{a.role ?? '—'}</dd>
          <dt>On the team since</dt><dd>{new Date(a.createdAt).toLocaleDateString('en-US')}{a.addedBy && <div className="help">added by {a.addedBy}</div>}</dd>
        </dl></div>
          {data.canManage && (
            <form className="panel-body" style={{ borderTop: '1px solid var(--line)', display: 'flex', flexDirection: 'column', gap: 12 }} onSubmit={save}>
              {!data.company && <div className="field"><label htmlFor="tm-team">Team</label><select id="tm-team" name="team" defaultValue={a.team}>{Object.entries(l.teams).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></div>}
              <div className="field"><label htmlFor="tm-level">Level</label><select id="tm-level" name="level" defaultValue={a.level}>
                {Object.entries(l.levels).filter(([k]) => k !== 'company_admin' || (!data.company && a.team === 'third_party')).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></div>
              <div className="field"><label htmlFor="tm-active">Status</label><select id="tm-active" name="active" defaultValue={a.active ? '1' : '0'}><option value="1">Working tickets</option><option value="0">Off tickets</option></select>
                <span className="help">Taking someone off tickets sends their open tickets back to Unassigned.</span></div>
              <div><button className="btn">Save</button></div>
            </form>
          )}
        </div>
      </div>
      <div className="panel"><div className="panel-head"><h2>Open Tickets</h2></div><div className="table-wrap"><table data-page="10">
        <thead><tr><th>Ticket</th><th>Subject</th><th>Customer</th><th>Status</th><th>Opened</th></tr></thead>
        <tbody>
          {data.open.map((t) => (
            <tr key={t.id} className="click" onClick={() => navigate(`/ranger/tickets/${t.id}`)}><td><Link className="mono" to={`/ranger/tickets/${t.id}`}>{t.number}</Link></td><td className="wrap">{t.subject}</td><td>{t.name}</td>
              <td><StatusPill lists={l} value={t.status} /></td><td className="nowrap">{new Date(t.createdAt).toLocaleDateString('en-US')}</td></tr>
          ))}
          {data.open.length === 0 && <EmptyRow cols={5}>No open tickets.</EmptyRow>}
        </tbody>
      </table></div></div>
      <div className="panel"><div className="panel-head"><h2>Latest Ratings</h2></div><div className="table-wrap"><table data-page="10">
        <thead><tr><th>Rated</th><th>Ticket</th><th>Rating</th><th>What They Said</th></tr></thead>
        <tbody>
          {data.ratings.map((t) => (
            <tr key={t.id} className="click" onClick={() => navigate(`/ranger/tickets/${t.id}`)}><td className="nowrap">{new Date(t.ratedAt).toLocaleDateString('en-US')}</td><td><Link className="mono" to={`/ranger/tickets/${t.id}`}>{t.number}</Link></td>
              <td className="nowrap">{t.rating === 'good' ? '👍 Helped' : "👎 Didn't help"}</td><td className="wrap">{t.ratingComment ?? '—'}</td></tr>
          ))}
          {data.ratings.length === 0 && <EmptyRow cols={4}>No ratings yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}
