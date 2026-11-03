import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../api';
import { ErrorBox, Panel, Pill, Skeleton } from '../../ui';

interface PlansData {
  markets: { id: number; name: string; short: string }[];
  plans: {
    id: number; type: string; term: number; name: string; internal: string; etf: string; mrc: number; green: number; active: boolean;
    tags: string[]; rates: Record<string, number | null>;
  }[];
}

/** Plans with today's energy charge in each utility area (¢/kWh). Read-only for now. */
export default function Plans() {
  const q = useQuery({ queryKey: ['astro', 'plans'], queryFn: () => api<PlansData>('/api/astro/plans'), staleTime: 5 * 60_000 });
  const [type, setType] = useState('');
  const [q2, setQ] = useState('');
  const rows = useMemo(
    () => (q.data?.plans ?? []).filter((p) => (!type || p.type === type) && (!q2 || `${p.name} ${p.internal}`.toLowerCase().includes(q2.toLowerCase()))),
    [q.data, type, q2],
  );
  return (
    <>
      <h1>Plans</h1>
      <Panel>
        <div className="filters">
          <input type="search" placeholder="Plan name or code" value={q2} onChange={(e) => setQ(e.target.value)} aria-label="Search plans" />
          <select value={type} onChange={(e) => setType(e.target.value)} aria-label="Type">
            <option value="">Resi & Biz</option><option value="Resi">Resi</option><option value="Biz">Biz</option>
          </select>
        </div>
      </Panel>
      <Panel>
        <ErrorBox error={q.error} />
        {q.isPending ? <Skeleton rows={12} cols={8} /> : (
          <div className="table-wrap">
            <table className="table compact">
              <thead>
                <tr><th>Plan</th><th>Type</th><th className="num">Term</th><th>ETF</th><th className="num">Base $</th>{q.data!.markets.map((m) => <th key={m.id} className="num">{m.short}</th>)}</tr>
              </thead>
              <tbody>
                {rows.map((p) => (
                  <tr key={p.id} className={p.active ? '' : 'dim'}>
                    <td>{p.name} <span className="muted mono small">{p.internal}</span> {!p.active && <Pill>inactive</Pill>}</td>
                    <td>{p.type}</td>
                    <td className="num">{p.term}</td>
                    <td>{p.etf}</td>
                    <td className="num">{p.mrc.toFixed(2)}</td>
                    {q.data!.markets.map((m) => <td key={m.id} className="num">{p.rates[m.short]?.toFixed(3) ?? '—'}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </>
  );
}
