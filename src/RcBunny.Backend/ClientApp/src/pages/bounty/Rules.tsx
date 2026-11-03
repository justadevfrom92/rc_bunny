import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../../api';

interface Rule { id: number; event: string; name: string; stars: number; active: boolean }

/** How stars are earned. "Pay a bill" is given out every night by the Reward stars job; the others are given by staff on a member's page. */
export default function Rules() {
  const { data, error, reload } = useLoad<Rule[]>('/api/bounty/rules');
  const [problem, setProblem] = useState<string | null>(null);

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); }, (e: Error) => setProblem(e.message));
  const add = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    run(api.post('/api/bounty/rules', { name: f.get('name'), stars: Number(f.get('stars')), active: true }).then(() => form.reset()));
  };
  const update = (r: Rule, changes: Partial<Rule>) => run(api.put(`/api/bounty/rules/${r.id}`, { ...r, ...changes }));

  return (
    <>
      <h1>Earning Rules</h1>
      <p className="muted">"Pay a bill" is given automatically every night (Sheriff → Crons → Reward stars). Staff give the others from a member's page.</p>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      <form className="card row" onSubmit={add}>
        <input name="name" placeholder="New rule, e.g. Renew early" required />
        <input name="stars" type="number" min="1" placeholder="Stars" required style={{ maxWidth: 120 }} />
        <button className="primary">Add</button>
      </form>
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Rule</th><th>Given</th><th className="num">Stars</th><th>Active</th></tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.id} className={r.active ? '' : 'inactive'}>
                <td>{r.name}</td><td>{r.event === 'bill_paid' ? 'Automatically, each bill paid' : 'By staff'}</td>
                <td className="num"><input type="number" min="1" defaultValue={r.stars} style={{ width: 90 }} onBlur={(e) => Number(e.target.value) !== r.stars && update(r, { stars: Number(e.target.value) })} /></td>
                <td><input type="checkbox" checked={r.active} onChange={(e) => update(r, { active: e.target.checked })} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
