import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { date, money, qs, useLoad } from '../../api';
import { Pager, Pill } from '../../ui';

interface Row { id: number; account: string; name: string; type: string; city: string; status: string; exception: string | null; balance: number; plan: string | null; market: string | null; createdAt: string }
interface SearchData {
  total: number; rows: Row[]; bookmarks: Row[]; statuses: string[]; exceptions: string[];
  stats: { accounts: number; onFlow: number; pending: number; exceptions: number };
}

function Table({ rows }: { rows: Row[] }) {
  return (
    <div className="scroll">
      <table>
        <thead><tr><th>Account</th><th>Name</th><th>Type</th><th>City</th><th>Plan</th><th>Status</th><th className="num">Balance</th><th>Ordered</th></tr></thead>
        <tbody>
          {rows.map((c) => (
            <tr key={c.id}>
              <td><Link to={`/corral/customers/${c.id}`}>{c.account}</Link></td>
              <td>{c.name}</td><td>{c.type}</td><td>{c.city}</td><td>{c.plan ?? '—'}</td>
              <td>{c.status}{c.exception && <> <Pill tone="bad">{c.exception}</Pill></>}</td>
              <td className="num">{money(c.balance)}</td><td>{date(c.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Corral → Customer Search: counts, your bookmarks, and search with filters. */
export default function Customers() {
  const [params, setParams] = useSearchParams();
  const f = { q: params.get('q') ?? '', type: params.get('type') ?? '', status: params.get('status') ?? '', exception: params.get('exception') ?? '', limit: params.get('limit') ?? '25' };
  const page = Number(params.get('page') ?? 1);
  const [text, setText] = useState(f.q);
  const { data, error } = useLoad<SearchData>(`/api/customers?${qs({ ...f, page })}`);

  const set = (changes: Record<string, string>) => setParams(qs({ ...f, page: 1, ...changes }));
  const search = (e: FormEvent) => { e.preventDefault(); set({ q: text }); };
  const searched = f.q || f.type || f.status || f.exception;

  return (
    <>
      <h1>Customer Search</h1>
      {data && (
        <div className="stats">
          <div className="card"><span className="muted">Accounts</span><strong>{data.stats.accounts}</strong></div>
          <button className="card" onClick={() => set({ status: 'Good - On Flow' })}><span className="muted">On flow</span><strong>{data.stats.onFlow}</strong></button>
          <div className="card"><span className="muted">Pending</span><strong>{data.stats.pending}</strong></div>
          <button className="card" onClick={() => set({ exception: '*' })}><span className="muted">With an exception</span><strong>{data.stats.exceptions}</strong></button>
        </div>
      )}
      <form className="row" onSubmit={search}>
        <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Name, email, account, ticket, ESIID, address, city, zip or phone" />
        <select value={f.type} onChange={(e) => set({ type: e.target.value })}><option value="">All types</option><option>Residential</option><option>Small Business</option></select>
        <select value={f.status} onChange={(e) => set({ status: e.target.value })}>
          <option value="">All statuses</option>{data?.statuses.map((s) => <option key={s}>{s}</option>)}
        </select>
        <select value={f.exception} onChange={(e) => set({ exception: e.target.value })}>
          <option value="">Any exception</option><option value="*">Any account with an exception</option>{data?.exceptions.map((s) => <option key={s}>{s}</option>)}
        </select>
        <select value={f.limit} onChange={(e) => set({ limit: e.target.value })} title="Rows per page">
          {['10', '25', '50', '100'].map((n) => <option key={n} value={n}>{n} a page</option>)}<option value="0">All</option>
        </select>
        <button className="primary">Search</button>
        {searched && <button type="button" onClick={() => { setText(''); setParams(''); }}>Clear</button>}
      </form>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          {!searched && data.bookmarks.length > 0 && (
            <section className="card"><h2>Bookmarks</h2><Table rows={data.bookmarks} /></section>
          )}
          <p className="muted">{data.total} accounts</p>
          <Table rows={data.rows} />
          <Pager page={page} total={data.total} size={Number(f.limit) || data.total || 1} onPage={(p) => set({ page: String(p) })} />
        </>
      )}
    </>
  );
}
