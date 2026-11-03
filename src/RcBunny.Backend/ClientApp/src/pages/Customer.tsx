import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { api, date, money, useLoad } from '../api';
import { useAuth } from '../auth';

interface Bill { id: number; billedOn: string; dueOn: string; kwh: number; amount: number }
interface Payment { id: number; paidOn: string; amount: number; method: string; status: string; source: string }
interface Note { id: number; author: string; body: string; createdAt: string }
interface Detail {
  id: number; account: string; name: string; email: string | null; phone: string | null; address: string; city: string; zip: string;
  status: string; balance: number; dueDate: string | null; autopay: boolean; plan: string | null; market: string | null;
  bills: Bill[]; payments: Payment[]; notes: Note[];
}

export default function Customer() {
  const { id } = useParams();
  const { can } = useAuth();
  const { data: c, error, reload } = useLoad<Detail>(`/api/customers/${id}`);
  const [note, setNote] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);

  if (error) return <p className="error">{error}</p>;
  if (!c) return <p>Loading…</p>;

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); }, (err: Error) => setProblem(err.message));

  const addNote = (e: FormEvent) => {
    e.preventDefault();
    run(api.post(`/api/customers/${c.id}/notes`, { body: note }).then(() => setNote('')));
  };

  const reverse = (p: Payment) => {
    if (confirm(`Reverse the ${money(p.amount)} payment from ${date(p.paidOn)}?`)) run(api.post(`/api/customers/payments/${p.id}/reverse`));
  };

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    run(api.put(`/api/customers/${c.id}`, { email: form.get('email'), phone: form.get('phone'), status: form.get('status') }).then(() => setEditing(false)));
  };

  return (
    <>
      <p><Link to="/customers">← Customers</Link></p>
      <h1>{c.name} <span className="muted">#{c.account}</span></h1>
      {problem && <p className="error">{problem}</p>}

      <div className="grid">
        <section className="card">
          <h2>Account</h2>
          {editing ? (
            <form onSubmit={save}>
              <label>Email<input name="email" defaultValue={c.email ?? ''} /></label>
              <label>Phone<input name="phone" defaultValue={c.phone ?? ''} /></label>
              <label>Status<input name="status" defaultValue={c.status} required /></label>
              <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(false)}>Cancel</button></div>
            </form>
          ) : (
            <>
              <dl>
                <dt>Status</dt><dd>{c.status}</dd>
                <dt>Balance</dt><dd>{money(c.balance)}{c.dueDate && ` due ${date(c.dueDate)}`}</dd>
                <dt>Plan</dt><dd>{c.plan ?? '—'}</dd>
                <dt>Service</dt><dd>{c.address}, {c.city} {c.zip} · {c.market}</dd>
                <dt>Email</dt><dd>{c.email ?? '—'}</dd>
                <dt>Phone</dt><dd>{c.phone ?? '—'}</dd>
                <dt>AutoPay</dt><dd>{c.autopay ? 'On' : 'Off'}</dd>
              </dl>
              <button onClick={() => setEditing(true)}>Edit</button>
            </>
          )}
        </section>

        <section className="card">
          <h2>Notes</h2>
          <form onSubmit={addNote}>
            <textarea value={note} onChange={(e) => setNote(e.target.value)} rows={3} placeholder="Add a note" />
            <button className="primary">Add note</button>
          </form>
          <ul className="notes">
            {c.notes.map((n) => <li key={n.id}><small>{n.author} · {date(n.createdAt)}</small><p>{n.body}</p></li>)}
          </ul>
        </section>
      </div>

      <section className="card">
        <h2>Payments</h2>
        <table>
          <thead><tr><th>Date</th><th>Method</th><th>Source</th><th>Status</th><th className="num">Amount</th><th /></tr></thead>
          <tbody>
            {c.payments.map((p) => (
              <tr key={p.id}>
                <td>{date(p.paidOn)}</td><td>{p.method}</td><td>{p.source}</td><td>{p.status}</td><td className="num">{money(p.amount)}</td>
                <td>{can('refunds') && p.status === 'Paid' && <button onClick={() => reverse(p)}>Reverse</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="card">
        <h2>Bills</h2>
        <table>
          <thead><tr><th>Billed</th><th>Due</th><th className="num">kWh</th><th className="num">Amount</th></tr></thead>
          <tbody>
            {c.bills.map((b) => <tr key={b.id}><td>{date(b.billedOn)}</td><td>{date(b.dueOn)}</td><td className="num">{b.kwh.toLocaleString()}</td><td className="num">{money(b.amount)}</td></tr>)}
          </tbody>
        </table>
      </section>
    </>
  );
}
