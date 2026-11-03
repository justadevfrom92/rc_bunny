import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { api, date, useLoad } from '../../api';

interface Call { id: number; customerId: number | null; customer: string | null; phone: string; direction: string; minutes: number; outcome: string; notes: string | null; author: string; createdAt: string }

export default function Calls() {
  const { data, error, reload } = useLoad<Call[]>('/api/calls');
  const [problem, setProblem] = useState<string | null>(null);

  const log = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    api.post('/api/calls', { phone: f.get('phone'), direction: f.get('direction'), minutes: Number(f.get('minutes')), outcome: f.get('outcome'), notes: f.get('notes') })
      .then(() => { form.reset(); setProblem(null); reload(); }, (err: Error) => setProblem(err.message));
  };

  return (
    <>
      <h1>Phone Calls</h1>
      <form className="card row" onSubmit={log}>
        <input name="phone" placeholder="Phone number" required />
        <select name="direction"><option value="in">Inbound</option><option value="out">Outbound</option></select>
        <input name="minutes" type="number" min="0" placeholder="Minutes" required />
        <input name="outcome" placeholder="Outcome" required />
        <input name="notes" placeholder="Notes" />
        <button className="primary">Log call</button>
      </form>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>When</th><th>Phone</th><th>Customer</th><th>Direction</th><th className="num">Min</th><th>Outcome</th><th>By</th></tr></thead>
          <tbody>
            {data.map((c) => (
              <tr key={c.id}>
                <td>{date(c.createdAt)}</td><td>{c.phone}</td>
                <td>{c.customerId ? <Link to={`/corral/customers/${c.customerId}`}>{c.customer}</Link> : '—'}</td>
                <td>{c.direction === 'in' ? 'Inbound' : 'Outbound'}</td><td className="num">{c.minutes}</td>
                <td>{c.outcome}{c.notes && <span className="muted"> — {c.notes}</span>}</td><td>{c.author}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
