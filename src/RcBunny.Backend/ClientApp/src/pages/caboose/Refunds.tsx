import { type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, money, useLoad } from '../../api';
import { useAuth } from '../../auth';
import { AccountNo, EmptyRow, PageHead, Panel, RecordLink, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const cap = (s: string) => s[0].toUpperCase() + s.slice(1);
const tone: Record<string, string> = { requested: 'info', approved: 'ok', paid: 'ok', rejected: 'bad' };

/** Caboose → Refunds (ebunny's caboose/refunds): open refunds, credit balances without one, recently closed. */
export default function Refunds() {
  const { data, error, reload } = useLoad<any>('/api/caboose/refunds');
  const [run, bar] = useRun(reload);
  const { can } = useAuth();
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const decide = (id: number, d: string) => run(api.post(`/api/caboose/refunds/${id}/decide`, { do: d }), { approve: 'Approved', reject: 'Rejected', paid: 'Marked paid' }[d]);
  return (
    <>
      <PageHead title="Refunds" sub="Refunds of credit balances and deposits. Anyone in Caboose can request one; approving needs the refunds right."
        actions={<Link className="btn cyan" to="/caboose/refunds/new">Request a Refund</Link>} />
      {bar}
      <div className="stack">
        <Panel title="Open" right={<span className="muted">{data.open.length}</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Requested</th><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Reason</th><th>Method</th><th>Status</th><th /></tr></thead>
            <tbody>
              {data.open.map((r: any) => (
                <tr key={r.id} className="click" onClick={() => navigate(`/caboose/refunds/${r.id}`)}>
                  <td className="nowrap"><Link to={`/caboose/refunds/${r.id}`}><b>#{r.id}</b></Link></td><td>{date(r.createdAt)}<br /><span className="help">{r.requestedBy}</span></td>
                  <td onClick={(e) => e.stopPropagation()}><AccountNo id={r.customerId} account={r.account} /><br /><span className="help">{r.customer}</span></td>
                  <td>{cap(r.kind)}</td><td className="num">{money(r.amount)}</td><td className="wrap">{r.reason}</td><td>{r.method}</td>
                  <td><span className={`pill ${r.status === 'approved' ? 'ok' : 'info'}`}>{cap(r.status)}</span></td>
                  <td className="actions" onClick={(e) => e.stopPropagation()}>{!can('refunds') ? <span className="help">Needs refunds right</span>
                    : r.status === 'requested' ? <><button className="btn sm" onClick={() => decide(r.id, 'approve')}>Approve</button><button className="btn sm ghost" onClick={() => decide(r.id, 'reject')}>Reject</button></>
                    : <button className="btn sm" onClick={() => confirm(`Mark refund paid?\nConfirm the ${r.method.toLowerCase()} for ${money(r.amount)} was sent.`) && decide(r.id, 'paid')}>Mark Paid</button>}</td></tr>
              ))}
              {data.open.length === 0 && <EmptyRow cols={9}>No open refunds.</EmptyRow>}
            </tbody>
          </table></div>
        </Panel>
        <Panel title="Credit Balances Without a Refund" right={<span className="muted">{data.credits.length}</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>Account</th><th>Name</th><th>Status</th><th className="num">Credit</th><th /></tr></thead>
            <tbody>
              {data.credits.map((c: any) => (
                <tr key={c.id}><td><AccountNo id={c.id} account={c.account} /></td><td>{c.name}</td><td>{c.status}</td><td className="num">{money(Math.abs(c.balance))}</td>
                  <td className="actions"><Link className="btn sm ghost" to={`/caboose/refunds/new?account=${c.account}&amount=${Math.abs(c.balance).toFixed(2)}`}>Request Refund</Link></td></tr>
              ))}
              {data.credits.length === 0 && <EmptyRow cols={5}>None.</EmptyRow>}
            </tbody>
          </table></div>
        </Panel>
        <Panel title="Recently Closed">
          <div className="table-wrap"><table>
            <thead><tr><th>Closed</th><th>Account</th><th>Kind</th><th className="num">Amount</th><th>Status</th><th>Decided By</th></tr></thead>
            <tbody>
              {data.done.map((r: any) => (
                <tr key={r.id} className="click" onClick={() => navigate(`/caboose/refunds/${r.id}`)}><td><Link to={`/caboose/refunds/${r.id}`}>{date(r.paidAt ?? r.decidedAt ?? r.createdAt)}</Link></td>
                  <td onClick={(e) => e.stopPropagation()}><AccountNo id={r.customerId} account={r.account} /></td><td>{cap(r.kind)}</td><td className="num">{money(r.amount)}</td>
                  <td><span className={`pill ${r.status === 'paid' ? 'ok' : ''}`}>{cap(r.status)}</span></td><td>{r.decidedBy}</td></tr>
              ))}
              {data.done.length === 0 && <EmptyRow cols={6}>None yet.</EmptyRow>}
            </tbody>
          </table></div>
        </Panel>
      </div>
    </>
  );
}

/** Request a Refund (ebunny's caboose/refund-form). */
export function RefundForm() {
  const [params] = useSearchParams();
  const [run, bar] = useRun();
  const navigate = useNavigate();
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/caboose/refunds', { account: f.get('account'), kind: f.get('kind'), amount: Number(f.get('amount')), method: f.get('method'), reason: f.get('reason') })
      .then((r) => { navigate('/caboose/refunds'); return r; }));
  };
  return (
    <>
      <PageHead title="Request a Refund" sub="A refund of a credit balance or deposit. Approving it needs the refunds right." />
      {bar}
      <form className="panel simple-form" onSubmit={submit}>
        <div className="panel-body form-grid">
          <label htmlFor="f-account">Account #</label><input id="f-account" name="account" required defaultValue={params.get('account') ?? ''} />
          <label htmlFor="f-kind">Refund Of</label><select id="f-kind" name="kind"><option value="balance">Credit balance</option><option value="deposit">Deposit</option></select>
          <label htmlFor="f-amount">Amount ($)</label><input id="f-amount" name="amount" type="number" step="0.01" min="0.01" required defaultValue={params.get('amount') ?? ''} />
          <label htmlFor="f-method">Send By</label><select id="f-method" name="method"><option>Check</option><option>Card</option><option>ACH</option></select>
          <label htmlFor="f-reason">Reason</label><input id="f-reason" name="reason" required />
        </div>
        <div className="panel-body actions"><button className="btn cyan">Request Refund</button><Link className="btn ghost" to="/caboose/refunds">Cancel</Link></div>
      </form>
    </>
  );
}

/** One refund (ebunny's caboose/refund-show). */
export function RefundShow() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/caboose/refunds/${id}`);
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const r = data.refund, c = data.customer;
  const decide = (e: FormEvent<HTMLFormElement>, d: string) => { e.preventDefault(); const f = new FormData(e.currentTarget); run(api.post(`/api/caboose/refunds/${r.id}/decide`, { do: d, note: f.get('note'), checkNumber: f.get('check_number') }), { approve: 'Approved', reject: 'Rejected', paid: 'Marked paid' }[d]); };
  const signed = (v: number) => `${v < 0 ? '-' : ''}${money(Math.abs(v))}${v < 0 ? ' (credit)' : ''}`;
  return (
    <>
      <PageHead title={`Refund #${r.id} · ${money(r.amount)}`} sub={<>{cap(r.kind)} refund by {r.method} · <span className={`pill ${tone[r.status] ?? ''}`}>{cap(r.status)}</span></>}
        actions={<Link className="btn ghost" to="/caboose/refunds">All Refunds</Link>} />
      {bar}
      <Panel title="Progress"><div className="table-wrap"><table>
        <thead><tr><th>Step</th><th>When</th><th>Who</th><th>Detail</th></tr></thead>
        <tbody>
          <tr><td><b>Requested</b></td><td>{dateTime(r.createdAt)}</td><td>{r.requestedBy || 'System'}</td><td className="wrap">{r.reason}</td></tr>
          <tr><td><b>{r.status === 'rejected' ? 'Rejected' : 'Approved'}</b></td><td>{r.decidedAt ? dateTime(r.decidedAt) : 'Waiting'}</td><td>{r.decidedBy ?? '—'}</td><td className="wrap">{r.decisionNote ?? (r.decidedAt ? '—' : 'Needs someone with the refunds right')}</td></tr>
          {r.status !== 'rejected' && <tr><td><b>Paid</b></td><td>{r.paidAt ? dateTime(r.paidAt) : 'Waiting'}</td><td>{r.paidAt ? 'Finance' : '—'}</td><td>{r.paidAt ? `${r.method}${r.checkNumber ? ` #${r.checkNumber}` : ''}` : 'Sent once approved'}</td></tr>}
        </tbody>
      </table></div></Panel>
      <div className="grid-3">
        <Panel title="Refund"><div className="panel-body"><dl className="kv">
          <dt>Amount</dt><dd><b>{money(r.amount)}</b></dd><dt>Of</dt><dd>{r.kind === 'deposit' ? 'Deposit held' : 'Credit balance'}</dd><dt>Method</dt><dd>{r.method}</dd>
          <dt>Reason</dt><dd>{r.reason}</dd><dt>Status</dt><dd>{cap(r.status)}</dd>{r.checkNumber && <><dt>Check #</dt><dd className="mono">{r.checkNumber}</dd></>}
        </dl></div></Panel>
        <Panel title="Account"><div className="panel-body"><dl className="kv">
          <dt>Account</dt><dd><AccountNo id={c.id} account={c.account} /></dd><dt>Name</dt><dd>{c.name}</dd><dt>Status</dt><dd>{c.status}</dd>
          <dt>Balance</dt><dd>{signed(c.balance)}</dd><dt>Deposit Held</dt><dd>{money(c.depositHeld)}</dd>
          <dt>Can Refund Now</dt><dd>{money(data.limit)} {r.kind === 'deposit' ? 'of deposit' : 'of credit'}</dd>
        </dl></div></Panel>
        <Panel title="Next Step"><div className="panel-body">
          {['requested', 'approved'].includes(r.status) ? (data.canDecide ? (r.status === 'requested' ? (
            <form className="stack" onSubmit={(e) => decide(e, ((e.nativeEvent as SubmitEvent).submitter as HTMLButtonElement)?.value || 'approve')}>
              <div className="field"><label htmlFor="note">Note (optional)</label><input id="note" name="note" maxLength={250} placeholder="e.g. Final bill confirmed" /></div>
              <div className="actions"><button className="btn cyan" value="approve">Approve</button><button className="btn ghost" value="reject">Reject</button></div>
            </form>
          ) : (
            <form className="stack" onSubmit={(e) => decide(e, 'paid')}>
              <div className="field"><label htmlFor="check_number">{r.method === 'Check' ? 'Check number' : 'Confirmation #'}</label><input id="check_number" name="check_number" maxLength={30} className="mono" /></div>
              <div className="actions"><button className="btn cyan">Mark Paid</button></div>
              <p className="help">Marking paid takes {money(r.amount)} off the {r.kind === 'deposit' ? 'deposit held' : 'credit balance'}.</p>
            </form>
          )) : <p className="muted">Approving and paying refunds needs the refunds right.</p>)
            : <p>{r.status === 'paid' ? `Sent ${r.paidAt ? date(r.paidAt) : ''}. Nothing left to do.` : 'Rejected. Request a new refund if anything changes.'}</p>}
        </div></Panel>
      </div>
      <div className={data.others.length ? 'grid-2' : ''}>
        {data.others.length > 0 && <Panel title="Other Refunds on This Account"><div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Requested</th><th>Kind</th><th className="num">Amount</th><th>Status</th></tr></thead>
          <tbody>{data.others.map((o: any) => <tr key={o.id} className="click" onClick={() => navigate(`/caboose/refunds/${o.id}`)}><td className="nowrap"><Link to={`/caboose/refunds/${o.id}`}><b>#{o.id}</b></Link></td><td>{date(o.createdAt)}</td><td>{cap(o.kind)}</td><td className="num">{money(o.amount)}</td><td>{cap(o.status)}</td></tr>)}</tbody>
        </table></div></Panel>}
        <Panel title="Recent Payments"><div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Paid</th><th>Method</th><th className="num">Amount</th><th>Status</th></tr></thead>
          <tbody>{data.payments.map((p: any) => <tr key={p.id}><td className="nowrap"><RecordLink type="payment" id={p.id} /></td><td>{date(p.paidOn)}</td><td>{p.method} · {p.source}</td><td className="num">{money(p.amount)}</td><td><span className={`pill ${p.status === 'Success' ? 'ok' : 'bad'}`}>{p.status}</span></td></tr>)}
            {data.payments.length === 0 && <EmptyRow cols={5}>No payments yet.</EmptyRow>}</tbody>
        </table></div></Panel>
      </div>
      {data.notes.length > 0 && <Panel title="Account Notes" right={<span className="muted">about refunds</span>}><div className="table-wrap"><table><tbody>
        {data.notes.map((n: any) => <tr key={n.id}><td className="nowrap">{dateTime(n.createdAt)}</td><td>{n.author}</td><td className="wrap">{n.body}</td></tr>)}
      </tbody></table></div></Panel>}
    </>
  );
}
