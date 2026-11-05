import { type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, date, qs, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, Panel, RecordLink, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Bounty → Members (ebunny's bounty/members): search, the top members, and one member's stars to look at and adjust. */
export default function Members() {
  const [params, setParams] = useSearchParams();
  const q = params.get('q') ?? '', account = params.get('account') ?? '';
  const { data, error, reload } = useLoad<any>(`/api/bounty/members?${qs({ q, account })}`);
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  const open = (acct: string, keepQ = false) => navigate(`/bounty/members?${qs({ account: acct, q: keepQ ? q : '' })}`);

  const search = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs({ q: String(new FormData(e.currentTarget).get('q')) })); };
  const adjust = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    run(api.post(`/api/bounty/members/${data.member.id}/adjust`, { stars: Number(f.get('stars')), reason: f.get('reason') }).then((r) => { form.reset(); return r; }), 'Stars adjusted');
  };

  return (
    <>
      <PageHead title="Members" sub="Look up a customer's stars, see their history and add or remove stars." />
      {error && <div className="flash bad">{error}</div>}
      {bar}
      <form className="panel" onSubmit={search} key={q}><div className="panel-body form-row">
        <input name="q" defaultValue={q} placeholder="Account, name or email" aria-label="Search members" style={{ flex: 1 }} /><button className="btn">Search</button>
      </div></form>
      {data && q !== '' && (
        <div className="panel"><div className="table-wrap"><table><tbody>
          {data.results.map((c: any) => (
            <tr key={c.id} className="click" onClick={() => open(c.account, true)}>
              <td><Link to={`/bounty/members?${qs({ account: c.account, q })}`}>{c.account}</Link></td><td>{c.name}</td><td>{c.status}</td><td className="num">{c.stars.toLocaleString()} ★</td></tr>
          ))}
          {data.results.length === 0 && <tr><td className="empty">No customers match.</td></tr>}
        </tbody></table></div></div>
      )}
      {data && data.top.length > 0 && (
        <Panel title="Top Members" right={<span className="muted">most stars</span>}>
          <div className="table-wrap"><table>
            <thead><tr><th className="num">#</th><th>Member</th><th>Account</th><th>Status</th><th className="num">Stars</th></tr></thead>
            <tbody>{data.top.map((c: any, i: number) => (
              <tr key={c.id} className="click" onClick={() => open(c.account)}><td className="num">{i + 1}</td>
                <td><Link to={`/bounty/members?account=${c.account}`}><b>{c.name}</b></Link></td><td><span onClick={(e) => e.stopPropagation()}><AccountNo id={c.id} account={c.account} /></span></td>
                <td>{c.status}</td><td className="num">{c.stars.toLocaleString()} ★</td></tr>
            ))}</tbody>
          </table></div>
        </Panel>
      )}
      {data?.member && (
        <div className="grid-2" style={{ gridTemplateColumns: '1fr 340px', alignItems: 'start' }}>
          <Panel title={<h2>{data.member.name} · <AccountNo id={data.member.id} account={data.member.account} /></h2>} right={<span><b>{data.member.stars.toLocaleString()}</b> stars</span>}>
            <div className="table-wrap"><table>
              <thead><tr><th>ID</th><th>Date</th><th>Reason</th><th className="num">Stars</th></tr></thead>
              <tbody>
                {data.history.map((s: any) => (
                  <tr key={s.record + s.id} className={`click${s.drawing ? ' drawing-row' : ''}`} onClick={() => navigate(`/bounty/records/${s.record}/${s.id}`)}>
                    <td className="nowrap"><RecordLink type={s.record} id={s.id} /></td><td>{date(s.at)}</td>
                    <td>{s.reason}{s.drawing && <> <span className="pill info">Drawing</span></>}</td><td className="num">{s.drawing ? '—' : `${s.stars >= 0 ? '+' : ''}${s.stars}`}</td></tr>
                ))}
                {data.history.length === 0 && <EmptyRow cols={4}>No stars yet.</EmptyRow>}
              </tbody>
            </table></div>
          </Panel>
          <form className="panel" onSubmit={adjust}>
            <div className="panel-head"><h2>Adjust Stars</h2></div>
            <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div className="field"><label htmlFor="a-stars">Stars (+ to add, − to remove)</label><input id="a-stars" name="stars" type="number" required /></div>
              <div className="field"><label htmlFor="a-reason">Reason</label><input id="a-reason" name="reason" required placeholder="e.g. Goodwill for outage" /></div>
              <button className="btn cyan">Save Adjustment</button>
            </div>
          </form>
        </div>
      )}
    </>
  );
}
