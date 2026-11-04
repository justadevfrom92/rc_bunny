import { Link, useNavigate, useParams } from 'react-router';
import { api, date, dateTime, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, Panel, age, statusTone, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** ebunny's corral/_queue-history: exceptions people fixed, newest first. */
function History({ rows, names }: { rows: any[]; names?: Record<string, string> }) {
  return (
    <Panel id="history" title="History" right={<span className="muted">{rows.length.toLocaleString()} fixed</span>}>
      <div className="table-wrap"><table>
        <thead><tr><th>Fixed</th>{names && <th>Queue</th>}<th>Account</th><th>Item</th><th>Opened</th><th>Took</th><th>Fixed By</th></tr></thead>
        <tbody>
          {rows.map((w) => (
            <tr key={w.id}><td className="nowrap">{dateTime(w.resolvedAt)}</td>
              {names && <td>{names[w.queue] ?? w.queue}</td>}
              <td><AccountNo id={w.customerId} account={w.account} /><br /><span className="help">{w.customer}</span></td>
              <td className="wrap">{w.summary}</td><td className="nowrap">{date(w.createdAt)}</td><td className="nowrap">{age(w.createdAt, w.resolvedAt)}</td><td>{w.resolvedBy ?? 'System'}</td></tr>
          ))}
          {rows.length === 0 && <EmptyRow cols={names ? 7 : 6}>Nothing fixed yet.</EmptyRow>}
        </tbody>
      </table></div>
    </Panel>
  );
}

/** Corral → Exceptions overview (ebunny's corral/queues). */
export function Queues() {
  const { data, error } = useLoad<any>('/api/corral/queues');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const names = Object.fromEntries(data.queues.map((q: any) => [q.key, q.name]));
  return (
    <>
      <PageHead title="Exception Queues" sub="Work items that need a person." />
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Queue</th><th>What it holds</th><th className="num">Open</th></tr></thead>
        <tbody>{data.queues.map((q: any) => (
          <tr key={q.key} className="click" onClick={() => navigate(`/corral/queues/${q.key}`)}>
            <td><Link to={`/corral/queues/${q.key}`}><b>{q.name}</b></Link></td><td className="wrap muted">{q.about}</td>
            <td className="num"><span className={`pill ${q.count === 0 ? 'ok' : q.count > 5 ? 'bad' : 'warn'}`}>{q.count}</span></td></tr>
        ))}</tbody>
      </table></div></div>
      <History rows={data.history} names={names} />
    </>
  );
}

/** One exception queue (ebunny's corral/queue): accounts waiting, each with Mark Fixed. */
export function Queue() {
  const { key } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/queues/${key}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title={data.name} sub={data.about} actions={<Link className="btn ghost" to="/corral/queues">All Queues</Link>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Account</th><th>Customer</th><th>Plan</th><th>Status</th><th>Item</th><th>Age</th><th /></tr></thead>
        <tbody>
          {data.items.map((w: any) => (
            <tr key={w.id}>
              <td><AccountNo id={w.customerId} account={w.account} /></td><td>{w.customer}</td><td>{w.plan}</td>
              <td>{w.status && <span className={`pill ${statusTone(w.status)}`}>{w.status}</span>}</td>
              <td className="wrap">{w.summary}</td><td>{age(w.createdAt)}</td>
              <td><button className="btn sm cyan" onClick={() => run(api.post(`/api/corral/work-items/${w.id}/resolve`), 'Marked as fixed')}>Mark Fixed</button></td>
            </tr>
          ))}
          {data.items.length === 0 && <EmptyRow cols={7}>Queue is clear.</EmptyRow>}
        </tbody>
      </table></div></div>
      <History rows={data.history} />
    </>
  );
}
