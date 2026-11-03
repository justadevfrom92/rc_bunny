import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { api, date } from '../../api';

interface Result {
  customers: { id: number; account: string; name: string; address: string; city: string; status: string }[];
  transactions: { id: number; type: string; status: string; message: string | null; sentAt: string }[];
}

export default function EsiidLookup() {
  const [esiid, setEsiid] = useState('');
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState<string | null>(null);

  const look = (e: FormEvent) => {
    e.preventDefault();
    api.get<Result>(`/api/customers/esiid/${encodeURIComponent(esiid.trim())}`).then(setResult, (err: Error) => setError(err.message));
  };

  return (
    <>
      <h1>ESIID Lookup</h1>
      <form className="row" onSubmit={look}>
        <input value={esiid} onChange={(e) => setEsiid(e.target.value)} placeholder="ESIID (the meter's ID)" required />
        <button className="primary">Look up</button>
      </form>
      {error && <p className="error">{error}</p>}
      {result && (
        <>
          <section className="card">
            <h2>Accounts on this meter</h2>
            {result.customers.length === 0 ? <p className="muted">No account has this ESIID.</p> : (
              <table>
                <thead><tr><th>Account</th><th>Name</th><th>Address</th><th>Status</th></tr></thead>
                <tbody>{result.customers.map((c) => <tr key={c.id}><td><Link to={`/corral/customers/${c.id}`}>{c.account}</Link></td><td>{c.name}</td><td>{c.address}, {c.city}</td><td>{c.status}</td></tr>)}</tbody>
              </table>
            )}
          </section>
          <section className="card">
            <h2>ERCOT history</h2>
            {result.transactions.length === 0 ? <p className="muted">Nothing sent for this meter.</p> : (
              <table>
                <thead><tr><th>Sent</th><th>Type</th><th>Status</th><th>Message</th></tr></thead>
                <tbody>{result.transactions.map((t) => <tr key={t.id}><td>{date(t.sentAt)}</td><td>{t.type}</td><td>{t.status}</td><td>{t.message}</td></tr>)}</tbody>
              </table>
            )}
          </section>
        </>
      )}
    </>
  );
}
