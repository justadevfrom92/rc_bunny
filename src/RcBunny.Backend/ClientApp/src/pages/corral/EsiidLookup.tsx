import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { date, qs, useLoad } from '../../api';
import { RecordLink, Status } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** ESIID Lookup: accounts at an address or meter ID, and what's been sent to ERCOT for those meters. */
export default function EsiidLookup() {
  const [search, setSearch] = useState({ q: '', zip: '' });
  const { data, error } = useLoad<any>(search.q ? `/api/corral/esiid?${qs(search)}` : null);
  const submit = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); const f = new FormData(e.currentTarget); setSearch({ q: String(f.get('q')).trim(), zip: String(f.get('zip')).trim() }); };

  return (
    <>
      <h1>ESIID Lookup</h1>
      <p className="muted">Search our accounts by service address or ESIID. Looking up meters we don't serve needs an ERCOT data connection, which isn't set up.</p>
      <form className="row" onSubmit={submit}>
        <input name="q" placeholder="Street address or ESIID" required />
        <input name="zip" placeholder="Zip (optional)" maxLength={5} style={{ maxWidth: 140 }} />
        <button className="primary">Look up</button>
      </form>
      {error && <p className="error">{error}</p>}
      {data && (
        <>
          <section className="card"><h2>Accounts ({data.results.length})</h2>
            {data.results.length === 0 ? <p className="muted">No account at that address or ESIID.</p> : (
              <table>
                <thead><tr><th>Account</th><th>Name</th><th>Address</th><th>ESIID</th><th>Utility</th><th>Meter</th><th>Status</th></tr></thead>
                <tbody>{data.results.map((c: any) => <tr key={c.id}><td><Link to={`/corral/customers/${c.id}`}>{c.account}</Link></td><td>{c.name}</td>
                  <td>{c.address}{c.unit && ` #${c.unit}`}, {c.city} {c.zip}</td><td className="mono">{c.esiid ?? '—'}</td><td>{c.market}</td><td>{c.meterType} {c.loadZone}</td><td>{c.status}</td></tr>)}</tbody>
              </table>
            )}
          </section>
          <section className="card"><h2>ERCOT history</h2>
            {data.transactions.length === 0 ? <p className="muted">Nothing sent for these meters.</p> : (
              <table>
                <thead><tr><th>ID</th><th>Date</th><th>ESIID</th><th>Purpose</th><th>Type</th><th>Status</th></tr></thead>
                <tbody>{data.transactions.map((t: any) => <tr key={t.id}><td><RecordLink type="ercot_transaction" id={t.id} /></td><td>{date(t.transDate)}</td><td className="mono">{t.esiid}</td><td>{t.purpose}</td>
                  <td><span className="mono">{t.transType}</span> {t.label}</td><td><Status value={t.status} /></td></tr>)}</tbody>
              </table>
            )}
          </section>
        </>
      )}
    </>
  );
}
