import { useState, type FormEvent } from 'react';
import { api, date, money, useLoad } from '../../api';
import { useAuth } from '../../auth';
import AccountLink from '../AccountLink';

interface Refund { id: number; customerId: number; account: string; customer: string; kind: string; amount: number; reason: string; status: string; requestedBy: string; decidedBy: string | null; decisionNote: string | null; checkNumber: string | null; paidAt: string | null; createdAt: string }
interface Credit { id: number; account: string; name: string; balance: number; status: string }

/** Refunds go requested → approved → paid. Approving and marking paid need the "refunds" permission. */
export default function Refunds() {
  const { me, can } = useAuth();
  const { data, error, reload } = useLoad<{ open: Refund[]; done: Refund[]; credits: Credit[] }>('/api/caboose/refunds');
  const [problem, setProblem] = useState<string | null>(null);
  const [account, setAccount] = useState('');
  const [amount, setAmount] = useState('');

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); }, (e: Error) => setProblem(e.message));
  const ask = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/caboose/refunds', { account, kind: f.get('kind'), amount: Number(amount), reason: f.get('reason') }).then(() => { setAccount(''); setAmount(''); }));
  };
  const decide = (r: Refund, action: 'approve' | 'reject' | 'paid') => {
    const checkNumber = action === 'paid' ? prompt('Check number (optional)') ?? '' : '';
    const note = action === 'reject' ? prompt('Why reject it? (optional)') ?? '' : '';
    run(api.post(`/api/caboose/refunds/${r.id}/decide`, { do: action, note, checkNumber }));
  };

  return (
    <>
      <h1>Refunds</h1>
      <form className="card row" onSubmit={ask}>
        <strong>Ask for a refund</strong>
        <input value={account} onChange={(e) => setAccount(e.target.value)} placeholder="Account number" required />
        <select name="kind"><option value="balance">Credit balance</option><option value="deposit">Deposit</option></select>
        <input value={amount} onChange={(e) => setAmount(e.target.value)} type="number" step="0.01" min="0.01" placeholder="Amount" required />
        <input name="reason" placeholder="Why" />
        <button className="primary">Ask</button>
      </form>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <h2>In progress</h2>
          {data.open.length === 0 ? <p className="muted">None.</p> : (
            <table>
              <thead><tr><th>Asked</th><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Why</th><th>Status</th><th /></tr></thead>
              <tbody>
                {data.open.map((r) => (
                  <tr key={r.id}>
                    <td>{date(r.createdAt)}</td><td><AccountLink id={r.customerId} account={r.account} name={r.customer} /></td>
                    <td>{r.kind}</td><td className="num">{money(r.amount)}</td><td>{r.reason} <span className="muted">({r.requestedBy})</span></td>
                    <td><span className="tag">{r.status}</span></td>
                    <td className="row">
                      {!can('refunds') ? <span className="muted">Needs approval</span>
                        : r.status === 'approved' ? <button className="primary" onClick={() => decide(r, 'paid')}>Mark paid</button>
                        : r.requestedBy === me!.name ? <span className="muted">Your request</span>
                        : <><button className="primary" onClick={() => decide(r, 'approve')}>Approve</button><button onClick={() => decide(r, 'reject')}>Reject</button></>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <h2 style={{ marginTop: 24 }}>Credit balances to refund</h2>
          {data.credits.length === 0 ? <p className="muted">None.</p> : (
            <table>
              <thead><tr><th>Account</th><th>Status</th><th className="num">Credit</th><th /></tr></thead>
              <tbody>
                {data.credits.map((c) => (
                  <tr key={c.id}>
                    <td><AccountLink id={c.id} account={c.account} name={c.name} /></td><td>{c.status}</td><td className="num">{money(-c.balance)}</td>
                    <td><button onClick={() => { setAccount(c.account); setAmount((-c.balance).toFixed(2)); }}>Start a refund</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <h2 style={{ marginTop: 24 }}>Finished</h2>
          <table>
            <thead><tr><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Result</th><th>By</th></tr></thead>
            <tbody>
              {data.done.map((r) => (
                <tr key={r.id}>
                  <td><AccountLink id={r.customerId} account={r.account} name={r.customer} /></td><td>{r.kind}</td><td className="num">{money(r.amount)}</td>
                  <td>{r.status}{r.paidAt && ` ${date(r.paidAt)}`}{r.checkNumber && `, check ${r.checkNumber}`}{r.decisionNote && ` — ${r.decisionNote}`}</td><td>{r.decidedBy}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}
