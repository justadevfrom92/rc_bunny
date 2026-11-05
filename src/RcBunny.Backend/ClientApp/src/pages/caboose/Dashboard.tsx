import { Link } from 'react-router';
import { money, useLoad } from '../../api';

interface DashboardData {
  paid30: number; payments30: number; byMethod: { method: string; total: number; count: number }[];
  pendingLedger: number; openRefunds: number; depositsDue: number; depositsHeld: number;
  aging: { bucket: string; accounts: number; total: number }[];
}

export default function Dashboard() {
  const { data, error } = useLoad<DashboardData>('/api/caboose/dashboard');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  return (
    <>
      <h1>Caboose</h1>
      <div className="stats">
        <Link className="card" to="/caboose/payments"><span className="muted">Paid, last 30 days</span><strong>{money(data.paid30)}</strong><small>{data.payments30} payments</small></Link>
        <Link className="card" to="/caboose/credits-debits"><span className="muted">Credits & debits waiting</span><strong>{data.pendingLedger}</strong></Link>
        <Link className="card" to="/caboose/refunds"><span className="muted">Refunds in progress</span><strong>{data.openRefunds}</strong></Link>
        <Link className="card" to="/caboose/deposits"><span className="muted">Deposits due</span><strong>{money(data.depositsDue)}</strong></Link>
        <Link className="card" to="/caboose/deposits"><span className="muted">Deposits held</span><strong>{money(data.depositsHeld)}</strong></Link>
      </div>
      <div className="grid">
        <section className="card">
          <h2>Payments by method (30 days)</h2>
          <table><tbody>{data.byMethod.map((m) => <tr key={m.method}><td>{m.method}</td><td className="num">{m.count}</td><td className="num">{money(m.total)}</td></tr>)}</tbody></table>
        </section>
        <section className="card">
          <h2>Receivables aging</h2>
          <table><tbody>{data.aging.map((a) => <tr key={a.bucket}><td>{a.bucket}</td><td className="num">{a.accounts}</td><td className="num">{money(a.total)}</td></tr>)}</tbody></table>
          <Link to="/caboose/aging">See the accounts →</Link>
        </section>
      </div>
    </>
  );
}
