import { useState } from 'react';
import { api, useLoad } from '../../api';

interface Check { id: number; name: string; ok: boolean; detail: string; checkedAt: string }
interface Control { id: number; key: string; name: string; down: boolean; reason: string | null; changedAt: string }

export default function Heartbeat() {
  const { data, error, reload } = useLoad<{ checks: Check[]; controls: Control[] }>('/api/sheriff/heartbeat');
  const [problem, setProblem] = useState<string | null>(null);
  const [running, setRunning] = useState(false);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const runChecks = () => { setRunning(true); api.post('/api/sheriff/heartbeat/run').then(reload).finally(() => setRunning(false)); };
  const flip = (c: Control) => {
    const reason = c.down ? '' : prompt(`Why take ${c.name} down? Visitors will see this.`, 'We are doing some maintenance and will be back shortly.');
    if (reason === null) return;
    api.post(`/api/sheriff/heartbeat/controls/${c.id}`, { down: !c.down, reason }).then(() => { setProblem(null); reload(); }, (e: Error) => setProblem(e.message));
  };

  return (
    <>
      <div className="row spread"><h1>Heartbeat</h1><button onClick={runChecks} disabled={running}>{running ? 'Checking…' : 'Run checks now'}</button></div>
      {problem && <p className="error">{problem}</p>}
      <div className="stats">
        {data.checks.length === 0 && <p className="muted">No checks yet. They run every 5 minutes.</p>}
        {data.checks.map((c) => (
          <div key={c.id} className={`card ${c.ok ? 'ok' : 'bad'}`}>
            <span className="muted">{c.name}</span><strong>{c.ok ? 'OK' : 'Problem'}</strong>
            <small>{c.detail} · {new Date(c.checkedAt).toLocaleTimeString('en-US')}</small>
          </div>
        ))}
      </div>
      <section className="card">
        <h2>Controls</h2>
        {data.controls.map((c) => (
          <div key={c.id} className="row spread">
            <span><b>{c.name}</b>: {c.down ? <span className="error">down — {c.reason}</span> : 'up'}</span>
            <button className={c.down ? 'primary' : ''} onClick={() => flip(c)}>{c.down ? 'Bring it back up' : 'Take it down'}</button>
          </div>
        ))}
        <p className="muted">The website checks this every 30 seconds.</p>
      </section>
    </>
  );
}
