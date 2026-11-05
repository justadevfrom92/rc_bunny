import { Link, useParams } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { AccountNo, PageHead } from '../../ui';
import { icon, size } from './Ticket';

interface Data {
  ticket: { id: number; number: string; subject: string; name: string; customerId: number | null; account: string | null };
  file: { id: number; name: string; mime: string; size: number; fromCustomer: boolean; createdAt: string; isImage: boolean; isPdf: boolean; isText: boolean };
  by: string | null; message: { id: number; kind: string; author: string; via: string; body: string; createdAt: string } | null; text: string | null; exists: boolean;
}

/** One ticket attachment (ebunny's ranger/attachment): the file with a preview, who sent it and on which message. */
export default function Attachment() {
  const { id, fileId } = useParams();
  const { data, error } = useLoad<Data>(`/api/ranger/tickets/${id}/files/${fileId}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const { ticket: t, file: a } = data;
  const src = `/api/ranger/tickets/${t.id}/files/${a.id}/file`;
  return (
    <>
      <PageHead title={a.name} sub={<>{icon(a)} {size(a.size)} · {a.mime} · on <span className="mono">{t.number}</span></>}
        actions={<><a className="btn cyan" href={src}>Download</a> <Link className="btn ghost" to={`/ranger/tickets/${t.id}`}>Back to {t.number}</Link></>} />
      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0,2fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div className="panel"><div className="panel-head"><h2>Preview</h2></div><div className="panel-body">
          {data.exists && a.isImage ? <img className="att-preview" src={`${src}?inline=true`} alt={a.name} />
            : data.exists && a.isPdf ? <iframe className="att-frame" src={`${src}?inline=true`} title={a.name} />
            : data.text !== null ? <pre className="att-text">{data.text}</pre>
            : <p className="muted">No preview for this kind of file. Download it to open it.</p>}
        </div></div>
        <div className="panel"><div className="panel-head"><h2>File</h2></div><div className="panel-body"><dl className="kv">
          <dt>Name</dt><dd>{a.name}</dd>
          <dt>Size</dt><dd>{size(a.size)}</dd>
          <dt>Type</dt><dd>{a.mime}</dd>
          <dt>From</dt><dd>{a.fromCustomer ? `${t.name} (customer)` : data.by ?? 'Support'}</dd>
          <dt>Sent</dt><dd>{dateTime(a.createdAt)}{data.message && <div className="help">with {data.message.via === 'email' ? 'an email' : 'a My Account message'}</div>}</dd>
          <dt>Ticket</dt><dd><Link className="mono" to={`/ranger/tickets/${t.id}`}>{t.number}</Link> {t.subject}</dd>
          {t.customerId && <><dt>Account</dt><dd><AccountNo id={t.customerId} account={t.account} /></dd></>}
        </dl></div>
          {data.message && <div className="panel-body" style={{ borderTop: '1px solid var(--line)' }}><div className="help" style={{ marginBottom: 4 }}>The message it came with</div>
            <div className="rt-body" style={{ whiteSpace: 'pre-line' }}>{data.message.body.length > 600 ? data.message.body.slice(0, 597) + '...' : data.message.body}</div></div>}
        </div>
      </div>
    </>
  );
}
