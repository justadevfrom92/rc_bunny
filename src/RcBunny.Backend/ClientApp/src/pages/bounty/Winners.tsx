import { api, date, useLoad } from '../../api';
import AccountLink from '../AccountLink';

interface Winner { id: number; month: string; customerId: number; account: string; customer: string; prize: string; drawnBy: string; drawnAt: string; entries: number }

export default function Winners() {
  const { data, error, reload } = useLoad<Winner[]>('/api/bounty/winners');
  const remove = (w: Winner) => {
    if (confirm(`Remove the ${w.month} winner? That month can then be drawn again.`)) api.del(`/api/bounty/winners/${w.id}`).then(reload);
  };

  return (
    <>
      <h1>Drawing Winners</h1>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : data.length === 0 ? <p className="muted">No winners yet.</p> : (
        <table>
          <thead><tr><th>Month</th><th>Winner</th><th>Prize</th><th className="num">Entries</th><th>Drawn</th><th /></tr></thead>
          <tbody>
            {data.map((w) => (
              <tr key={w.id}>
                <td>{w.month}</td><td><AccountLink id={w.customerId} account={w.account} name={w.customer} /></td><td>{w.prize}</td>
                <td className="num">{w.entries}</td><td>{date(w.drawnAt)} by {w.drawnBy}</td><td><button onClick={() => remove(w)}>Remove</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
