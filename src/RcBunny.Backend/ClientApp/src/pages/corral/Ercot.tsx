import { type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { date, qs, useLoad } from '../../api';
import { Pager, RecordLink, Status } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Corral → ERCOT: every request to and answer from ERCOT, with filters. */
export default function Ercot() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['esiid', 'account', 'tracking', 'type', 'purpose', 'status', 'start', 'end'].map((k) => [k, params.get(k) ?? '']));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/ercot?${qs({ ...f, page })}`);
  const filter = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs(Object.fromEntries(new FormData(e.currentTarget).entries()) as any)); };
  const count = (s: string) => data?.counts.find((c: any) => c.status === s)?.count ?? 0;

  return (
    <>
      <h1>ERCOT</h1>
      <p className="muted">EDI transactions with ERCOT (814 enrollments, 650 service orders, 867 usage). There's no ERCOT connection yet, so requests are recorded here but not sent.</p>
      {data && <div className="stats">{['linked', 'unlinked', 'cancelled'].map((s) => <button key={s} className="card" onClick={() => setParams(qs({ status: s }))}><span className="muted">{s}</span><strong>{count(s)}</strong></button>)}</div>}
      <form className="row" onSubmit={filter} key={params.toString()}>
        <input name="esiid" placeholder="ESIID" defaultValue={f.esiid} /><input name="account" placeholder="Account" defaultValue={f.account} style={{ maxWidth: 140 }} />
        <input name="tracking" placeholder="Tracking #" defaultValue={f.tracking} style={{ maxWidth: 150 }} />
        <select name="type" defaultValue={f.type}><option value="">Any type</option>{data?.types.map((t: string) => <option key={t}>{t}</option>)}</select>
        <select name="purpose" defaultValue={f.purpose}><option value="">Requests and responses</option><option value="request">Requests</option><option value="response">Responses</option></select>
        <select name="status" defaultValue={f.status}><option value="">Any status</option><option>linked</option><option>unlinked</option><option>cancelled</option></select>
        <input name="start" type="date" defaultValue={f.start} /><input name="end" type="date" defaultValue={f.end} />
        <button className="primary">Show</button>
      </form>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <p className="muted">{data.total} transactions</p>
          <table>
            <thead><tr><th>ID</th><th>Trans Date</th><th>ESIID</th><th>Account</th><th>Purpose</th><th>Scheduled</th><th>Type</th><th>Tracking</th><th>Status</th></tr></thead>
            <tbody>{data.rows.map((t: any) => (
              <tr key={t.id}><td><RecordLink type="ercot_transaction" id={t.id} /></td><td>{date(t.transDate)}</td><td className="mono">{t.esiid}</td>
                <td>{t.customerId ? <Link to={`/corral/customers/${t.customerId}#ercot`}>{t.customer}</Link> : '—'}</td><td>{t.purpose}</td><td>{t.scheduledOn ? date(t.scheduledOn) : '—'}</td>
                <td><span className="mono">{t.transType}</span> {t.label}</td><td className="mono">{t.tracking ?? '—'}</td><td><Status value={t.status} /></td></tr>
            ))}</tbody>
          </table>
          <Pager page={page} total={data.total} size={50} onPage={(p) => setParams(qs({ ...f, page: p }))} />
        </>
      )}
    </>
  );
}
