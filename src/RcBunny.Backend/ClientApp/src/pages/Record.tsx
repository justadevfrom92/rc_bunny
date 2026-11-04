import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api, dateTime, useLoad } from '../api';
import { useRun } from '../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const show = (v: unknown) => (v == null || v === '' ? '—' : typeof v === 'boolean' ? (v ? 'Yes' : 'No') : String(v));

/** Any row's own page (a note, a payment, a plan term, a job run…): its fields, Edit, Delete and its actions. */
export default function Record() {
  const { type, id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/records/${type}/${id}`);
  const [editing, setEditing] = useState(false);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const values = Object.fromEntries(data.fields.filter((x: any) => x.editable).map((x: any) => [x.name, x.kind === 'yes/no' ? String(f.get(x.name) === 'on') : f.get(x.name)]));
    run(api.put(`/api/records/${type}/${id}`, values).then(() => setEditing(false)), 'Saved.');
  };
  const remove = () => confirm(`Delete this ${data.label.toLowerCase()} for good?`) &&
    run(api.del(`/api/records/${type}/${id}`).then(() => navigate(data.customer ? `/corral/customers/${data.customer.id}` : -1 as any)));

  return (
    <>
      <p><button className="link-btn" onClick={() => navigate(-1)}>← Back</button>{data.customer && <> · <Link to={`/corral/customers/${data.customer.id}`}>{data.customer.name} #{data.customer.account}</Link></>}</p>
      <div className="row spread">
        <h1>{data.label} #{data.id}</h1>
        <div className="row">
          {data.actions.map((a: any) => <button key={a.key} onClick={() => run(api.post(`/api/records/${type}/${id}/${a.key}`))}>{a.label}</button>)}
          {!editing && <button onClick={() => setEditing(true)}>Edit</button>}
          {data.canDelete && <button onClick={remove}>Delete (permanent!)</button>}
        </div>
      </div>
      {bar}
      <form className="card" onSubmit={save}>
        <table><tbody>
          {data.fields.map((f: any) => (
            <tr key={f.name}><th style={{ width: '30%' }}>{f.name}</th><td>
              {editing && f.editable
                ? f.kind === 'yes/no' ? <input type="checkbox" name={f.name} defaultChecked={!!f.value} />
                : <input name={f.name} defaultValue={f.value ?? ''} type={f.kind === 'date' ? 'date' : 'text'} style={{ width: '100%' }} />
                : <span className="wrap-text">{show(f.value)}</span>}
            </td></tr>
          ))}
        </tbody></table>
        {editing && <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(false)}>Cancel</button></div>}
      </form>
      {data.history.length > 0 && (
        <section className="card"><h2>History</h2>
          <table><tbody>{data.history.map((h: any) => <tr key={h.id}><td>{dateTime(h.createdAt)}</td><td>{h.who}</td><td>{h.action}</td><td>{h.summary}</td></tr>)}</tbody></table>
        </section>
      )}
    </>
  );
}
