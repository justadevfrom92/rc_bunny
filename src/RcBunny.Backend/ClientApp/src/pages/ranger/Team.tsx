import { Link, useSearchParams } from 'react-router';
import { dateTime, qs, useLoad } from '../../api';
import { Pill } from '../../ui';
import { ApprovalText, type Approval, type Lists } from './shared';

interface Agent {
  id: number; userId: number; name: string; email: string; company: string | null; team: string; level: string; active: boolean; addedBy: string | null; createdAt: string;
  open: number; solved: number; approval: Approval;
}

/** Everyone who works tickets (in-office and 3rd-party), with their open tickets and approval. */
export default function Team() {
  const [params, setParams] = useSearchParams();
  const { data, error } = useLoad<{ team: string | null; days: number; company: string | null; manager: boolean; agents: Agent[]; lists: Lists }>(`/api/ranger/team?${params}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const l = data.lists;
  const set = (k: string, v: string) => setParams(qs({ team: data.team, days: data.days, [k]: v }));

  return (
    <>
      <div className="row spread"><h1>Ticket Team{data.company && ` · ${data.company}`}</h1><Link className="button primary" to="/ranger/team/add">Add Someone</Link></div>
      <div className="row">
        <label>Team<select value={data.team ?? ''} onChange={(e) => set('team', e.target.value)}><option value="">Everyone</option>{Object.entries(l.teams).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
        <label>Numbers for<select value={data.days} onChange={(e) => set('days', e.target.value)}><option value="30">Last 30 days</option><option value="90">Last 90 days</option><option value="365">Last year</option></select></label>
      </div>
      <div className="scroll">
        <table>
          <thead><tr><th>Name</th><th>Team</th><th>Level</th><th className="num">Open</th><th className="num">Solved</th><th>Approval</th><th>Added</th></tr></thead>
          <tbody>
            {data.agents.map((a) => (
              <tr key={a.id} className={a.active ? '' : 'inactive'}>
                <td><Link to={`/ranger/team/${a.id}`}>{a.name}</Link>{!a.active && <> <Pill>Off tickets</Pill></>}<div className="muted">{a.email}</div></td>
                <td>{l.teams[a.team]}{a.company && <div className="muted">{a.company}</div>}</td>
                <td>{a.level === 'agent' ? l.levels[a.level] : <Pill tone="info">{l.levels[a.level]}</Pill>}</td>
                <td className="num"><Link to={`/ranger/tickets?view=active&owner=${a.userId}`}>{a.open}</Link></td>
                <td className="num">{a.solved}</td>
                <td><ApprovalText a={a.approval} /></td>
                <td className="nowrap">{dateTime(a.createdAt)}{a.addedBy && <div className="muted">by {a.addedBy}</div>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
