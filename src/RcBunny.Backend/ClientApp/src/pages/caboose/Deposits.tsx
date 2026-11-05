import { money, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, Panel } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Caboose → Deposits (ebunny's caboose/deposits): due and held. */
export default function Deposits() {
  const { data, error } = useLoad<any>('/api/caboose/deposits');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const table = (rows: any[], label: string, empty: string) => (
    <div className="table-wrap"><table>
      <thead><tr><th>Account</th><th>Customer</th><th>Status</th><th className="num">{label}</th></tr></thead>
      <tbody>
        {rows.map((c) => <tr key={c.id}><td><AccountNo id={c.id} account={c.account} /></td><td>{c.name}</td><td>{c.status}</td><td className="num">{money(c.amount)}</td></tr>)}
        {rows.length === 0 && <EmptyRow cols={4}>{empty}</EmptyRow>}
      </tbody>
    </table></div>
  );
  const sum = (rows: any[]) => money(rows.reduce((t, c) => t + c.amount, 0));
  return (
    <>
      <PageHead title="Deposits" sub="Deposits due from customers and deposits held. Record a deposit in Payments (For: Deposit); refund one in Refunds." />
      <div className="grid-2">
        <Panel title="Due" right={<span className="muted">{sum(data.due)}</span>}>{table(data.due, 'Due', 'No deposits due.')}</Panel>
        <Panel title="Held" right={<span className="muted">{sum(data.held)}</span>}>{table(data.held, 'Held', 'No deposits held.')}</Panel>
      </div>
    </>
  );
}
