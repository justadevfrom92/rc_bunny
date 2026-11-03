import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { api, date, useLoad } from '../../api';

interface Text { id: number; customerId: number | null; customer: string | null; phone: string; body: string; direction: string; author: string; createdAt: string }

export default function Texts() {
  const { data, error, reload } = useLoad<Text[]>('/api/texts');
  const [problem, setProblem] = useState<string | null>(null);

  const send = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    api.post('/api/texts', { phone: f.get('phone'), body: f.get('body') }).then(() => { form.reset(); setProblem(null); reload(); }, (err: Error) => setProblem(err.message));
  };

  return (
    <>
      <h1>SMS</h1>
      <p className="muted">No text provider is connected yet, so texts are saved here but not delivered.</p>
      <form className="card row" onSubmit={send}>
        <input name="phone" placeholder="Phone number" required style={{ maxWidth: 180 }} />
        <input name="body" maxLength={320} placeholder="Message" required />
        <button className="primary">Send</button>
      </form>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>When</th><th>To / from</th><th>Customer</th><th>Message</th><th>By</th></tr></thead>
          <tbody>
            {data.map((t) => (
              <tr key={t.id}>
                <td>{date(t.createdAt)}</td><td>{t.direction === 'in' ? '← ' : '→ '}{t.phone}</td>
                <td>{t.customerId ? <Link to={`/corral/customers/${t.customerId}`}>{t.customer}</Link> : '—'}</td>
                <td>{t.body}</td><td>{t.author}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
