import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { api } from '../../api';

interface PlanOption { id: number; name: string; termMonths: number; price: number }

/** Sign up a new customer. Enter the zip first to see the plans sold there. */
export default function CreateOrder({ business }: { business: boolean }) {
  const navigate = useNavigate();
  const [zip, setZip] = useState('');
  const [plans, setPlans] = useState<{ market: string; plans: PlanOption[] } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const findPlans = () => {
    setError(null);
    api.get<{ market: string; plans: PlanOption[] }>(`/api/orders/plans?zip=${zip}&type=${business ? 'Business' : 'Residential'}`)
      .then(setPlans, (err: Error) => { setPlans(null); setError(err.message); });
  };

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    api.post<{ id: number }>('/api/orders', {
      type: business ? 'Business' : 'Residential', name: f.get('name'), email: f.get('email'), phone: f.get('phone'),
      address: f.get('address'), city: f.get('city'), zip, esiid: f.get('esiid'), planId: Number(f.get('planId')),
    }).then((r) => navigate(`/corral/customers/${r.id}`), (err: Error) => setError(err.message));
  };

  return (
    <>
      <h1>Create Order{business && ' - Biz'}</h1>
      <div className="card">
        <div className="row">
          <input value={zip} onChange={(e) => setZip(e.target.value)} placeholder="Service zip code" maxLength={5} />
          <button onClick={findPlans} disabled={zip.length !== 5}>Show plans</button>
          {plans && <span className="muted">{plans.market}</span>}
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      {plans && (
        <form className="card" onSubmit={submit}>
          <label>Plan
            <select name="planId" required>
              {plans.plans.map((p) => <option key={p.id} value={p.id}>{p.name} · {p.termMonths <= 1 ? 'monthly' : `${p.termMonths} mo`} · {p.price.toFixed(1)}¢</option>)}
            </select>
          </label>
          <label>{business ? 'Business name' : 'Customer name'}<input name="name" required /></label>
          <div className="row">
            <label>Email<input name="email" type="email" /></label>
            <label>Phone<input name="phone" /></label>
          </div>
          <div className="row">
            <label>Service address<input name="address" required /></label>
            <label>City<input name="city" required /></label>
          </div>
          <label>ESIID (if known)<input name="esiid" /></label>
          <button className="primary">Create order</button>
        </form>
      )}
    </>
  );
}
