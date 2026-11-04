import { Link, useParams } from 'react-router';
import { dateTime, useLoad } from '../../api';
import { size } from './Ticket';

interface Data {
  ticket: { id: number; number: string; subject: string; name: string };
  file: { id: number; name: string; mime: string; size: number; fromCustomer: boolean; createdAt: string; isImage: boolean; isPdf: boolean; isText: boolean };
  by: string | null; message: { id: number; kind: string; author: string; body: string; createdAt: string } | null; text: string | null; exists: boolean;
}

/** One attachment: who sent it, when, on which message, with a preview for images, PDFs and text. */
export default function Attachment() {
  const { id, fileId } = useParams();
  const { data, error } = useLoad<Data>(`/api/ranger/tickets/${id}/files/${fileId}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { ticket: t, file: f } = data;
  const src = `/api/ranger/tickets/${t.id}/files/${f.id}/file`;

  return (
    <>
      <p><Link to={`/ranger/tickets/${t.id}`}>← {t.number} {t.subject}</Link></p>
      <h1>{f.name}</h1>
      <p className="muted">{size(f.size)} · {f.mime} · from {data.by ?? 'Support'}{f.fromCustomer && ' (the customer)'} · {dateTime(f.createdAt)}</p>
      <p><a className="button" href={src}>Download</a></p>
      {!data.exists ? <p className="error">The file is missing from storage.</p>
        : f.isImage ? <img className="preview" src={`${src}?inline=true`} alt={f.name} />
        : f.isPdf ? <iframe className="preview" title={f.name} src={`${src}?inline=true`} style={{ width: '100%', height: 600 }} />
        : data.text !== null ? <pre className="card wrap-text">{data.text}</pre>
        : <p className="muted">No preview for this kind of file. Download it to open it.</p>}
      {data.message && (
        <section className="card">
          <h2>Sent with</h2>
          <small className="muted">{data.message.kind === 'customer' ? t.name : data.message.author} · {dateTime(data.message.createdAt)}</small>
          <p className="wrap-text">{data.message.body}</p>
        </section>
      )}
    </>
  );
}
