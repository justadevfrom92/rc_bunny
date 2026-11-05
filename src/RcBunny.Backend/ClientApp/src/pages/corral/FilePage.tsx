import { Link, useParams, useSearchParams } from 'react-router';
import { api, date, dateTime, useLoad } from '../../api';
import { PageHead, Panel, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const kinds: Record<string, string> = { efl: 'Electricity Facts Label', yrac: 'Your Rights as a Customer', tos: 'Terms of Service', welcome: 'Welcome Packet', invoice: 'Invoice' };

/** One file on the account (ebunny's corral/customers/file): the PDF in English or Spanish, print, download, regenerate, email. */
export default function FilePage() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const lang = params.get('lang') ?? '';
  const { data, error, reload } = useLoad<any>(`/api/customers/files/${id}${lang ? `?lang=${lang}` : ''}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const { file, customer: c, doc } = data;
  const pdf = `/api/customers/files/${file.id}/download?lang=${data.lang}`;
  const languages = Object.entries(data.languages) as [string, string][];

  return (
    <>
      <PageHead title={file.name} sub={<>{c.name} · Account <Link className="mono" to={`/corral/customers/${c.id}`}>{c.account}</Link> · added {date(file.createdAt)}</>}
        actions={<>
          <Link className="btn ghost btn-back" to={`/corral/customers/${c.id}#files`}>Back to Account</Link>
          {doc && <><a className="btn cyan" href={pdf} target="_blank" rel="noopener">View PDF</a><a className="btn ghost" href={`${pdf}&download=true`}>Download PDF</a>
            <button className="btn ghost" onClick={() => window.print()}>Print</button></>}
          {file.upload && <a className="btn ghost" href={`/api/customers/files/${file.id}/download`}>Download</a>}
        </>} />
      {bar}
      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div className="stack" style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <Panel title={doc ? 'PDF' : 'File'} right={doc && <div className="chips small">{languages.map(([k, l]) => <Link key={k} className={`chip-link${data.lang === k ? ' on' : ''}`} to={`?lang=${k}`}>{l}</Link>)}</div>}>
            {doc ? (
              <div className="pdf-viewer"><div className="pdf-page">
                <p className="muted">Energy Texas · {c.name} · Account {c.account}</p>
                <h2>{doc.title}</h2>
                {doc.paragraphs.map((p: string, i: number) => <p key={i}>{p}</p>)}
                {doc.rows.length > 0 && <table><tbody>{doc.rows.map((r: any) => <tr key={r.label}><th>{r.label}</th><td>{r.value}</td></tr>)}</tbody></table>}
              </div></div>
            ) : <div className="panel-body"><p>Uploaded file <b className="mono">{file.name}</b>. Download it to open it; emailing sends it as an attachment.</p></div>}
          </Panel>
          {doc && (
            <Panel title="File Details" right={<button className="btn sm ghost" onClick={() => confirm(`Regenerate ${file.name}?\nThe PDF is built again from the account as it is now: name, address, plan and latest bill.`) && run(api.post(`/api/customers/files/${file.id}/regenerate`), 'PDF regenerated')}>Regenerate</button>}>
              <div className="panel-body"><dl className="kv">
                <dt>File Name</dt><dd className="mono">{file.name}</dd>
                <dt>Type</dt><dd>{kinds[file.kind] ?? file.kind[0].toUpperCase() + file.kind.slice(1)} · PDF</dd>
                <dt>Created</dt><dd>{dateTime(file.createdAt)}</dd>
                <dt>Last Generated</dt><dd>{dateTime(file.createdAt)}{file.regenerated > 0 && <span className="help"> · regenerated {file.regenerated} {file.regenerated === 1 ? 'time' : 'times'}</span>}</dd>
                <dt>Language</dt><dd>{data.languages[data.lang]}</dd>
              </dl></div>
            </Panel>
          )}
        </div>
        <div className="stack" style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <form className="panel" onSubmit={(e) => { e.preventDefault(); const f = new FormData(e.currentTarget);
            run(api.post(`/api/customers/files/${file.id}/send`, { lang: f.get('lang'), email: f.get('email'), note: f.get('note') })); }}>
            <div className="panel-head"><h2>Email to Customer</h2></div>
            <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div className="field"><label htmlFor="fs-email">To</label><input id="fs-email" name="email" type="email" required defaultValue={c.email ?? ''} /></div>
              <div className="field"><label>Language</label>
                <div className="chips small">{languages.map(([k, l]) => <label key={k}><input type="radio" name="lang" value={k} defaultChecked={data.lang === k} /><span>{l}</span></label>)}</div>
                <div className="help">Their preferred language: {c.language ?? 'English'}.</div></div>
              <div className="field"><label htmlFor="fs-note">Note (optional)</label><textarea id="fs-note" name="note" maxLength={1000} style={{ minHeight: 70 }} placeholder="Added above the document" /></div>
              <div><button className="btn cyan">Send Email</button></div>
            </div>
          </form>
          <Panel title="Sent">
            <div className="table-wrap"><table><tbody>
              {data.sent.map((s: any) => <tr key={s.id}><td className="nowrap">{dateTime(s.createdAt)}</td><td>{s.template.includes('(') ? s.template.slice(s.template.indexOf('(') + 1, s.template.lastIndexOf(')')) : s.template}</td><td>{s.who ?? '—'}</td></tr>)}
              {data.sent.length === 0 && <tr><td className="empty">Not emailed yet.</td></tr>}
            </tbody></table></div>
          </Panel>
        </div>
      </div>
    </>
  );
}
