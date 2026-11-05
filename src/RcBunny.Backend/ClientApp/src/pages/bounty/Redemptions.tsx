import { Link, useSearchParams } from 'react-router';
import { api, date, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, RecordLink, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Bounty → Redemptions (ebunny's bounty/redemptions): gift cards to send, or every redemption. */
export default function Redemptions() {
  const [params] = useSearchParams();
  const pendingOnly = params.get('show') !== 'all';
  const { data, error, reload } = useLoad<any[]>(`/api/bounty/redemptions?pending=${pendingOnly}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  return (
    <>
      <PageHead title="Redemptions" sub="Offers customers redeemed. Gift cards are sent by the gift card vendor; mark each one sent here." />
      {bar}
      <div className="panel">
        <div className="panel-head"><nav className="tabs"><Link to="/bounty/redemptions" className={pendingOnly ? 'on' : ''}>Gift Cards to Send</Link><Link to="/bounty/redemptions?show=all" className={pendingOnly ? '' : 'on'}>All Redemptions</Link></nav></div>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Date</th><th>Account</th><th>Offer</th><th className="num">Stars</th><th>Gift Card</th><th /></tr></thead>
          <tbody>
            {data?.map((s) => (
              <tr key={s.id}><td className="nowrap"><RecordLink type="star_entry" id={s.id} /></td><td>{date(s.createdAt)}</td>
                <td><AccountNo id={s.customerId} account={s.account} /><br /><span className="help">{s.customer} · {s.email}</span></td>
                <td>{s.offer}</td><td className="num">{Math.abs(s.stars).toLocaleString()}</td>
                <td>{s.fulfillment ? <span className={`pill ${s.fulfillment === 'sent' ? 'ok' : 'warn'}`}>{s.fulfillment}</span> : '—'}</td>
                <td>{s.fulfillment === 'pending' && <button className="btn sm" onClick={() => run(api.post(`/api/bounty/redemptions/${s.id}/sent`), 'Marked sent')}>Mark Sent</button>}</td></tr>
            ))}
            {data?.length === 0 && <EmptyRow cols={7}>{pendingOnly ? 'No gift cards waiting.' : 'No redemptions yet.'}</EmptyRow>}
          </tbody>
        </table></div>
      </div>
    </>
  );
}
