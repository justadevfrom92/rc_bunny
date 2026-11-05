import { Link, useNavigate } from 'react-router';
import { api, date, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Bounty → Drawing Winners (ebunny's bounty/winners). */
export default function Winners() {
  const { data, error, reload } = useLoad<any[]>('/api/bounty/winners');
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const month = (m: string) => (/^\d{4}-\d{2}$/.test(m) ? new Date(m + '-01T00:00').toLocaleDateString('en-US', { month: 'long', year: 'numeric' }) : m);
  return (
    <>
      <PageHead title="Drawing Winners" sub="Every monthly drawing winner. Winners are picked on Monthly Drawing." actions={<Link className="btn ghost" to="/bounty/drawing">Monthly Drawing</Link>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>Account</th><th>Winner</th><th>Month</th><th>Prize</th><th>Date Won</th><th><span className="sr-only">Remove</span></th></tr></thead>
        <tbody>
          {data.map((w) => (
            <tr key={w.id} className="click" onClick={() => navigate(`/bounty/winners/${w.id}`)}>
              <td className="nowrap"><Link to={`/bounty/winners/${w.id}`}><b>#{w.id}</b></Link></td><td onClick={(e) => e.stopPropagation()}><AccountNo id={w.customerId} account={w.account} /></td>
              <td>{w.customer}</td><td className="nowrap">{month(w.month)}</td><td>{w.prize}</td><td className="nowrap">{w.drawnAt ? date(w.drawnAt) : '—'}</td>
              <td className="actions" onClick={(e) => e.stopPropagation()}><button className="btn sm ghost" onClick={() => confirm(`Remove this winner?\nThe ${w.month} winner (account ${w.account}) is removed from Drawing Winners.`) && run(api.del(`/api/bounty/winners/${w.id}`), 'Winner removed')}>Remove</button></td>
            </tr>
          ))}
          {data.length === 0 && <EmptyRow cols={7}>No winners yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}
