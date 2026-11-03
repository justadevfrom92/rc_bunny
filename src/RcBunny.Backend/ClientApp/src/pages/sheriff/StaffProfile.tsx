import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { post, queryClient } from '../../api';
import { useMe } from '../../auth';
import { staffDetail, useMeta } from '../../queries';
import { date, dateTime, ErrorBox, Panel, Pill, Skeleton } from '../../ui';

export default function StaffProfile() {
  const id = Number(useParams().id);
  const me = useMe();
  const qc = useQueryClient();
  const { data: meta } = useMeta();
  const q = useQuery(staffDetail(id));

  const toggle = useMutation({
    mutationFn: (active: boolean) => post(`/api/sheriff/users/${id}/active`, { active }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sheriff'] }),
  });
  const logInAs = useMutation({
    mutationFn: () => post(`/api/auth/log-in-as/${id}`),
    onSuccess: () => {
      queryClient.clear();
      window.location.assign('/');
    },
  });

  if (q.isPending) return <div className="panel"><Skeleton rows={8} /></div>;
  if (q.error || !q.data) return <ErrorBox error={q.error} />;
  const u = q.data;
  const label = (p: string) => meta?.apps.find((a) => a.key === p)?.name ?? meta?.rights[p] ?? p;
  const allPerms = [...(meta?.apps.map((a) => a.key) ?? []), ...Object.keys(meta?.rights ?? {})];

  return (
    <>
      <p className="crumbs"><Link to="/sheriff/users">Staff Directory</Link> › {u.name}</p>
      <div className="page-head">
        <div>
          <h1>{u.name} {!u.active && <Pill tone="bad">Disabled</Pill>} {u.company && <Pill tone="warn">3rd party</Pill>}</h1>
          <p className="muted">{u.title ?? '—'} · {u.company ?? u.department ?? '—'} · {u.email}</p>
        </div>
        <div className="row">
          <Link className="btn" to={`/sheriff/users/${id}/edit`}>Edit</Link>
          {u.id !== me.id && (
            <button className="btn outline" disabled={toggle.isPending} onClick={() => toggle.mutate(!u.active)}>
              {u.active ? 'Disable sign-in' : 'Enable sign-in'}
            </button>
          )}
          <button
            className="btn outline"
            disabled={!u.canLogInAs || logInAs.isPending}
            title={u.cannotLogInAs ?? 'See the admin exactly as they do'}
            onClick={() => logInAs.mutate()}
          >
            Log in as
          </button>
        </div>
      </div>
      <ErrorBox error={toggle.error ?? logInAs.error} />
      {!u.canLogInAs && u.cannotLogInAs && u.id !== me.id && <p className="muted small">Log in as: {u.cannotLogInAs}</p>}

      <div className="grid-2">
        <Panel title="Access">
          <p className="small">Role: <strong>{u.role ?? 'none'}</strong></p>
          <table className="table compact">
            <thead><tr><th>App / right</th><th>Role</th><th>This person</th></tr></thead>
            <tbody>
              {allPerms.map((p) => {
                const inRole = u.rolePerms.includes(p);
                const has = u.perms.includes(p);
                const change = u.extraPerms.includes(p) ? 'added' : u.deniedPerms.includes(p) ? 'removed' : null;
                return (
                  <tr key={p}>
                    <td>{label(p)}</td>
                    <td>{inRole ? '✓' : ''}</td>
                    <td>
                      {has ? <Pill tone="ok">Yes</Pill> : <span className="muted">No</span>}{' '}
                      {change && <Pill tone={change === 'added' ? 'info' : 'warn'}>{change}</Pill>}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {u.accessNote && <p className="small muted">Why it differs: {u.accessNote}</p>}
        </Panel>
        <Panel title="Profile">
          <dl className="facts">
            <dt>Employee ID</dt><dd>{u.employeeId ?? '—'}</dd>
            <dt>Manager</dt><dd>{u.managerId ? <Link to={`/sheriff/users/${u.managerId}`}>{u.manager}</Link> : '—'}</dd>
            <dt>Location</dt><dd>{u.location ?? '—'}</dd>
            <dt>Ext.</dt><dd>{u.phoneExt ?? '—'}</dd>
            <dt>Hired</dt><dd>{date(u.hiredOn)}</dd>
            <dt>Access ends</dt><dd>{u.accessEndsOn ? date(u.accessEndsOn) : 'No end date'}</dd>
            <dt>Last sign-in</dt><dd>{dateTime(u.lastLoginAt)}</dd>
            <dt>Reports</dt>
            <dd>{u.reports.length ? u.reports.map((r, i) => <span key={r.id}>{i > 0 && ', '}<Link to={`/sheriff/users/${r.id}`}>{r.name}</Link></span>) : '—'}</dd>
          </dl>
        </Panel>
      </div>

      <Panel title="Recent activity">
        {u.recent.length === 0 ? <p className="muted">Nothing yet.</p> : (
          <table className="table compact">
            <tbody>
              {u.recent.map((a) => <tr key={a.id}><td>{dateTime(a.createdAt)}</td><td>{a.action.replace(/_/g, ' ')}</td><td>{a.subject}</td><td className="muted">{a.detail}</td></tr>)}
            </tbody>
          </table>
        )}
      </Panel>
    </>
  );
}
