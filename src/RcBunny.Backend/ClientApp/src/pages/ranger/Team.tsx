import { Link, useNavigate, useSearchParams } from 'react-router';
import { useLoad } from '../../api';
import { EmptyRow, PageHead } from '../../ui';
import { ApprovalText, TeamPill, dayChoices, daysLabel, type Approval, type Lists } from './shared';

interface Agent {
  id: number; userId: number; name: string; email: string; company: string | null; title: string | null; team: string; level: string; active: boolean; addedBy: string | null; createdAt: string;
  open: number; solved: number; approval: Approval;
}

/** Ranger → Ticket Team (ebunny's ranger/team): everyone who works tickets, with open, solved and approval. */
export default function Team() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<{ team: string | null; days: number; company: string | null; agents: Agent[]; lists: Lists }>(`/api/ranger/team?${params}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const l = data.lists;
  const url = (set: Record<string, string | number | null>) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(set)) if (v === null) next.delete(k); else next.set(k, String(v));
    return `/ranger/team${next.size ? `?${next}` : ''}`;
  };
  return (
    <>
      <PageHead title={data.company ? `${data.company} — Ticket Team` : 'Ticket Team'}
        sub={`${data.company ? "Your company's people who work Ranger tickets." : 'Everyone who works Ranger tickets, in the office and at 3rd-party companies.'} Click someone for their page. Approval = 👍 ÷ all ratings, last ${data.days} days.`}
        actions={<Link className="btn cyan" to="/ranger/team/add">Add a Team Member</Link>} />
      <div className="actions" style={{ marginBottom: 12, justifyContent: 'space-between' }}>
        {data.company ? <span className="help">Only your company's people.</span> : <div className="chips small">
          <Link className={`chip-link${!data.team ? ' on' : ''}`} to={url({ team: null })}>Everyone</Link>
          {Object.entries(l.teams).map(([k, label]) => <Link key={k} className={`chip-link${data.team === k ? ' on' : ''}`} to={url({ team: k })}>{label}</Link>)}
        </div>}
        <div className="chips small">{dayChoices.map((d) => <Link key={d} className={`chip-link${data.days === d ? ' on' : ''}`} to={url({ days: d })}>{daysLabel(d)}</Link>)}</div>
      </div>
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Name</th><th>Team</th><th>Level</th><th className="num">Open</th><th className="num">Solved</th><th>Approval</th><th>Added</th></tr></thead>
        <tbody>
          {data.agents.map((a) => (
            <tr key={a.id} className={`click${a.active ? '' : ' muted'}`} onClick={() => navigate(`/ranger/team/${a.id}`)}>
              <td className="wrap" style={{ minWidth: 180 }}><Link to={`/ranger/team/${a.id}`}><b>{a.name}</b></Link><div className="help">{a.title}{a.company ? ` · ${a.company}` : ''}</div></td>
              <td><TeamPill lists={l} team={a.team} /></td>
              <td>{l.levels[a.level]}{!a.active && <div className="help">Off tickets</div>}</td>
              <td className="num">{a.open}</td><td className="num">{a.solved}</td>
              <td><ApprovalText a={a.approval} /></td>
              <td>{new Date(a.createdAt).toLocaleDateString('en-US', { month: 'numeric', day: 'numeric', year: '2-digit' })}{a.addedBy && <div className="help">by {a.addedBy}</div>}</td>
            </tr>
          ))}
          {data.agents.length === 0 && <EmptyRow cols={7}>Nobody yet. Add a team member.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}
