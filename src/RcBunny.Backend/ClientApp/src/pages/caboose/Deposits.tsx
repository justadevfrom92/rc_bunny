import { money, useLoad } from '../../api';
import AccountLink from './AccountLink';

interface Row { id: number; account: string; name: string; status: string; amount: number }

export default function Deposits() {
  const { data, error } = useLoad<{ due: Row[]; held: Row[] }>('/api/caboose/deposits');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const table = (rows: Row[], label: string) => rows.length === 0 ? <p className="muted">None.</p> : (
    <table>
      <thead><tr><th>Account</th><th>Status</th><th className="num">{label}</th></tr></thead>
      <tbody>{rows.map((r) => <tr key={r.id}><td><AccountLink id={r.id} account={r.account} name={r.name} /></td><td>{r.status}</td><td className="num">{money(r.amount)}</td></tr>)}</tbody>
      <tfoot><tr><th colSpan={2}>Total</th><th className="num">{money(rows.reduce((s, r) => s + r.amount, 0))}</th></tr></tfoot>
    </table>
  );

  return (
    <>
      <h1>Deposits</h1>
      <p className="muted">Record a deposit payment on the Payments page (kind: Deposit). Refund a held deposit on the Refunds page.</p>
      <div className="grid">
        <section className="card"><h2>Due</h2>{table(data.due, 'Due')}</section>
        <section className="card"><h2>Held</h2>{table(data.held, 'Held')}</section>
      </div>
    </>
  );
}
