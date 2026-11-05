import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, Panel, Stat, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const monthName = (m: string) => new Date(m + '-01T00:00').toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
const lastDay = (m: string) => { const d = new Date(m + '-01T00:00'); return new Date(d.getFullYear(), d.getMonth() + 1, 0).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' }); };

/** Bounty → Monthly Drawing (ebunny's bounty/drawing): a month's entries and winner, and past winners. */
export default function Drawing() {
  const [params, setParams] = useSearchParams();
  const { data, error } = useLoad<any>(`/api/bounty/drawing${params.get('month') ? `?month=${params.get('month')}` : ''}`);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const current = data.month === data.thisMonth, w = data.winner;
  const people = new Set(data.entries.map((e: any) => e.customerId)).size;
  return (
    <>
      <PageHead title="Monthly Drawing" sub='Customers enter by redeeming the free "Monthly Drawing" offer. The winner is saved to Bounty → Drawing Winners.'
        actions={w && current ? undefined : <Link className="btn cyan" to="/bounty/drawing/draw">Draw a Winner</Link>} />
      <select className="pill-select" aria-label="Month" style={{ marginBottom: 18 }} value={data.month} onChange={(e) => setParams(e.target.value === data.thisMonth ? {} : { month: e.target.value })}>
        {data.months.map((m: string) => <option key={m} value={m}>{monthName(m)}</option>)}
      </select>
      <div className="grid-3 stat-row">
        <Stat label={`Entries in ${monthName(data.month).split(' ')[0]}`} value={data.entries.length.toLocaleString()} note={`from ${people} ${people === 1 ? 'customer' : 'customers'}`} />
        <div className="stat"><span>Winner</span><strong style={{ fontSize: '1.1rem' }}>{w ? <>Account <AccountNo id={w.customerId} account={w.account} /></> : current ? 'Not drawn yet' : 'None drawn'}</strong>{w && <small>{w.prize} · drawn {date(w.drawnAt)}</small>}</div>
        <div className="stat"><span>Entries Close</span><strong style={{ fontSize: '1.1rem' }}>{lastDay(data.month)}</strong></div>
      </div>
      <div className="stack">
        <Panel title={`Entries for ${monthName(data.month)}`} right={<span className="muted">{data.entries.length}</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Entered</th><th>Account</th><th>Name</th></tr></thead>
            <tbody>
              {data.entries.map((e: any) => <tr key={e.id}><td className="nowrap"><b>#{e.id}</b></td><td className="nowrap">{dateTime(e.createdAt)}</td><td><AccountNo id={e.customerId} account={e.account} /></td><td>{e.customer}</td></tr>)}
              {data.entries.length === 0 && <EmptyRow cols={4}>No entries in {monthName(data.month)}.</EmptyRow>}
            </tbody>
          </table></div>
        </Panel>
        <Panel title="Past Winners">
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>Month</th><th>Prize</th><th>Winner Account</th><th>Drawn On</th></tr></thead>
            <tbody>
              {data.past.map((r: any) => <tr key={r.id} className="click" onClick={() => navigate(`/bounty/winners/${r.id}`)}><td className="nowrap"><Link to={`/bounty/winners/${r.id}`}><b>#{r.id}</b></Link></td>
                <td>{r.month}</td><td>{r.prize}</td><td><AccountNo id={r.customerId} account={r.account} /></td><td>{r.drawnAt ? date(r.drawnAt) : '—'}</td></tr>)}
              {data.past.length === 0 && <EmptyRow cols={5}>None.</EmptyRow>}
            </tbody>
          </table></div>
        </Panel>
      </div>
    </>
  );
}

/** Draw a Winner (ebunny's bounty/draw): this month's drawing. */
export function Draw() {
  const { data, error } = useLoad<any>('/api/bounty/drawing');
  const [run, bar] = useRun();
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const count = data.entries.length, people = new Set(data.entries.map((e: any) => e.customerId)).size, w = data.winner;
  return (
    <>
      <PageHead title="Draw a Winner" sub={`The ${monthName(data.month)} drawing. One entry is picked at random; the winner gets a note on their account and is saved to Bounty → Drawing Winners.`}
        actions={<Link className="btn ghost" to="/bounty/drawing">Back to Entries</Link>} />
      {bar}
      {w ? (
        <div className="panel"><div className="panel-body"><p style={{ margin: 0 }}>{monthName(data.month)} already has a winner: account <b className="mono">{w.account}</b> ({w.prize}, drawn {date(w.drawnAt)}).</p></div></div>
      ) : (
        <form className="panel" onSubmit={(e) => { e.preventDefault();
          if (!confirm(`Draw the ${monthName(data.month).split(' ')[0]} winner?\nOne of ${count} entries is picked at random. This can't be redone.`)) return;
          run(api.post('/api/bounty/drawing/draw', { prize: new FormData(e.currentTarget).get('prize') }).then((r) => { navigate('/bounty/drawing'); return r; })); }}>
          <div className="panel-head"><h2>{monthName(data.month)} Drawing</h2></div>
          <div className="panel-body">
            <div className="grid-3 stat-row">
              <Stat label="Entries" value={count.toLocaleString()} />
              <Stat label="Customers Entered" value={people.toLocaleString()} />
              <Stat label="Each Entry's Chance" value={count ? `1 in ${count.toLocaleString()}` : '—'} />
            </div>
            <div className="field"><label htmlFor="prize">Prize</label><input id="prize" name="prize" required maxLength={100} defaultValue="$100 Bill Credit" /></div>
          </div>
          <div className="panel-body actions"><button className="btn cyan" disabled={!count}>Draw Winner</button>{!count && <span className="muted">No entries this month yet.</span>}</div>
        </form>
      )}
    </>
  );
}

/** One drawing winner (ebunny's bounty/winner). */
export function Winner() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/bounty/winners/${id}`);
  const [run, bar] = useRun();
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.customer, won = data.drawnAt ? new Date(data.drawnAt).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' }) : '—';
  const remove = () => confirm('Remove this winner?\nThey are removed from Drawing Winners.') && run(api.del(`/api/bounty/winners/${data.id}`).then((r) => { navigate('/bounty/winners'); return r; }));
  return (
    <>
      <PageHead title={`${monthName(data.month)} Winner`} sub={`Winner #${data.id} · ${c.name}`}
        actions={<><Link className="btn ghost btn-back" to="/bounty/winners">Back to Drawing Winners</Link><button className="btn ghost" onClick={remove}>Remove Winner</button></>} />
      {bar}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <div className="grid-4 stat-row">
          <div className="stat"><span>Prize</span><strong style={{ fontSize: '1.15rem' }}>{data.prize || '—'}</strong></div>
          <div className="stat"><span>Date Won</span><strong style={{ fontSize: '1.15rem' }}>{won}</strong></div>
          <Stat label="Entries That Month" value={data.entries.toLocaleString()} />
          <Stat label="Their Entries" value={data.theirEntries} />
        </div>
        <Panel title="Winner"><div className="panel-body"><dl className="kv">
          <dt>Account</dt><dd><AccountNo id={c.id} account={c.account} /></dd>
          <dt>Name</dt><dd>{c.name}</dd><dt>Email</dt><dd>{c.email ?? '—'}</dd><dt>Phone</dt><dd>{c.phone ?? '—'}</dd>
          <dt>Address</dt><dd>{c.address}{c.unit && ` #${c.unit}`}, {c.city}, TX {c.zip}</dd><dt>Stars</dt><dd>{c.stars.toLocaleString()}</dd>
          <dt>Month</dt><dd><Link to={`/bounty/drawing?month=${data.month}`}>{monthName(data.month)}</Link></dd>
          <dt>Prize</dt><dd>{data.prize || '—'}</dd><dt>Date Won</dt><dd>{won}</dd>
        </dl></div></Panel>
        {data.note && <Panel title="Note on the Account"><div className="panel-body">
          <p style={{ margin: 0 }}>{data.note.body}</p><p className="help" style={{ margin: '6px 0 0' }}>{data.note.author ?? 'System'} · {dateTime(data.note.createdAt)}</p>
        </div></Panel>}
      </div>
    </>
  );
}
