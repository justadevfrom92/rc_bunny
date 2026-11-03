import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';
import { api, date, money, useLoad } from '../../api';
import AccountLink from './AccountLink';

interface Row { id: number; paidOn: string; amount: number; method: string; kind: string; source: string; status: string; customerId: number; account: string; customer: string }

export default function Payments() {
  const [params, setParams] = useSearchParams();
  const qs = params.toString();
  const { data, error, reload } = useLoad<{ start: string; end: string; rows: Row[]; total: number }>(`/api/caboose/payments${qs ? `?${qs}` : ''}`);
  const [problem, setProblem] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const filter = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    setParams(Object.fromEntries([...f.entries()].filter(([, v]) => v !== '')) as Record<string, string>);
  };
  const record = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    api.post('/api/caboose/payments', { account: f.get('account'), amount: Number(f.get('amount')), method: f.get('method'), kind: f.get('kind') })
      .then(() => { setMessage(`Recorded for account ${f.get('account')}.`); setProblem(null); form.reset(); reload(); }, (err: Error) => setProblem(err.message));
  };

  return (
    <>
      <h1>Payments</h1>
      <form className="card row" onSubmit={record}>
        <strong>Record a payment</strong>
        <input name="account" placeholder="Account number" required />
        <input name="amount" type="number" step="0.01" min="0.01" placeholder="Amount" required />
        <select name="method"><option>Check</option><option>Money order</option><option>Cash</option><option>Card</option></select>
        <select name="kind"><option>Balance Payment</option><option>Deposit</option></select>
        <button className="primary">Record</button>
      </form>
      {problem && <p className="error">{problem}</p>}
      {message && <p>{message}</p>}
      {data && (
        <form key={qs} className="row" onSubmit={filter}>
          <input name="start" type="date" defaultValue={data.start} />
          <input name="end" type="date" defaultValue={data.end} />
          <select name="status" defaultValue={params.get('status') ?? ''}><option value="">Any status</option><option>Paid</option><option>Reversed</option></select>
          <input name="account" placeholder="Account" defaultValue={params.get('account') ?? ''} />
          <button>Show</button>
        </form>
      )}
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <p className="muted">{data.rows.length} payments · {money(data.total)} paid</p>
          <table>
            <thead><tr><th>Date</th><th>Account</th><th>Kind</th><th>Method</th><th>Source</th><th>Status</th><th className="num">Amount</th></tr></thead>
            <tbody>
              {data.rows.map((p) => (
                <tr key={p.id} className={p.status === 'Paid' ? '' : 'inactive'}>
                  <td>{date(p.paidOn)}</td><td><AccountLink id={p.customerId} account={p.account} name={p.customer} /></td>
                  <td>{p.kind}</td><td>{p.method}</td><td>{p.source}</td><td>{p.status}</td><td className="num">{money(p.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </>
  );
}
