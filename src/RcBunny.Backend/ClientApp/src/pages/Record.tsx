import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, useLoad } from '../api';
import { PageHead, ResolutionForm, ResolutionPill, useRun } from '../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const show = (v: unknown) => (v == null || v === '' ? '' : typeof v === 'boolean' ? (v ? '1' : '0') : String(v));
const stamp = (s: string) => new Date(s).toLocaleString('sv-SE').slice(0, 19);

/**
 * Any row's own page (ebunny's corral/items/show): its fields, the actions (Edit, Delete and the row's own), and the
 * process logs. Edit shows ebunny's items/edit form in place.
 */
export default function Record() {
  const { type, id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/records/${type}/${id}`);
  const [editing, setEditing] = useState(false);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.customer;
  const created = data.fields.find((f: any) => f.name === 'CreatedAt')?.value as string | undefined;

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const values = Object.fromEntries(data.fields.filter((x: any) => x.editable).map((x: any) => [x.name, x.kind === 'yes/no' ? String(f.get(x.name) === 'on') : f.get(x.name)]));
    run(api.put(`/api/records/${type}/${id}`, values).then(() => setEditing(false)), 'Saved.');
  };
  const remove = () => confirm(`PERMANENTLY DELETE?\n${data.label} ${data.id} is removed for good. This can't be undone.`) &&
    run(api.del(`/api/records/${type}/${id}`).then(() => navigate(c ? `/corral/customers/${c.id}` : -1 as any)));
  const back = c && <Link className="btn ghost" to={`/corral/customers/${c.id}`}>Back to account</Link>;

  if (editing) return (
    <>
      <PageHead title={`Edit ${data.label}`} sub={<>{data.label} · id <span className="mono">{data.id}</span></>} actions={<button className="btn ghost" onClick={() => setEditing(false)}>Cancel</button>} />
      {bar}
      <form className="panel" onSubmit={save}>
        <div className="panel-body form-grid">
          {data.fields.map((f: any) => (
            <Field key={f.name} f={f} />
          ))}
        </div>
        <div className="panel-body"><button className="btn cyan">Save</button></div>
      </form>
    </>
  );

  return (
    <>
      <PageHead title={data.label} sub={<>id <span className="mono">{data.id}</span>{c && <> · account <span className="mono">{c.account}</span> · {c.name}</>}</>} actions={back || undefined} />
      {bar}
      <div className="it-grid">
        <div className="panel">
          <div className="panel-head it-dark"><h2>{data.label}{created && <span className="it-created"> --- created: {new Date(created).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' })} @ {new Date(created).toLocaleTimeString('en-US')}</span>}</h2></div>
          <div className="table-wrap"><table className="it-fields" data-page="0">
            <tbody>{data.fields.map((f: any) => <tr key={f.name}><td>{f.name}</td><td>{show(f.value)}</td></tr>)}</tbody>
          </table></div>
        </div>
        <div className="it-side">
          {type === 'contact_log' && (() => {
            const r = data.fields.find((f: any) => f.name === 'Resolution')?.value, by = data.fields.find((f: any) => f.name === 'ResolvedBy')?.value;
            return <div className="panel"><div className="panel-head"><h2>Resolution</h2><ResolutionPill value={r} /></div>
              <div className="panel-body"><ResolutionForm value={r} by={by} onSave={(v) => run(api.post(`/api/corral/resolution/contact/${id}`, { resolution: v }), 'Resolution saved.')} /></div></div>;
          })()}
          <div className="panel">
            <div className="panel-head"><h2>Actions</h2></div>
            <div className="panel-body it-actions">
              <button className="btn cyan" onClick={() => setEditing(true)}>Edit</button>
              {data.actions.map((a: any) => <span key={a.key} className="inline"><button className={`btn ${a.key === 'resend' ? 'warn' : 'ghost'}`} onClick={() => confirm(`${a.label}?\nAre you sure?`) && run(api.post(`/api/records/${type}/${id}/${a.key}`))}>{a.label}</button></span>)}
              {data.canDelete && <span className="inline it-delete"><button className="btn red" onClick={remove}>Delete</button></span>}
            </div>
          </div>
          <div className="panel">
            <div className="panel-head"><h2>Process Logs</h2><span className="muted">{data.history.length}</span></div>
            <div className="panel-body it-logs">
              {data.history.map((l: any) => (
                <div key={l.id}><b>{stamp(l.createdAt)} - {l.action}</b><div className="it-log">{c && <>Ticket <Link className="mono" to={`/corral/customers/${c.id}/ticket`}>{c.account}</Link><br /></>}<big>{l.summary}</big> <span className="muted">· {l.who || 'System'}</span></div></div>
              ))}
              {data.history.length === 0 && <p className="muted" style={{ margin: 0 }}>Nothing logged for this item yet.</p>}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

/** One field on the edit form: locked when it can't change, a textarea for long text. */
function Field({ f }: { f: any }) {
  const v = show(f.value);
  return (
    <>
      <label htmlFor={`f-${f.name}`} className="mono">{f.name}</label>
      <div>
        {!f.editable ? <><input id={`f-${f.name}`} value={v} disabled />{f.name !== 'Id' && <div className="help">Comes from the account; change it on its own screen.</div>}</>
          : f.kind === 'yes/no' ? <label className="check"><input id={`f-${f.name}`} type="checkbox" name={f.name} defaultChecked={!!f.value} /> Yes</label>
          : v.length > 70 ? <textarea id={`f-${f.name}`} name={f.name} rows={3} style={{ minHeight: 70, fontFamily: 'inherit' }} defaultValue={v} />
          : <input id={`f-${f.name}`} name={f.name} defaultValue={v} />}
      </div>
    </>
  );
}
