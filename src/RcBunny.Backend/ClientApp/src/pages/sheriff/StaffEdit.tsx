import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ApiError, post, put } from '../../api';
import { staffDetail, useMeta, useRoles, useStaff } from '../../queries';
import { ErrorBox, Panel, Skeleton } from '../../ui';

type Choice = 'role' | 'allow' | 'deny';

interface Form {
  name: string;
  email: string;
  employeeId: string;
  title: string;
  department: string;
  managerId: string;
  phoneExt: string;
  location: string;
  hiredOn: string;
  company: string;
  accessEndsOn: string;
  roleId: string;
  access: Record<string, Choice>;
  accessNote: string;
  password: string;
}

const empty: Form = {
  name: '', email: '', employeeId: '', title: '', department: '', managerId: '', phoneExt: '', location: '', hiredOn: '',
  company: '', accessEndsOn: '', roleId: '', access: {}, accessNote: '', password: '',
};

/** Add a person, or edit one: profile, employer (in-office / 3rd party + end date), role and per-person access. */
export default function StaffEdit() {
  const idParam = useParams().id;
  const id = idParam ? Number(idParam) : null;
  const navigate = useNavigate();
  const qc = useQueryClient();
  const { data: meta } = useMeta();
  const roles = useRoles();
  const staff = useStaff();
  const existing = useQuery({ ...staffDetail(id ?? 0), enabled: id !== null });
  const [form, setForm] = useState<Form>(empty);

  useEffect(() => {
    const u = existing.data;
    if (!u) return;
    const access: Record<string, Choice> = {};
    u.extraPerms.forEach((p) => (access[p] = 'allow'));
    u.deniedPerms.forEach((p) => (access[p] = 'deny'));
    setForm({
      name: u.name, email: u.email, employeeId: u.employeeId ?? '', title: u.title ?? '', department: u.department ?? '',
      managerId: u.managerId?.toString() ?? '', phoneExt: u.phoneExt ?? '', location: u.location ?? '', hiredOn: u.hiredOn ?? '',
      company: u.company ?? '', accessEndsOn: u.accessEndsOn ?? '', roleId: u.roleId?.toString() ?? '', access,
      accessNote: u.accessNote ?? '', password: '',
    });
  }, [existing.data]);

  const role = roles.data?.find((r) => r.id === Number(form.roleId));
  const allPerms = useMemo(() => [...(meta?.apps.map((a) => a.key) ?? []), ...Object.keys(meta?.rights ?? {})], [meta]);
  const effective = allPerms.filter((p) => {
    const c = form.access[p] ?? 'role';
    const inRole = role?.perms.includes(p) ?? false;
    return c === 'allow' ? true : c === 'deny' ? false : inRole;
  });
  const hasExceptions = allPerms.some((p) => (form.access[p] ?? 'role') !== 'role');

  const save = useMutation({
    mutationFn: () => {
      const body = {
        ...form,
        managerId: form.managerId ? Number(form.managerId) : null,
        roleId: Number(form.roleId),
        hiredOn: form.hiredOn || null,
        accessEndsOn: form.accessEndsOn || null,
        department: form.department || null,
        password: form.password || null,
      };
      return id ? put<{ id: number }>(`/api/sheriff/users/${id}`, body) : post<{ id: number }>('/api/sheriff/users', body);
    },
    onSuccess: async (res) => {
      await qc.invalidateQueries({ queryKey: ['sheriff'] });
      await qc.invalidateQueries({ queryKey: ['me'] }); // in case you changed yourself
      navigate(`/sheriff/users/${res.id}`);
    },
  });

  const set = <K extends keyof Form>(k: K, v: Form[K]) => setForm((f) => ({ ...f, [k]: v }));
  const label = (p: string) => meta?.apps.find((a) => a.key === p)?.name ?? meta?.rights[p] ?? p;
  const fieldError = (name: string) => save.error instanceof ApiError && save.error.field === name ? true : undefined;

  if (id && existing.isPending) return <div className="panel"><Skeleton rows={10} /></div>;

  const submit = (e: FormEvent) => {
    e.preventDefault();
    save.mutate();
  };

  return (
    <form onSubmit={submit}>
      <p className="crumbs"><Link to="/sheriff/users">Staff Directory</Link> › {id ? form.name : 'Add a person'}</p>
      <h1>{id ? `Edit ${form.name}` : 'Add a person'}</h1>
      <ErrorBox error={save.error} />
      <div className="grid-2">
        <Panel title="Profile">
          <div className="form-grid">
            <label>Name<input value={form.name} onChange={(e) => set('name', e.target.value)} required aria-invalid={fieldError('name')} /></label>
            <label>Email<input type="email" value={form.email} onChange={(e) => set('email', e.target.value)} required aria-invalid={fieldError('email')} /></label>
            <label>Employee ID<input value={form.employeeId} onChange={(e) => set('employeeId', e.target.value)} aria-invalid={fieldError('employeeId')} /></label>
            <label>Title<input value={form.title} onChange={(e) => set('title', e.target.value)} /></label>
            <label>Department
              <select value={form.department} onChange={(e) => set('department', e.target.value)}>
                <option value="">—</option>
                {meta?.departments.map((d) => <option key={d}>{d}</option>)}
              </select>
            </label>
            <label>Manager
              <select value={form.managerId} onChange={(e) => set('managerId', e.target.value)} aria-invalid={fieldError('managerId')}>
                <option value="">—</option>
                {staff.data?.filter((s) => s.id !== id && s.active).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </label>
            <label>Location<input value={form.location} onChange={(e) => set('location', e.target.value)} /></label>
            <label>Ext.<input value={form.phoneExt} onChange={(e) => set('phoneExt', e.target.value)} maxLength={10} /></label>
            <label>Hired on<input type="date" value={form.hiredOn} onChange={(e) => set('hiredOn', e.target.value)} /></label>
            <label>{id ? 'New password (optional)' : 'Temporary password'}
              <input type="password" autoComplete="new-password" minLength={12} value={form.password} onChange={(e) => set('password', e.target.value)} required={!id} aria-invalid={fieldError('password')} />
            </label>
          </div>
        </Panel>
        <Panel title="Works for">
          <div className="form-grid">
            <label>3rd-party company <span className="muted small">(blank = in-office)</span>
              <input value={form.company} onChange={(e) => set('company', e.target.value)} placeholder="e.g. Brightline Contact Solutions" />
            </label>
            <label>Access ends on <span className="muted small">(sign-in stops after this day)</span>
              <input type="date" value={form.accessEndsOn} onChange={(e) => set('accessEndsOn', e.target.value)} />
            </label>
          </div>
          {form.company && !form.accessEndsOn && <p className="alert warn small">3rd-party people usually get an end date (their contract end).</p>}
        </Panel>
      </div>

      <Panel title="Access">
        <label className="inline">Role{' '}
          <select value={form.roleId} onChange={(e) => set('roleId', e.target.value)} required aria-invalid={fieldError('roleId')}>
            <option value="">Pick a role</option>
            {roles.data?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        </label>
        <table className="table compact access-matrix">
          <thead><tr><th>App / right</th><th>Role gives</th><th>As the role</th><th>Add for them</th><th>Take away</th><th>Result</th></tr></thead>
          <tbody>
            {allPerms.map((p) => {
              const inRole = role?.perms.includes(p) ?? false;
              const c = form.access[p] ?? 'role';
              const pick = (v: Choice) => set('access', { ...form.access, [p]: v });
              return (
                <tr key={p}>
                  <td>{label(p)}</td>
                  <td>{inRole ? '✓' : ''}</td>
                  <td><input type="radio" name={`a-${p}`} checked={c === 'role'} onChange={() => pick('role')} aria-label={`${label(p)}: as the role`} /></td>
                  <td><input type="radio" name={`a-${p}`} checked={c === 'allow'} disabled={inRole} onChange={() => pick('allow')} aria-label={`${label(p)}: add`} /></td>
                  <td><input type="radio" name={`a-${p}`} checked={c === 'deny'} disabled={!inRole} onChange={() => pick('deny')} aria-label={`${label(p)}: take away`} /></td>
                  <td>{effective.includes(p) ? <span className="ok-text">Yes</span> : <span className="muted">No</span>}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {hasExceptions && (
          <label>Why their access differs from the role
            <input value={form.accessNote} onChange={(e) => set('accessNote', e.target.value)} maxLength={250} placeholder="e.g. Covers refunds while Finance is short-staffed" />
          </label>
        )}
      </Panel>

      <div className="row">
        <button className="btn" disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save'}</button>
        <Link className="btn outline" to={id ? `/sheriff/users/${id}` : '/sheriff/users'}>Cancel</Link>
      </div>
    </form>
  );
}
