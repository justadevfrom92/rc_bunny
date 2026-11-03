import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { api, post, qs } from '../../api';
import { date, ErrorBox, money, Panel, Skeleton } from '../../ui';

interface Queue { key: string; name: string; description: string; open: number; oldestDays: number | null }

/** Exceptions → Queues: every exception queue with what's open and how old the oldest item is. */
export function Queues() {
  const q = useQuery({ queryKey: ['corral', 'queues'], queryFn: () => api<Queue[]>('/api/corral/queues') });
  return (
    <>
      <h1>Exception Queues</h1>
      <Panel>
        {q.isPending ? <Skeleton rows={10} cols={4} /> : (
          <table className="table">
            <thead><tr><th>Queue</th><th>What's in it</th><th className="num">Open</th><th className="num">Oldest</th></tr></thead>
            <tbody>
              {q.data?.map((x) => (
                <tr key={x.key}>
                  <td><Link to={`/corral/r/work-items?f.queue=${x.key}`}>{x.name}</Link></td>
                  <td className="muted">{x.description}</td>
                  <td className="num">{x.open > 0 ? <strong>{x.open}</strong> : <span className="muted">0</span>}</td>
                  <td className="num">{x.oldestDays == null ? '—' : <span className={x.oldestDays > 7 ? 'bad-text' : ''}>{x.oldestDays} days</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Panel>
    </>
  );
}

interface Stage { stage: string; value: number; leads: { id: number; name: string; company: string | null; estValue: number; nextFollowUpOn: string | null; owner: string | null; kind: string }[] }

/** CRM → Pipeline: leads by stage, moved with one click. */
export function Pipeline() {
  const [mine, setMine] = useState(false);
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['corral', 'pipeline', mine], queryFn: () => api<Stage[]>(`/api/corral/crm/pipeline${qs({ mine })}`) });
  const move = useMutation({
    mutationFn: ({ id, stage }: { id: number; stage: string }) => post(`/api/r/crm-leads/${id}/actions/stage`, { stage }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['corral'] }),
  });
  const stages = q.data?.map((s) => s.stage) ?? [];
  return (
    <>
      <div className="page-head">
        <h1>Pipeline</h1>
        <div className="row">
          <label className="inline"><input type="checkbox" checked={mine} onChange={(e) => setMine(e.target.checked)} /> Only mine</label>
          <Link className="btn" to="/corral/r/crm-leads/new">+ New Lead</Link>
        </div>
      </div>
      <ErrorBox error={move.error} />
      {q.isPending ? <Skeleton rows={6} cols={6} /> : (
        <div className="kanban">
          {q.data?.map((s, si) => (
            <div key={s.stage} className="kanban-col">
              <h3><span style={{ textTransform: 'capitalize' }}>{s.stage}</span><span className="muted small">{s.leads.length} · {money(s.value)}</span></h3>
              {s.leads.map((l) => (
                <div key={l.id} className="kanban-card">
                  <Link to={`/corral/r/crm-leads/${l.id}`}><strong>{l.company ?? l.name}</strong></Link>
                  <span className="muted small">{l.company ? l.name : l.kind} · {money(l.estValue)}</span>
                  <span className="small">{l.owner ?? 'No owner'}{l.nextFollowUpOn && <> · follow up {date(l.nextFollowUpOn)}</>}</span>
                  <div className="row" style={{ marginTop: 4 }}>
                    {si > 0 && <button className="btn outline small" onClick={() => move.mutate({ id: l.id, stage: stages[si - 1] })}>←</button>}
                    {si < stages.length - 1 && <button className="btn outline small" onClick={() => move.mutate({ id: l.id, stage: stages[si + 1] })}>→</button>}
                  </div>
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </>
  );
}

interface OrderOptions { market: { id: number; name: string; description: string } | null; plans: { id: number; name: string; internal: string; term: number; etf: string }[]; channels: { msid: string; name: string }[] }

/** Create Order / Create Order - Biz: a phone sign-up, which lands like a website order (status Submitted). */
export function CreateOrder() {
  const [params] = useSearchParams();
  const biz = params.get('type') === 'biz';
  const navigate = useNavigate();
  const [f, setF] = useState({ firstName: '', lastName: '', company: '', email: '', phone: '', address: '', unit: '', city: '', zip: '', esiid: '', planId: '', startDate: '', enrollmentType: 'Switch', msid: '', promoCode: '', autopay: true, paperless: true, note: '' });
  const set = (k: keyof typeof f, v: string | boolean) => setF((x) => ({ ...x, [k]: v }));
  const opts = useQuery({
    queryKey: ['corral', 'order-options', biz, f.zip.length === 5 ? f.zip : ''],
    queryFn: () => api<OrderOptions>(`/api/corral/orders/options${qs({ type: biz ? 'Biz' : 'Resi', zip: f.zip.length === 5 ? f.zip : undefined })}`),
  });
  const save = useMutation({
    mutationFn: () => post<{ id: number; account: string }>('/api/corral/orders', { ...f, type: biz ? 'Small Business' : 'Residential', planId: Number(f.planId), startDate: f.startDate || null, esiid: f.esiid || null, msid: f.msid || null, promoCode: f.promoCode || null }),
    onSuccess: (r) => navigate(`/corral/customers/${r.id}`),
  });
  const submit = (e: FormEvent) => { e.preventDefault(); save.mutate(); };
  const txt = (k: keyof typeof f, label: string, props: Record<string, unknown> = {}) => (
    <label>{label}<input value={String(f[k])} onChange={(e) => set(k, e.target.value)} {...props} /></label>
  );
  return (
    <form onSubmit={submit}>
      <h1>{biz ? 'Create Order - Biz' : 'Create Order'}</h1>
      <ErrorBox error={save.error} />
      <div className="grid-2">
        <Panel title="Customer">
          <div className="form-grid">
            {biz && txt('company', 'Business name', { required: true })}
            {txt('firstName', biz ? 'Contact first name' : 'First name', { required: true })}
            {txt('lastName', biz ? 'Contact last name' : 'Last name', { required: true })}
            {txt('email', 'Email', { type: 'email', required: true })}
            {txt('phone', 'Phone', { required: true })}
          </div>
        </Panel>
        <Panel title="Service address">
          <div className="form-grid">
            {txt('address', 'Street', { required: true })}
            {txt('unit', 'Unit')}
            {txt('city', 'City', { required: true })}
            {txt('zip', 'Zip', { required: true, maxLength: 5, inputMode: 'numeric' })}
            {txt('esiid', 'ESIID (if known)')}
          </div>
          {f.zip.length === 5 && <p className="small">{opts.data?.market ? <>Utility: <strong>{opts.data.market.description}</strong></> : <span className="bad-text">We don't serve that zip.</span>}</p>}
        </Panel>
      </div>
      <Panel title="Plan and start">
        <div className="form-grid">
          <label>Plan
            <select value={f.planId} onChange={(e) => set('planId', e.target.value)} required>
              <option value="">Pick a plan</option>
              {opts.data?.plans.map((p) => <option key={p.id} value={p.id}>{p.name} ({p.internal}) · {p.term === 1 ? 'Month-to-month' : `${p.term} mo`}</option>)}
            </select>
          </label>
          <label>Enrollment
            <select value={f.enrollmentType} onChange={(e) => set('enrollmentType', e.target.value)}><option>Switch</option><option value="MoveIn">Move-in</option></select>
          </label>
          {txt('startDate', 'Requested start', { type: 'date' })}
          <label>MSID
            <select value={f.msid} onChange={(e) => set('msid', e.target.value)}>
              <option value="">2001 - Call Center</option>
              {opts.data?.channels.map((c) => <option key={c.msid} value={`${c.msid} - ${c.name}`}>{c.msid} - {c.name}</option>)}
            </select>
          </label>
          {txt('promoCode', 'Promo code')}
          <label className="check"><input type="checkbox" checked={f.autopay} onChange={(e) => set('autopay', e.target.checked)} /> AutoPay</label>
          <label className="check"><input type="checkbox" checked={f.paperless} onChange={(e) => set('paperless', e.target.checked)} /> Paperless</label>
          <label className="span-all">Note<textarea rows={3} value={f.note} onChange={(e) => set('note', e.target.value)} /></label>
        </div>
      </Panel>
      <div className="row"><button className="btn" disabled={save.isPending}>{save.isPending ? 'Placing…' : 'Place Order'}</button></div>
    </form>
  );
}
