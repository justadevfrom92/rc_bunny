import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { post, put } from '../../api';
import { handleForbidden, useCan } from '../../auth';
import { customerDetail, useMeta } from '../../queries';
import type { CustomerDetail as Detail } from '../../types';
import { date, dateTime, ErrorBox, money, num, Panel, Pill, Refreshing, Skeleton } from '../../ui';

/** The account page. Opens instantly when prefetched from search; each write updates the cache. */
export default function CustomerDetail() {
  const id = Number(useParams().id);
  const qc = useQueryClient();
  const { data: meta } = useMeta();
  const q = useQuery(customerDetail(id));
  const canRefund = useCan('refunds');

  // After a write: refresh this account and the lists/stats that show it.
  const refresh = () => Promise.all([
    qc.invalidateQueries({ queryKey: ['corral', 'customer', id] }),
    qc.invalidateQueries({ queryKey: ['corral', 'search'] }),
    qc.invalidateQueries({ queryKey: ['corral', 'stats'] }),
  ]);

  const bookmark = useMutation({
    mutationFn: () => post<{ bookmarked: boolean }>(`/api/corral/customers/${id}/bookmark`),
    // Optimistic: the star flips immediately, and flips back if the server says no.
    onMutate: async () => {
      const key = ['corral', 'customer', id];
      await qc.cancelQueries({ queryKey: key });
      const prev = qc.getQueryData<Detail>(key);
      if (prev) qc.setQueryData<Detail>(key, { ...prev, bookmarked: !prev.bookmarked });
      return { prev };
    },
    onError: (_e, _v, ctx) => ctx?.prev && qc.setQueryData(['corral', 'customer', id], ctx.prev),
    onSettled: () => qc.invalidateQueries({ queryKey: ['corral', 'bookmarks'] }),
  });

  const status = useMutation({
    mutationFn: (s: string) => put(`/api/corral/customers/${id}/status`, { status: s }),
    onSuccess: refresh,
    onError: handleForbidden,
  });

  const reverse = useMutation({
    mutationFn: (paymentId: number) => post(`/api/corral/payments/${paymentId}/reverse`),
    onSuccess: refresh,
    onError: handleForbidden,
  });

  if (q.isPending) return <div className="panel"><Skeleton rows={10} cols={4} /></div>;
  if (q.error || !q.data) return <ErrorBox error={q.error ?? new Error('Not found')} />;
  const { customer: c, bookmarked } = q.data;

  return (
    <>
      <p className="crumbs"><Link to="/corral/customers">Customer Search</Link> › {c.account}</p>
      <div className="page-head">
        <div>
          <h1>
            {c.name}{' '}
            <button className={`star ${bookmarked ? 'on' : ''}`} aria-pressed={bookmarked} title={bookmarked ? 'Remove bookmark' : 'Bookmark'} onClick={() => bookmark.mutate()}>
              {bookmarked ? '★' : '☆'}
            </button>
          </h1>
          <p className="muted">
            Account <span className="mono">{c.account}</span> · Ticket <span className="mono">{c.ticket}</span> · {c.type}
          </p>
        </div>
        <Refreshing on={q.isFetching} />
      </div>
      {c.exception && <div className="alert warn">Exception: <strong>{c.exception}</strong></div>}

      <div className="grid-2">
        <Panel title="Service" id="service">
          <dl className="facts">
            <dt>Status</dt>
            <dd>
              <select value={c.status} disabled={status.isPending} onChange={(e) => status.mutate(e.target.value)} aria-label="Status">
                {Object.keys(meta?.corral.statuses ?? {}).map((s) => <option key={s}>{s}</option>)}
              </select>{' '}
              <Pill tone={meta?.corral.statuses[c.status]}>{c.status}</Pill>
              <ErrorBox error={status.error} />
            </dd>
            <dt>Address</dt><dd>{c.address}, {c.city} {c.zip}</dd>
            <dt>ESIID</dt><dd className="mono">{c.esiid ?? '—'}</dd>
            <dt>Utility</dt><dd>{c.market?.description ?? '—'}</dd>
            <dt>Plan</dt><dd>{c.plan ? `${c.plan.name} (${c.plan.internal})` : '—'}</dd>
            <dt>Enrollment</dt><dd>{c.enrollmentType} · starts {date(c.startDate)}</dd>
            <dt>Source</dt><dd>{c.source}</dd>
          </dl>
        </Panel>
        <Panel title="Account">
          <dl className="facts">
            <dt>Balance</dt><dd><strong>{money(c.balance)}</strong> · due {date(c.dueDate)}</dd>
            <dt>Deposit due</dt><dd>{money(c.depositDue)}</dd>
            <dt>Phone</dt><dd>{c.phone ?? '—'}</dd>
            <dt>Email</dt><dd>{c.email ?? '—'}</dd>
            <dt>Options</dt>
            <dd>
              {c.autopay && <Pill tone="ok">AutoPay</Pill>} {c.paperless && <Pill tone="ok">Paperless</Pill>}{' '}
              {c.peakPerks && <Pill tone="info">Peak Perks</Pill>} {!c.marketingOptIn && <Pill tone="warn">No marketing</Pill>}
            </dd>
            <dt>Rewards</dt><dd>{num(c.stars)} stars</dd>
          </dl>
        </Panel>
      </div>

      <Panel title="Payments" id="payments" actions={!canRefund && <span className="muted small">Reversals need the Refunds right</span>}>
        <ErrorBox error={reverse.error} />
        {c.payments.length === 0 ? <p className="muted">No payments.</p> : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Date</th><th>Reference</th><th>Method</th><th>Source</th><th>Status</th><th className="num">Amount</th><th /></tr></thead>
              <tbody>
                {c.payments.map((p) => (
                  <tr key={p.id}>
                    <td>{date(p.paidOn)}</td>
                    <td className="mono">{p.reference}</td>
                    <td>{p.method}</td>
                    <td>{p.source}</td>
                    <td><Pill tone={p.status === 'Success' ? 'ok' : p.status === 'Reversed' ? 'warn' : 'bad'}>{p.status}</Pill></td>
                    <td className="num">{money(p.amount)}</td>
                    <td>
                      {canRefund && p.status === 'Success' && (
                        <button
                          className="btn outline small danger"
                          disabled={reverse.isPending}
                          onClick={() => confirm(`Reverse ${p.reference} for ${money(p.amount)}? The amount goes back on the balance.`) && reverse.mutate(p.id)}
                        >
                          Reverse
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <div className="grid-2">
        <Panel title="Bills">
          {c.bills.length === 0 ? <p className="muted">No bills yet.</p> : (
            <table className="table">
              <thead><tr><th>Billed</th><th>Invoice</th><th className="num">kWh</th><th className="num">Amount</th></tr></thead>
              <tbody>
                {c.bills.map((b) => (
                  <tr key={b.id}><td>{date(b.billedOn)}</td><td className="mono">{b.invoice}</td><td className="num">{num(b.kwh)}</td><td className="num">{money(b.amount)}</td></tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>
        <Notes id={id} detail={q.data} onSaved={refresh} />
      </div>
    </>
  );
}

function Notes({ id, detail, onSaved }: { id: number; detail: Detail; onSaved: () => Promise<unknown> }) {
  const { data: meta } = useMeta();
  const [body, setBody] = useState('');
  const [category, setCategory] = useState('');
  const [action, setAction] = useState('');
  const [priority, setPriority] = useState('');
  const add = useMutation({
    mutationFn: () => post(`/api/corral/customers/${id}/notes`, { body, category: category || null, action: action || null, priority: priority || null }),
    onSuccess: async () => {
      setBody('');
      setCategory('');
      setAction('');
      setPriority('');
      await onSaved();
    },
  });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    add.mutate();
  };
  return (
    <Panel title="Notes" id="notes">
      <form className="note-form" onSubmit={submit}>
        <textarea value={body} onChange={(e) => setBody(e.target.value)} placeholder="Add a note…" rows={3} maxLength={5000} required />
        <div className="row">
          <select value={category} onChange={(e) => { setCategory(e.target.value); setAction(''); }} aria-label="Category">
            <option value="">Category</option>
            {Object.keys(meta?.corral.noteCategories ?? {}).map((k) => <option key={k}>{k}</option>)}
          </select>
          <select value={action} onChange={(e) => setAction(e.target.value)} disabled={!category} aria-label="Action">
            <option value="">Action</option>
            {(meta?.corral.noteCategories[category] ?? []).map((k) => <option key={k}>{k}</option>)}
          </select>
          <select value={priority} onChange={(e) => setPriority(e.target.value)} aria-label="Priority">
            <option value="">Priority</option>
            {meta?.corral.priorities.map((k) => <option key={k}>{k}</option>)}
          </select>
          <button className="btn small" disabled={add.isPending || !body.trim()}>{add.isPending ? 'Saving…' : 'Add note'}</button>
        </div>
        <ErrorBox error={add.error} />
      </form>
      <ul className="notes">
        {detail.customer.notes.map((n) => (
          <li key={n.id}>
            <div className="muted small">
              {dateTime(n.createdAt)} · {n.author}
              {n.category && <> · {n.category}{n.action ? ` → ${n.action}` : ''}</>}
              {n.priority && <> · <Pill tone={n.priority === 'High' ? 'bad' : n.priority === 'Medium' ? 'warn' : ''}>{n.priority}</Pill></>}
            </div>
            <p>{n.body}</p>
          </li>
        ))}
      </ul>
    </Panel>
  );
}
