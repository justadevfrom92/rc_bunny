import { useLoad } from '../../api';

interface Integration { name: string; usedFor: string; setting: string; configured: boolean }

export default function Integrations() {
  const { data, error } = useLoad<Integration[]>('/api/sheriff/integrations');
  return (
    <>
      <h1>Integrations</h1>
      <p className="muted">The outside services ebunny used. None is connected in this version yet; until one is, that feature records what it would have sent.</p>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Service</th><th>Used for</th><th>Setting</th><th>Status</th></tr></thead>
          <tbody>
            {data.map((i) => (
              <tr key={i.name}><td>{i.name}</td><td>{i.usedFor}</td><td><code>{i.setting}</code></td><td>{i.configured ? 'Setting found (not connected yet)' : <span className="muted">Not set up</span>}</td></tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
