import { useState } from 'react';
import { api, useLoad } from '../../api';

interface Run { id: number; job: string; startedAt: string; ok: boolean; message: string; runBy: string | null }
interface Job { name: string; schedule: string; last: Run | null }

export default function Crons() {
  const { data, error, reload } = useLoad<{ jobs: Job[]; runs: Run[] }>('/api/sheriff/jobs');
  const [running, setRunning] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const run = (name: string) => { setRunning(name); api.post(`/api/sheriff/jobs/run?name=${encodeURIComponent(name)}`).then(reload).finally(() => setRunning(null)); };
  const when = (s: string) => new Date(s).toLocaleString('en-US');

  return (
    <>
      <h1>Crons</h1>
      <table>
        <thead><tr><th>Job</th><th>Runs</th><th>Last run</th><th /></tr></thead>
        <tbody>
          {data.jobs.map((j) => (
            <tr key={j.name}>
              <td>{j.name}</td><td>{j.schedule}</td>
              <td>{j.last ? <><span className={j.last.ok ? '' : 'error'}>{j.last.ok ? 'OK' : 'Failed'}</span> · {when(j.last.startedAt)} · {j.last.message}</> : <span className="muted">Not yet</span>}</td>
              <td><button onClick={() => run(j.name)} disabled={running !== null}>{running === j.name ? 'Running…' : 'Run now'}</button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <h2 style={{ marginTop: 24 }}>Job runs</h2>
      <table>
        <thead><tr><th>When</th><th>Job</th><th>Result</th><th>Message</th><th>Run by</th></tr></thead>
        <tbody>{data.runs.map((r) => <tr key={r.id}><td>{when(r.startedAt)}</td><td>{r.job}</td><td className={r.ok ? '' : 'error'}>{r.ok ? 'OK' : 'Failed'}</td><td>{r.message}</td><td>{r.runBy ?? 'Schedule'}</td></tr>)}</tbody>
      </table>
    </>
  );
}
