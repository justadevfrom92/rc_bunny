import { date, money, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Caboose → Receivables Aging (ebunny's caboose/aging): open balances by days past due. */
export default function Aging() {
  const { data, error } = useLoad<any>('/api/caboose/aging');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Receivables Aging" sub="Open balances by days past due." />
      <div className="grid-4 stat-row" style={{ gridTemplateColumns: 'repeat(5,1fr)' }}>
        {data.summary.map((s: any) => <div key={s.bucket} className="stat"><span>{s.bucket}</span><strong>{money(s.total).replace(/\.\d\d$/, '')}</strong><small>{s.accounts} accounts</small></div>)}
      </div>
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Account</th><th>Customer</th><th>Status</th><th>Market</th><th className="num">Balance</th><th>Due</th><th className="num">Days Past Due</th><th>Aging</th></tr></thead>
        <tbody>
          {data.accounts.map((c: any) => (
            <tr key={c.id}><td><AccountNo id={c.id} account={c.account} /></td><td>{c.name}</td><td>{c.status}</td><td>{c.market}</td><td className="num">{money(c.balance)}</td>
              <td>{c.dueDate ? date(c.dueDate) : '—'}</td><td className="num">{c.daysPastDue}</td>
              <td><span className={`pill ${c.daysPastDue > 60 ? 'bad' : c.daysPastDue > 0 ? 'warn' : 'ok'}`}>{c.bucket}</span></td></tr>
          ))}
          {data.accounts.length === 0 && <EmptyRow cols={8}>No open balances.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}
