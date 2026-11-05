import { api, dateTime, useLoad } from '../../api';
import { EmptyRow, PageHead, Panel, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const command = (name: string) => 'app:' + name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

/** Sheriff → Crons (ebunny's sheriff/jobs): the scheduled jobs, Run Now, and every run. */
export default function Crons() {
  const { data, error, reload } = useLoad<any>('/api/sheriff/jobs');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const status = (r: any) => (r.ok ? 'OK' : 'Failed');
  return (
    <>
      <PageHead title="Crons" sub="Scheduled background jobs. The admin runs them on their schedule while it's running." />
      {bar}
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Job</th><th>Command</th><th>Schedule</th><th>Last Run</th><th className="num">Duration</th><th>Status</th><th /></tr></thead>
        <tbody>{data.jobs.map((j: any) => (
          <tr key={j.name}><td><b>{j.name}</b>{j.last?.message && <><br /><span className="help">{j.last.message}</span></>}</td>
            <td className="mono">{command(j.name)}</td><td className="mono">{j.schedule}</td>
            <td className="nowrap">{j.last ? <>#{j.last.id} · {dateTime(j.last.startedAt)}</> : 'Never'}</td>
            <td className="num">—</td>
            <td>{j.last && <span className={`pill ${j.last.ok ? 'ok' : 'bad'}`}>{status(j.last)}</span>}</td>
            <td><button className="btn sm ghost" onClick={() => run(api.post(`/api/sheriff/jobs/run?name=${encodeURIComponent(j.name)}`), `${j.name} ran`)}>Run Now</button></td></tr>
        ))}</tbody>
      </table></div></div>
      <Panel title="Recent Runs" right={<span className="muted">every cron run, newest first</span>}>
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Started</th><th>Job</th><th>Status</th><th className="num">Duration</th><th>Message</th></tr></thead>
          <tbody>
            {data.runs.map((r: any) => (
              <tr key={r.id}><td className="nowrap"><b>#{r.id}</b></td><td className="nowrap">{dateTime(r.startedAt)}</td>
                <td><b>{r.job}</b><div className="help mono">{command(r.job)}</div></td>
                <td><span className={`pill ${r.ok ? 'ok' : 'bad'}`}>{status(r)}</span></td><td className="num">—</td>
                <td className="wrap">{r.message.length > 120 ? r.message.slice(0, 120) + '...' : r.message}</td></tr>
            ))}
            {data.runs.length === 0 && <EmptyRow cols={6}>No runs yet.</EmptyRow>}
          </tbody>
        </table></div>
      </Panel>
    </>
  );
}
