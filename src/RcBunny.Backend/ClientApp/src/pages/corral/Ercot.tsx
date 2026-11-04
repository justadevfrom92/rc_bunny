import { type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { date, qs, useLoad } from '../../api';
import { EmptyRow, Field, PageHead, PagerBar, Panel } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const tone: Record<string, string> = { linked: 'ok', unlinked: 'warn' };

/** Corral → ERCOT (ebunny's corral/ercot): market transactions across every account. */
export default function Ercot() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['esiid', 'account', 'tracking', 'type', 'purpose', 'status', 'start', 'end'].map((k) => [k, params.get(k) ?? '']));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/ercot?${qs({ ...f, page })}`);
  const filter = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs(Object.fromEntries(new FormData(e.currentTarget).entries()) as any)); };
  const count = (s: string) => data?.counts.find((c: any) => c.status === s)?.count ?? 0;

  return (
    <>
      <PageHead title="ERCOT" sub={`Search market transactions across every account: ${['linked', 'unlinked', 'cancelled'].map((s) => `${count(s).toLocaleString()} ${s}`).join(' · ')}`} />
      {error && <div className="flash bad">{error}</div>}
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={filter} key={params.toString()}><div className="panel-head"><h2>Search Transactions</h2></div><div className="panel-body">
            <div className="form-row">
              <Field label="ESIID" htmlFor="esiid" grow><input id="esiid" name="esiid" defaultValue={f.esiid} inputMode="numeric" /></Field>
              <Field label="Account" htmlFor="account"><input id="account" name="account" defaultValue={f.account} inputMode="numeric" style={{ width: '14ch' }} /></Field>
              <Field label="Tracking #" htmlFor="tracking"><input id="tracking" name="tracking" defaultValue={f.tracking} style={{ width: '22ch' }} /></Field>
              <Field label="Trans Type" htmlFor="type"><select id="type" name="type" defaultValue={f.type}><option value="">-- Any --</option>{data?.types.map((t: string) => <option key={t}>{t}</option>)}</select></Field>
              <Field label="Purpose" htmlFor="purpose"><select id="purpose" name="purpose" defaultValue={f.purpose}><option value="">-- Any --</option><option>request</option><option>response</option></select></Field>
              <Field label="Status" htmlFor="status"><select id="status" name="status" defaultValue={f.status}><option value="">-- Any --</option><option>linked</option><option>unlinked</option><option>cancelled</option></select></Field>
              <Field label="From" htmlFor="start"><input id="start" name="start" type="date" defaultValue={f.start} /></Field>
              <Field label="To" htmlFor="end"><input id="end" name="end" type="date" defaultValue={f.end} /></Field>
              <button className="btn">Search</button>
              {Object.values(f).some(Boolean) && <Link className="btn ghost" to="/corral/ercot">Clear</Link>}
            </div>
          </div></form>
        </aside>
        <div className="search-main">
          <Panel title="Transactions" right={<span className="muted">{data?.total.toLocaleString()} found</span>}>
            <div className="table-wrap"><table>
              <thead><tr><th>Trans Date</th><th>Account</th><th>ESIID</th><th>Purpose</th><th>Scheduled</th><th>Trans Type</th><th>Tracking</th><th>Status</th></tr></thead>
              <tbody>
                {data?.rows.map((t: any) => (
                  <tr key={t.id}><td>{date(t.transDate)}</td>
                    <td>{t.customerId && <><Link to={`/corral/customers/${t.customerId}#ercot`}>{t.account}</Link><br /><span className="help">{t.customer}</span></>}</td>
                    <td className="mono">{t.esiid ?? '—'}</td><td>{t.purpose}</td><td>{t.scheduledOn ? date(t.scheduledOn) : '—'}</td>
                    <td><span className="mono">{t.transType}</span> {t.label}</td><td className="mono">{t.tracking ?? '—'}</td>
                    <td><span className={`pill ${tone[t.status] ?? 'bad'}`}>{t.status}</span></td></tr>
                ))}
                {data?.rows.length === 0 && <EmptyRow cols={8}>No transactions match.</EmptyRow>}
              </tbody>
            </table></div>
            {data && <PagerBar page={page} total={data.total} size={50} onPage={(p) => setParams(qs({ ...f, page: p }))} />}
          </Panel>
        </div>
      </div>
    </>
  );
}
