import { useState, type FormEvent } from 'react';
import { api, useLoad } from '../../api';
import { useAuth } from '../../auth';

interface User { id: number; name: string; email: string; roleId: number; role: string; active: boolean }
interface Role { id: number; name: string }

/** Sheriff → Staff Directory: who can sign in, and their role. */
export default function Staff() {
  const { refresh } = useAuth();
  const { data, error, reload } = useLoad<{ users: User[]; roles: Role[] }>('/api/staff');
  const [problem, setProblem] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  // After a change, reload and re-check our own access (we might have changed our own role).
  const run = (action: Promise<unknown>, done?: string) =>
    action.then(() => { setProblem(null); setMessage(done ?? null); reload(); refresh(); }, (e: Error) => setProblem(e.message));

  const update = (u: User, changes: Partial<User>) => {
    const next = { ...u, ...changes };
    run(api.put(`/api/staff/${u.id}`, { roleId: next.roleId, active: next.active }));
  };
  const setPassword = (u: User) => {
    const password = prompt(`New password for ${u.name} (8+ characters)`);
    if (password) run(api.post(`/api/staff/${u.id}/password`, { password }), `${u.name}'s password is changed.`);
  };
  const add = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post('/api/staff', { name: f.get('name'), email: f.get('email'), password: f.get('password'), roleId: Number(f.get('role')) }).then(() => setAdding(false)));
  };

  return (
    <>
      <div className="row spread"><h1>Staff Directory</h1><button onClick={() => setAdding(!adding)}>Add a person</button></div>
      {problem && <p className="error">{problem}</p>}
      {message && <p>{message}</p>}
      {adding && (
        <form className="card row" onSubmit={add}>
          <input name="name" placeholder="Name" required />
          <input name="email" type="email" placeholder="Email" required />
          <input name="password" type="password" placeholder="Password (8+ characters)" minLength={8} required />
          <select name="role">{data.roles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}</select>
          <button className="primary">Add</button>
        </form>
      )}
      <table>
        <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Can sign in</th><th /></tr></thead>
        <tbody>
          {data.users.map((u) => (
            <tr key={u.id} className={u.active ? '' : 'inactive'}>
              <td>{u.name}</td><td>{u.email}</td>
              <td><select value={u.roleId} onChange={(e) => update(u, { roleId: Number(e.target.value) })}>{data.roles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}</select></td>
              <td><input type="checkbox" checked={u.active} onChange={(e) => update(u, { active: e.target.checked })} /></td>
              <td><button onClick={() => setPassword(u)}>Set password</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
