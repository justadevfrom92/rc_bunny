import { useParams, useSearchParams } from 'react-router';
import { api, dateTime, useLoad } from '../../api';
import { useRun } from '../../ui';
import { Link } from 'react-router';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** One file on an account: a generated document (view, PDF, print, email, regenerate) in English or Spanish, or an upload to download. */
export default function FilePage() {
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const lang = params.get('lang') ?? '';
  const { data, error, reload } = useLoad<any>(`/api/customers/files/${id}${lang ? `?lang=${lang}` : ''}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { file, customer: c, doc } = data;
  const pdf = `/api/customers/files/${file.id}/download?lang=${data.lang}`;

  return (
    <>
      <p className="no-print"><Link to={`/corral/customers/${c.id}#files`}>← {c.name} #{c.account}</Link></p>
      <div className="row spread no-print">
        <h1>{file.name}</h1>
        {!file.upload && (
          <div className="row">
            {Object.entries(data.languages).map(([k, v]) => <button key={k} className={k === data.lang ? 'primary' : ''} onClick={() => setParams({ lang: k })}>{v as string}</button>)}
          </div>
        )}
      </div>
      {bar}
      {file.upload ? (
        <section className="card"><p>An uploaded file, added {dateTime(file.createdAt)}.</p><a className="button" href={`/api/customers/files/${file.id}/download`}>Download</a></section>
      ) : (
        <>
          <div className="row no-print">
            <a className="button" href={pdf} target="_blank" rel="noreferrer">View PDF</a>
            <a className="button" href={`${pdf}&download=true`}>Download PDF</a>
            <button onClick={() => window.print()}>Print</button>
            <button onClick={() => run(api.post(`/api/customers/files/${file.id}/regenerate`))}>Regenerate{file.regenerated > 0 && ` (${file.regenerated})`}</button>
          </div>
          <div className="doc">
            <p className="muted">Energy Texas · {c.name} · Account {c.account}</p>
            <h2>{doc.title}</h2>
            {doc.paragraphs.map((p: string, i: number) => <p key={i}>{p}</p>)}
            {doc.rows.length > 0 && <table><tbody>{doc.rows.map((r: any) => <tr key={r.label}><th>{r.label}</th><td>{r.value}</td></tr>)}</tbody></table>}
          </div>
        </>
      )}
      <form className="card no-print" onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget);
        run(api.post(`/api/customers/files/${file.id}/send`, { lang: f.get('lang'), email: f.get('email'), note: f.get('note') })); }}>
        <h2>Email it to the customer</h2>
        <div className="row">
          <input name="email" type="email" defaultValue={c.email ?? ''} required />
          <select name="lang" defaultValue={data.lang}>{Object.entries(data.languages).map(([k, v]) => <option key={k} value={k}>{v as string}</option>)}</select>
        </div>
        <textarea name="note" rows={2} placeholder="A note to go with it (optional)" />
        <button className="primary">Send</button>
      </form>
      {data.sent.length > 0 && (
        <section className="card no-print"><h2>Sent</h2>
          <table><tbody>{data.sent.map((s: any) => <tr key={s.id}><td>{dateTime(s.createdAt)}</td><td>{s.template}</td><td>{s.status}</td><td>{s.who}</td></tr>)}</tbody></table>
        </section>
      )}
    </>
  );
}
