import { useState } from 'react';
import { api, useLoad } from '../../api';
import { useAuth } from '../../auth';
import { PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Sheriff → Roles (ebunny's sheriff/roles): which apps and rights each role has; Save Roles saves them all. */
export default function Roles() {
  const { data, error } = useLoad<any>('/api/staff');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <RolesForm data={data} />;
}

function RolesForm({ data }: { data: any }) {
  const { refresh } = useAuth();
  const [run, bar] = useRun(refresh);
  const [perms, setPerms] = useState<Record<number, string[]>>(() => Object.fromEntries(data.roles.map((r: any) => [r.id, r.permissions])));
  const labels: Record<string, string> = { ...data.apps, ...data.rights };
  const toggle = (id: number, key: string) => setPerms({ ...perms, [id]: perms[id].includes(key) ? perms[id].filter((p) => p !== key) : [...perms[id], key] });
  const save = () => run(Promise.all(data.roles.filter((r: any) => r.name !== 'Administrator')
    .map((r: any) => api.put(`/api/staff/roles/${r.id}`, { name: r.name, permissions: perms[r.id] }))), 'Roles saved');

  return (
    <form style={{ display: 'flex', flexDirection: 'column', gap: 20 }} onSubmit={(e) => { e.preventDefault(); save(); }}>
      <PageHead title="Roles" sub="Which apps and rights each role has. Administrator always has everything." actions={<button className="btn cyan">Save Roles</button>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Role</th><th className="num">Users</th>{Object.values(labels).map((l) => <th key={l} style={{ textAlign: 'center' }}>{l}</th>)}</tr></thead>
        <tbody>{data.roles.map((r: any) => (
          <tr key={r.id}><td><b>{r.name}</b></td><td className="num">{r.people}</td>
            {Object.entries(labels).map(([key, label]) => (
              <td key={key} style={{ textAlign: 'center' }}>
                <input type="checkbox" checked={r.name === 'Administrator' || perms[r.id].includes(key)} disabled={r.name === 'Administrator'} onChange={() => toggle(r.id, key)} aria-label={`${r.name}: ${label}`} />
              </td>
            ))}</tr>
        ))}</tbody>
      </table></div></div>
    </form>
  );
}
