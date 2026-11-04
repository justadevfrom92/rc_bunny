import { useState, type FormEvent, type ReactNode } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, money, qs, upload, useLoad } from '../../api';
import { Pill, RecordLink, Status, duration, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */
type Any = any;

const moneyParens = (v: number) => (v < 0 ? `(${money(-v)})` : money(v));
const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function Panel({ id, title, extra, children }: { id?: string; title: ReactNode; extra?: ReactNode; children: ReactNode }) {
  return (
    <section className="card" id={id}>
      <div className="row spread"><h2>{title}</h2><span className="row">{extra}</span></div>
      {children}
    </section>
  );
}

/** Corral's account page: every section of ebunny's, top to bottom. */
export default function Customer() {
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const allPayments = params.get('allPayments') === 'true', allMethods = params.get('allMethods') === 'true';
  const { data, error, reload } = useLoad<Any>(`/api/customers/${id}?${qs({ allPayments, allMethods })}`);
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  const flash = (useLocation().state as { message?: string } | null)?.message;

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const c = data.customer, o = data.options;
  const go = (path: string) => navigate(`/corral/customers/${c.id}/${path}`);
  const toggle = (key: string, on: boolean) => setParams(qs({ allPayments: key === 'allPayments' ? on : allPayments, allMethods: key === 'allMethods' ? on : allMethods }));

  return (
    <>
      <div className="row spread">
        <div>
          <h1>{c.name}</h1>
          <p className="muted">Account <span className="mono">{c.account}</span> · Ticket <span className="mono">{c.ticket}</span> · <Pill tone={data.statusTone}>{c.status}</Pill></p>
        </div>
        <div className="row">
          <button onClick={() => run(api.post(`/api/customers/${c.id}/bookmark`))}>{data.bookmarked ? '★ Bookmarked' : '☆ Bookmark'}</button>
          <Link className="button" to={`/corral/customers/${c.id}/history`}>Account Ticket</Link>
        </div>
      </div>
      <nav className="jump">
        {[['actions', 'Actions'], ['general', 'General'], ['products', 'Products'], ['address', 'Address'], ['stars', 'Stars'], ['details', 'Details'], ['files', 'Files'],
          ['notes', 'Notes'], ['calls', 'Calls & Texts'], ['bills', 'Bills & Payments'], ['plans', 'Plans & Addresses'], ['ercot', 'ERCOT'], ['tech', 'TECH'], ['warehouse', 'Warehouse'], ['logs', 'Logs']]
          .map(([key, label]) => <a key={key} href={`#${key}`}>{label}</a>)}
      </nav>
      {flash && !bar && <p className="notice-ok">{flash}</p>}
      {bar}
      {data.banners.map((b: Any) => <div key={b.title} className="alert bad"><b>{b.title}</b> {b.text}</div>)}
      {data.crossSell && <div className="alert info"><b>Generic Protection</b> Energy Texas has partnered with AIG to help customers avoid costly home repairs: heating and cooling, electrical system and surge protection. Ask if they're interested.</div>}

      {/* Flags · Balances · Actions */}
      <div className="grid-3" id="actions">
        <Panel id="flags" title="Flags" extra={<Link to={`/corral/customers/${c.id}/log/flags`}>Log</Link>}>
          <div className="mini-scroll">
            {data.flags.length === 0 && <p className="muted">No flags.</p>}
            {data.flags.map((f: Any) => (
              <div key={f.id} className="row-line"><span><b>{f.flag}</b><br /><small>{dateTime(f.createdAt)} · {f.author}</small></span>
                <button className="link-btn" onClick={() => run(api.del(`/api/customers/flags/${f.id}`))}>[ remove ]</button></div>
            ))}
          </div>
          <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/${c.id}/flags`, { name: new FormData(e.currentTarget).get('flag') })); }}>
            <select name="flag" required><option value="">-- New Flag --</option>{o.flags.map((f: string) => <option key={f}>{f}</option>)}</select>
            <button>Add</button>
          </form>
        </Panel>

        <Panel id="balances" title="Balances" extra={<Link to={`/corral/customers/${c.id}/log/balances`}>Log</Link>}>
          <dl><dt>Current Balance</dt><dd><b>{moneyParens(c.balance)}</b></dd><dt>Due Date</dt><dd>{c.dueDate ? date(c.dueDate) : '—'}</dd></dl>
          <div className="row">
            <button className="primary" onClick={() => go('actions/make-payment')}>Make Payment</button>
            <button onClick={() => go('actions/add-bill-credit')}>Add Credit</button>
          </div>
          {(['credit', 'debit'] as const).map((kind) => (
            <div key={kind}>
              <h3>Pending {kind === 'credit' ? 'Credits' : 'Debits'}</h3>
              {data.pending.filter((e: Any) => e.kind === kind).length === 0 && <small>None</small>}
              {data.pending.filter((e: Any) => e.kind === kind).map((e: Any) => (
                <div key={e.id} className="row-line"><span>{money(e.amount)} — {e.description}</span>
                  <button className="link-btn" onClick={() => confirm(`Delete this pending ${kind}?`) && run(api.del(`/api/customers/ledger/${e.id}`))}>[ delete ]</button></div>
              ))}
            </div>
          ))}
        </Panel>

        <Panel title="Actions" extra={<Link to={`/corral/customers/${c.id}/log/actions`}>Log</Link>}>
          {(['status', 'service'] as const).map((menu) => (
            <form key={menu} className="row" onSubmit={(e) => { e.preventDefault(); go(`actions/${new FormData(e.currentTarget).get('go')}`); }}>
              <select name="go" required>
                <option value="">{menu === 'status' ? '-- Status --' : '-- Customer Service --'}</option>
                {o.actions.filter((a: Any) => a.menu === menu).map((a: Any) => <option key={a.key} value={a.key}>{a.label}</option>)}
              </select>
              <button>Go</button>
            </form>
          ))}
          <h3>Last 10</h3>
          <div className="mini-scroll">
            {data.lastActions.length === 0 && <small>No actions run yet.</small>}
            {data.lastActions.map((n: Any) => <div key={n.id} className="row-line"><span><b>{n.action}</b><br /><small>{dateTime(n.createdAt)} · {n.author}</small></span></div>)}
          </div>
        </Panel>
      </div>

      {/* General: contact information and products, then shop orders */}
      <div className="grid" id="general">
        <Panel title="Contact Information" extra={<Link to={`/corral/customers/${c.id}/contact-log`}>Contact Log</Link>}>
          <ContactInfo c={c} authorized={data.authorizedUsers} onSaved={reload} go={go} />
        </Panel>
        <Panel id="products" title="Products" extra={<Link to={`/corral/customers/${c.id}/history?log=products`}>Product Log</Link>}>
          {data.products.length === 0 && <p className="muted">No products.</p>}
          {data.products.map((p: Any) => (
            <div key={p.id} className="row-line"><span><b>{p.product}</b> <small>added {date(p.createdAt)}</small>{p.details && <><br /><small>{p.details}</small></>}</span>
              <button className="link-btn" onClick={() => confirm(`Remove ${p.product}?`) && run(api.del(`/api/customers/products/${p.id}`))}>[ remove ]</button></div>
          ))}
          <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/${c.id}/products`, { name: new FormData(e.currentTarget).get('product') })); }}>
            <select name="product" required><option value="">-- Add Product --</option>
              {o.products.filter((p: string) => !data.products.some((x: Any) => x.product === p)).map((p: string) => <option key={p}>{p}</option>)}</select>
            <button>Add</button>
          </form>
        </Panel>
      </div>
      <Panel id="shop" title="Shop Orders" extra={<Link className="button" to={`/corral/customers/${c.id}/order-products`}>Order Products</Link>}>
        <table>
          <thead><tr><th>ID</th><th>Placed</th><th>Items</th><th className="num">Total</th><th>Status</th></tr></thead>
          <tbody>
            {data.shopOrders.length === 0 && <tr><td colSpan={5} className="muted">No shop orders yet.</td></tr>}
            {data.shopOrders.map((s: Any) => (
              <tr key={s.id}><td><Link to={`/corral/products/orders/${s.id}`}><b>#{s.id}</b></Link><div className="mono muted">{s.number}</div></td>
                <td>{date(s.createdAt)}<div className="muted">{s.placedBy ? `by ${s.placedBy}` : 'My Account'}</div></td>
                <td>{s.items}</td><td className="num">{money(s.total)}{s.monthly > 0 && <div className="muted">+{money(s.monthly)}/mo</div>}</td><td><Status value={s.status} /></td></tr>
            ))}
          </tbody>
        </table>
      </Panel>

      {/* Service address · Billing address · Stars */}
      <div className="grid-3" id="address">
        <Panel title="Service Address">
          <dl>
            <dt>Street</dt><dd>{c.address}</dd><dt>Unit</dt><dd>{c.unit ?? '—'}</dd><dt>City</dt><dd>{c.city}, TX {c.zip}</dd><dt>Utility</dt><dd>{data.market ?? '—'}</dd>
            <dt>ESIID</dt><dd className="mono">{c.esiid ?? '—'}</dd><dt>Meter Number</dt><dd className="mono">{c.meterNumber ?? '—'}</dd>
            <dt>Load Profile</dt><dd className="mono">{c.loadProfile ?? '—'}</dd><dt>Load Zone</dt><dd>{c.loadZone ?? '—'}</dd>
            <dt>Customer Type</dt><dd>{c.type === 'Residential' ? 'R' : 'C'}</dd><dt>Meter Type</dt><dd>{c.meterType ?? '—'}</dd>
          </dl>
          <div className="row"><button onClick={() => go('actions/transfer-service')}>Transfer Service</button><button onClick={() => go('actions/move-out')}>Move Out</button></div>
        </Panel>
        <Panel title="Billing Address">
          <dl><dt>Street</dt><dd>{c.billingStreet ?? c.address}</dd><dt>Unit</dt><dd>{c.billingUnit ?? '—'}</dd>
            <dt>City</dt><dd>{c.billingCity ?? c.city}, {c.billingState ?? 'TX'} {c.billingZip ?? c.zip}</dd></dl>
        </Panel>
        <Panel id="stars" title="Current Stars" extra={<b>{c.stars.toLocaleString()}</b>}>
          <div className="row">
            <button onClick={() => run(api.post(`/api/customers/${c.id}/stars/recalculate`))}>Recalculate</button>
            <button className="primary" onClick={() => go('stars')}>Spend Stars</button>
          </div>
          <h3>Rewards History</h3>
          <table>
            <thead><tr><th>ID</th><th>Date</th><th>Reward</th><th className="num">Stars</th><th className="num">Net</th></tr></thead>
            <tbody>
              {data.rewards.length === 0 && <tr><td colSpan={5} className="muted">No stars yet.</td></tr>}
              {data.rewards.map((r: Any) => <tr key={r.id}><td><RecordLink type="star_entry" id={r.id} /></td><td>{date(r.date)}</td><td>{r.reason}</td>
                <td className="num">{r.stars >= 0 ? '+' : ''}{r.stars.toLocaleString()}</td><td className="num">{r.net.toLocaleString()}</td></tr>)}
            </tbody>
          </table>
        </Panel>
      </div>

      {/* Status · Plan · Marketing details */}
      <div className="grid-3" id="details">
        <Panel title="Status Details">
          <dl>
            <dt>Internal ID</dt><dd className="mono">{c.ticket}</dd><dt>Confirmation #</dt><dd className="mono">{c.account}</dd>
            <dt>Internal Status</dt><dd>{c.status}</dd><dt>Status Info</dt><dd>{c.statusInfo ?? '—'}</dd>
            <dt>Order Date</dt><dd>{dateTime(c.createdAt)}</dd><dt>Move/Switch</dt><dd>{c.moveSwitch}</dd>
            <dt>Requested Start</dt><dd>{c.requestedStart ? date(c.requestedStart) : '—'}</dd><dt>Actual Start</dt><dd>{c.actualStart ? date(c.actualStart) : '—'}</dd>
            {c.exception && <><dt>Exception</dt><dd><Pill tone="bad">{c.exception}</Pill></dd></>}
          </dl>
          <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.put(`/api/customers/${c.id}/status`, { status: new FormData(e.currentTarget).get('status') }), 'Status updated'); }}>
            <select name="status" defaultValue={c.status} key={c.status}>{o.statuses.map((s: string) => <option key={s}>{s}</option>)}</select>
            <button>Update</button>
          </form>
        </Panel>
        <Panel title="Plan Details">
          <dl>
            <dt>Plan Order Date</dt><dd>{data.term ? dateTime(data.term.orderedAt) : '—'}</dd><dt>Plan Name</dt><dd>{data.plan ?? '—'}</dd>
            <dt>Rate ID</dt><dd className="mono">{c.rateClass ?? '—'}</dd><dt>Rate</dt><dd>{data.term ? `${data.term.rate2000.toFixed(5)}¢ @ 2000 kwh` : '—'}</dd>
            <dt>Term</dt><dd>{data.planTerm == null ? '—' : data.planTerm <= 1 ? 'month-to-month' : `${data.planTerm} months`}</dd>
          </dl>
          <Link className="button" to={`/corral/orders/new?renew=${c.account}`}>Renew / Change Plan</Link>
        </Panel>
        <Panel title="Marketing Details">
          <form key={`${c.channel}${c.msid}${c.promoCode}`} onSubmit={(e) => {
            e.preventDefault(); const f = new FormData(e.currentTarget);
            run(api.put(`/api/customers/${c.id}/marketing`, { channel: f.get('channel'), msid: f.get('msid'), promoCode: f.get('promo') }), 'Marketing details saved');
          }}>
            <label>Channel<input name="channel" defaultValue={c.channel} /></label>
            <label>MSID<input name="msid" defaultValue={c.msid ?? ''} /></label>
            <label>Promo Code<input name="promo" defaultValue={c.promoCode ?? ''} /></label>
            <button>Save</button>
          </form>
        </Panel>
      </div>

      {/* Files */}
      <Panel id="files" title="Files" extra={<span className="muted">{data.files.length}</span>}>
        <form className="row" onSubmit={(e) => {
          e.preventDefault(); const input = e.currentTarget.elements.namedItem('file') as HTMLInputElement;
          if (input.files?.[0]) run(upload(`/api/customers/${c.id}/files`, input.files[0]).then(() => input.form?.reset()), 'File uploaded');
        }}>
          <input type="file" name="file" required /><button>Upload</button><small>PDF, image, text or Word, up to 10 MB.</small>
        </form>
        <table><tbody>
          {data.files.map((f: Any) => <tr key={f.id}><td>{dateTime(f.createdAt)}</td><td><Link to={`/corral/files/${f.id}`}>{f.name}</Link></td><td className="muted">{f.kind.toUpperCase()}</td></tr>)}
        </tbody></table>
      </Panel>

      {/* Notes and All Notes */}
      <div className="grid" id="notes">
        <Panel title={`Notes (${data.notes.length})`}>
          <NoteForm id={c.id} categories={o.noteCategories} priorities={o.priorities} run={run} />
          <div className="scroll"><table>
            <thead><tr><th>ID</th><th>Date</th><th>By</th><th>Category / Action</th><th>Priority</th><th>Note</th></tr></thead>
            <tbody>
              {data.notes.length === 0 && <tr><td colSpan={6} className="muted">No notes yet.</td></tr>}
              {data.notes.slice(0, 10).map((n: Any) => <tr key={n.id}><td><RecordLink type="note" id={n.id} /></td><td>{dateTime(n.createdAt)}</td><td>{n.author}</td>
                <td>{n.category ?? '—'}{n.action && <div className="muted">{n.action}</div>}</td><td><Status value={n.priority} /></td><td className="wrap-text">{n.body}</td></tr>)}
            </tbody>
          </table></div>
        </Panel>
        <Panel title="All Notes" extra={<span className="muted">{data.notes.length}</span>}>
          <div className="note-feed mini-scroll" style={{ maxHeight: 520 }}>
            {data.notes.map((n: Any) => (
              <article key={n.id}><div className="note-meta"><RecordLink type="note" id={n.id} /><span>{dateTime(n.createdAt)}</span><span>{n.author}</span>
                {n.category && <span>{n.category}{n.action && ` · ${n.action}`}</span>}<Status value={n.priority} /></div><p>{n.body}</p></article>
            ))}
          </div>
        </Panel>
      </div>

      {/* Calls and texts */}
      <div className="grid" id="calls">
        <Panel title={`Phone Calls (${data.calls.length})`} extra={<><Link to={`/corral/calls?account=${c.account}`}>All Calls</Link><Link className="button" to={`/corral/contacts/new?account=${c.account}`}>New Contact</Link></>}>
          <table>
            <thead><tr><th>ID</th><th>Called</th><th>Issue</th><th className="num">Duration</th><th>Answered By</th><th>Resolution</th></tr></thead>
            <tbody>
              {data.calls.length === 0 && <tr><td colSpan={6} className="muted">No calls yet.</td></tr>}
              {data.calls.map((p: Any) => <tr key={p.id}><td><Link to={`/corral/calls/${p.id}`}><b>#{p.id}</b></Link></td><td>{dateTime(p.startedAt)}<div className="muted">{p.direction}</div></td>
                <td>{p.issue ?? '—'}</td><td className="num">{duration(p.durationSec)}</td><td>{p.answeredBy ?? '—'}{p.agentId && <div className="mono muted">{p.agentId}</div>}</td><td><Status value={p.resolution} /></td></tr>)}
            </tbody>
          </table>
        </Panel>
        <Panel title={`Texts (${data.texts.length})`} extra={<><Link to={`/corral/sms?account=${c.account}`}>All Texts</Link>{c.phone && <Link className="button" to={`/corral/sms/new?account=${c.account}`}>Send a Text</Link>}</>}>
          <table>
            <thead><tr><th>ID</th><th>Sent</th><th>Direction</th><th>Message</th><th>Resolution</th></tr></thead>
            <tbody>
              {data.texts.length === 0 && <tr><td colSpan={5} className="muted">No texts yet.</td></tr>}
              {data.texts.map((m: Any) => <tr key={m.id}><td><Link to={`/corral/sms/${m.id}`}><b>#{m.id}</b></Link></td><td>{dateTime(m.createdAt)}</td>
                <td>{m.direction === 'in' ? 'From customer' : 'To customer'}</td><td>{m.body}</td><td><Status value={m.resolution} /></td></tr>)}
            </tbody>
          </table>
        </Panel>
      </div>

      {/* Bills & payments */}
      <div id="bills" className="grid">
        <Panel title="Billing Summary">
          <table>
            <thead><tr><th>ID</th><th>Date</th><th className="num">Billed</th><th className="num">Paid</th><th>Notes</th></tr></thead>
            <tbody>
              {data.summary.length === 0 && <tr><td colSpan={5} className="muted">No billing yet{data.bills.length === 0 && ": service hasn't started, the first bill comes about 30 days after the switch"}.</td></tr>}
              {data.summary.map((s: Any) => <tr key={`${s.type}${s.id}`}><td><RecordLink type={s.type} id={s.id} /><div className="muted">{s.type === 'bill' ? 'Bill' : 'Payment'}</div></td>
                <td>{date(s.date)}</td><td className="num">{s.billed != null ? moneyParens(s.billed) : '-'}</td><td className="num">{s.paid != null ? moneyParens(s.paid) : '-'}</td><td className="mono">{s.note}</td></tr>)}
            </tbody>
          </table>
        </Panel>
        <Panel title="Usage History" extra={<span className="muted">kWh</span>}>
          <div className="scroll"><table>
            <thead><tr><th>Year</th>{months.map((m) => <th key={m} className="num">{m}</th>)}</tr></thead>
            <tbody>
              {data.usage.length === 0 && <tr><td colSpan={13} className="muted">No usage yet.</td></tr>}
              {data.usage.map((u: Any) => <tr key={u.year}><td><b>{u.year}</b></td>{months.map((_, i) => <td key={i} className="num">{u.months[i + 1]?.toLocaleString() ?? '-'}</td>)}</tr>)}
            </tbody>
          </table></div>
        </Panel>
      </div>
      <Panel title="Bill Details">
        <div className="scroll"><table>
          <thead><tr><th>Item</th><th>Type</th><th>Period</th><th className="num">Usage</th><th>Invoice Date</th><th>Due Date</th><th>Paid Date</th><th className="num">Billed</th><th className="num">Paid</th><th className="num">Balance</th><th>Invoice</th><th>Ontime</th></tr></thead>
          <tbody>
            {data.bills.length === 0 && <tr><td colSpan={12} className="muted">No bills yet.</td></tr>}
            {data.bills.map((b: Any) => <tr key={b.id}><td className="mono"><Link to={`/corral/customers/${c.id}/history?bill=${b.id}`}>{b.reference}</Link></td><td>{b.billType}</td>
              <td>{b.periodStart && date(b.periodStart)} - {b.periodEnd && date(b.periodEnd)}</td><td className="num">{b.kwh.toLocaleString()}</td><td>{date(b.billedOn)}</td><td>{date(b.dueOn)}</td>
              <td>{b.paidOn ? date(b.paidOn) : '—'}</td><td className="num">{moneyParens(b.amount)}</td><td className="num">{moneyParens(b.amountPaid)}</td><td className="num">{moneyParens(b.balanceAfter)}</td>
              <td className="mono">{b.invoice}</td><td>{b.ontime ? 'Yes' : 'No'}</td></tr>)}
          </tbody>
        </table></div>
      </Panel>
      <Panel id="payments" title="Payment Details" extra={<>
        <button onClick={() => toggle('allPayments', !allPayments)}>{allPayments ? 'Hide invalid transactions' : 'Show all transactions'}</button>
        <button onClick={() => go('actions/transfer-payments')}>Transfer Payments</button></>}>
        <div className="scroll"><table>
          <thead><tr><th>Item</th><th>Date</th><th>Time</th><th>Source</th><th>Account</th><th>Type</th><th>Status</th><th className="num">Amount</th><th>Confirmation #</th></tr></thead>
          <tbody>
            {data.payments.length === 0 && <tr><td colSpan={9} className="muted">No payments yet.</td></tr>}
            {data.payments.map((p: Any) => <tr key={p.id}><td className="mono"><Link to={`/records/payment/${p.id}`}>{p.reference || `#${p.id}`}</Link></td><td>{p.paidOn}</td><td>{p.paidTime ?? ''}</td><td>{p.source}</td>
              <td>{p.method}{p.last4 && <div className="muted">ending {p.last4}</div>}</td><td>{p.kind}</td><td><Status value={p.status} /></td><td className="num">{moneyParens(p.amount)}</td>
              <td className="mono">{p.confirmation}{p.status === 'Reversed' && p.reversedAt && ` Reversed ${date(p.reversedAt)}`}
                {o.canRefund && p.status === 'Success' && <> <button className="link-btn" onClick={() => confirm(`Reverse ${p.reference} for ${money(p.amount)}? It goes back on the balance.`) && run(api.post(`/api/customers/payments/${p.id}/reverse`), 'Payment reversed')}>[ reverse ]</button></>}</td></tr>)}
          </tbody>
        </table></div>
      </Panel>
      <Panel id="payment-methods" title="Payment Methods" extra={<button onClick={() => toggle('allMethods', !allMethods)}>{allMethods ? 'Hide deleted methods' : 'Show all methods'}</button>}>
        <table>
          <thead><tr><th>Item</th><th>Date</th><th>Type</th><th>Expires</th><th>Nickname</th><th>Vendor</th><th className="num">Total Paid</th><th>Autopay</th><th /></tr></thead>
          <tbody>
            {data.methods.length === 0 && <tr><td colSpan={9} className="muted">No payment methods.</td></tr>}
            {data.methods.map((m: Any) => <tr key={m.id} className={m.removedAt ? 'inactive' : ''}><td><RecordLink type="payment_method" id={m.id} /></td><td>{date(m.createdAt)}</td>
              <td>{m.type} - {m.last4}</td><td>{m.expires ?? '—'}</td><td>{m.nickname}</td><td>{m.vendor}</td><td className="num">{money(data.methodTotals[m.id] ?? 0)}</td><td>{m.autopay ? 'Yes' : ''}</td>
              <td>{m.removedAt ? 'removed' : <>
                <button className="link-btn" onClick={() => confirm('Remove this payment method?') && run(api.del(`/api/customers/methods/${m.id}`))}>[ remove ]</button>{' '}
                {!m.autopay && <button className="link-btn" onClick={() => run(api.post(`/api/customers/methods/${m.id}/autopay`))}>[ set autopay ]</button>}</>}</td></tr>)}
          </tbody>
        </table>
      </Panel>

      {/* Plans & addresses */}
      <Panel id="plans" title="Plan Change Log">
        <div className="scroll"><table>
          <thead><tr><th>ID</th><th>Date Ordered</th><th>Contract Start</th><th>Contract End</th><th>Actual Term</th><th>Rate</th><th>Energy Charge</th><th>Plan</th><th /></tr></thead>
          <tbody>
            {data.planTerms.length === 0 && <tr><td colSpan={9} className="muted">No plans.</td></tr>}
            {data.planTerms.map((t: Any) => <PlanTermRow key={t.id} t={t} run={run} />)}
          </tbody>
        </table></div>
      </Panel>
      <Panel title="Address Change Log">
        <div className="scroll"><table>
          <thead><tr><th>ID</th><th>Date Ordered</th><th>Service Start</th><th>Service End</th><th>ESIID</th><th>Address</th><th /></tr></thead>
          <tbody>{data.addresses.map((a: Any) => <AddressRow key={a.id} a={a} run={run} />)}</tbody>
        </table></div>
      </Panel>

      {/* ERCOT */}
      <Panel id="ercot" title="ERCOT Transactions" extra={<span className="mono muted">{c.address?.toUpperCase()}, {c.city?.toUpperCase()} {c.zip} · {c.esiid}</span>}>
        <div className="scroll"><table>
          <thead><tr><th>ID</th><th>Date Saved</th><th>Trans Date</th><th>Purpose</th><th>Scheduled</th><th>Trans Type</th><th>Tracking</th><th>Status</th><th /></tr></thead>
          <tbody>
            {data.ercot.length === 0 && <tr><td colSpan={9} className="muted">No ERCOT transactions.</td></tr>}
            {data.ercot.map((t: Any) => <tr key={t.id}><td><RecordLink type="ercot_transaction" id={t.id} /></td><td>{dateTime(t.createdAt)}</td><td>{date(t.transDate)}</td><td>{t.purpose}</td>
              <td>{t.scheduledOn ? date(t.scheduledOn) : '—'}</td><td><span className="mono">{t.transType}</span> {t.label}</td><td className="mono">{t.tracking ?? '—'}</td><td><Status value={t.status} /></td>
              <td>{t.status === 'linked' && <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/ercot/${t.id}`, { do: new FormData(e.currentTarget).get('do') })); }}>
                <select name="do"><option value="unlink">Unlink</option>{t.purpose === 'request' && <option value="cancel">Cancel</option>}</select><button>Go</button></form>}</td></tr>)}
          </tbody>
        </table></div>
      </Panel>

      {/* TECH: order-flow queues */}
      <Panel id="tech" title="TECH · Move Queues">
        {data.queueLogs.filter((q: Any) => !q.exitedAt).length === 0 && <p className="muted">Not in any queue.</p>}
        {data.queueLogs.filter((q: Any) => !q.exitedAt).map((q: Any) => (
          <form key={q.id} className="row" onSubmit={(e) => { e.preventDefault(); run(api.put(`/api/customers/queue-logs/${q.id}`, { name: new FormData(e.currentTarget).get('queue') })); }}>
            <Pill tone="info">{q.queue}</Pill>
            <select name="queue" defaultValue={q.queue}><option value="remove">- - Remove This Queue - -</option>{o.queues.map((n: string) => <option key={n}>{n}</option>)}</select>
            <button>Save</button>
          </form>
        ))}
        <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/${c.id}/queues`, { name: new FormData(e.currentTarget).get('queue') })); }}>
          <select name="queue" required><option value="">- - Add Queue - -</option>{o.queues.map((n: string) => <option key={n}>{n}</option>)}</select><button>Add</button>
        </form>
        <h3>Details</h3>
        <div className="mini-scroll"><table><tbody>
          {data.queueLogs.map((q: Any) => <tr key={q.id}><td>{dateTime(q.enteredAt)}</td><td className="mono">{q.queue}</td><td>{q.exitedAt ? `left ${dateTime(q.exitedAt)}` : 'current'}</td><td>{q.note}</td></tr>)}
        </tbody></table></div>
      </Panel>

      {/* Warehouse */}
      <Panel id="warehouse" title="Warehouse" extra={<span className="muted">updated {new Date().toLocaleString('en-US')}</span>}>
        <dl className="kv-wide">{Object.entries(data.warehouse).map(([k, v]) => <Pair key={k} k={k} v={v} />)}</dl>
      </Panel>

      {/* Logs */}
      <Panel id="logs" title="Logs">
        <div className="row">
          <Link className="button" to={`/corral/customers/${c.id}/api-log`}>API Log</Link>
          <Link className="button" to={`/corral/customers/${c.id}/contact-log`}>Contact Log</Link>
          <Link className="button" to={`/corral/customers/${c.id}/history?log=attributes`}>Attribute Log</Link>
          <Link className="button" to={`/corral/customers/${c.id}/history`}>Child Items</Link>
          {data.logs.map((l: Any) => <Link key={l.key} className="button" to={`/corral/customers/${c.id}/history?log=${l.key}`}>{l.title} <Pill>{l.count}</Pill></Link>)}
        </div>
      </Panel>
    </>
  );
}

const Pair = ({ k, v }: { k: string; v: unknown }) => <><dt className="mono">{k}</dt><dd className="mono">{v == null ? '' : String(v)}</dd></>;

function ContactInfo({ c, authorized, onSaved, go }: { c: Any; authorized: Any[]; onSaved: () => void; go: (p: string) => void }) {
  const [editing, setEditing] = useState(false);
  const [run, bar] = useRun(() => { setEditing(false); onSaved(); });
  if (editing)
    return (
      <form onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget);
        run(api.put(`/api/customers/${c.id}/contact`, { email: f.get('email'), phone: f.get('phone'), phoneType: f.get('phoneType'), language: f.get('language') })); }}>
        {bar}
        <label>Email<input name="email" defaultValue={c.email ?? ''} /></label>
        <label>Phone<input name="phone" defaultValue={c.phone ?? ''} /></label>
        <label>Phone type<select name="phoneType" defaultValue={c.phoneType ?? 'Mobile'}><option>Mobile</option><option>Home</option><option>Work</option></select></label>
        <label>Language<select name="language" defaultValue={c.language}><option>English</option><option>Spanish</option></select></label>
        <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(false)}>Cancel</button></div>
      </form>
    );
  return (
    <>
      <dl>
        <dt>IP</dt><dd className="mono">{c.ip ?? '—'}</dd><dt>IP Location</dt><dd>{c.ipLocation ?? '—'}</dd>
        <dt>First Name</dt><dd>{c.firstName}</dd><dt>Last Name</dt><dd>{c.lastName}</dd><dt>Email</dt><dd>{c.email}</dd>
        <dt>Phone</dt><dd>{c.phone ? <><a href={`tel:${c.phone}`}>{c.phone}</a> <Link to={`/corral/sms/new?account=${c.account}`}>[ text ]</Link></> : '—'}</dd>
        <dt>Phone type</dt><dd>{c.phoneType ?? '—'}</dd><dt>SSN</dt><dd className="mono">{c.ssnLast4 ? `***-**-${c.ssnLast4}` : '—'}</dd>
        <dt>TEC Score</dt><dd>{c.tecScore ?? '—'}</dd><dt>Status Info</dt><dd>{c.statusInfo ?? '—'}</dd><dt>Language</dt><dd>{c.language}</dd>
        <dt>Username</dt><dd>{c.username ?? '—'} <button className="link-btn" onClick={() => go('actions/reset-login')}>[ reset ]</button></dd>
        <dt>Linked Accounts</dt><dd>{c.linkedAccounts || '(none)'}</dd>
        <dt>Authorized Users</dt><dd>{authorized.length === 0 ? <span className="muted">(none)</span> : authorized.map((u) => <div key={u.name + u.added}>{u.name} · {u.phone}</div>)}
          <button className="link-btn" onClick={() => go('actions/add-authorized-user')}>[ add ]</button></dd>
      </dl>
      <button onClick={() => setEditing(true)}>Edit contact details</button>
    </>
  );
}

function NoteForm({ id, categories, priorities, run }: { id: number; categories: Record<string, string[]>; priorities: string[]; run: (p: Promise<unknown>, done?: string) => Promise<void> }) {
  const [category, setCategory] = useState('');
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    run(api.post(`/api/customers/${id}/notes`, { body: f.get('body'), category: f.get('category'), action: f.get('action'), priority: f.get('priority') }).then(() => { form.reset(); setCategory(''); }), 'Note added');
  };
  return (
    <form onSubmit={submit}>
      <div className="row">
        <select name="category" value={category} onChange={(e) => setCategory(e.target.value)}><option value="">-- Category --</option>{Object.keys(categories).map((k) => <option key={k}>{k}</option>)}</select>
        <select name="action" disabled={!category}><option value="">-- Action --</option>{(categories[category] ?? []).map((a) => <option key={a}>{a}</option>)}</select>
        <select name="priority"><option value="">-- Priority --</option>{priorities.map((p) => <option key={p}>{p}</option>)}</select>
      </div>
      <textarea name="body" required rows={3} placeholder="New note" />
      <button className="primary">Add Note</button>
    </form>
  );
}

function PlanTermRow({ t, run }: { t: Any; run: (p: Promise<unknown>) => Promise<void> }) {
  const [what, setWhat] = useState('welcome');
  return (
    <tr>
      <td><RecordLink type="plan_term" id={t.id} /></td><td>{dateTime(t.orderedAt)}</td><td>{t.contractStart ? date(t.contractStart) : '—'}</td><td>{t.contractEnd ? date(t.contractEnd) : '—'}</td>
      <td><Status value={t.status} /></td><td>{t.rate2000.toFixed(1)} ¢ @ 2,000</td><td className="mono">{t.energyCharge.toFixed(8)}</td><td>{t.plan}<div className="mono muted">{t.rateClass}</div></td>
      <td><form className="row" onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget); run(api.post(`/api/customers/plan-terms/${t.id}`, { do: what, energyCharge: f.get('energyCharge') ? Number(f.get('energyCharge')) : null })); }}>
        <select value={what} onChange={(e) => setWhat(e.target.value)}><option value="welcome">Send Welcome Packet</option><option value="new-welcome">Send New Welcome Packet</option>
          {t.status !== 'current' && <option value="activate">Activate</option>}<option value="edit-rate">Edit Rate</option></select>
        {what === 'edit-rate' && <input name="energyCharge" type="number" step="0.00000001" min="0" max="1" defaultValue={t.energyCharge} style={{ width: '12ch' }} />}
        <button>Go</button></form></td>
    </tr>
  );
}

function AddressRow({ a, run }: { a: Any; run: (p: Promise<unknown>, done?: string) => Promise<void> }) {
  const [editing, setEditing] = useState(false);
  return (
    <>
      <tr>
        <td><RecordLink type="service_address" id={a.id} /></td><td>{dateTime(a.orderedAt)}</td><td>{a.serviceStart ? date(a.serviceStart) : '—'}</td><td>{a.serviceEnd ? date(a.serviceEnd) : '—'}</td>
        <td className="mono">{a.esiid}</td><td>{a.street?.toUpperCase()}<br />{a.city}, TX {a.zip}</td><td><button onClick={() => setEditing(!editing)}>Change Dates</button></td>
      </tr>
      {editing && (
        <tr><td colSpan={7}>
          <form className="row" onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget);
            run(api.put(`/api/customers/addresses/${a.id}`, { serviceStart: f.get('start') || null, serviceEnd: f.get('end') || null }).then(() => setEditing(false)), 'Service dates updated'); }}>
            <label>Service start (move-in)<input type="date" name="start" defaultValue={a.serviceStart ?? ''} /></label>
            <label>Service end (move-out)<input type="date" name="end" defaultValue={a.serviceEnd ?? ''} /></label>
            <button className="primary">Save Dates</button><button type="button" onClick={() => setEditing(false)}>Cancel</button>
          </form>
        </td></tr>
      )}
    </>
  );
}
