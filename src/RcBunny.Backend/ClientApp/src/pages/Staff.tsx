import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../api';
import { useAuth } from '../auth';

interface User { id: number; name: string; email: string; roleId: number; role: string; active: boolean }
interface Role { id: number; name: string; permissions: string[] }
interface StaffData { users: User[]; roles: Role[]; permissions: Record<string, string> }

export default function Staff() {
  const { refresh } = useAuth();
  const { data, error, reload } = useLoad<StaffData>('/api/staff');
  const [problem, setProblem] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  // After any change, reload the list and re-check our own access (we might have changed our own role).
  const run = (action: Promise<unknown>) => action.then(() => { setProblem(null); reload(); refresh(); }, (e: Error) => setProblem(e.message));

  const updateUser = (u: User, changes: Partial<User>) => {
    const next = { ...u, ...changes };
    run(api.put(`/api/staff/${u.id}`, { roleId: next.roleId, active: next.active }));
  };

  const togglePermission = (r: Role, key: string) => {
    const permissions = r.permissions.includes(key) ? r.permissions.filter((p) => p !== key) : [...r.permissions, key];
    run(api.put(`/api/staff/roles/${r.id}`, { permissions }));
  };

  const addUser = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    run(api.post('/api/staff', { name: form.get('name'), email: form.get('email'), password: form.get('password'), roleId: Number(form.get('role')) })
      .then(() => setAdding(false)));
  };

  return (
    <>
      <h1>Staff</h1>
      {problem && <p className="error">{problem}</p>}

      <section className="card">
        <div className="row spread"><h2>People</h2><button onClick={() => setAdding(!adding)}>Add a person</button></div>
        {adding && (
          <form className="row" onSubmit={addUser}>
            <input name="name" placeholder="Name" required />
            <input name="email" type="email" placeholder="Email" required />
            <input name="password" type="password" placeholder="Password (8+ characters)" minLength={8} required />
            <select name="role">{data.roles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}</select>
            <button className="primary">Add</button>
          </form>
        )}
        <table>
          <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Can sign in</th></tr></thead>
          <tbody>
            {data.users.map((u) => (
              <tr key={u.id} className={u.active ? '' : 'inactive'}>
                <td>{u.name}</td>
                <td>{u.email}</td>
                <td>
                  <select value={u.roleId} onChange={(e) => updateUser(u, { roleId: Number(e.target.value) })}>
                    {data.roles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                  </select>
                </td>
                <td><input type="checkbox" checked={u.active} onChange={(e) => updateUser(u, { active: e.target.checked })} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="card">
        <h2>What each role can do</h2>
        <div className="scroll">
          <table>
            <thead><tr><th>Role</th>{Object.entries(data.permissions).map(([key, label]) => <th key={key} title={label}>{key}</th>)}</tr></thead>
            <tbody>
              {data.roles.map((r) => (
                <tr key={r.id}>
                  <td>{r.name}</td>
                  {Object.keys(data.permissions).map((key) => (
                    <td key={key}><input type="checkbox" checked={r.permissions.includes(key)} onChange={() => togglePermission(r, key)} /></td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <ul className="muted">{Object.entries(data.permissions).map(([key, label]) => <li key={key}><b>{key}</b>: {label}</li>)}</ul>
      </section>
    </>
  );
}
