import { useState, type FormEvent, type ReactNode } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, money, qs, upload, useLoad } from '../../api';
import { EmptyRow, PageHead, Pill, RecordLink, Status, duration, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */
type Any = any;

const moneyParens = (v: number) => (v < 0 ? `(${money(-v)})` : money(v));
const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** ebunny's .panel: heading row with buttons on the right; the content in .panel-body, or a table straight under the head (table). */
function Panel({ id, title, extra, table, className, children }: { id?: string; title: ReactNode; extra?: ReactNode; table?: boolean; className?: string; children: ReactNode }) {
  return (
    <div className={`panel${className ? ' ' + className : ''}`} id={id}>
      <div className="panel-head"><h2>{title}</h2>{extra}</div>
      {table ? children : <div className="panel-body">{children}</div>}
    </div>
  );
}

const Btn = ({ to, ghost, cyan, children }: { to: string; ghost?: boolean; cyan?: boolean; children: ReactNode }) =>
  <Link className={`btn sm${ghost ? ' ghost' : ''}${cyan ? ' cyan' : ''}`} to={to}>{children}</Link>;

/** Corral's account page: every section of ebunny's, top to bottom. */
export default function Customer() {
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const allPayments = params.get('allPayments') === 'true', allMethods = params.get('allMethods') === 'true';
  const { data, error, reload } = useLoad<Any>(`/api/customers/${id}?${qs({ allPayments, allMethods })}`);
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  const flash = (useLocation().state as { message?: string } | null)?.message;

  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.customer, o = data.options;
  const go = (path: string) => navigate(`/corral/customers/${c.id}/${path}`);
  const toggle = (key: string, on: boolean) => setParams(qs({ allPayments: key === 'allPayments' ? on : allPayments, allMethods: key === 'allMethods' ? on : allMethods }));

  return (
    <>
      <PageHead title={c.name}
        sub={<>Account <span className="mono">{c.account}</span> · Ticket <span className="mono">{c.ticket}</span> · <Pill tone={data.statusTone}>{c.status}</Pill></>}
        actions={<>
          <button className="btn ghost" onClick={() => run(api.post(`/api/customers/${c.id}/bookmark`))}>{data.bookmarked ? '★ Bookmarked' : '☆ Bookmark'}</button>
          <Link className="btn ghost" to={`/corral/customers/${c.id}/history`}>Account Ticket</Link>
        </>} />
      <nav className="jump" aria-label="Sections">
        {[['actions', 'Actions'], ['general', 'General'], ['products', 'Products'], ['address', 'Address'], ['stars', 'Stars'], ['details', 'Details'], ['files', 'Files'],
          ['notes', 'Notes'], ['calls', 'Calls & Texts'], ['bills', 'Bills & Payments'], ['plans', 'Plans & Addresses'], ['ercot', 'ERCOT'], ['tech', 'TECH'], ['warehouse', 'Warehouse'], ['logs', 'Logs']]
          .map(([key, label]) => <a key={key} href={`#${key}`}>{label}</a>)}
      </nav>
      {flash && !bar && <div className="flash ok">{flash}</div>}
      {bar}
      {data.banners.map((b: Any) => <div key={b.title} className="alert bad"><b>{b.title}</b> {b.text}</div>)}
      {data.crossSell && <div className="alert info"><b>Generic Protection</b> Energy Texas has partnered with AIG to help customers avoid costly home repairs: heating and cooling, electrical system and surge protection. Ask if they're interested.</div>}

      {/* Flags · Balances · Actions */}
      <div className="grid-3" id="actions">
        <Panel id="flags" title="Flags" extra={<Btn ghost to={`/corral/customers/${c.id}/log/flags`}>Log</Btn>}>
          <div className="mini-scroll">
            {data.flags.length === 0 && <p className="muted">No flags.</p>}
            {data.flags.map((f: Any) => (
              <div key={f.id} className="row-line"><span><b>{f.flag}</b><br /><span className="help">{f.createdAt.replace('T', ' ').slice(0, 19)} · {f.author ?? 'System'}</span></span>
                <button className="link-btn" onClick={() => run(api.del(`/api/customers/flags/${f.id}`))}>[ remove ]</button></div>
            ))}
          </div>
          <form className="form-row" style={{ marginTop: 12 }} onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/${c.id}/flags`, { name: new FormData(e.currentTarget).get('flag') })); }}>
            <select name="flag" aria-label="Add flag" required><option value="">-- New Flag --</option>{o.flags.map((f: string) => <option key={f}>{f}</option>)}</select>
            <button className="btn sm">Add</button>
          </form>
        </Panel>

        <Panel id="balances" title="Balances" extra={<Btn ghost to={`/corral/customers/${c.id}/log/balances`}>Log</Btn>}>
          <dl className="kv"><dt>Current Balance</dt><dd><b>{moneyParens(c.balance)}</b></dd><dt>Due Date</dt><dd>{c.dueDate ? date(c.dueDate) : '—'}</dd></dl>
          <div className="actions" style={{ margin: '12px 0' }}>
            <Btn to={`/corral/customers/${c.id}/actions/make-payment`}>Make Payment</Btn>
            <Btn ghost to={`/corral/customers/${c.id}/actions/add-bill-credit`}>Add Credit</Btn>
          </div>
          <div className="pending-split">
          {(['credit', 'debit'] as const).map((kind) => (
            <div key={kind}>
              <h3>Pending {kind === 'credit' ? 'Credits' : 'Debits'}</h3>
              {data.pending.filter((e: Any) => e.kind === kind).length === 0 && <p className="help">None</p>}
              {data.pending.filter((e: Any) => e.kind === kind).map((e: Any) => (
                <div key={e.id} className="row-line"><span>{money(e.amount)} — {e.description}</span>
                  <button className="link-btn" onClick={() => confirm(`Delete this pending ${kind}?`) && run(api.del(`/api/customers/ledger/${e.id}`))}>[ delete ]</button></div>
              ))}
            </div>
          ))}
          </div>
        </Panel>

        <Panel title="Actions" extra={<Btn ghost to={`/corral/customers/${c.id}/log/actions`}>Log</Btn>}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {(['status', 'service'] as const).map((menu) => (
            <form key={menu} className="form-row" onSubmit={(e) => { e.preventDefault(); go(`actions/${new FormData(e.currentTarget).get('go')}`); }}>
              <select name="go" required aria-label={menu === 'status' ? 'Status action' : 'Customer service action'}>
                <option value="">{menu === 'status' ? '-- Status --' : '-- Customer Service --'}</option>
                {o.actions.filter((a: Any) => a.menu === menu).map((a: Any) => <option key={a.key} value={a.key}>{a.label}</option>)}
              </select>
              <button className="btn sm">Go</button>
            </form>
          ))}
          <div><h3>Last 10</h3>
            <div className="mini-scroll">
              {data.lastActions.length === 0 && <p className="help">No actions run yet.</p>}
              {data.lastActions.map((n: Any) => <div key={n.id} className="row-line"><span><b>{n.action}</b><br /><span className="help">{dateTime(n.createdAt)} · {n.author ?? 'System'}</span></span></div>)}
            </div></div>
          </div>
        </Panel>
      </div>

      {/* General: contact information and products, then shop orders */}
      <div className="grid-2" id="general">
        <Panel title="Contact Information" extra={<Btn ghost to={`/corral/customers/${c.id}/contact-log`}>Contact Log</Btn>}>
          <ContactInfo c={c} authorized={data.authorizedUsers} onSaved={reload} />
        </Panel>
        <Panel id="products" title="Products" extra={<Btn ghost to={`/corral/customers/${c.id}/history?log=products`}>Product Log</Btn>}>
          {data.products.length === 0 && <p className="muted">No products.</p>}
          {data.products.map((p: Any) => (
            <div key={p.id} className="row-line"><span><b>{p.product}</b> <span className="help">added {date(p.createdAt)}</span>{p.details && <><br /><span className="help">{p.details}</span></>}</span>
              <button className="link-btn" onClick={() => confirm(`Remove ${p.product}?`) && run(api.del(`/api/customers/products/${p.id}`))}>[ remove ]</button></div>
          ))}
          <form className="form-row" style={{ marginTop: 12 }} onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/${c.id}/products`, { name: new FormData(e.currentTarget).get('product') })); }}>
            <select name="product" required aria-label="Add product"><option value="">-- Add Product --</option>
              {o.products.filter((p: string) => !data.products.some((x: Any) => x.product === p)).map((p: string) => <option key={p}>{p}</option>)}</select>
            <button className="btn sm">Add</button>
          </form>
        </Panel>
      <Panel id="shop" className="span-all" table title="Shop Orders" extra={<Btn cyan to={`/corral/customers/${c.id}/order-products`}>Order Products</Btn>}>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Placed</th><th>Items</th><th className="num">Total</th><th>Status</th></tr></thead>
          <tbody>
            {data.shopOrders.length === 0 && <EmptyRow cols={5}>No shop orders yet.</EmptyRow>}
            {data.shopOrders.map((s: Any) => (
              <tr key={s.id} className="click" onClick={() => navigate(`/corral/products/orders/${s.id}`)}><td className="nowrap"><Link to={`/corral/products/orders/${s.id}`}><b>#{s.id}</b></Link><div className="help mono">{s.number}</div></td>
                <td className="nowrap">{date(s.createdAt)}<div className="help">{s.placedBy ? `by ${s.placedBy}` : 'My Account'}</div></td>
                <td className="wrap">{s.items}</td><td className="num">{money(s.total)}{s.monthly > 0 && <div className="help">+{money(s.monthly)}/mo</div>}</td><td><Status value={s.status} /></td></tr>
            ))}
          </tbody>
        </table></div>
      </Panel>
      </div>

      {/* Service address · Billing address · Stars */}
      <div className="grid-3" id="address">
        <Panel title="Service Address">
          <dl className="kv">
            <dt>Street</dt><dd>{c.address}</dd><dt>Unit</dt><dd>{c.unit ?? '—'}</dd><dt>City</dt><dd>{c.city}, TX {c.zip}</dd><dt>Utility</dt><dd>{data.market ?? '—'}</dd>
            <dt>ESIID</dt><dd className="mono">{c.esiid ?? '—'}</dd><dt>Meter Number</dt><dd className="mono">{c.meterNumber ?? '—'}</dd>
            <dt>Load Profile</dt><dd className="mono">{c.loadProfile ?? '—'}</dd><dt>Load Zone</dt><dd>{c.loadZone ?? '—'}</dd>
            <dt>Customer Type</dt><dd>{c.type === 'Residential' ? 'R' : 'C'}</dd><dt>Meter Type</dt><dd>{c.meterType ?? '—'}</dd>
          </dl>
          <div className="actions" style={{ marginTop: 12 }}><Btn ghost to={`/corral/customers/${c.id}/actions/transfer-service`}>Transfer Service</Btn><Btn ghost to={`/corral/customers/${c.id}/actions/move-out`}>Move Out</Btn></div>
        </Panel>
        <Panel title="Billing Address">
          <dl className="kv"><dt>Street</dt><dd>{c.billingStreet ?? c.address}</dd><dt>Unit</dt><dd>{c.billingUnit ?? '—'}</dd>
            <dt>City</dt><dd>{c.billingCity ?? c.city}, {c.billingState ?? 'TX'} {c.billingZip ?? c.zip}</dd></dl>
        </Panel>
        <Panel id="stars" title="Current Stars" extra={<b style={{ fontSize: '1.4rem' }}>{c.stars.toLocaleString()}</b>}>
          <div className="actions" style={{ marginBottom: 12 }}>
            <button className="btn sm ghost" onClick={() => run(api.post(`/api/customers/${c.id}/stars/recalculate`))}>Recalculate</button>
            <Btn to={`/corral/customers/${c.id}/stars`}>Spend Stars</Btn>
          </div>
          <h3>Rewards History</h3>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Date</th><th>Reward</th><th className="num">Stars</th><th className="num">Net</th></tr></thead>
            <tbody>
              {data.rewards.length === 0 && <EmptyRow cols={5}>No stars yet.</EmptyRow>}
              {data.rewards.slice(0, 12).map((r: Any) => <tr key={r.id}><td className="nowrap"><RecordLink type="star_entry" id={r.id} /></td><td>{date(r.date)}</td><td>{r.reason}</td>
                <td className="num">{r.stars >= 0 ? '+' : ''}{r.stars.toLocaleString()}</td><td className="num">{r.net.toLocaleString()}</td></tr>)}
            </tbody>
          </table></div>
        </Panel>
      </div>

      {/* Status · Plan · Marketing details */}
      <div className="grid-3" id="details">
        <Panel title="Status Details">
          <dl className="kv">
            <dt>Internal ID</dt><dd className="mono">{c.ticket}</dd><dt>Confirmation #</dt><dd className="mono">{c.account}</dd>
            <dt>Internal Status</dt><dd>{c.status}</dd><dt>Status Info</dt><dd>{c.statusInfo ?? '—'}</dd>
            <dt>Order Date</dt><dd>{dateTime(c.createdAt)}</dd><dt>Move/Switch</dt><dd>{c.moveSwitch}</dd>
            <dt>Requested Start</dt><dd>{c.requestedStart ? date(c.requestedStart) : '—'}</dd><dt>Actual Start</dt><dd>{c.actualStart ? date(c.actualStart) : '—'}</dd>
            {c.exception && <><dt>Exception</dt><dd><Pill tone="bad">{c.exception}</Pill></dd></>}
          </dl>
          <form className="form-row" style={{ marginTop: 12 }} onSubmit={(e) => { e.preventDefault(); run(api.put(`/api/customers/${c.id}/status`, { status: new FormData(e.currentTarget).get('status') }), 'Status updated'); }}>
            <select name="status" aria-label="Status" defaultValue={c.status} key={c.status}>{o.statuses.map((s: string) => <option key={s}>{s}</option>)}</select>
            <button className="btn sm">Update</button>
          </form>
        </Panel>
        <Panel title="Plan Details">
          <dl className="kv">
            <dt>Plan Order Date</dt><dd>{data.term ? dateTime(data.term.orderedAt) : '—'}</dd><dt>Plan Name</dt><dd>{data.plan ?? '—'}</dd>
            <dt>Rate ID</dt><dd className="mono">{c.rateClass ?? '—'}</dd><dt>Rate</dt><dd>{data.term ? `${data.term.rate2000.toFixed(5)}¢ @ 2000 kwh` : '—'}</dd>
            <dt>Term</dt><dd>{data.planTerm == null ? '—' : data.planTerm <= 1 ? 'month-to-month' : `${data.planTerm} months`}</dd>
          </dl>
          <div className="actions" style={{ marginTop: 12 }}><Btn to={`/corral/orders/new?renew=${c.account}`}>Renew / Change Plan</Btn></div>
        </Panel>
        <Panel title="Marketing Details">
          <form key={`${c.channel}${c.msid}${c.promoCode}`} onSubmit={(e) => {
            e.preventDefault(); const f = new FormData(e.currentTarget);
            run(api.put(`/api/customers/${c.id}/marketing`, { channel: f.get('channel'), msid: f.get('msid'), promoCode: f.get('promo') }), 'Marketing details saved');
          }}>
            <div className="form-grid" style={{ gridTemplateColumns: '110px 1fr' }}>
              <label htmlFor="channel">Channel</label><input id="channel" name="channel" defaultValue={c.channel} />
              <label htmlFor="msid">MSID</label><input id="msid" name="msid" defaultValue={c.msid ?? ''} />
              <label htmlFor="promo">Promo Code</label><input id="promo" name="promo" defaultValue={c.promoCode ?? ''} />
            </div>
            <button className="btn sm" style={{ marginTop: 10 }}>Save</button>
          </form>
        </Panel>
      </div>

      {/* Files */}
      <Panel id="files" title="Files" extra={<span className="muted">{data.files.length}</span>}>
        <form className="form-row" style={{ marginBottom: 12 }} onSubmit={(e) => {
          e.preventDefault(); const input = e.currentTarget.elements.namedItem('file') as HTMLInputElement;
          if (input.files?.[0]) run(upload(`/api/customers/${c.id}/files`, input.files[0]).then(() => input.form?.reset()), 'File uploaded');
        }}>
          <input type="file" name="file" required aria-label="File to upload" /><button className="btn sm">Upload</button><span className="help">PDF, image, text or Word, up to 10 MB.</span>
        </form>
        <div className="table-wrap"><table><tbody>
          {data.files.map((f: Any) => <tr key={f.id} className="click" onClick={() => navigate(`/corral/files/${f.id}`)}><td style={{ width: '18ch' }}>{dateTime(f.createdAt)}</td><td><Link to={`/corral/files/${f.id}`}>{f.name}</Link></td><td className="muted">{f.kind.toUpperCase()}</td></tr>)}
        </tbody></table></div>
      </Panel>

      {/* Notes and All Notes */}
      <div className="grid-2-1" id="notes-row">
        <div className="panel" id="notes"><div className="panel-head"><h2>Notes ({data.notes.length})</h2></div>
          <div className="panel-body"><NoteForm id={c.id} categories={o.noteCategories} priorities={o.priorities} run={run} /></div>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Date</th><th>By</th><th>Category / Action</th><th>Priority</th><th>Note</th></tr></thead>
            <tbody>
              {data.notes.length === 0 && <EmptyRow cols={6}>No notes yet.</EmptyRow>}
              {data.notes.slice(0, 10).map((n: Any) => <tr key={n.id} className="click" onClick={() => navigate(`/records/note/${n.id}`)}><td className="nowrap"><RecordLink type="note" id={n.id} /></td><td className="nowrap">{dateTime(n.createdAt)}</td><td>{n.author ?? 'System'}</td>
                <td>{n.category ?? '—'}{n.action && <div className="help">{n.action}</div>}</td><td>{n.priority ? <Status value={n.priority} /> : '—'}</td><td className="wrap" style={{ minWidth: 180 }}>{n.body}</td></tr>)}
            </tbody>
          </table></div>
        </div>
        <Panel id="all-notes" table title="All Notes" extra={<span className="muted">{data.notes.length}</span>}>
          <div className="note-feed">
            {data.notes.map((n: Any) => (
              <article key={n.id}><div className="note-meta"><RecordLink type="note" id={n.id} /><span>{dateTime(n.createdAt)}</span><span>{n.author ?? 'System'}</span>
                {n.category && <span>{n.category}{n.action && ` · ${n.action}`}</span>}{n.priority && <Status value={n.priority} />}</div><p>{n.body}</p></article>
            ))}
            {data.notes.length === 0 && <p className="muted" style={{ padding: 16 }}>No notes yet.</p>}
          </div>
        </Panel>
      </div>

      {/* Calls and texts */}
      <div className="grid-2" id="calls" style={{ alignItems: 'start' }}>
        <Panel table title={`Phone Calls (${data.calls.length})`} extra={<span className="actions"><Btn ghost to={`/corral/calls?account=${c.account}`}>All Calls</Btn><Btn cyan to={`/corral/contacts/new?account=${c.account}`}>New Contact</Btn></span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Called</th><th>Issue</th><th className="num">Duration</th><th>Answered By</th><th>Resolution</th></tr></thead>
            <tbody>
              {data.calls.length === 0 && <EmptyRow cols={6}>No calls yet.</EmptyRow>}
              {data.calls.map((p: Any) => <tr key={p.id} className="click" onClick={() => navigate(`/corral/calls/${p.id}`)}><td className="nowrap"><Link to={`/corral/calls/${p.id}`}><b>#{p.id}</b></Link></td><td className="nowrap">{dateTime(p.startedAt)}<br /><span className="help">{p.direction?.[0]?.toUpperCase()}{p.direction?.slice(1)}</span></td>
                <td>{p.issue ?? '—'}</td><td className="num">{duration(p.durationSec)}</td><td>{p.answeredBy ?? '—'}{p.agentId && <><br /><span className="help mono">{p.agentId}</span></>}</td><td><Status value={p.resolution} /></td></tr>)}
            </tbody>
          </table></div>
        </Panel>
        <Panel table title={`Texts (${data.texts.length})`} extra={<span className="actions"><Btn ghost to={`/corral/sms?account=${c.account}`}>All Texts</Btn>{c.phone && <Btn cyan to={`/corral/sms/new?account=${c.account}`}>Send a Text</Btn>}</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Sent</th><th>Direction</th><th>Message</th><th>Resolution</th></tr></thead>
            <tbody>
              {data.texts.length === 0 && <EmptyRow cols={5}>No texts yet.</EmptyRow>}
              {data.texts.map((m: Any) => <tr key={m.id} className="click" onClick={() => navigate(`/corral/sms/${m.id}`)}><td className="nowrap"><Link to={`/corral/sms/${m.id}`}><b>#{m.id}</b></Link></td><td className="nowrap">{dateTime(m.createdAt)}</td>
                <td>{m.direction === 'in' ? 'From customer' : 'To customer'}</td><td className="wrap">{m.body}</td><td><Status value={m.resolution} /></td></tr>)}
            </tbody>
          </table></div>
        </Panel>
      </div>

      {/* Bills & payments */}
      <div id="bills" style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <div className="grid-2">
        <Panel table title="Billing Summary">
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Date</th><th className="num">Billed</th><th className="num">Paid</th><th>Notes</th></tr></thead>
            <tbody>
              {data.summary.length === 0 && <EmptyRow cols={5}>No billing yet{data.bills.length === 0 && ": service hasn't started, the first bill comes about 30 days after the switch"}.</EmptyRow>}
              {data.summary.map((s: Any) => <tr key={`${s.type}${s.id}`} className="click" onClick={() => navigate(`/records/${s.type}/${s.id}`)}><td className="nowrap"><RecordLink type={s.type} id={s.id} /><div className="help">{s.type === 'bill' ? 'Bill' : 'Payment'}</div></td>
                <td>{date(s.date)}</td><td className="num">{s.billed != null ? moneyParens(s.billed) : '-'}</td><td className="num">{s.paid != null ? moneyParens(s.paid) : '-'}</td><td className="mono">{s.note}</td></tr>)}
            </tbody>
          </table></div>
        </Panel>
        <Panel table title="Usage History" extra={<span className="muted">kWh</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>Year</th>{months.map((m) => <th key={m} className="num">{m}</th>)}</tr></thead>
            <tbody>
              {data.usage.length === 0 && <EmptyRow cols={13}>No usage yet{data.bills.length === 0 && ': usage shows after the first bill'}.</EmptyRow>}
              {data.usage.map((u: Any) => <tr key={u.year}><td><b>{u.year}</b></td>{months.map((_, i) => <td key={i} className="num">{u.months[i + 1]?.toLocaleString() ?? '-'}</td>)}</tr>)}
            </tbody>
          </table></div>
        </Panel>
      </div>
      <Panel table title="Bill Details">
        <div className="table-wrap"><table>
          <thead><tr><th>Item</th><th>Type</th><th>Period</th><th className="num">Usage</th><th>Invoice Date</th><th>Due Date</th><th>Paid Date</th><th className="num">Amount Billed</th><th className="num">Amount Paid</th><th className="num">Balance</th><th>Invoice</th><th>Ontime</th></tr></thead>
          <tbody>
            {data.bills.length === 0 && <EmptyRow cols={12}>No bills yet: service hasn't started.</EmptyRow>}
            {data.bills.map((b: Any) => <tr key={b.id}><td className="mono"><Link to={`/corral/customers/${c.id}/history?bill=${b.id}`}>{b.reference}</Link></td><td>{b.billType}</td>
              <td>{b.periodStart && date(b.periodStart)} - {b.periodEnd && date(b.periodEnd)}</td><td className="num">{b.kwh.toLocaleString()}</td><td>{date(b.billedOn)}</td><td>{date(b.dueOn)}</td>
              <td>{b.paidOn ? date(b.paidOn) : '—'}</td><td className="num">{moneyParens(b.amount)}</td><td className="num">{moneyParens(b.amountPaid)}</td><td className="num">{moneyParens(b.balanceAfter)}</td>
              <td className="mono">{b.invoice}</td><td>{b.ontime ? 'Yes' : 'No'}</td></tr>)}
          </tbody>
        </table></div>
      </Panel>
      <Panel table id="payments" title="Payment Details" extra={<span className="actions">
        <button className="btn sm ghost" onClick={() => toggle('allPayments', !allPayments)}>{allPayments ? 'Hide invalid transactions' : 'Show all transactions'}</button>
        <Btn ghost to={`/corral/customers/${c.id}/actions/transfer-payments`}>Transfer Payments</Btn></span>}>
        <div className="table-wrap"><table>
          <thead><tr><th>Item</th><th>Date</th><th>Time</th><th>Source</th><th>Account</th><th>Type</th><th>Status</th><th className="num">Amount</th><th>Confirmation #</th></tr></thead>
          <tbody>
            {data.payments.length === 0 && <EmptyRow cols={9}>No payments yet{data.bills.length === 0 && ': nothing is due until the first bill'}.</EmptyRow>}
            {data.payments.map((p: Any) => <tr key={p.id}><td className="mono"><Link to={`/records/payment/${p.id}`}>{p.reference || `#${p.id}`}</Link></td><td>{p.paidOn}</td><td>{p.paidTime ?? ''}</td><td>{p.source}</td>
              <td>{p.method ?? 'Unknown account'}{p.last4 && <><br /><span className="help">ending {p.last4}</span></>}</td><td>{p.kind}</td><td><Status value={p.status} /></td><td className="num">{moneyParens(p.amount)}</td>
              <td className="mono">{p.confirmation}{p.status === 'Reversed' && p.reversedAt && ` Reversed ${date(p.reversedAt)}`}
                {o.canRefund && p.status === 'Success' && <> <button className="link-btn" onClick={() => confirm(`Reverse ${p.reference} for ${money(p.amount)}? It goes back on the balance.`) && run(api.post(`/api/customers/payments/${p.id}/reverse`), 'Payment reversed')}>[ reverse ]</button></>}</td></tr>)}
          </tbody>
        </table></div>
      </Panel>
      <Panel table id="payment-methods" title="Payment Methods" extra={<button className="btn sm ghost" onClick={() => toggle('allMethods', !allMethods)}>{allMethods ? 'Hide deleted methods' : 'Show all methods'}</button>}>
        <div className="table-wrap"><table>
          <thead><tr><th>Item</th><th>Date</th><th>Type</th><th>Expires</th><th>Nickname</th><th>Vendor</th><th className="num">Total Paid</th><th>Autopay</th><th /></tr></thead>
          <tbody>
            {data.methods.length === 0 && <EmptyRow cols={9}>No payment methods.</EmptyRow>}
            {data.methods.map((m: Any) => <tr key={m.id} className={m.removedAt ? 'muted' : ''}><td className="mono"><Link to={`/records/payment_method/${m.id}`}>{m.id}</Link></td><td>{date(m.createdAt)}</td>
              <td>{m.type} - {m.last4}</td><td>{m.expires ?? '—'}</td><td>{m.nickname}</td><td>{m.vendor}</td><td className="num">{money(data.methodTotals[m.id] ?? 0)}</td><td>{m.autopay ? 'Yes' : ''}</td>
              <td>{m.removedAt ? 'removed' : <>
                <button className="link-btn" onClick={() => confirm('Remove this payment method?') && run(api.del(`/api/customers/methods/${m.id}`))}>[ remove ]</button>{' '}
                {!m.autopay && <button className="link-btn" onClick={() => run(api.post(`/api/customers/methods/${m.id}/autopay`))}>[ set autopay ]</button>}</>}</td></tr>)}
          </tbody>
        </table></div>
      </Panel>
      </div>

      {/* Plans & addresses */}
      <Panel table id="plans" title="Plan Change Log">
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Date Ordered</th><th>Contract Start</th><th>Contract End</th><th>Actual Term</th><th>Rate</th><th>Energy Charge</th><th>Plan</th><th /></tr></thead>
          <tbody>
            {data.planTerms.length === 0 && <EmptyRow cols={9}>No plans.</EmptyRow>}
            {data.planTerms.map((t: Any) => <PlanTermRow key={t.id} t={t} run={run} />)}
          </tbody>
        </table></div>
      </Panel>
      <Panel table title="Address Change Log">
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Date Ordered</th><th>Service Start</th><th>Service End</th><th>Service Length</th><th>ESIID</th><th>Address</th><th><span className="sr-only">Change dates</span></th></tr></thead>
          <tbody>{data.addresses.map((a: Any) => <AddressRow key={a.id} a={a} run={run} />)}</tbody>
        </table></div>
      </Panel>

      {/* ERCOT */}
      <Panel table id="ercot" title="ERCOT Transactions" extra={<span className="mono muted">{c.address?.toUpperCase()}, {c.city?.toUpperCase()} {c.zip} · {c.esiid}</span>}>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Date Saved</th><th>Trans Date</th><th>Purpose</th><th>Scheduled</th><th>Trans Type</th><th>Tracking</th><th>Status</th><th /></tr></thead>
          <tbody>
            {data.ercot.length === 0 && <EmptyRow cols={9}>No ERCOT transactions.</EmptyRow>}
            {data.ercot.map((t: Any) => <tr key={t.id}><td className="nowrap"><RecordLink type="ercot_transaction" id={t.id} /></td><td>{t.createdAt.replace('T', ' ').slice(0, 19)}</td><td>{date(t.transDate)}</td><td>{t.purpose}</td>
              <td>{t.scheduledOn ? date(t.scheduledOn) : '—'}</td><td><span className="mono">{t.transType}</span> {t.label}</td><td className="mono">{t.tracking ?? '—'}</td><td>{t.status}</td>
              <td>{t.status === 'linked' && <form className="form-row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/ercot/${t.id}`, { do: new FormData(e.currentTarget).get('do') })); }}>
                <select name="do" aria-label="Transaction action"><option value="unlink">Unlink</option>{t.purpose === 'request' && <option value="cancel">Cancel</option>}</select><button className="btn sm">Go</button></form>}</td></tr>)}
          </tbody>
        </table></div>
      </Panel>

      {/* TECH: order-flow queues */}
      <Panel id="tech" title="TECH · Move Queues">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {data.queueLogs.filter((q: Any) => !q.exitedAt).length === 0 && <p className="muted">Not in any queue.</p>}
        {data.queueLogs.filter((q: Any) => !q.exitedAt).map((q: Any) => (
          <form key={q.id} className="form-row" onSubmit={(e) => { e.preventDefault(); run(api.put(`/api/customers/queue-logs/${q.id}`, { name: new FormData(e.currentTarget).get('queue') })); }}>
            <span className="pill info mono">{q.queue}</span>
            <select name="queue" defaultValue={q.queue} aria-label={`Move from ${q.queue}`}><option value="remove">- - Remove This Queue - -</option>{o.queues.map((n: string) => <option key={n}>{n}</option>)}</select>
            <button className="btn sm">Save</button>
          </form>
        ))}
        <form className="form-row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/customers/${c.id}/queues`, { name: new FormData(e.currentTarget).get('queue') })); }}>
          <select name="queue" required aria-label="Add queue"><option value="">- - Add Queue - -</option>{o.queues.map((n: string) => <option key={n}>{n}</option>)}</select><button className="btn sm">Add</button>
        </form>
        <h3 style={{ marginTop: 8 }}>Details</h3>
        <div className="table-wrap" style={{ maxHeight: 300, overflowY: 'auto' }}><table><tbody>
          {data.queueLogs.flatMap((q: Any) => [
            <tr key={q.id}><td style={{ width: '22ch' }}>{queueTime(q.enteredAt)}</td><td className="mono">{queueLog(q.queue)}</td><td>{q.note}</td></tr>,
            q.exitedAt && <tr key={q.id + 'out'}><td>{queueTime(q.exitedAt)}</td><td className="mono">{queueLog(q.queue)}_out</td><td /></tr>,
          ])}
        </tbody></table></div>
        </div>
      </Panel>

      {/* Warehouse */}
      <Panel id="warehouse" title="Warehouse" extra={<span className="muted">updated {new Date().toLocaleDateString('en-US')} at {new Date().toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}</span>}>
        <dl className="kv warehouse">{Object.entries(data.warehouse).map(([k, v]) => <Pair key={k} k={k} v={v} />)}</dl>
      </Panel>

      {/* Logs */}
      <div className="panel" id="logs"><div className="panel-head"><h2>Logs</h2></div><div className="panel-body actions">
        <Link className="btn ghost" to={`/corral/customers/${c.id}/api-log`}>API Log</Link>
        <Link className="btn ghost" to={`/corral/customers/${c.id}/contact-log`}>Contact Log</Link>
        <Link className="btn ghost" to={`/corral/customers/${c.id}/history?log=attributes`}>Attribute Log</Link>
        <Link className="btn ghost" to={`/corral/customers/${c.id}/history`}>Child Items</Link>
        {data.logs.map((l: Any) => <Link key={l.key} className="btn ghost" to={`/corral/customers/${c.id}/history?log=${l.key}`}>{l.title} <span className="count-pill">{l.count}</span></Link>)}
      </div></div>
    </>
  );
}

/** The TECH queue log lines, as ebunny writes them: 10/26/2026 09:04:05 PM, queuelog_cart_full_ready_flowings */
const queueTime = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' }).replace(',', '');
const queueLog = (q: string) => 'queuelog_' + q.replace(/^Queue/, '').replace(/([a-z0-9])([A-Z])/g, '$1_$2').toLowerCase() + 's';

const Pair = ({ k, v }: { k: string; v: unknown }) => <><dt className="mono">{k}</dt><dd className="mono">{v == null ? '' : String(v)}</dd></>;

function ContactInfo({ c, authorized, onSaved }: { c: Any; authorized: Any[]; onSaved: () => void }) {
  const [editing, setEditing] = useState(false);
  const [run, bar] = useRun(() => { setEditing(false); onSaved(); });
  if (editing)
    return (
      <form onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget);
        run(api.put(`/api/customers/${c.id}/contact`, { email: f.get('email'), phone: f.get('phone'), phoneType: f.get('phoneType'), language: f.get('language') })); }}>
        {bar}
        <div className="form-grid" style={{ gridTemplateColumns: '110px 1fr' }}>
          <label htmlFor="ci-email">Email</label><input id="ci-email" name="email" defaultValue={c.email ?? ''} />
          <label htmlFor="ci-phone">Phone</label><input id="ci-phone" name="phone" defaultValue={c.phone ?? ''} />
          <label htmlFor="ci-type">Phone type</label><select id="ci-type" name="phoneType" defaultValue={c.phoneType ?? 'Mobile'}><option>Mobile</option><option>Home</option><option>Work</option></select>
          <label htmlFor="ci-lang">Language</label><select id="ci-lang" name="language" defaultValue={c.language}><option>English</option><option>Spanish</option></select>
        </div>
        <div className="actions" style={{ marginTop: 12 }}><button className="btn sm cyan">Save</button><button type="button" className="btn sm ghost" onClick={() => setEditing(false)}>Cancel</button></div>
      </form>
    );
  return (
    <>
      <dl className="kv">
        <dt>IP</dt><dd className="mono">{c.ip ?? '—'}</dd><dt>IP Location</dt><dd>{c.ipLocation ?? '—'}</dd>
        <dt>First Name</dt><dd>{c.firstName}</dd><dt>Last Name</dt><dd>{c.lastName}</dd><dt>Email</dt><dd>{c.email}</dd>
        <dt>Phone</dt><dd>{c.phone ? <span className="phone-actions"><a href={`tel:${c.phone}`}>{c.phone}</a> <Link to={`/corral/sms/new?account=${c.account}`}>[ text ]</Link></span> : '—'}</dd>
        <dt>Phone type</dt><dd>{c.phoneType ?? '—'}</dd><dt>SSN</dt><dd className="mono">{c.ssnLast4 ? `***-**-${c.ssnLast4}` : '—'}</dd>
        <dt>TEC Score</dt><dd>{c.tecScore ?? '—'}</dd><dt>Status Info</dt><dd>{c.statusInfo ?? '—'}</dd><dt>Language</dt><dd>{c.language}</dd>
        <dt>Username</dt><dd>{c.username ?? '—'} <Link to={`/corral/customers/${c.id}/actions/reset-login`}>[ reset ]</Link></dd>
        <dt>Linked Accounts</dt><dd>{c.linkedAccounts || '(none)'}</dd>
        <dt>Authorized Users</dt><dd>{authorized.length === 0 ? <span className="muted">(none)</span> : authorized.map((u) => <span key={u.name + u.added}>{u.name} · {u.phone}<br /></span>)}
          {' '}<Link to={`/corral/customers/${c.id}/actions/add-authorized-user`}>[ add ]</Link></dd>
      </dl>
      <div className="actions" style={{ marginTop: 12 }}><button className="btn sm ghost" onClick={() => setEditing(true)}>Edit contact details</button></div>
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
    <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 16 }}>
      <div className="form-row">
        <select name="category" aria-label="Category" value={category} onChange={(e) => setCategory(e.target.value)}><option value="">-- Category --</option>{Object.keys(categories).map((k) => <option key={k}>{k}</option>)}</select>
        <select name="action" aria-label="Action" disabled={!category}><option value="">-- Action --</option>{(categories[category] ?? []).map((a) => <option key={a}>{a}</option>)}</select>
        <select name="priority" aria-label="Priority"><option value="">-- Priority --</option>{priorities.map((p) => <option key={p}>{p}</option>)}</select>
      </div>
      <textarea name="body" required placeholder="New note" aria-label="New note" style={{ minHeight: 70 }} />
      <div><button className="btn">Add Note</button></div>
    </form>
  );
}

const monthsBetween = (from: string, to: string) => {
  const a = new Date(from), b = new Date(to);
  return Math.max(0, (b.getFullYear() - a.getFullYear()) * 12 + b.getMonth() - a.getMonth() - (b.getDate() < a.getDate() ? 1 : 0));
};
const monthsSince = (from: string) => monthsBetween(from, new Date().toISOString().slice(0, 10));

function PlanTermRow({ t, run }: { t: Any; run: (p: Promise<unknown>) => Promise<void> }) {
  const [what, setWhat] = useState('welcome');
  return (
    <tr>
      <td className="nowrap"><RecordLink type="plan_term" id={t.id} /></td><td>{t.orderedAt.replace('T', ' ').slice(0, 19)}</td><td>{t.contractStart ? date(t.contractStart) : '—'}</td><td>{t.contractEnd ? date(t.contractEnd) : '—'}</td>
      <td><Pill tone={t.status === 'current' ? 'ok' : t.status === 'ended' ? '' : 'info'}>{t.status === 'current' && t.contractStart ? `current - ${monthsSince(t.contractStart)} months in` : t.status}</Pill></td><td>{t.rate2000.toFixed(1)} ¢ @ 2,000</td><td className="mono">{t.energyCharge.toFixed(8)}</td><td>{t.plan}<br /><span className="mono help">{t.rateClass}</span></td>
      <td><form className="form-row" onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget); run(api.post(`/api/customers/plan-terms/${t.id}`, { do: what, energyCharge: f.get('energyCharge') ? Number(f.get('energyCharge')) : null })); }}>
        <select value={what} aria-label="Plan action" onChange={(e) => setWhat(e.target.value)}><option value="welcome">Send Welcome Packet</option><option value="new-welcome">Send New Welcome Packet</option>
          {t.status !== 'current' && <option value="activate">Activate</option>}<option value="edit-rate">Edit Rate</option></select>
        {what === 'edit-rate' && <input name="energyCharge" type="number" step="0.00000001" min="0" max="1" defaultValue={t.energyCharge} style={{ width: '12ch' }} aria-label="Energy charge" />}
        <button className="btn sm">Go</button></form></td>
    </tr>
  );
}

function AddressRow({ a, run }: { a: Any; run: (p: Promise<unknown>, done?: string) => Promise<void> }) {
  const [editing, setEditing] = useState(false);
  return (
    <>
      <tr>
        <td className="nowrap"><RecordLink type="service_address" id={a.id} /></td><td className="nowrap">{dateTime(a.orderedAt)}</td><td>{a.serviceStart ? date(a.serviceStart) : '—'}</td><td>{a.serviceEnd ? date(a.serviceEnd) : '—'}</td>
        <td>{a.serviceStart ? (a.serviceEnd ? `${monthsBetween(a.serviceStart, a.serviceEnd)} months` : `current - ${monthsSince(a.serviceStart)} months in`) : 'not started'}</td>
        <td className="mono">{a.esiid}</td><td>{a.street?.toUpperCase()}<br />{a.city}, TX {a.zip}</td>
        <td className="actions"><button type="button" className="btn sm ghost" aria-expanded={editing} onClick={() => setEditing(!editing)}>Change Dates</button></td>
      </tr>
      {editing && (
        <tr className="edit-row"><td colSpan={8}>
          <form className="date-edit" onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget);
            run(api.put(`/api/customers/addresses/${a.id}`, { serviceStart: f.get('start') || null, serviceEnd: f.get('end') || null }).then(() => setEditing(false)), 'Service dates updated'); }}>
            <div className="field"><label htmlFor={`ms${a.id}`}>Service start (move-in)</label><input id={`ms${a.id}`} type="date" name="start" defaultValue={a.serviceStart ?? ''} /></div>
            <div className="field"><label htmlFor={`me${a.id}`}>Service end (move-out)</label><input id={`me${a.id}`} type="date" name="end" defaultValue={a.serviceEnd ?? ''} /></div>
            <div className="actions"><button className="btn sm cyan">Save Dates</button><button type="button" className="btn sm ghost" onClick={() => setEditing(false)}>Cancel</button></div>
          </form>
        </td></tr>
      )}
    </>
  );
}
