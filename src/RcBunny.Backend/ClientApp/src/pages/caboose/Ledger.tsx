import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, date, dateTime, money, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, Panel, RecordLink, age, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const cap = (s: string) => s[0].toUpperCase() + s.slice(1);
const statusText = (s: string) => (s === 'deleted' ? 'Rejected' : cap(s));
const signed = (v: number) => `${v < 0 ? '-' : ''}${money(Math.abs(v))}${v < 0 ? ' (credit)' : ''}`;

/** Caboose → Credits & Debits (ebunny's caboose/ledger): pending ones to apply or reject, and recently decided. */
export default function Ledger() {
  const { data, error, reload } = useLoad<any>('/api/caboose/ledger');
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const decide = (id: number, d: string) => run(api.post(`/api/caboose/ledger/${id}/decide`, { do: d }), d === 'apply' ? 'Applied to the balance' : 'Rejected');
  return (
    <>
      <PageHead title="Credits & Debits" sub="Bill credits, promos and manual debits added in Corral or by rewards wait here until finance applies them to the balance." />
      {bar}
      <Panel title="Pending" right={<span className="muted">{data.pending.length}</span>}>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Added</th><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Description</th><th>By</th><th /></tr></thead>
          <tbody>
            {data.pending.map((e: any) => (
              <tr key={e.id} className="click" onClick={() => navigate(`/caboose/credits-debits/${e.id}`)}>
                <td className="nowrap"><Link to={`/caboose/credits-debits/${e.id}`}><b>#{e.id}</b></Link></td><td>{date(e.createdAt)}</td>
                <td onClick={(x) => x.stopPropagation()}><AccountNo id={e.customerId} account={e.account} /><br /><span className="help">{e.customer}</span></td>
                <td>{cap(e.kind)}</td><td className="num">{money(e.amount)}</td><td>{e.description}</td><td>{e.requestedBy || 'System'}</td>
                <td className="actions" onClick={(x) => x.stopPropagation()}><button className="btn sm" onClick={() => decide(e.id, 'apply')}>Apply</button><button className="btn sm ghost" onClick={() => decide(e.id, 'reject')}>Reject</button></td></tr>
            ))}
            {data.pending.length === 0 && <EmptyRow cols={8}>Nothing pending.</EmptyRow>}
          </tbody>
        </table></div>
      </Panel>
      <Panel title="Recently Decided">
        <div className="table-wrap"><table>
          <thead><tr><th>Decided</th><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Description</th><th>By</th><th>Status</th></tr></thead>
          <tbody>
            {data.recent.map((e: any) => (
              <tr key={e.id} className="click" onClick={() => navigate(`/caboose/credits-debits/${e.id}`)}>
                <td><Link to={`/caboose/credits-debits/${e.id}`}>{date(e.decidedAt ?? e.createdAt)}</Link></td><td onClick={(x) => x.stopPropagation()}><AccountNo id={e.customerId} account={e.account} /></td>
                <td>{cap(e.kind)}</td><td className="num">{money(e.amount)}</td><td>{e.description}</td><td>{e.decidedBy ?? '—'}</td>
                <td><span className={`pill ${e.status === 'applied' ? 'ok' : ''}`}>{statusText(e.status)}</span></td></tr>
            ))}
            {data.recent.length === 0 && <EmptyRow cols={7}>None yet.</EmptyRow>}
          </tbody>
        </table></div>
      </Panel>
    </>
  );
}

/** One credit or debit (ebunny's caboose/ledger-show). */
export function LedgerShow() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/caboose/ledger/${id}`);
  const [run, bar] = useRun(reload);
  const [note, setNote] = useState('');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const e = data.entry, c = data.customer, pending = e.status === 'pending';
  const tone = ({ pending: 'info', applied: 'ok' } as Record<string, string>)[e.status] ?? '';
  const decide = (d: string) => run(api.post(`/api/caboose/ledger/${e.id}/decide`, { do: d, note }), d === 'apply' ? 'Applied to the balance' : 'Rejected');
  return (
    <>
      <PageHead title={`${cap(e.kind)} #${e.id} · ${money(e.amount)}`} sub={<>{e.description} · <span className={`pill ${tone}`}>{statusText(e.status)}</span></>}
        actions={<Link className="btn ghost" to="/caboose/credits-debits">All Credits &amp; Debits</Link>} />
      {bar}
      <div className="grid-3">
        <Panel title={cap(e.kind)}><div className="panel-body"><dl className="kv">
          <dt>Kind</dt><dd>{cap(e.kind)} ({e.kind === 'credit' ? 'lowers' : 'raises'} the balance)</dd>
          <dt>Amount</dt><dd><b>{money(e.amount)}</b></dd><dt>Description</dt><dd>{e.description}</dd>
          <dt>Added</dt><dd>{dateTime(e.createdAt)} ({age(e.createdAt)} ago)</dd><dt>Added By</dt><dd>{e.requestedBy || 'System (rewards, promos, products)'}</dd>
          <dt>Status</dt><dd>{statusText(e.status)}</dd>
          {!pending && <><dt>Decided</dt><dd>{e.decidedAt ? dateTime(e.decidedAt) : '—'}</dd><dt>Decided By</dt><dd>{e.decidedBy ?? '—'}</dd><dt>Note</dt><dd>{e.decisionNote ?? '—'}</dd></>}
        </dl></div></Panel>
        <Panel title="Account"><div className="panel-body"><dl className="kv">
          <dt>Account</dt><dd><AccountNo id={c.id} account={c.account} /></dd><dt>Name</dt><dd>{c.name}</dd><dt>Status</dt><dd>{c.status}</dd>
          <dt>Balance Now</dt><dd>{signed(c.balance)}</dd>
          {pending && <><dt>After Applying</dt><dd><b>{signed(data.after)}</b></dd></>}
          <dt>Deposit Held</dt><dd>{money(c.depositHeld)}</dd><dt>AutoPay</dt><dd>{c.autopay ? 'On' : 'Off'}</dd>
        </dl></div></Panel>
        <Panel title="Decision"><div className="panel-body">
          {pending ? (
            <form className="stack" onSubmit={(ev) => { ev.preventDefault(); decide('apply'); }}>
              <div className="field"><label htmlFor="note">Note (optional)</label><input id="note" name="note" maxLength={250} value={note} onChange={(ev) => setNote(ev.target.value)} placeholder="e.g. Matches the product order" /></div>
              <div className="actions"><button className="btn cyan">Apply to Balance</button><button type="button" className="btn ghost" onClick={() => decide('reject')}>Reject</button></div>
              <p className="help">Applying {e.kind === 'credit' ? 'takes' : 'adds'} {money(e.amount)} {e.kind === 'credit' ? 'off' : 'to'} the balance and leaves a note on the account.</p>
            </form>
          ) : <p>{e.status === 'applied' ? 'Applied to the balance' : 'Rejected'} by {e.decidedBy ?? 'finance'} on {e.decidedAt ? date(e.decidedAt) : '—'}.</p>}
        </div></Panel>
      </div>
      {data.others.length > 0 && (
        <Panel title="Other Credits & Debits on This Account" right={<span className="muted">{data.others.length}</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Added</th><th>Kind</th><th className="num">Amount</th><th>Description</th><th>By</th><th>Status</th></tr></thead>
            <tbody>{data.others.map((o: any) => (
              <tr key={o.id} className="click" onClick={() => navigate(`/caboose/credits-debits/${o.id}`)}><td className="nowrap"><Link to={`/caboose/credits-debits/${o.id}`}><b>#{o.id}</b></Link></td>
                <td>{date(o.createdAt)}</td><td>{cap(o.kind)}</td><td className="num">{money(o.amount)}</td><td>{o.description}</td><td>{o.requestedBy || 'System'}</td>
                <td><span className={`pill ${({ pending: 'info', applied: 'ok' } as Record<string, string>)[o.status] ?? ''}`}>{statusText(o.status)}</span></td></tr>
            ))}</tbody>
          </table></div>
        </Panel>
      )}
      <div className="grid-2">
        <Panel title="Recent Bills"><div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Billed</th><th>Invoice</th><th className="num">Amount</th><th className="num">Balance After</th></tr></thead>
          <tbody>{data.bills.map((b: any) => <tr key={b.id}><td className="nowrap"><RecordLink type="bill" id={b.id} /></td><td>{date(b.billedOn)}</td><td className="mono">{b.invoice ?? b.reference}</td><td className="num">{money(b.amount)}</td><td className="num">{money(b.balanceAfter)}</td></tr>)}
            {data.bills.length === 0 && <EmptyRow cols={5}>No bills yet.</EmptyRow>}</tbody>
        </table></div></Panel>
        <Panel title="Recent Payments"><div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Paid</th><th>Method</th><th className="num">Amount</th><th>Status</th></tr></thead>
          <tbody>{data.payments.map((p: any) => <tr key={p.id}><td className="nowrap"><RecordLink type="payment" id={p.id} /></td><td>{date(p.paidOn)}</td><td>{p.method} · {p.source}</td><td className="num">{money(p.amount)}</td><td><span className={`pill ${p.status === 'Success' ? 'ok' : 'bad'}`}>{p.status}</span></td></tr>)}
            {data.payments.length === 0 && <EmptyRow cols={5}>No payments yet.</EmptyRow>}</tbody>
        </table></div></Panel>
      </div>
      {data.notes.length > 0 && (
        <Panel title="Account Notes" right={<span className="muted">about credits and debits</span>}><div className="table-wrap"><table><tbody>
          {data.notes.map((n: any) => <tr key={n.id}><td className="nowrap">{dateTime(n.createdAt)}</td><td>{n.author}</td><td className="wrap">{n.body}</td></tr>)}
        </tbody></table></div></Panel>
      )}
    </>
  );
}
