import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { money, useLoad } from '../../api';

interface Row { id: number; account: string; name: string; type: string; city: string; status: string; balance: number }

export default function Customers() {
  const [params, setParams] = useSearchParams();
  const q = params.get('q') ?? '', type = params.get('type') ?? '', status = params.get('status') ?? '';
  const page = Number(params.get('page') ?? 1);
  const [text, setText] = useState(q);
  const { data, error } = useLoad<{ total: number; rows: Row[]; statuses: string[] }>(
    `/api/customers?q=${encodeURIComponent(q)}&type=${encodeURIComponent(type)}&status=${encodeURIComponent(status)}&page=${page}`);

  const set = (changes: Record<string, string>) => setParams({ q, type, status, ...changes });
  const search = (e: FormEvent) => { e.preventDefault(); set({ q: text, page: '1' }); };
  const pages = data ? Math.ceil(data.total / 25) : 1;

  return (
    <>
      <h1>Customer Search</h1>
      <form className="row" onSubmit={search}>
        <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Account, name, email, phone, address or ESIID" />
        <select value={type} onChange={(e) => set({ type: e.target.value, page: '1' })}>
          <option value="">All types</option><option>Residential</option><option>Small Business</option>
        </select>
        <select value={status} onChange={(e) => set({ status: e.target.value, page: '1' })}>
          <option value="">All statuses</option>
          {data?.statuses.map((s) => <option key={s}>{s}</option>)}
        </select>
        <button className="primary">Search</button>
      </form>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <p className="muted">{data.total} accounts</p>
          <table>
            <thead><tr><th>Account</th><th>Name</th><th>Type</th><th>City</th><th>Status</th><th className="num">Balance</th></tr></thead>
            <tbody>
              {data.rows.map((c) => (
                <tr key={c.id}>
                  <td><Link to={`/corral/customers/${c.id}`}>{c.account}</Link></td>
                  <td>{c.name}</td><td>{c.type}</td><td>{c.city}</td><td>{c.status}</td><td className="num">{money(c.balance)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {pages > 1 && (
            <div className="row">
              <button disabled={page <= 1} onClick={() => set({ page: String(page - 1) })}>Previous</button>
              <span>Page {page} of {pages}</span>
              <button disabled={page >= pages} onClick={() => set({ page: String(page + 1) })}>Next</button>
            </div>
          )}
        </>
      )}
    </>
  );
}
