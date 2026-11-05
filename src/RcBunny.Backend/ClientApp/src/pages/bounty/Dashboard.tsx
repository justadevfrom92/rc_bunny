import { Link, useNavigate } from 'react-router';
import { useLoad } from '../../api';
import { PageHead, Panel, Stat } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Bounty → Rewards dashboard (ebunny's bounty/dashboard). */
export default function Dashboard() {
  const { data, error } = useLoad<any>('/api/bounty/dashboard');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Rewards" sub="The rewards program: stars earned and spent, offers and gift cards." />
      <div className="grid-4 stat-row">
        <Stat label="Members With Stars" value={data.members.toLocaleString()} />
        <Stat label="Stars Outstanding" value={data.outstanding.toLocaleString()} />
        <Stat label="Earned This Month" value={data.earned.toLocaleString()} />
        <Stat label="Redeemed This Month" value={data.redeemed.toLocaleString()} />
      </div>
      {data.giftsPending > 0 && <div className="alert info"><b>{data.giftsPending} gift card{data.giftsPending > 1 ? 's' : ''}</b> waiting to be sent. <Link to="/bounty/redemptions">Redemptions</Link></div>}
      <div className="grid-2">
        <Panel title="Most Redeemed Offers"><div className="table-wrap"><table><tbody>
          {data.popular.map((p: any) => <tr key={p.name}><td>{p.name}</td><td className="num">{p.n}</td></tr>)}
          {data.popular.length === 0 && <tr><td className="empty">No redemptions yet.</td></tr>}
        </tbody></table></div></Panel>
        <Panel title="Top Members"><div className="table-wrap"><table><tbody>
          {data.top.map((c: any) => (
            <tr key={c.id} className="click" onClick={() => navigate(`/bounty/members?account=${c.account}`)}>
              <td><Link to={`/bounty/members?account=${c.account}`}>{c.account}</Link></td><td>{c.name}</td><td className="num">{c.stars.toLocaleString()} ★</td></tr>
          ))}
        </tbody></table></div></Panel>
      </div>
    </>
  );
}
