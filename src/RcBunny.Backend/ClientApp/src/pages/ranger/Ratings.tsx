import { Link } from 'react-router';
import { date, useLoad } from '../../api';

interface RatingsData {
  total: number; good: number;
  byAgent: { agent: string; total: number; good: number }[];
  recent: { id: number; subject: string; name: string; rating: string; ratingComment: string | null; agent: string | null; solvedAt: string | null }[];
}

const pct = (good: number, total: number) => (total === 0 ? '—' : `${Math.round((100 * good) / total)}%`);

export default function Ratings() {
  const { data, error } = useLoad<RatingsData>('/api/ranger/ratings');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  return (
    <>
      <h1>Approval Ratings</h1>
      <div className="stats">
        <div className="card"><span className="muted">Approval</span><strong>{pct(data.good, data.total)}</strong></div>
        <div className="card"><span className="muted">Ratings</span><strong>{data.total}</strong></div>
      </div>
      <section className="card">
        <h2>By person</h2>
        <table>
          <thead><tr><th>Agent</th><th className="num">Ratings</th><th className="num">Approval</th></tr></thead>
          <tbody>{data.byAgent.map((a) => <tr key={a.agent}><td>{a.agent}</td><td className="num">{a.total}</td><td className="num">{pct(a.good, a.total)}</td></tr>)}</tbody>
        </table>
      </section>
      <section className="card">
        <h2>Recent</h2>
        <table>
          <thead><tr><th>Ticket</th><th>Agent</th><th>Rating</th><th>Comment</th><th>Solved</th></tr></thead>
          <tbody>
            {data.recent.map((r) => (
              <tr key={r.id}>
                <td><Link to={`/ranger/tickets/${r.id}`}>#{r.id} {r.subject}</Link></td><td>{r.agent ?? '—'}</td>
                <td>{r.rating === 'good' ? '👍 Good' : '👎 Not good'}</td><td>{r.ratingComment}</td><td>{r.solvedAt && date(r.solvedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}
