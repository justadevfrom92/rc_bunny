import { Link, useSearchParams } from 'react-router';
import { dateTime, useLoad } from '../../api';

export interface MessageRow {
  id: number; name: string; email: string; phone: string | null; topic: string; message: string; createdAt: string;
  handledAt: string | null; handledBy: string | null; repliedAt: string | null; customerId: number | null;
}

/** What people sent through the website's Contact Us form: open ones oldest first, and the last 25 handled. */
export default function Messages() {
  const [params, setParams] = useSearchParams();
  const topic = params.get('topic');
  const { data, error } = useLoad<{ counts: { topic: string; n: number }[]; open: MessageRow[]; handled: MessageRow[] }>(`/api/messages${topic ? `?topic=${encodeURIComponent(topic)}` : ''}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const total = data.counts.reduce((s, c) => s + c.n, 0);

  const table = (rows: MessageRow[], done: boolean) => rows.length === 0 ? <p className="muted">Nothing here.</p> : (
    <table>
      <thead><tr><th>From</th><th>Topic</th><th>Message</th><th>Sent</th>{done && <th>Handled</th>}</tr></thead>
      <tbody>{rows.map((m) => (
        <tr key={m.id}>
          <td><Link to={`/ranger/messages/${m.id}`}>{m.name}</Link><div className="muted">{m.email}{m.phone && ` · ${m.phone}`}</div></td>
          <td>{m.topic}</td><td className="wrap-text">{m.message.length > 140 ? m.message.slice(0, 140) + '…' : m.message}{m.repliedAt && <div className="muted">Replied {dateTime(m.repliedAt)}</div>}</td>
          <td className="nowrap">{dateTime(m.createdAt)}</td>
          {done && <td className="nowrap">{m.handledAt && dateTime(m.handledAt)}<div className="muted">{m.handledBy}</div></td>}
        </tr>
      ))}</tbody>
    </table>
  );

  return (
    <>
      <h1>Web Messages</h1>
      <div className="row">
        <button className={topic ? '' : 'primary'} onClick={() => setParams({})}>All open <span className="muted">{total}</span></button>
        {data.counts.map((c) => <button key={c.topic} className={topic === c.topic ? 'primary' : ''} onClick={() => setParams({ topic: c.topic })}>{c.topic} <span className="muted">{c.n}</span></button>)}
      </div>
      <section className="card"><h2>Open</h2>{table(data.open, false)}</section>
      <section className="card"><h2>Recently Handled</h2>{table(data.handled, true)}</section>
    </>
  );
}
