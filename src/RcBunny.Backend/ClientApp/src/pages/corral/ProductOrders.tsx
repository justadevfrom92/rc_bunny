import { useState } from 'react';
import { Link } from 'react-router';
import { api, date, money, useLoad } from '../../api';

interface Order { id: number; number: string; customerId: number; customer: string; account: string; product: string; quantity: number; total: number; status: string; createdAt: string }

export default function ProductOrders() {
  const [status, setStatus] = useState('');
  const { data, error, reload } = useLoad<{ statuses: string[]; orders: Order[] }>(`/api/products/orders${status ? `?status=${status}` : ''}`);

  const change = (o: Order, next: string) => api.post(`/api/products/orders/${o.id}/status?status=${next}`).then(reload);

  return (
    <>
      <h1>Product Orders</h1>
      <div className="row">
        <button className={status === '' ? 'primary' : ''} onClick={() => setStatus('')}>All</button>
        {data?.statuses.map((s) => <button key={s} className={status === s ? 'primary' : ''} onClick={() => setStatus(s)}>{s}</button>)}
      </div>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Order</th><th>Date</th><th>Customer</th><th>Product</th><th className="num">Total</th><th>Status</th></tr></thead>
          <tbody>
            {data.orders.map((o) => (
              <tr key={o.id}>
                <td>{o.number}</td><td>{date(o.createdAt)}</td>
                <td><Link to={`/corral/customers/${o.customerId}`}>{o.customer}</Link> <span className="muted">#{o.account}</span></td>
                <td>{o.product} × {o.quantity}</td><td className="num">{money(o.total)}</td>
                <td><select value={o.status} onChange={(e) => change(o, e.target.value)}>{data.statuses.map((s) => <option key={s}>{s}</option>)}</select></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
