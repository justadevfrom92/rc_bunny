import { Link, useSearchParams } from 'react-router';
import { api, date, useLoad } from '../../api';

interface Item { id: number; customerId: number; customer: string; account: string; status: string; summary: string; createdAt: string }

export default function Queues() {
  const [params, setParams] = useSearchParams();
  const queue = params.get('queue');
  const { data: queues, reload: reloadQueues } = useLoad<{ queue: string; count: number }[]>('/api/queues');
  const { data: items, error, reload } = useLoad<Item[]>(queue ? `/api/queues/items?queue=${encodeURIComponent(queue)}` : null);

  const resolve = (i: Item) => api.post(`/api/queues/items/${i.id}/resolve`).then(() => { reload(); reloadQueues(); });

  return (
    <>
      <h1>Exception Queues</h1>
      <div className="stats">
        {queues?.map((q) => (
          <button key={q.queue} className={`card ${q.queue === queue ? 'primary' : ''}`} onClick={() => setParams({ queue: q.queue })}>
            <span>{q.queue}</span><strong>{q.count}</strong>
          </button>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
      {!queue ? <p className="muted">Pick a queue.</p> : !items ? <p>Loading…</p> : items.length === 0 ? <p className="muted">Nothing waiting in {queue}.</p> : (
        <table>
          <thead><tr><th>Since</th><th>Account</th><th>Status</th><th>Why</th><th /></tr></thead>
          <tbody>
            {items.map((i) => (
              <tr key={i.id}>
                <td>{date(i.createdAt)}</td>
                <td><Link to={`/corral/customers/${i.customerId}`}>{i.customer}</Link> <span className="muted">#{i.account}</span></td>
                <td>{i.status}</td><td>{i.summary}</td>
                <td><button onClick={() => resolve(i)}>Resolve</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
