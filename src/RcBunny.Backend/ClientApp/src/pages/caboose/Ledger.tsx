import { useState, type FormEvent } from 'react';
import { api, date, money, useLoad } from '../../api';
import { useAuth } from '../../auth';
import AccountLink from '../AccountLink';

interface Entry { id: number; customerId: number; account: string; customer: string; kind: string; amount: number; description: string; status: string; requestedBy: string; decidedBy: string | null; decidedAt: string | null; decisionNote: string | null; createdAt: string }

/** Credits lower a balance, debits raise it. They change the balance only when someone with "refunds" approves them. */
export default function Ledger() {
  const { me, can } = useAuth();
  const { data, error, reload } = useLoad<{ pending: Entry[]; recent: Entry[] }>('/api/caboose/ledger');
  const [problem, setProblem] = useState<string | null>(null);

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); }, (e: Error) => setProblem(e.message));
  const ask = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    run(api.post('/api/caboose/ledger', { account: f.get('account'), kind: f.get('kind'), amount: Number(f.get('amount')), reason: f.get('reason') }).then(() => form.reset()));
  };
  const decide = (entry: Entry, apply: boolean) => {
    const note = apply ? '' : prompt('Why reject it? (optional)') ?? '';
    run(api.post(`/api/caboose/ledger/${entry.id}/decide`, { do: apply ? 'apply' : 'reject', note }));
  };

  return (
    <>
      <h1>Credits & Debits</h1>
      <form className="card row" onSubmit={ask}>
        <strong>Ask for one</strong>
        <input name="account" placeholder="Account number" required />
        <select name="kind"><option value="credit">Credit (lowers the balance)</option><option value="debit">Debit (raises the balance)</option></select>
        <input name="amount" type="number" step="0.01" min="0.01" placeholder="Amount" required />
        <input name="reason" placeholder="Why" required />
        <button className="primary">Ask</button>
      </form>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <h2>Waiting for approval</h2>
          {data.pending.length === 0 ? <p className="muted">Nothing waiting.</p> : (
            <table>
              <thead><tr><th>Asked</th><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Why</th><th>By</th><th /></tr></thead>
              <tbody>
                {data.pending.map((e) => (
                  <tr key={e.id}>
                    <td>{date(e.createdAt)}</td><td><AccountLink id={e.customerId} account={e.account} name={e.customer} /></td>
                    <td>{e.kind}</td><td className="num">{money(e.amount)}</td><td>{e.description}</td><td>{e.requestedBy}</td>
                    <td className="row">
                      {can('refunds') && e.requestedBy !== me!.name
                        ? <><button className="primary" onClick={() => decide(e, true)}>Apply</button><button onClick={() => decide(e, false)}>Reject</button></>
                        : <span className="muted">{can('refunds') ? 'Your request' : 'Needs approval'}</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <h2 style={{ marginTop: 24 }}>Recently decided</h2>
          <table>
            <thead><tr><th>Decided</th><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Result</th><th>By</th></tr></thead>
            <tbody>
              {data.recent.map((e) => (
                <tr key={e.id}>
                  <td>{e.decidedAt && date(e.decidedAt)}</td><td><AccountLink id={e.customerId} account={e.account} name={e.customer} /></td>
                  <td>{e.kind}</td><td className="num">{money(e.amount)}</td><td>{e.status}{e.decisionNote && ` — ${e.decisionNote}`}</td><td>{e.decidedBy}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}
