import { Link } from 'react-router';
import { useLoad } from '../../api';

interface DashboardData { members: number; outstanding: number; earned30: number; spent30: number; toSend: number; drawingEntries: number; topOffers: { offer: string; redeemed: number }[] }

export default function Dashboard() {
  const { data, error } = useLoad<DashboardData>('/api/bounty/dashboard');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const n = (x: number) => x.toLocaleString('en-US');

  return (
    <>
      <h1>Bounty</h1>
      <div className="stats">
        <Link className="card" to="/bounty/members"><span className="muted">Members with stars</span><strong>{n(data.members)}</strong></Link>
        <div className="card"><span className="muted">Stars not spent yet</span><strong>{n(data.outstanding)}</strong></div>
        <div className="card"><span className="muted">Earned, last 30 days</span><strong>{n(data.earned30)}</strong></div>
        <div className="card"><span className="muted">Spent, last 30 days</span><strong>{n(data.spent30)}</strong></div>
        <Link className="card" to="/bounty/redemptions"><span className="muted">Rewards to send</span><strong>{data.toSend}</strong></Link>
        <Link className="card" to="/bounty/drawing"><span className="muted">In this month's drawing</span><strong>{data.drawingEntries}</strong></Link>
      </div>
      <section className="card">
        <h2>Most redeemed</h2>
        <table><tbody>{data.topOffers.map((o) => <tr key={o.offer}><td>{o.offer}</td><td className="num">{o.redeemed}</td></tr>)}</tbody></table>
      </section>
    </>
  );
}
