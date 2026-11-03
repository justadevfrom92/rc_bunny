import { useLoad } from '../../api';

interface Check { id: number; name: string; ok: boolean; detail: string; checkedAt: string }

export default function Checks() {
  const { data, error } = useLoad<Check[]>('/api/sheriff/heartbeat/checks');
  return (
    <>
      <h1>Checks</h1>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>When</th><th>Check</th><th>Result</th><th>Detail</th></tr></thead>
          <tbody>{data.map((c) => <tr key={c.id}><td>{new Date(c.checkedAt).toLocaleString('en-US')}</td><td>{c.name}</td><td className={c.ok ? '' : 'error'}>{c.ok ? 'OK' : 'Problem'}</td><td>{c.detail}</td></tr>)}</tbody>
        </table>
      )}
    </>
  );
}
