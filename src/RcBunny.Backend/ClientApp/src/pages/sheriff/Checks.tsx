import { api, useLoad } from '../../api';
import { PageHead, age, useRun } from '../../ui';
import { hbLabel, hbTone } from './Heartbeat';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Sheriff → Heartbeat Checks (ebunny's sheriff/heartbeat-checks): every check right now, by group. */
export default function Checks() {
  const { data, error, reload } = useLoad<any>('/api/sheriff/heartbeat');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const groups = [['Checks', data.checks.filter((c: any) => !c.control)], ['Controls', data.checks.filter((c: any) => c.control)]] as [string, any[]][];
  return (
    <>
      <PageHead title="Heartbeat Checks" sub="Everything the heartbeat checks every 5 minutes, and the switches (like taking the website down for maintenance)."
        actions={<button className="btn cyan" onClick={() => run(api.post('/api/sheriff/heartbeat/run'), 'Checked')}>Check Now</button>} />
      {bar}
      {groups.filter(([, items]) => items.length).map(([group, items]) => (
        <div key={group} className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>{group}</h2><span className="muted">{items.length} checks</span></div>
          <div className="table-wrap"><table>
            <thead><tr><th>Status</th><th>Check</th><th>Now</th><th>Last Checked</th></tr></thead>
            <tbody>{items.map((c) => (
              <tr key={c.key} className={c.status === 'warn' || c.status === 'bad' ? 'row-warn' : ''}>
                <td><span className={`pill ${hbTone[c.status]}`}>{hbLabel[c.status]}</span></td>
                <td><b>{c.name}</b></td><td className="wrap">{c.detail}</td><td className="wrap"><div className="help">{age(c.checkedAt)} ago</div></td></tr>
            ))}</tbody>
          </table></div>
        </div>
      ))}
    </>
  );
}
