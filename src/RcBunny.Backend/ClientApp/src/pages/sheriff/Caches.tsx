import { useMutation, useQueryClient } from '@tanstack/react-query';
import { post } from '../../api';
import { ErrorBox, Panel } from '../../ui';

const layers = [
  ['Browser (this tab)', 'TanStack Query keeps each screen\'s data in memory: 30 s fresh, 10 min kept. Reference data (menus, statuses) 1 hour.'],
  ['HTTP', 'Every GET /api answer has an ETag; unchanged data comes back as a 304 with no body. Built JS/CSS files are cached for a year (hashed names).'],
  ['Server (HybridCache)', 'Search results and stats 60 s, an account 10 min, staff directory 5 min, each person\'s access 10 min. Writes remove the affected entries by tag straight away.'],
  ['Website', 'Public pages are output-cached for 60 s and the catalog (plans, rates, fees) for 5 min; a change in the admin shows within that time.'],
] as const;

const tags = [
  ['customers', 'Customer search results and Corral stats'],
  ['access', 'Everyone\'s access (forces every permission to be re-read)'],
  ['staff', 'Staff Directory list'],
  ['catalog', 'Plans and rates (Astro)'],
] as const;

export default function Caches() {
  const qc = useQueryClient();
  const clear = useMutation({
    mutationFn: (tag?: string) => post<{ cleared: string[] }>(`/api/sheriff/cache/clear${tag ? `?tag=${encodeURIComponent(tag)}` : ''}`),
    onSuccess: () => qc.invalidateQueries(),
  });
  return (
    <>
      <h1>Caches</h1>
      <Panel title="How caching works here">
        <dl className="facts wide">
          {layers.map(([k, v]) => <div key={k}><dt>{k}</dt><dd>{v}</dd></div>)}
        </dl>
      </Panel>
      <Panel title="Clear server caches">
        <p className="muted">Entries rebuild on the next request. You shouldn't need this; saves already clear what they change.</p>
        <ErrorBox error={clear.error} />
        {clear.data && <div className="alert ok">Cleared: {clear.data.cleared.join(', ')}</div>}
        <table className="table compact">
          <tbody>
            {tags.map(([t, d]) => (
              <tr key={t}><td className="mono">{t}</td><td>{d}</td><td><button className="btn outline small" onClick={() => clear.mutate(t)}>Clear</button></td></tr>
            ))}
          </tbody>
        </table>
        <button className="btn danger" onClick={() => clear.mutate(undefined)}>Clear everything</button>
      </Panel>
    </>
  );
}
