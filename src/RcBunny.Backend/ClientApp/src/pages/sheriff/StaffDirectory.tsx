import { useMemo } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { prefetchStaff, useMeta, useStaff } from '../../queries';
import { date, ErrorBox, Panel, Pill, Refreshing, Skeleton } from '../../ui';

/**
 * Staff Directory. The whole list is fetched once and cached; filtering is instant and client-side
 * (a directory is hundreds or a few thousand rows). "Who can use X" answers access questions quickly.
 */
export default function StaffDirectory() {
  const [params, setParams] = useSearchParams();
  const { data: meta } = useMeta();
  const staff = useStaff();
  const navigate = useNavigate();
  const f = {
    q: params.get('q') ?? '',
    dept: params.get('dept') ?? '',
    who: params.get('who') ?? '',          // '', 'in-office', '3rd-party', or a company name
    role: params.get('role') ?? '',
    perm: params.get('perm') ?? '',
    status: params.get('status') ?? 'active',
  };
  const set = (k: keyof typeof f, v: string) => {
    const p = new URLSearchParams(params);
    if (v) p.set(k, v); else p.delete(k);
    setParams(p, { replace: true });
  };

  const companies = useMemo(() => [...new Set((staff.data ?? []).map((s) => s.company).filter(Boolean))] as string[], [staff.data]);
  const roles = useMemo(() => [...new Set((staff.data ?? []).map((s) => s.role).filter(Boolean))] as string[], [staff.data]);
  const today = new Date().toISOString().slice(0, 10);

  const rows = useMemo(() => {
    const q = f.q.toLowerCase();
    return (staff.data ?? []).filter((s) => {
      const ended = !!s.accessEndsOn && s.accessEndsOn < today;
      if (f.status === 'active' && (!s.active || ended)) return false;
      if (f.status === 'disabled' && s.active && !ended) return false;
      if (f.dept && s.department !== f.dept) return false;
      if (f.who === 'in-office' && s.company) return false;
      if (f.who === '3rd-party' && !s.company) return false;
      if (f.who && !['in-office', '3rd-party'].includes(f.who) && s.company !== f.who) return false;
      if (f.role && s.role !== f.role) return false;
      if (f.perm && !s.perms.includes(f.perm)) return false;
      return !q || [s.name, s.email, s.employeeId, s.title, s.company].some((v) => v?.toLowerCase().includes(q));
    });
  }, [staff.data, f.q, f.dept, f.who, f.role, f.perm, f.status, today]);

  const permLabel = (p: string) => meta?.apps.find((a) => a.key === p)?.name ?? meta?.rights[p] ?? p;

  return (
    <>
      <div className="page-head">
        <h1>Staff Directory</h1>
        <Link className="btn" to="/sheriff/users/new">+ Add a person</Link>
      </div>
      <Panel>
        <div className="filters">
          <input type="search" placeholder="Name, email, employee ID, title…" value={f.q} onChange={(e) => set('q', e.target.value)} aria-label="Search staff" autoFocus />
          <select value={f.who} onChange={(e) => set('who', e.target.value)} aria-label="Employer">
            <option value="">Everyone</option>
            <option value="in-office">In-office employees</option>
            <option value="3rd-party">All 3rd-party</option>
            {companies.map((c) => <option key={c}>{c}</option>)}
          </select>
          <select value={f.dept} onChange={(e) => set('dept', e.target.value)} aria-label="Department">
            <option value="">All departments</option>
            {meta?.departments.map((d) => <option key={d}>{d}</option>)}
          </select>
          <select value={f.role} onChange={(e) => set('role', e.target.value)} aria-label="Role">
            <option value="">All roles</option>
            {roles.map((r) => <option key={r}>{r}</option>)}
          </select>
          <select value={f.perm} onChange={(e) => set('perm', e.target.value)} aria-label="Who can use">
            <option value="">Who can use…</option>
            {meta?.apps.map((a) => <option key={a.key} value={a.key}>{a.name}</option>)}
            {Object.entries(meta?.rights ?? {}).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
          <select value={f.status} onChange={(e) => set('status', e.target.value)} aria-label="Status">
            <option value="active">Can sign in</option>
            <option value="disabled">Disabled or ended</option>
            <option value="all">Everyone</option>
          </select>
        </div>
      </Panel>
      <Panel title={<>People <span className="muted small">{rows.length}</span></>} actions={<Refreshing on={staff.isFetching && !staff.isPending} />}>
        <ErrorBox error={staff.error} />
        {staff.isPending ? <Skeleton rows={10} cols={5} /> : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Name</th><th>Role</th><th>Access</th><th>Works for</th><th>Last sign-in</th></tr></thead>
              <tbody>
                {rows.map((s) => (
                  <tr key={s.id} className={`clickable ${s.active ? '' : 'dim'}`} onMouseEnter={() => void prefetchStaff(s.id)} onClick={() => navigate(`/sheriff/users/${s.id}`)}>
                    <td>
                      <Link to={`/sheriff/users/${s.id}`}>{s.name}</Link>
                      <div className="muted small">{s.title ?? '—'} · {s.email}</div>
                    </td>
                    <td>{s.role}{s.hasExceptions && <> <Pill tone="info">custom</Pill></>}</td>
                    <td className="perm-list">{s.perms.map((p) => <Pill key={p}>{permLabel(p)}</Pill>)}</td>
                    <td>
                      {s.company ? <><Pill tone="warn">3rd party</Pill> {s.company}</> : s.department ?? '—'}
                      {s.accessEndsOn && <div className="muted small">until {date(s.accessEndsOn)}</div>}
                    </td>
                    <td>{s.active ? date(s.lastLoginAt) : <Pill tone="bad">Disabled</Pill>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </>
  );
}
