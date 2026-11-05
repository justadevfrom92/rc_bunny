import { type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, date, money, qs, useLoad } from '../../api';
import { useAuth } from '../../auth';
import { DateInput } from '../../fields';
import { AccountNo, EmptyRow, Field, PageHead, Panel, RecordLink, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const tone: Record<string, string> = { Success: 'ok', Pending: 'info', Reversed: 'warn' };

/** Caboose → Payments (ebunny's caboose/payments): every payment, with Reverse. */
export default function Payments() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['start', 'end', 'status', 'account'].map((k) => [k, params.get(k) ?? '']));
  const { data, error, reload } = useLoad<any>(`/api/caboose/payments?${qs(f)}`);
  const [run, bar] = useRun(reload);
  const { can } = useAuth();
  const search = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs(Object.fromEntries(new FormData(e.currentTarget).entries()) as any)); };
  if (error) return <div className="flash bad">{error}</div>;
  return (
    <>
      <PageHead title="Payments" sub="Every payment, with reversal (needs the refunds right). Record checks, money orders and cash received."
        actions={<Link className="btn cyan" to="/caboose/payments/new">Record a Payment</Link>} />
      {bar}
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={search} key={params.toString()}><div className="panel-head"><h2>Search Payments</h2></div><div className="panel-body form-row">
            <Field label="From" htmlFor="start"><DateInput id="start" name="start" defaultValue={data?.start ?? f.start} /></Field>
            <Field label="To" htmlFor="end"><DateInput id="end" name="end" defaultValue={data?.end ?? f.end} /></Field>
            <Field label="Status" htmlFor="status"><select id="status" name="status" defaultValue={f.status}><option value="">Any</option>{['Success', 'Pending', 'Failed', 'Reversed'].map((s) => <option key={s}>{s}</option>)}</select></Field>
            <Field label="Account" htmlFor="account"><input id="account" name="account" defaultValue={f.account} style={{ width: '13ch' }} /></Field>
            <button className="btn">Search</button>
          </div></form>
        </aside>
        <div className="search-main">
          <Panel title="Payments" right={<span className="muted">{data ? money(data.total) : ''} successful</span>}>
            <div className="table-wrap"><table>
              <thead><tr><th>ID</th><th>Date</th><th>Account</th><th className="num">Amount</th><th>Method</th><th>Source</th><th>Kind</th><th>Status</th><th /></tr></thead>
              <tbody>
                {data?.rows.map((p: any) => (
                  <tr key={p.id}><td className="nowrap"><RecordLink type="payment" id={p.id} /></td><td>{date(p.paidOn)}</td>
                    <td><AccountNo id={p.customerId} account={p.account} /><br /><span className="help">{p.customer}</span></td>
                    <td className="num">{money(p.amount)}</td><td>{p.method}</td><td>{p.source}</td><td>{p.kind}</td>
                    <td><span className={`pill ${tone[p.status] ?? 'bad'}`}>{p.status}</span></td>
                    <td>{p.status === 'Success' && can('refunds') && <button className="btn sm ghost" onClick={() => confirm(`Reverse this payment?\n${money(p.amount)} goes back on account ${p.account}.`) && run(api.post(`/api/customers/payments/${p.id}/reverse`), 'Payment reversed')}>Reverse</button>}</td></tr>
                ))}
                {data?.rows.length === 0 && <EmptyRow cols={9}>No payments match.</EmptyRow>}
              </tbody>
            </table></div>
          </Panel>
        </div>
      </div>
    </>
  );
}

/** Record a Payment (ebunny's caboose/payment-form): checks, money orders, cash and wires received. */
export function PaymentForm() {
  const [run, bar] = useRun();
  const navigate = useNavigate();
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/caboose/payments', { account: f.get('account'), amount: Number(f.get('amount')), kind: f.get('kind'), method: f.get('method'), reference: f.get('reference') })
      .then((r) => { navigate('/caboose/payments'); return r; }));
  };
  return (
    <>
      <PageHead title="Record a Payment" sub="Checks, money orders, cash and wires received. The amount comes off the account balance." />
      {bar}
      <form className="panel simple-form" onSubmit={submit}>
        <div className="panel-body form-grid">
          <label htmlFor="r-account">Account #</label><input id="r-account" name="account" required />
          <label htmlFor="r-amount">Amount ($)</label><input id="r-amount" name="amount" type="number" step="0.01" min="0.01" required />
          <label htmlFor="r-kind">For</label><select id="r-kind" name="kind"><option>Balance Payment</option><option>Deposit</option></select>
          <label htmlFor="r-method">Method</label><select id="r-method" name="method">{['Check', 'Money Order', 'Cash', 'Wire'].map((m) => <option key={m}>{m}</option>)}</select>
          <label htmlFor="r-ref">Check # / Reference</label><input id="r-ref" name="reference" />
        </div>
        <div className="panel-body actions"><button className="btn cyan">Record Payment</button><Link className="btn ghost" to="/caboose/payments">Cancel</Link></div>
      </form>
    </>
  );
}
