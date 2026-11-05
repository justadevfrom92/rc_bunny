import { Link, useNavigate } from 'react-router';
import { api, useLoad } from '../../api';
import { PageHead, Panel, age, shortTime, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

export const hbLabel: Record<string, string> = { ok: 'Healthy', warn: 'Warnings', bad: 'Problems', down: 'Taken Down', none: 'No check ran' };
export const hbTone: Record<string, string> = { ok: 'ok', warn: 'warn', bad: 'bad', down: 'info' };

/** The last 24 hours as 96 blocks of 15 minutes (ebunny's sheriff/_hb-strip). */
export function HbStrip({ blocks }: { blocks: any[] }) {
  const time = (d: Date) => d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
  return (
    <>
      <div className="hb-strip" role="group" aria-label="Last 24 hours">
        {blocks.map((b, i) => { const from = new Date(b.from), to = new Date(from.getTime() + 15 * 60000); return (
          <i key={i} className={`hb-${b.status}`} data-tip={`${time(from)}–${time(to)}: ${b.n ? hbLabel[b.status] : 'no check ran'}`} title={`${time(from)}–${time(to)}: ${b.n ? hbLabel[b.status] : 'no check ran'}`} />); })}
      </div>
      <div className="hb-axis"><span>24 hours ago</span><span>12 hours ago</span><span>now</span></div>
      <div className="hour-legend"><span><i className="hb-dot hb-ok" /> Healthy</span><span><i className="hb-dot hb-warn" /> Warning</span><span><i className="hb-dot hb-bad" /> Problem</span><span><i className="hb-dot hb-down" /> Taken down</span><span><i className="hb-dot hb-none" /> No check ran</span></div>
    </>
  );
}

/** Sheriff → Heartbeat (ebunny's sheriff/heartbeat): is everything running, the last 24 hours, checks, crons and latest actions. */
export default function Heartbeat() {
  const { data, error, reload } = useLoad<any>('/api/sheriff/heartbeat');
  const [run, bar] = useRun(reload);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const bad = data.checks.filter((c: any) => c.status === 'bad').length, down = data.checks.filter((c: any) => c.status === 'down').length;
  const order: Record<string, number> = { bad: 0, warn: 1, down: 2, ok: 3 };
  const flip = (c: any) => {
    const reason = c.status === 'down' ? '' : prompt(`Why take ${c.name} down? Visitors will see this.`, 'We are doing some maintenance and will be back shortly.');
    if (reason !== null) run(api.post(`/api/sheriff/heartbeat/controls/${c.control}`, { down: c.status !== 'down', reason }), c.status === 'down' ? `${c.name} is back up` : `${c.name} is taken down`);
  };

  return (
    <>
      <PageHead title="Heartbeat"
        sub={`Checked every 5 minutes by the scheduler. ${data.lastCheck ? `Last check ${age(data.lastCheck)} ago.` : 'No check has run yet.'}`}
        actions={<button className="btn cyan" onClick={() => run(api.post('/api/sheriff/heartbeat/run'), 'Checked')}>Check Now</button>} />
      {bar}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <div className={`hb-banner hb-${data.status}`}>
          <span className="hb-heart" aria-hidden="true">❤</span>
          <div><b>{hbLabel[data.status]}</b><span>{data.status === 'ok' ? 'Everything is running.' : `${bad ? `${bad} ${bad === 1 ? 'problem' : 'problems'}` : ''}${bad && down ? ', ' : ''}${down ? `${down} taken down` : ''} below.`}</span></div>
          {data.uptime != null && <div className="hb-uptime"><strong>{data.uptime}%</strong><small>healthy, last 24 hours</small></div>}
        </div>

        <div className="grid-4 stat-row">
          <div className="stat"><span>Scheduler</span><strong style={{ fontSize: '1.25rem' }}>{data.scheduler ? `${age(data.scheduler)} ago` : 'Never'}</strong><small>last beat</small></div>
          <div className="stat"><span>Checks</span><strong>{data.beats24.toLocaleString()}</strong><small>last 24 hours</small></div>
          <div className="stat"><span>Jobs Waiting</span><strong>0</strong><small>in the queue now</small></div>
          <div className={`stat${data.failed24 > 0 ? ' alert' : ''}`}><span>Failed Jobs</span><strong>{data.failed24}</strong><small>last 24 hours</small></div>
        </div>

        <Panel title="Last 24 Hours" right={<span className="muted">{data.beats24.toLocaleString()} checks · each block is 15 minutes, the worst result in it</span>}>
          <div className="panel-body"><HbStrip blocks={data.blocks} /></div>
        </Panel>

        <Panel title="Checks" right={<Link className="btn sm ghost" to="/sheriff/heartbeat/checks">All Checks</Link>}>
          <div className="table-wrap"><table>
            <thead><tr><th>Status</th><th>Check</th><th>Now</th><th>What to Do</th></tr></thead>
            <tbody>{[...data.checks].sort((a: any, b: any) => order[a.status] - order[b.status]).map((c: any) => (
              <tr key={c.key} className={c.status !== 'ok' ? 'row-warn' : ''}>
                <td><span className={`pill ${hbTone[c.status]}`}>{hbLabel[c.status]}</span></td>
                <td><b>{c.name}</b><div className="help">{c.control ? 'Control' : 'Check'} · {shortTime(c.checkedAt)}</div></td>
                <td className="wrap">{c.detail}</td>
                <td className="wrap help">{c.control ? <button className={`btn sm ${c.status === 'down' ? 'cyan' : 'ghost'}`} onClick={() => flip(c)}>{c.status === 'down' ? 'Bring Back Up' : 'Take Down'}</button> : c.status === 'ok' ? '—' : 'Look at the check detail and the server.'}</td></tr>
            ))}</tbody>
          </table></div>
        </Panel>

        <Panel title="Crons" right={<Link className="btn sm ghost" to="/sheriff/jobs">All Crons</Link>}>
          <div className="table-wrap"><table>
            <thead><tr><th>Cron</th><th>Runs</th><th>Last Run</th><th>Result</th></tr></thead>
            <tbody>{data.crons.map((c: any) => (
              <tr key={c.name} className="click" onClick={() => navigate('/sheriff/jobs')}><td><Link to="/sheriff/jobs"><b>{c.name}</b></Link></td><td className="mono">{c.schedule}</td>
                <td className="nowrap">{c.last ? `${age(c.last.startedAt)} ago` : 'Never'}</td>
                <td>{c.last ? <><span className={`pill ${c.last.ok ? 'ok' : 'bad'}`}>{c.last.ok ? 'OK' : 'Failed'}</span><div className="help">{c.last.message.slice(0, 80)}</div></> : <span className="muted">—</span>}</td></tr>
            ))}</tbody>
          </table></div>
        </Panel>

        <Panel title="Latest Actions" right={<Link className="btn sm ghost" to="/sheriff/timeline">Timeline</Link>}>
          <div className="table-wrap"><table>
            <thead><tr><th>ID</th><th>When</th><th>Who</th><th>What</th></tr></thead>
            <tbody>
              {data.actions.map((h: any) => (
                <tr key={h.id} className="click" onClick={() => navigate(`/sheriff/records/history_entry/${h.id}`)}><td className="nowrap"><Link to={`/sheriff/records/history_entry/${h.id}`}><b>#{h.id}</b></Link></td>
                  <td className="nowrap">{shortTime(h.createdAt)}</td><td>{h.who || 'System'}</td><td className="wrap">{h.summary}</td></tr>
              ))}
              {data.actions.length === 0 && <tr><td colSpan={4} className="empty">No actions recorded.</td></tr>}
            </tbody>
          </table></div>
        </Panel>
      </div>
    </>
  );
}
