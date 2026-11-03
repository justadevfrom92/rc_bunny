import { useState } from 'react';
import { api, date, useLoad } from '../../api';
import AccountLink from '../AccountLink';

interface Redemption { id: number; customerId: number; account: string; customer: string; address: string; email: string | null; offer: string; effect: string; value: string; stars: number; fulfillment: string | null; by: string; createdAt: string }

/** Gift cards and products wait here until someone sends them. */
export default function Redemptions() {
  const [pending, setPending] = useState(true);
  const { data, error, reload } = useLoad<Redemption[]>(`/api/bounty/redemptions?pending=${pending}`);

  return (
    <>
      <h1>Redemptions</h1>
      <div className="row">
        <button className={pending ? 'primary' : ''} onClick={() => setPending(true)}>To send</button>
        <button className={pending ? '' : 'primary'} onClick={() => setPending(false)}>All</button>
      </div>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : data.length === 0 ? <p className="muted">Nothing to send.</p> : (
        <table>
          <thead><tr><th>Date</th><th>Member</th><th>Reward</th><th>Send to</th><th className="num">Stars</th><th /></tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.id}>
                <td>{date(r.createdAt)}</td><td><AccountLink id={r.customerId} account={r.account} name={r.customer} /></td>
                <td>{r.offer}{r.value && <span className="muted"> · {r.value}</span>}</td>
                <td>{r.effect === 'gift' ? r.email : r.effect === 'product' ? r.address : '—'}</td><td className="num">{r.stars.toLocaleString()}</td>
                <td>{r.fulfillment === 'pending' ? <button className="primary" onClick={() => api.post(`/api/bounty/redemptions/${r.id}/sent`).then(reload)}>Mark sent</button> : r.fulfillment ?? 'done'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
