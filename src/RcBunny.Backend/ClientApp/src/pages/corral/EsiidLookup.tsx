import { type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { qs, useLoad } from '../../api';
import { EmptyRow, Field, PageHead, Panel } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Corral → ESIID Lookup (ebunny's corral/esiid): premises on file by street address or ESIID. */
export default function EsiidLookup() {
  const [params, setParams] = useSearchParams();
  const q = params.get('q') ?? '', zip = params.get('zip') ?? '';
  const { data, error } = useLoad<any>(q ? `/api/corral/esiid?${qs({ q, zip })}` : null);
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    setParams(qs({ q: String(f.get('q')).trim(), zip: String(f.get('zip')).trim() }));
  };

  return (
    <>
      <PageHead title="ESIID Lookup" sub="Search premises on file by street address or ESIID. A live ERCOT premise lookup needs an ERCOT data integration." />
      {error && <div className="flash bad">{error}</div>}
      <Panel body>
        <form className="form-row" onSubmit={submit} key={q + zip}>
          <Field label="Street Address or ESIID" htmlFor="q" grow><input id="q" name="q" defaultValue={q} required placeholder="e.g. 1047 Example Ln or 1008901…" /></Field>
          <Field label="Zip" htmlFor="zip"><input id="zip" name="zip" defaultValue={zip} maxLength={5} inputMode="numeric" style={{ width: 100 }} /></Field>
          <button className="btn">Look Up</button>
        </form>
      </Panel>
      <Panel title="Results">
        {!q ? <div className="empty">Enter an address or ESIID to search.</div> : !data ? <div className="empty">Loading…</div> : (
          <div className="table-wrap"><table>
            <thead><tr><th>ESIID</th><th>Address</th><th>TDSP</th><th>Premise</th><th>Current Customer</th><th /></tr></thead>
            <tbody>
              {data.results.map((c: any) => (
                <tr key={c.id}><td className="mono">{c.esiid ?? '—'}</td><td>{c.address}, {c.city} {c.zip}</td><td>{c.market}</td>
                  <td>{c.type === 'Residential' ? 'Residential' : 'Small Non-Residential'}</td>
                  <td><Link to={`/corral/customers/${c.id}`}>{c.name}</Link></td>
                  <td>{c.esiid && <Link className="btn sm" to={`/corral/orders/new?esiid=${c.esiid}`}>Start Order</Link>}</td></tr>
              ))}
              {data.results.length === 0 && <EmptyRow cols={6}>No premises found. Check the spelling or try just the house number and street name.</EmptyRow>}
            </tbody>
          </table></div>
        )}
      </Panel>
    </>
  );
}
