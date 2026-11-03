import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../../api';
import { useAuth } from '../../auth';

interface Role { id: number; name: string; permissions: string[]; people: number }

/** Sheriff → Roles: tick what each role can do. Changes reach people on their next click. */
export default function Roles() {
  const { refresh } = useAuth();
  const { data, error, reload } = useLoad<{ roles: Role[]; permissions: Record<string, string> }>('/api/staff');
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); refresh(); }, (e: Error) => setProblem(e.message));
  const toggle = (r: Role, key: string) => {
    const permissions = r.permissions.includes(key) ? r.permissions.filter((p) => p !== key) : [...r.permissions, key];
    run(api.put(`/api/staff/roles/${r.id}`, { name: r.name, permissions }));
  };
  const add = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget;
    run(api.post('/api/staff/roles', { name: new FormData(form).get('name'), permissions: [] }).then(() => form.reset()));
  };

  return (
    <>
      <h1>Roles</h1>
      {problem && <p className="error">{problem}</p>}
      <form className="card row" onSubmit={add}>
        <input name="name" placeholder="New role name" required />
        <button className="primary">Add role</button>
      </form>
      <div className="scroll">
        <table>
          <thead><tr><th>Role</th><th className="num">People</th>{Object.entries(data.permissions).map(([key, label]) => <th key={key} title={label}>{key}</th>)}</tr></thead>
          <tbody>
            {data.roles.map((r) => (
              <tr key={r.id}>
                <td>{r.name}</td><td className="num">{r.people}</td>
                {Object.keys(data.permissions).map((key) => (
                  <td key={key}><input type="checkbox" checked={r.permissions.includes(key)} onChange={() => toggle(r, key)} /></td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ul className="muted">{Object.entries(data.permissions).map(([key, label]) => <li key={key}><b>{key}</b>: {label}</li>)}</ul>
    </>
  );
}
