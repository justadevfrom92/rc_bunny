import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, qs, useLoad } from '../../api';
import { useAuth } from '../../auth';
import { DateInput, PasswordInput } from '../../fields';
import { EmptyRow, PageHead, PagerBar, Panel, age, shortTime, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const Loading = () => <div className="empty">Loading…</div>;
const first = (name: string) => name.split(' ')[0];
const plural = (n: number, word: string) => `${n} ${n === 1 ? word : word === 'person' ? 'people' : word + 's'}`;

/** Can Open / Rights cells: what the person has; added for them in its own colour, taken away struck through. */
function Access({ u, list }: { u: any; list: Record<string, string> }) {
  return <>{Object.entries(list).map(([key, name]) =>
    u.has.includes(key) ? <span key={key} className={`acc${u.added.includes(key) ? ' added' : ''}`} title={u.added.includes(key) ? 'Added for this person' : 'From their role'}>{name}</span>
      : u.removed.includes(key) ? <span key={key} className="acc removed" title="Their role has it; taken away for this person">{name}</span> : null)}</>;
}

/** Sheriff → Staff Directory (ebunny's sheriff/users): employees by department, then 3rd-party users by company. */
export default function Staff() {
  const { data, error } = useLoad<any>('/api/staff');
  const [params] = useSearchParams();
  const [q, setQ] = useState(params.get('q') ?? '');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;

  const words = q.toLowerCase().split(/\s+/).filter(Boolean);
  const hay = (u: any) => [u.name, u.title, u.email, u.department, u.company, u.employeeId, u.role].join(' ').toLowerCase();
  const match = (u: any) => words.every((w) => hay(u).includes(w));
  const groups = data.groups.map((g: any) => ({ ...g, shown: g.people.filter(match) })).filter((g: any) => g.shown.length);
  const disabled = data.disabled.filter(match);
  const shown = groups.reduce((n: number, g: any) => n + g.shown.length, 0) + disabled.length;
  const rights = Object.fromEntries(Object.keys(data.rights).map((k) => [k, k[0].toUpperCase() + k.slice(1)]));

  return (
    <>
      <PageHead title="Staff Directory" sub="Everyone who signs in to the admin: employees by department, then 3rd-party company users by company. Open someone for their profile, permissions and Log in as."
        actions={<Link className="btn cyan" to="/sheriff/users/new">New User</Link>} />
      <form className="search-box" role="search" onSubmit={(e) => e.preventDefault()}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="M20 20l-3.5-3.5" /></svg>
        <input type="search" name="q" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search name, title, department, company, email or employee #" aria-label="Search staff" autoComplete="off" />
        <span className="search-count muted">{words.length ? `${shown} found` : ''}</span>
      </form>
      {shown === 0 && <div className="panel"><div className="panel-body muted">Nobody matches that search.</div></div>}

      <div className="stack">
        {groups.map((g: any) => (
          <div key={(g.external ? 'c:' : 'd:') + g.name} className="panel">
            <div className="panel-head"><h2>{g.external && <span className="pill info" style={{ marginRight: 6 }}>3rd party</span>}{g.name}</h2><span className="muted">{plural(g.shown.length, 'person')}</span></div>
            <div className="table-wrap"><table className="table" data-page="0">
              <thead><tr><th>{g.external ? 'User' : 'Employee'}</th><th>Role</th><th>Can Open</th><th>Rights</th><th>{g.external ? 'Contact / Access Ends' : 'Manager'}</th><th>Last Sign-in</th></tr></thead>
              <tbody>{g.shown.map((u: any) => (
                <tr key={u.id} className="click" onClick={() => navigate(`/sheriff/users/${u.id}`)}>
                  <td><Link to={`/sheriff/users/${u.id}`}><b>{u.name}</b></Link>{u.id === data.me && <span className="muted"> (you)</span>}
                    <div className="help">{u.title ?? '—'}{u.employeeId && <> · <span className="mono">{u.employeeId}</span></>}</div>
                    <div className="help">{u.email}{u.phoneExt && ` · ext ${u.phoneExt}`}</div></td>
                  <td>{u.role ?? '—'}</td>
                  <td className="wrap"><Access u={u} list={data.apps} /></td>
                  <td className="wrap"><Access u={u} list={rights} />{u.accessNote && <div className="help">{u.accessNote}</div>}</td>
                  <td>{g.external ? <>{u.manager ?? '—'}{u.accessEndsOn && <div className="help">until {date(u.accessEndsOn)}</div>}</> : u.manager ?? '—'}</td>
                  <td className="nowrap">{u.lastLoginAt ? dateTime(u.lastLoginAt) : 'Never'}</td>
                </tr>
              ))}</tbody>
            </table></div>
          </div>
        ))}
        {disabled.length > 0 && (
          <div className="panel"><div className="panel-head"><h2>Disabled</h2><span className="muted">can't sign in</span></div>
            <div className="table-wrap"><table className="table"><tbody>{disabled.map((u: any) => (
              <tr key={u.id} className="click" onClick={() => navigate(`/sheriff/users/${u.id}`)}>
                <td><Link to={`/sheriff/users/${u.id}`}><b>{u.name}</b></Link><div className="help">{u.title} · {u.email}</div></td><td>{u.department ?? '—'}</td><td>{u.role}</td>
                <td className="actions" onClick={(e) => e.stopPropagation()}><EnableButton id={u.id} /></td></tr>
            ))}</tbody></table></div>
          </div>
        )}
      </div>
    </>
  );
}

function EnableButton({ id }: { id: number }) {
  const [run] = useRun(() => location.reload());
  return <button className="btn sm ghost" onClick={() => run(api.post(`/api/staff/${id}/active`))}>Enable</button>;
}

/** One person (ebunny's sheriff/user-show): profile, contact and sign-in, team, permissions and recent activity. */
export function UserShow() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/staff/${id}`);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  const u = data.user;
  const why = (key: string, has: boolean) => u.added.includes(key) ? 'Added for this person' : u.removed.includes(key) ? 'Taken away for this person (their role has it)' : has ? 'From their role' : '—';
  const hired = u.hiredOn && new Date(u.hiredOn) < new Date() ? age(u.hiredOn) : null;

  return (
    <>
      <PageHead title={u.name}
        sub={<>{u.title ?? 'Employee'} · {u.company ? `${u.company} (3rd party)` : u.department ?? 'No department'}{!u.active && <> · <b>Sign-in disabled</b></>}</>}
        actions={<>
          <Link className="btn ghost" to={`/sheriff/timeline?user=${u.id}`}>Timeline</Link>
          <Link className="btn ghost" to={`/sheriff/users/${u.id}/history`}>History</Link>
          <Link className="btn ghost" to={`/sheriff/users/${u.id}/edit`}>Edit</Link>
        </>} />
      <div className="grid-3">
        <Panel title="Profile"><div className="panel-body"><dl className="kv">
          <dt>Title</dt><dd>{u.title ?? '—'}</dd>
          {u.company ? <>
            <dt>Company</dt><dd>{u.company} <span className="pill info">3rd party</span></dd>
            <dt>Access Ends</dt><dd>{u.accessEndsOn ? date(u.accessEndsOn) : 'No end date'}</dd>
          </> : <>
            <dt>Department</dt><dd>{u.department ?? '—'}</dd>
            <dt>Employee #</dt><dd className="mono">{u.employeeId ?? '—'}</dd>
          </>}
          <dt>{u.company ? 'Our Contact' : 'Manager'}</dt><dd>{u.managerId ? <Link to={`/sheriff/users/${u.managerId}`}>{u.manager}</Link> : '—'}</dd>
          <dt>Started</dt><dd>{u.hiredOn ? date(u.hiredOn) : '—'}{hired && <span className="muted"> ({hired})</span>}</dd>
          <dt>Location</dt><dd>{u.location ?? '—'}</dd>
        </dl></div></Panel>
        <Panel title="Contact & Sign-in"><div className="panel-body"><dl className="kv">
          <dt>Email</dt><dd><a href={`mailto:${u.email}`}>{u.email}</a></dd>
          <dt>Phone Ext.</dt><dd>{u.phoneExt ?? '—'}</dd>
          <dt>Status</dt><dd><span className={`pill ${u.active ? 'ok' : 'bad'}`}>{u.active ? 'Can sign in' : 'Disabled'}</span></dd>
          <dt>Last Sign-in</dt><dd>{u.lastLoginAt ? <>{dateTime(u.lastLoginAt)} <span className="muted">({age(u.lastLoginAt)} ago)</span></> : 'Never'}</dd>
          <dt>Recent Sign-ins</dt><dd>{data.logins.length ? data.logins.map((l: any, i: number) => <div key={i} className="help">{shortTime(l.createdAt)} · {l.summary}</div>) : '—'}</dd>
        </dl></div></Panel>
        <Panel title="Team" right={<span className="muted">{data.reports.length} {u.company ? '' : 'direct'} {data.reports.length === 1 ? 'report' : 'reports'}</span>}>
          {data.reports.length ? (
            <div className="table-wrap"><table><tbody>{data.reports.map((r: any) => (
              <tr key={r.id} className="click" onClick={() => navigate(`/sheriff/users/${r.id}`)}><td><Link to={`/sheriff/users/${r.id}`}><b>{r.name}</b></Link><div className="help">{r.company ? `${r.company} · 3rd party` : r.title}</div></td></tr>
            ))}</tbody></table></div>
          ) : <div className="panel-body muted">No one reports to {first(u.name)}.{u.managerId && <> {first(u.name)} reports to <Link to={`/sheriff/users/${u.managerId}`}>{u.manager}</Link>.</>}</div>}
        </Panel>
      </div>

      <Panel title="Admin Permissions" right={<span className="muted">role: <b>{u.role ?? 'none'}</b> · <Link to={`/sheriff/users/${u.id}/edit`}>change</Link></span>}>
        <div className="table-wrap"><table>
          <thead><tr><th>App or Right</th><th>Access</th><th>Why</th></tr></thead>
          <tbody>
            {Object.entries(data.apps).map(([key, name]) => { const has = u.has.includes(key); return (
              <tr key={key}><td><b>{name as string}</b></td><td><span className={`pill ${has ? 'ok' : ''}`}>{has ? 'Can open' : 'No access'}</span></td><td className="help">{why(key, has)}</td></tr>); })}
            {Object.entries(data.rights).map(([key, label]) => { const has = u.has.includes(key); return (
              <tr key={key}><td><b>{label as string}</b></td><td><span className={`pill ${has ? 'ok' : ''}`}>{has ? 'Allowed' : 'Not allowed'}</span></td><td className="help">{why(key, has)}</td></tr>); })}
          </tbody>
        </table></div>
        {u.accessNote && <div className="panel-body help">Reason for the exceptions: {u.accessNote}</div>}
      </Panel>

      <Panel title="Recent Activity" right={<Link className="btn sm ghost" to={`/sheriff/users/${u.id}/history`}>All History</Link>}>
        <div className="table-wrap"><table><tbody>
          {data.activity.map((h: any) => (
            <tr key={h.id} className="click" onClick={() => navigate(`/sheriff/records/history_entry/${h.id}`)}>
              <td className="nowrap"><Link to={`/sheriff/records/history_entry/${h.id}`}><b>#{h.id}</b></Link></td><td className="nowrap" style={{ width: 180 }}>{dateTime(h.createdAt)}</td><td className="wrap">{h.summary}</td></tr>
          ))}
          {data.activity.length === 0 && <tr><td className="empty">No activity recorded.</td></tr>}
        </tbody></table></div>
      </Panel>
    </>
  );
}

/** New User / Edit (ebunny's sheriff/user-form): the person, sign-in, and access (role plus Allow / Deny for this person). */
export function UserForm() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/staff/form${id ? `?id=${id}` : ''}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  return <UserFormBody data={data} key={id ?? 'new'} />;
}

function UserFormBody({ data }: { data: any }) {
  const u = data.user;
  const navigate = useNavigate();
  const { refresh } = useAuth();
  const [run, bar] = useRun();
  const [roleId, setRoleId] = useState<number>(u?.roleId ?? data.roles[0]?.id);
  const initial = (key: string) => (u?.extraPermissions.split(',').includes(key) ? 'allow' : u?.deniedPermissions.split(',').includes(key) ? 'deny' : 'role');
  const [access, setAccess] = useState<Record<string, string>>(() => Object.fromEntries([...Object.keys(data.apps), ...Object.keys(data.rights)].map((k) => [k, initial(k)])));
  const rolePerms: string[] = data.roles.find((r: any) => r.id === roleId)?.permissions ?? [];
  const exceptions = Object.values(access).some((v) => v !== 'role');
  const title = u ? u.name : 'New User';

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), get = (k: string) => String(f.get(k) ?? '');
    const body = {
      name: get('name'), email: get('email'), password: get('password') || null, roleId, company: get('company'), accessEndsOn: get('access_ends_on') || null,
      employeeId: get('employee_id'), title: get('title'), department: get('department') || null, managerId: get('manager_id') ? Number(get('manager_id')) : null,
      location: get('location'), phoneExt: get('phone_ext'), hiredOn: get('hired_on') || null, access, accessNote: exceptions ? get('access_note') : null,
    };
    run((u ? api.put<{ id: number }>(`/api/staff/${u.id}`, body) : api.post<{ id: number }>('/api/staff', body)).then((r) => { refresh(); navigate(`/sheriff/users/${r.id}`); return r; }));
  };
  const toggle = () => {
    if (confirm(u.active ? `Disable ${u.name}?\nThey won't be able to sign in.` : `Enable ${u.name}?\nThey can sign in again.`))
      run(api.post(`/api/staff/${u.id}/active`).then((r) => { navigate(`/sheriff/users/${u.id}`); return r; }));
  };
  const row = (key: string, name: string) => (
    <tr key={key}><td><b>{name}</b></td>
      <td className="role-has">{rolePerms.includes(key) ? <span className="pill ok">Yes</span> : <span className="pill">No</span>}</td>
      <td className="access-choices">{(['role', 'allow', 'deny'] as const).map((v) => (
        <label key={v} className="check" style={{ marginRight: 12 }}><input type="radio" name={`access[${key}]`} value={v} checked={access[key] === v} onChange={() => setAccess({ ...access, [key]: v })} /> {{ role: 'Role default', allow: 'Allow', deny: 'Deny' }[v]}</label>
      ))}</td></tr>
  );

  return (
    <>
      <PageHead title={title}
        sub={u ? <>{u.title ?? 'Employee'} · {u.company ? `${u.company} (3rd party)` : u.department ?? 'No department'}</> : 'An employee, or a 3rd-party company user, who signs in to the admin.'}
        actions={u ? <>
          <Link className="btn ghost" to={`/sheriff/users/${u.id}`}>Profile</Link>
          <Link className="btn ghost" to={`/sheriff/timeline?user=${u.id}`}>Timeline</Link>
          <Link className="btn ghost" to={`/sheriff/users/${u.id}/history`}>History</Link>
          {u.id !== data.me && <button type="button" className={`btn ${u.active ? 'red' : 'cyan'}`} onClick={toggle}>{u.active ? 'Disable' : 'Enable'}</button>}
        </> : undefined} />
      {bar}
      <form className="stack" onSubmit={submit}>
        <div className="grid-2 user-form-top">
          <Panel title="Person"><div className="panel-body form-grid">
            <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={150} defaultValue={u?.name} />
            <label htmlFor="company">Company</label><div><input id="company" name="company" maxLength={120} defaultValue={u?.company ?? ''} placeholder="Empty for Energy Texas employees" /><div className="help">For 3rd-party users: the outside company they work for (call center, agency, vendor).</div></div>
            <label htmlFor="access_ends_on">Access ends</label><div><DateInput id="access_ends_on" name="access_ends_on" defaultValue={u?.accessEndsOn ?? ''} style={{ maxWidth: '20ch' }} /><div className="help">Usually the end of the company's contract. Empty for no end date.</div></div>
            <label htmlFor="employee_id">Employee #</label><input id="employee_id" name="employee_id" maxLength={20} className="mono" defaultValue={u?.employeeId ?? ''} style={{ maxWidth: '16ch' }} />
            <label htmlFor="title">Job title</label><input id="title" name="title" maxLength={100} defaultValue={u?.title ?? ''} />
            <label htmlFor="sel-department">Department</label><select name="department" id="sel-department" style={{ maxWidth: 320 }} defaultValue={u?.department ?? ''}><option value="">None</option>{data.departments.map((d: string) => <option key={d} value={d}>{d}</option>)}</select>
            <label>Manager</label><div className="table-wrap pick-list" style={{ maxHeight: 200 }}><table className="table"><tbody>
              <tr><td style={{ width: 32 }}><input type="radio" id="mgr0" name="manager_id" value="" defaultChecked={!u?.managerId} /></td><td><label htmlFor="mgr0">None</label></td></tr>
              {data.managers.map((m: any) => <tr key={m.id}><td><input type="radio" id={`mgr${m.id}`} name="manager_id" value={m.id} defaultChecked={u?.managerId === m.id} /></td><td><label htmlFor={`mgr${m.id}`}><b>{m.name}</b></label>{m.title && <span className="help"> {m.title}</span>}</td></tr>)}
            </tbody></table></div>
            <label htmlFor="location">Location</label><input id="location" name="location" maxLength={100} defaultValue={u?.location ?? ''} />
            <label htmlFor="phone_ext">Phone ext.</label><input id="phone_ext" name="phone_ext" maxLength={10} defaultValue={u?.phoneExt ?? ''} style={{ maxWidth: '10ch' }} />
            <label htmlFor="hired_on">Hired</label><DateInput id="hired_on" name="hired_on" defaultValue={u?.hiredOn ?? ''} style={{ maxWidth: '20ch' }} />
          </div></Panel>
          <Panel title="Sign-in"><div className="panel-body form-grid">
            <label htmlFor="email">Email</label><input id="email" name="email" type="email" required maxLength={200} defaultValue={u?.email} />
            <label htmlFor="password">{u ? 'New password' : 'Temporary password'}</label><div><PasswordInput id="password" name="password" minLength={12} autoComplete="new-password" required={!u} />
              <div className="help">At least 12 characters.{u ? ' Leave empty to keep their password.' : ''}</div></div>
          </div></Panel>
        </div>
        <div className="panel"><div className="panel-head"><h2>Access</h2></div>
          <div className="panel-body form-grid">
            <label>Role</label><div><div className="chips small" data-keep-pills>{data.roles.map((r: any) => (
              <label key={r.id}><input type="radio" name="role_id" value={r.id} checked={roleId === r.id} onChange={() => setRoleId(r.id)} /><span>{r.name}</span></label>))}</div>
              <div className="help">The role sets what they get by default. Change single apps or rights for this person below.</div></div>
          </div>
          <div className="table-wrap"><table className="table access-table" data-page="0">
            <thead><tr><th>App or right</th><th>From their role</th><th>For this person</th></tr></thead>
            <tbody>{Object.entries(data.apps).map(([k, n]) => row(k, n as string))}{Object.entries(data.rights).map(([k, n]) => row(k, n as string))}</tbody>
          </table></div>
          {exceptions && <div className="panel-body form-grid" id="access-reason">
            <label htmlFor="access_note">Reason for the exceptions</label><div><input id="access_note" name="access_note" maxLength={250} defaultValue={u?.accessNote ?? ''} placeholder="e.g. Deletes go through the CS Lead" />
              <div className="help">Why this person gets more or less than their role (the Allow and Deny choices above). It shows under their rights on the Users list.</div></div>
          </div>}
        </div>
        <div className="actions"><button className="btn cyan">{u ? 'Save User' : 'Add User'}</button><Link className="btn ghost" to="/sheriff/users">Cancel</Link></div>
      </form>
    </>
  );
}

/** Everything one person changed (ebunny's history/user). */
export function UserHistory() {
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/staff/${id}/history?${qs({ page })}`);
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <Loading />;
  const u = data.user;
  const tone: Record<string, string> = { created: 'ok', updated: 'info', deleted: 'bad', sent: 'info', received: 'info' };
  return (
    <>
      <PageHead title={`${u.name}'s History`} sub={`Everything ${first(u.name)} added, changed or deleted, newest first.`}
        actions={<Link className="btn ghost" to={`/sheriff/users/${u.id}`}>Profile</Link>} />
      <Panel title={`${data.total.toLocaleString()} ${data.total === 1 ? 'item' : 'items'}`}>
        <div className="table-wrap"><table className="hist">
          <thead><tr><th>ID</th><th>Date</th><th>Model</th><th>Action</th><th>Summary</th><th>Account</th><th>By</th></tr></thead>
          <tbody>
            {data.items.map((i: any) => (
              <tr key={i.id} className="click" onClick={() => navigate(`/sheriff/records/history_entry/${i.id}`)}>
                <td className="mono"><Link to={`/sheriff/records/history_entry/${i.id}`}>{i.id}</Link></td>
                <td style={{ whiteSpace: 'nowrap' }}>{i.createdAt.replace('T', ' ').slice(0, 19)}</td><td className="mono">{i.model}</td>
                <td><span className={`pill ${tone[i.action] ?? ''}`}>{i.action}</span></td><td className="wrap">{i.summary}</td><td className="mono">{i.account ?? '—'}</td><td>{u.name}</td>
              </tr>
            ))}
            {data.items.length === 0 && <EmptyRow cols={7}>No history matches.</EmptyRow>}
          </tbody>
        </table></div>
        <PagerBar page={page} total={data.total} size={25} onPage={(p) => setParams(qs({ page: p }))} />
      </Panel>
    </>
  );
}
