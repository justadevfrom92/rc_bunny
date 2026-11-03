import { useDeferredValue, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { timeline } from '../../queries';
import { dateTime, ErrorBox, Panel, Refreshing, Skeleton } from '../../ui';

/** Who signed in, logged in as someone, changed access, reversed payments, cleared caches… newest first. */
export default function Timeline() {
  const [q, setQ] = useState('');
  const deferred = useDeferredValue(q);
  const t = useQuery({ ...timeline(deferred), refetchInterval: 30_000 });
  return (
    <>
      <h1>Timeline</h1>
      <Panel>
        <div className="filters">
          <input type="search" placeholder="Search person, action, detail…" value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search timeline" />
        </div>
      </Panel>
      <Panel actions={<Refreshing on={t.isFetching && !t.isPending} />}>
        <ErrorBox error={t.error} />
        {t.isPending ? <Skeleton rows={12} cols={4} /> : (
          <table className="table compact">
            <thead><tr><th>When</th><th>Who</th><th>What</th><th>On</th><th>Detail</th></tr></thead>
            <tbody>
              {t.data?.map((a) => (
                <tr key={a.id}>
                  <td>{dateTime(a.createdAt)}</td>
                  <td>{a.userId ? <Link to={`/sheriff/users/${a.userId}`}>{a.actor}</Link> : a.actor}</td>
                  <td>{a.action.replace(/_/g, ' ')}</td>
                  <td>{a.subject}</td>
                  <td className="muted small">{a.detail}{a.ip && ` · ${a.ip}`}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Panel>
    </>
  );
}
