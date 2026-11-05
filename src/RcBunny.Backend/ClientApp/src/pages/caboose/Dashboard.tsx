import { Link } from 'react-router';
import { money, useLoad } from '../../api';
import { PageHead, Panel, Stat } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Caboose → Finance dashboard (ebunny's caboose/dashboard). */
export default function Dashboard() {
  const { data, error } = useLoad<any>('/api/caboose/dashboard');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Finance" sub="Cash, receivables, deposits and the work waiting for finance." />
      <div className="grid-4 stat-row">
        <Stat label="Collected Today" value={money(data.today)} />
        <Stat label="Collected This Month" value={money(data.month)} />
        <Link className="stat" to="/caboose/aging"><span>Accounts Receivable</span><strong>{money(data.receivable)}</strong><small>credit balances {money(Math.abs(data.credits))}</small></Link>
        <Link className="stat" to="/caboose/deposits"><span>Deposits Held</span><strong>{money(data.depositsHeld)}</strong><small>{money(data.depositsDue)} due</small></Link>
      </div>
      <div className="grid-3">
        <Link className="panel report-card" to="/caboose/credits-debits"><div className="panel-body"><h3>Pending Credits &amp; Debits</h3><p style={{ fontSize: '1.6rem' }}><b>{data.pendingLedger}</b></p><p className="muted">waiting to be applied</p></div></Link>
        <Link className="panel report-card" to="/caboose/refunds"><div className="panel-body"><h3>Refunds</h3><p style={{ fontSize: '1.6rem' }}><b>{data.refundsWaiting}</b></p><p className="muted">waiting for approval</p></div></Link>
        <Link className="panel report-card" to="/caboose/payments?status=Pending"><div className="panel-body"><h3>Pending Payments</h3><p style={{ fontSize: '1.6rem' }}><b>{data.pendingPayments}</b></p><p className="muted">not yet processed</p></div></Link>
      </div>
      <div className="grid-2">
        <Panel title="Payments by Method" right={<span className="muted">last 30 days</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>Method</th><th className="num">Payments</th><th className="num">Amount</th></tr></thead>
            <tbody>
              {data.byMethod.map((r: any) => <tr key={r.method}><td>{r.method}</td><td className="num">{r.count}</td><td className="num">{money(r.total)}</td></tr>)}
              {data.byMethod.length === 0 && <tr><td colSpan={3} className="empty">No payments.</td></tr>}
            </tbody>
          </table></div>
        </Panel>
        <Panel title="Receivables Aging" right={<Link className="btn sm ghost" to="/caboose/aging">Details</Link>}>
          <div className="table-wrap"><table>
            <thead><tr><th>Aging</th><th className="num">Accounts</th><th className="num">Balance</th></tr></thead>
            <tbody>{data.aging.map((a: any) => <tr key={a.bucket}><td>{a.bucket}</td><td className="num">{a.accounts}</td><td className="num">{money(a.total)}</td></tr>)}</tbody>
          </table></div>
        </Panel>
      </div>
    </>
  );
}
