import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { customerSearch, prefetchCustomer, useBookmarks, useCorralStats, useMeta, type SearchFilters } from '../../queries';
import type { CustomerRow } from '../../types';
import { ErrorBox, money, num, Panel, Pill, Refreshing, Skeleton } from '../../ui';

/** Customer Search: filters live in the URL (shareable, back button works); results and stats are cached. */
export default function CustomerSearch() {
  const [params, setParams] = useSearchParams();
  const { data: meta } = useMeta();
  const filters: SearchFilters = {
    q: params.get('q') ?? undefined,
    type: params.get('type') ?? undefined,
    status: params.get('status') ?? undefined,
    exception: params.get('exception') ?? undefined,
    page: Number(params.get('page') ?? 1),
    limit: Number(params.get('limit') ?? 25),
  };
  const [q, setQ] = useState(filters.q ?? '');

  // Typing searches after a short pause (no request per keystroke).
  useEffect(() => {
    const t = setTimeout(() => {
      if ((filters.q ?? '') !== q) update({ q, page: undefined });
    }, 300);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [q]);

  const update = (next: Partial<Record<keyof SearchFilters, string | number | undefined>>) => {
    const p = new URLSearchParams(params);
    for (const [k, v] of Object.entries(next)) (v === undefined || v === '' ? p.delete(k) : p.set(k, String(v)));
    setParams(p, { replace: true });
  };

  const search = useQuery(customerSearch(filters));
  const stats = useCorralStats();
  const bookmarks = useBookmarks();
  const pages = search.data ? Math.max(1, Math.ceil(search.data.total / search.data.limit)) : 1;

  return (
    <>
      <h1>{filters.exception === '*' ? 'Exception Accounts' : 'Customer Search'}</h1>
      <div className="stats">
        {(['accounts', 'onFlow', 'pending', 'exceptions'] as const).map((k) => (
          <div className="stat" key={k}>
            <strong>{stats.data ? num(stats.data[k]) : '·'}</strong>
            <span>{{ accounts: 'Accounts', onFlow: 'On flow', pending: 'Pending', exceptions: 'Exceptions' }[k]}</span>
          </div>
        ))}
      </div>

      <Panel>
        <form className="filters" onSubmit={(e) => e.preventDefault()} role="search">
          <input
            type="search"
            placeholder="Name, email, phone, account, ESIID, address…"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            aria-label="Search customers"
            autoFocus
          />
          <select value={filters.type ?? ''} onChange={(e) => update({ type: e.target.value, page: undefined })} aria-label="Type">
            <option value="">All types</option>
            <option>Residential</option>
            <option>Small Business</option>
          </select>
          <select value={filters.status ?? ''} onChange={(e) => update({ status: e.target.value, page: undefined })} aria-label="Status">
            <option value="">All statuses</option>
            {Object.keys(meta?.corral.statuses ?? {}).map((s) => <option key={s}>{s}</option>)}
          </select>
          <select value={filters.exception ?? ''} onChange={(e) => update({ exception: e.target.value, page: undefined })} aria-label="Exception">
            <option value="">Any exception</option>
            <option value="*">Has an exception</option>
            {meta?.corral.exceptions.map((s) => <option key={s}>{s}</option>)}
          </select>
          <select value={filters.limit} onChange={(e) => update({ limit: e.target.value, page: undefined })} aria-label="Rows per page">
            {[10, 25, 50, 100].map((n) => <option key={n} value={n}>{n} per page</option>)}
          </select>
        </form>
      </Panel>

      <Panel
        title={<>Results {search.data && <span className="muted small">{num(search.data.total)} accounts</span>}</>}
        actions={<Refreshing on={search.isFetching && !search.isPending} />}
      >
        <ErrorBox error={search.error} />
        {search.isPending ? <Skeleton rows={10} cols={6} /> : <CustomerTable rows={search.data?.rows ?? []} />}
        {search.data && pages > 1 && (
          <div className="pager">
            <button className="btn outline small" disabled={filters.page! <= 1} onClick={() => update({ page: filters.page! - 1 })}>← Prev</button>
            <span>Page {filters.page} of {pages}</span>
            <button className="btn outline small" disabled={filters.page! >= pages} onClick={() => update({ page: filters.page! + 1 })}>Next →</button>
          </div>
        )}
      </Panel>

      <Panel title="Your bookmarks">
        {bookmarks.isPending ? <Skeleton rows={3} cols={6} /> : bookmarks.data?.length ? <CustomerTable rows={bookmarks.data} /> : <p className="muted">Bookmark an account to keep it here.</p>}
      </Panel>
    </>
  );
}

export function CustomerTable({ rows }: { rows: CustomerRow[] }) {
  const { data: meta } = useMeta();
  const navigate = useNavigate();
  if (!rows.length) return <p className="muted">No accounts match. Try fewer filters.</p>;
  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr><th>Account</th><th>Name</th><th>Address</th><th>Plan</th><th>Status</th><th className="num">Balance</th></tr>
        </thead>
        <tbody>
          {rows.map((c) => (
            <tr
              key={c.id}
              className="clickable"
              onMouseEnter={() => void prefetchCustomer(c.id)}
              onClick={() => navigate(`/corral/customers/${c.id}`)}
            >
              <td className="mono"><Link to={`/corral/customers/${c.id}`} onFocus={() => void prefetchCustomer(c.id)}>{c.account}</Link></td>
              <td>{c.name}<div className="muted small">{c.type}</div></td>
              <td>{c.address}<div className="muted small">{c.city} {c.zip}</div></td>
              <td>{c.plan ?? '—'}<div className="muted small">{c.market}</div></td>
              <td>
                <Pill tone={meta?.corral.statuses[c.status]}>{c.status}</Pill>
                {c.exception && <div><Pill tone="bad">{c.exception}</Pill></div>}
              </td>
              <td className="num">{money(c.balance)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
