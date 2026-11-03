import { useState } from 'react';
import { Link } from 'react-router';
import { api, date, useLoad } from '../../api';

interface Transaction { id: number; customerId: number | null; customer: string | null; esiid: string; type: string; status: string; message: string | null; sentAt: string }
interface ErcotData { statuses: string[]; counts: { status: string; count: number }[]; transactions: Transaction[] }

export default function Ercot() {
  const [status, setStatus] = useState('Sent');
  const { data, error, reload } = useLoad<ErcotData>(`/api/ercot${status ? `?status=${status}` : ''}`);
  const [problem, setProblem] = useState<string | null>(null);

  const answer = (t: Transaction, next: string) => {
    const message = next === 'Rejected' ? prompt('Why did ERCOT reject it?') : '';
    if (message === null) return;
    api.post(`/api/ercot/${t.id}/status`, { status: next, message }).then(() => { setProblem(null); reload(); }, (e: Error) => setProblem(e.message));
  };
  const count = (s: string) => data?.counts.find((c) => c.status === s)?.count ?? 0;

  return (
    <>
      <h1>ERCOT</h1>
      <p className="muted">Requests sent to ERCOT for each meter. There's no ERCOT connection yet, so record the answer here. A rejection puts the account in the "Rejected by ERCOT" queue.</p>
      <div className="row">
        {data?.statuses.map((s) => <button key={s} className={status === s ? 'primary' : ''} onClick={() => setStatus(s)}>{s} ({count(s)})</button>)}
        <button className={status === '' ? 'primary' : ''} onClick={() => setStatus('')}>All</button>
      </div>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Sent</th><th>ESIID</th><th>Customer</th><th>Type</th><th>Status</th><th>Message</th><th /></tr></thead>
          <tbody>
            {data.transactions.map((t) => (
              <tr key={t.id}>
                <td>{date(t.sentAt)}</td><td>{t.esiid}</td>
                <td>{t.customerId ? <Link to={`/corral/customers/${t.customerId}`}>{t.customer}</Link> : '—'}</td>
                <td>{t.type}</td><td>{t.status}</td><td>{t.message}</td>
                <td className="row">
                  {t.status === 'Sent' && <><button onClick={() => answer(t, 'Accepted')}>Accepted</button><button onClick={() => answer(t, 'Rejected')}>Rejected</button></>}
                  {t.status === 'Accepted' && <button onClick={() => answer(t, 'Completed')}>Completed</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
