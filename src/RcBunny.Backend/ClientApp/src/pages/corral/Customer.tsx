import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { api, date, money, useLoad } from '../../api';
import { useAuth } from '../../auth';

interface Bill { id: number; billedOn: string; dueOn: string; kwh: number; amount: number }
interface Payment { id: number; paidOn: string; amount: number; method: string; status: string; source: string }
interface Note { id: number; author: string; body: string; createdAt: string }
interface Text { id: number; phone: string; body: string; direction: string; author: string; createdAt: string }
interface Call { id: number; phone: string; direction: string; minutes: number; outcome: string; notes: string | null; author: string; createdAt: string }
interface Ercot { id: number; esiid: string; type: string; status: string; message: string | null; sentAt: string }
interface WorkItem { id: number; queue: string; summary: string; createdAt: string; resolvedAt: string | null; resolvedBy: string | null }
interface ProductOrder { id: number; number: string; product: string; quantity: number; total: number; status: string; createdAt: string }
interface Detail {
  id: number; account: string; name: string; type: string; esiid: string | null; email: string | null; phone: string | null; address: string; city: string; zip: string;
  status: string; balance: number; dueDate: string | null; autopay: boolean; plan: string | null; market: string | null;
  bills: Bill[]; payments: Payment[]; notes: Note[];
  texts: Text[]; calls: Call[]; ercot: Ercot[]; workItems: WorkItem[]; productOrders: ProductOrder[];
}

export default function Customer() {
  const { id } = useParams();
  const { can } = useAuth();
  const { data: c, error, reload } = useLoad<Detail>(`/api/customers/${id}`);
  const [note, setNote] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);

  if (error) return <p className="error">{error}</p>;
  if (!c) return <p>Loading…</p>;

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); }, (err: Error) => setProblem(err.message));

  const addNote = (e: FormEvent) => {
    e.preventDefault();
    run(api.post(`/api/customers/${c.id}/notes`, { body: note }).then(() => setNote('')));
  };

  const reverse = (p: Payment) => {
    if (confirm(`Reverse the ${money(p.amount)} payment from ${date(p.paidOn)}?`)) run(api.post(`/api/customers/payments/${p.id}/reverse`));
  };

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    run(api.put(`/api/customers/${c.id}`, { email: form.get('email'), phone: form.get('phone'), status: form.get('status'), esiid: form.get('esiid') }).then(() => setEditing(false)));
  };

  const sendText = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget;
    run(api.post('/api/texts', { customerId: c.id, phone: c.phone, body: new FormData(form).get('body') }).then(() => form.reset()));
  };

  const logCall = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, data = new FormData(form);
    run(api.post('/api/calls', { customerId: c.id, phone: c.phone, direction: data.get('direction'), minutes: Number(data.get('minutes')), outcome: data.get('outcome'), notes: data.get('notes') })
      .then(() => form.reset()));
  };

  const resolve = (w: WorkItem) => run(api.post(`/api/queues/items/${w.id}/resolve`));

  return (
    <>
      <p><Link to="/corral/customers">← Customer Search</Link></p>
      <h1>{c.name} <span className="muted">#{c.account}</span></h1>
      {problem && <p className="error">{problem}</p>}

      <div className="grid">
        <section className="card">
          <h2>Account</h2>
          {editing ? (
            <form onSubmit={save}>
              <label>Email<input name="email" defaultValue={c.email ?? ''} /></label>
              <label>Phone<input name="phone" defaultValue={c.phone ?? ''} /></label>
              <label>Status<input name="status" defaultValue={c.status} required /></label>
              <label>ESIID<input name="esiid" defaultValue={c.esiid ?? ''} /></label>
              <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(false)}>Cancel</button></div>
            </form>
          ) : (
            <>
              <dl>
                <dt>Status</dt><dd>{c.status}</dd>
                <dt>Type</dt><dd>{c.type}</dd>
                <dt>ESIID</dt><dd>{c.esiid ?? '—'}</dd>
                <dt>Balance</dt><dd>{money(c.balance)}{c.dueDate && ` due ${date(c.dueDate)}`}</dd>
                <dt>Plan</dt><dd>{c.plan ?? '—'}</dd>
                <dt>Service</dt><dd>{c.address}, {c.city} {c.zip} · {c.market}</dd>
                <dt>Email</dt><dd>{c.email ?? '—'}</dd>
                <dt>Phone</dt><dd>{c.phone ?? '—'}</dd>
                <dt>AutoPay</dt><dd>{c.autopay ? 'On' : 'Off'}</dd>
              </dl>
              <button onClick={() => setEditing(true)}>Edit</button>
            </>
          )}
        </section>

        <section className="card">
          <h2>Notes</h2>
          <form onSubmit={addNote}>
            <textarea value={note} onChange={(e) => setNote(e.target.value)} rows={3} placeholder="Add a note" />
            <button className="primary">Add note</button>
          </form>
          <ul className="notes">
            {c.notes.map((n) => <li key={n.id}><small>{n.author} · {date(n.createdAt)}</small><p>{n.body}</p></li>)}
          </ul>
        </section>
      </div>

      {c.workItems.some((w) => !w.resolvedAt) && (
        <section className="card">
          <h2>Exceptions</h2>
          {c.workItems.filter((w) => !w.resolvedAt).map((w) => (
            <div key={w.id} className="row spread"><span><span className="tag">{w.queue}</span> {w.summary}</span><button onClick={() => resolve(w)}>Resolve</button></div>
          ))}
        </section>
      )}

      <div className="grid">
        <section className="card">
          <h2>Texts</h2>
          {c.phone ? (
            <form className="row" onSubmit={sendText}>
              <input name="body" maxLength={320} placeholder={`Text ${c.phone}`} required />
              <button className="primary">Send</button>
            </form>
          ) : <p className="muted">No phone number on the account.</p>}
          <ul className="notes">{c.texts.map((t) => <li key={t.id}><small>{t.direction === 'in' ? 'Customer' : t.author} · {date(t.createdAt)}</small><p>{t.body}</p></li>)}</ul>
        </section>

        <section className="card">
          <h2>Phone calls</h2>
          <form onSubmit={logCall}>
            <div className="row">
              <select name="direction"><option value="in">Inbound</option><option value="out">Outbound</option></select>
              <input name="minutes" type="number" min="0" placeholder="Minutes" required />
              <input name="outcome" placeholder="Outcome (e.g. Resolved)" required />
            </div>
            <textarea name="notes" rows={2} placeholder="Notes" />
            <button className="primary">Log call</button>
          </form>
          <ul className="notes">{c.calls.map((k) => <li key={k.id}><small>{k.direction === 'in' ? 'Inbound' : 'Outbound'} · {k.minutes} min · {k.author} · {date(k.createdAt)}</small><p><b>{k.outcome}</b>{k.notes && ` — ${k.notes}`}</p></li>)}</ul>
        </section>
      </div>

      <div className="grid">
        <section className="card">
          <h2>ERCOT</h2>
          {c.ercot.length === 0 ? <p className="muted">Nothing sent yet.</p> : (
            <table>
              <thead><tr><th>Sent</th><th>Type</th><th>Status</th><th>Message</th></tr></thead>
              <tbody>{c.ercot.map((t) => <tr key={t.id}><td>{date(t.sentAt)}</td><td>{t.type}</td><td>{t.status}</td><td>{t.message}</td></tr>)}</tbody>
            </table>
          )}
        </section>
        <section className="card">
          <h2>Product orders</h2>
          {c.productOrders.length === 0 ? <p className="muted">None.</p> : (
            <table>
              <thead><tr><th>Order</th><th>Product</th><th>Status</th><th className="num">Total</th></tr></thead>
              <tbody>{c.productOrders.map((o) => <tr key={o.id}><td>{o.number}</td><td>{o.product} × {o.quantity}</td><td>{o.status}</td><td className="num">{money(o.total)}</td></tr>)}</tbody>
            </table>
          )}
        </section>
      </div>

      <section className="card">
        <h2>Payments</h2>
        <table>
          <thead><tr><th>Date</th><th>Method</th><th>Source</th><th>Status</th><th className="num">Amount</th><th /></tr></thead>
          <tbody>
            {c.payments.map((p) => (
              <tr key={p.id}>
                <td>{date(p.paidOn)}</td><td>{p.method}</td><td>{p.source}</td><td>{p.status}</td><td className="num">{money(p.amount)}</td>
                <td>{can('refunds') && p.status === 'Paid' && <button onClick={() => reverse(p)}>Reverse</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="card">
        <h2>Bills</h2>
        <table>
          <thead><tr><th>Billed</th><th>Due</th><th className="num">kWh</th><th className="num">Amount</th></tr></thead>
          <tbody>
            {c.bills.map((b) => <tr key={b.id}><td>{date(b.billedOn)}</td><td>{date(b.dueOn)}</td><td className="num">{b.kwh.toLocaleString()}</td><td className="num">{money(b.amount)}</td></tr>)}
          </tbody>
        </table>
      </section>
    </>
  );
}
