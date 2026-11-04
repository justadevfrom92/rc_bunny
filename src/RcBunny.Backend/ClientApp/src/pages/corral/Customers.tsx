import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { qs, useLoad } from '../../api';
import { EmptyRow, Field, PageHead, PagerBar, Panel, Pill, Stat } from '../../ui';

interface Row {
  id: number; account: string; name: string; type: string; phone: string | null; email: string | null; address: string; city: string; zip: string;
  status: string; tone: string; exception: string | null; plan: string | null; market: string | null; source: string; createdAt: string;
}
interface SearchData {
  total: number; rows: Row[]; bookmarks: Row[]; statuses: string[]; exceptions: string[];
  stats: { accounts: number; onFlow: number; pending: number; exceptions: number };
}

/** Customer rows, as ebunny's corral/customers/_rows: account, created, status, customer, contact, address, plan, source. */
function Rows({ rows, empty }: { rows: Row[]; empty: string }) {
  const go = useNavigate();
  return (
    <div className="table-wrap"><table>
      <thead><tr><th>Account</th><th>Created</th><th>Status</th><th>Customer</th><th>Contact</th><th>Address</th><th>Plan</th><th>Source</th></tr></thead>
      <tbody>
        {rows.map((c) => (
          <tr key={c.id} className="click" onClick={() => go(`/corral/customers/${c.id}`)}>
            <td className="mono"><Link to={`/corral/customers/${c.id}`}>{c.account}</Link></td>
            <td>{c.createdAt.slice(0, 10)}</td>
            <td><Pill tone={c.tone}>{c.status}</Pill> {c.exception && <Pill tone="bad">{c.exception}</Pill>}</td>
            <td><b>{c.name}</b><br /><span className="muted">{c.type}</span></td>
            <td>{c.phone}<br /><span className="muted">{c.email}</span></td>
            <td>{c.address}<br /><span className="muted">{c.city}, TX {c.zip}</span></td>
            <td>{c.plan ?? '—'}<br /><span className="muted">{c.market}</span></td>
            <td>{c.source}</td>
          </tr>
        ))}
        {rows.length === 0 && <EmptyRow cols={8}>{empty}</EmptyRow>}
      </tbody>
    </table></div>
  );
}

/** Corral → Customer Search (ebunny's corral/customers/index). */
export default function Customers() {
  const [params, setParams] = useSearchParams();
  const f = { q: params.get('q') ?? '', type: params.get('type') ?? '', status: params.get('status') ?? '', exception: params.get('exception') ?? '', limit: params.get('limit') ?? '25' };
  const page = Number(params.get('page') ?? 1);
  const [text, setText] = useState(f.q);
  const { data, error } = useLoad<SearchData>(`/api/customers?${qs({ ...f, page })}`);

  const set = (changes: Record<string, string>) => setParams(qs({ ...f, page: 1, ...changes }));
  const search = (e: FormEvent) => { e.preventDefault(); set({ q: text }); };
  const searched = !!(f.q || f.type || f.status || f.exception);
  const size = Number(f.limit) || data?.total || 1;

  return (
    <>
      <PageHead title="Customer Search" sub="Find an account by name, phone, email, account number, ESIID or address." />
      {error && <div className="flash bad">{error}</div>}
      {data && (
        <div className="grid-4">
          <Stat label="Accounts" value={data.stats.accounts.toLocaleString()} />
          <Stat label="On Flow" value={data.stats.onFlow.toLocaleString()} note="utility accepted" onClick={() => set({ status: 'Good - On Flow' })} />
          <Stat label="Pending" value={data.stats.pending.toLocaleString()} note="credit, deposit or utility" />
          <Stat label="Exceptions" value={data.stats.exceptions.toLocaleString()} note="need attention" alert onClick={() => set({ exception: '*' })} />
        </div>
      )}

      <div className={searched ? '' : 'search-bookmarks'}>
        <Panel title="Search" body>
          <form className="form-row" onSubmit={search}>
            <Field label="Search" htmlFor="q" grow><input id="q" value={text} onChange={(e) => setText(e.target.value)} placeholder="Name, phone, email, account #, ESIID…" /></Field>
            <Field label="Customer Type" htmlFor="type">
              <select id="type" value={f.type} onChange={(e) => set({ type: e.target.value })}><option value="">-- All Customer Types --</option><option>Residential</option><option>Small Business</option></select>
            </Field>
            <Field label="Status" htmlFor="status">
              <select id="status" value={f.status} onChange={(e) => set({ status: e.target.value })}><option value="">-- All Statuses --</option>{data?.statuses.map((s) => <option key={s}>{s}</option>)}</select>
            </Field>
            <Field label="Exception" htmlFor="exception">
              <select id="exception" value={f.exception} onChange={(e) => set({ exception: e.target.value })}>
                <option value="">-- Exceptions --</option><option value="*">Any Exception</option>{data?.exceptions.map((s) => <option key={s}>{s}</option>)}
              </select>
            </Field>
            <Field label="Limit" htmlFor="limit">
              <select id="limit" value={f.limit} onChange={(e) => set({ limit: e.target.value })}>{['10', '25', '50', '100'].map((n) => <option key={n} value={n}>{n}</option>)}<option value="0">No Limit</option></select>
            </Field>
            <button className="btn">Search</button>
          </form>
        </Panel>
        {!searched && data && (
          <Panel title="Bookmarks"><Rows rows={data.bookmarks} empty="No bookmarks yet. Open an account and choose Bookmark." /></Panel>
        )}
      </div>

      {data && (
        <Panel title={searched ? 'Search Results' : 'Recent Accounts'}
          right={<span className="muted">Showing {data.rows.length ? `${((page - 1) * size + 1).toLocaleString()}–${((page - 1) * size + data.rows.length).toLocaleString()}` : 0} of {data.total.toLocaleString()}</span>}>
          <Rows rows={data.rows} empty="No accounts match. Try fewer filters." />
          <PagerBar page={page} total={data.total} size={size} onPage={(p) => setParams(qs({ ...f, page: p }))} />
        </Panel>
      )}
    </>
  );
}
