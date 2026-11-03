import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { put } from '../../api';
import { useMeta, useRoles } from '../../queries';
import { ErrorBox, Panel, Skeleton } from '../../ui';

/** Roles × apps/rights. Saving bumps the access version of everyone with a changed role, so their screens update. */
export default function Roles() {
  const { data: meta } = useMeta();
  const roles = useRoles();
  const qc = useQueryClient();
  const [perms, setPerms] = useState<Record<number, string[]>>({});
  const [saved, setSaved] = useState<string[] | null>(null);

  useEffect(() => {
    if (roles.data) setPerms(Object.fromEntries(roles.data.map((r) => [r.id, r.perms])));
  }, [roles.data]);

  const save = useMutation({
    mutationFn: () => put<{ changed: string[] }>('/api/sheriff/roles', perms),
    onSuccess: async (res) => {
      setSaved(res.changed);
      await qc.invalidateQueries({ queryKey: ['sheriff'] });
      await qc.invalidateQueries({ queryKey: ['me'] });
    },
  });

  if (!meta || roles.isPending) return <div className="panel"><Skeleton rows={8} cols={8} /></div>;
  const cols = [...meta.apps.map((a) => [a.key, a.name] as const), ...Object.entries(meta.rights)];
  const dirty = roles.data?.some((r) => JSON.stringify([...r.perms].sort()) !== JSON.stringify([...(perms[r.id] ?? [])].sort()));

  return (
    <>
      <h1>Roles</h1>
      <p className="muted">What each role can open. People can also have access added or taken away individually (Staff Directory → Edit).</p>
      <Panel>
        <ErrorBox error={save.error} />
        {saved && <div className="alert ok">{saved.length ? `Saved: ${saved.join(', ')}` : 'Nothing changed.'}</div>}
        <div className="table-wrap">
          <table className="table compact matrix">
            <thead>
              <tr><th>Role</th><th>People</th>{cols.map(([k, n]) => <th key={k}>{n}</th>)}</tr>
            </thead>
            <tbody>
              {roles.data?.map((r) => (
                <tr key={r.id}>
                  <td>{r.name}{r.administrator && <div className="muted small">always everything</div>}</td>
                  <td className="num">{r.users}</td>
                  {cols.map(([k, n]) => (
                    <td key={k} className="center">
                      <input
                        type="checkbox"
                        aria-label={`${r.name}: ${n}`}
                        disabled={r.administrator}
                        checked={(perms[r.id] ?? []).includes(k)}
                        onChange={(e) => {
                          setSaved(null);
                          setPerms((p) => ({ ...p, [r.id]: e.target.checked ? [...(p[r.id] ?? []), k] : (p[r.id] ?? []).filter((x) => x !== k) }));
                        }}
                      />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="row">
          <button className="btn" disabled={!dirty || save.isPending} onClick={() => save.mutate()}>{save.isPending ? 'Saving…' : 'Save roles'}</button>
        </div>
      </Panel>
    </>
  );
}
