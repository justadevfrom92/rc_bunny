import { useState } from 'react';
import { date, money, useLoad } from '../../api';
import AccountLink from '../AccountLink';

interface Account { id: number; account: string; name: string; status: string; balance: number; dueDate: string | null; daysPastDue: number; bucket: string }
interface Bucket { bucket: string; accounts: number; total: number }

export default function Aging() {
  const { data, error } = useLoad<{ accounts: Account[]; summary: Bucket[] }>('/api/caboose/aging');
  const [bucket, setBucket] = useState<string | null>(null);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const shown = bucket ? data.accounts.filter((a) => a.bucket === bucket) : data.accounts;

  return (
    <>
      <h1>Receivables Aging</h1>
      <p className="muted">Everyone who owes money, by how far past the due date they are.</p>
      <div className="stats">
        {data.summary.map((b) => (
          <button key={b.bucket} className={`card ${b.bucket === bucket ? 'primary' : ''}`} onClick={() => setBucket(b.bucket === bucket ? null : b.bucket)}>
            <span>{b.bucket}</span><strong>{money(b.total)}</strong><small>{b.accounts} accounts</small>
          </button>
        ))}
      </div>
      <table>
        <thead><tr><th>Account</th><th>Status</th><th>Due</th><th className="num">Days past due</th><th className="num">Balance</th></tr></thead>
        <tbody>
          {shown.map((a) => (
            <tr key={a.id}>
              <td><AccountLink id={a.id} account={a.account} name={a.name} /></td><td>{a.status}</td><td>{a.dueDate ? date(a.dueDate) : '—'}</td>
              <td className={`num ${a.daysPastDue > 30 ? 'overdue' : ''}`}>{a.daysPastDue}</td><td className="num">{money(a.balance)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
