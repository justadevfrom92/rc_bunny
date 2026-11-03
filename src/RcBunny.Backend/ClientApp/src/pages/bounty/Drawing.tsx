import { useState, type FormEvent } from 'react';
import { api, date, useLoad } from '../../api';
import AccountLink from '../AccountLink';

interface DrawingData {
  month: string; thisMonth: string;
  entries: { id: number; customerId: number; account: string; customer: string; createdAt: string }[];
  winner: { id: number; customerId: number; account: string; customer: string; prize: string; drawnBy: string; drawnAt: string } | null;
}

/** Customers enter by redeeming the "Monthly drawing entry" offer in My Account (one entry each per month). */
export default function Drawing() {
  const [month, setMonth] = useState<string | null>(null);
  const { data, error, reload } = useLoad<DrawingData>(`/api/bounty/drawing${month ? `?month=${month}` : ''}`);
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const draw = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    api.post('/api/bounty/drawing/draw', { prize: new FormData(e.currentTarget).get('prize') }).then(() => { setProblem(null); reload(); }, (err: Error) => setProblem(err.message));
  };

  return (
    <>
      <div className="row spread">
        <h1>Monthly Drawing</h1>
        <input type="month" value={data.month} onChange={(e) => setMonth(e.target.value)} style={{ maxWidth: 180 }} />
      </div>
      {problem && <p className="error">{problem}</p>}
      {data.winner ? (
        <div className="card">
          <h2>Winner</h2>
          <p><AccountLink id={data.winner.customerId} account={data.winner.account} name={data.winner.customer} /> won <b>{data.winner.prize}</b>, drawn by {data.winner.drawnBy} on {date(data.winner.drawnAt)}.</p>
        </div>
      ) : data.month === data.thisMonth ? (
        <form className="card row" onSubmit={draw}>
          <strong>Draw a winner</strong>
          <input name="prize" defaultValue="$500 bill credit" required />
          <button className="primary" disabled={data.entries.length === 0}>Draw from {data.entries.length} entries</button>
        </form>
      ) : <p className="muted">No winner was drawn that month.</p>}
      <h2>Entries ({data.entries.length})</h2>
      <table>
        <thead><tr><th>Entered</th><th>Member</th></tr></thead>
        <tbody>{data.entries.map((e) => <tr key={e.id}><td>{date(e.createdAt)}</td><td><AccountLink id={e.customerId} account={e.account} name={e.customer} /></td></tr>)}</tbody>
      </table>
    </>
  );
}
