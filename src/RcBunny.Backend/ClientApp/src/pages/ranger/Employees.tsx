import { useLoad } from '../../api';

interface Employee { id: number; name: string; email: string; role: string; active: boolean; team: string | null }

export default function Employees() {
  const { data, error } = useLoad<Employee[]>('/api/ranger/employees');
  return (
    <>
      <h1>Employees</h1>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Ticket team</th></tr></thead>
          <tbody>
            {data.map((e) => (
              <tr key={e.id} className={e.active ? '' : 'inactive'}>
                <td>{e.name}</td><td><a href={`mailto:${e.email}`}>{e.email}</a></td><td>{e.role}</td><td>{e.team ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
