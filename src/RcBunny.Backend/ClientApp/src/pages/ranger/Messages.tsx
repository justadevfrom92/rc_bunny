import { Link, useNavigate, useSearchParams } from 'react-router';
import { useLoad } from '../../api';
import { EmptyRow, PageHead, ResolutionPill, age, shortTime } from '../../ui';

export interface MessageRow {
  id: number; name: string; email: string; phone: string | null; topic: string; message: string; createdAt: string;
  handledAt: string | null; handledBy: string | null; repliedAt: string | null; resolution: string | null;
}
interface Data { topic: string | null; topics: Record<string, string>; counts: Record<string, number>; open: MessageRow[]; handled: MessageRow[] }

/** Ranger → Web Messages (ebunny's corral/messages): what people sent from the website's Contact Us form. */
export default function Messages() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<Data>(`/api/messages${params.get('topic') ? `?topic=${encodeURIComponent(params.get('topic')!)}` : ''}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const total = Object.values(data.counts).reduce((s, n) => s + n, 0);
  const cut = (s: string) => (s.length > 110 ? s.slice(0, 107) + '...' : s);
  return (
    <>
      <PageHead title="Web Messages" sub="Sent from the website's Contact Us form. Open a message to see the account it matches and reply." />
      <div className="actions" style={{ marginBottom: 14 }}><span className="muted">Topic:</span>
        <Link className={`btn sm${data.topic ? ' ghost' : ''}`} to="/ranger/messages">All <span className="count-pill">{total}</span></Link>
        {Object.entries(data.topics).map(([k, label]) => (
          <Link key={k} className={`btn sm${data.topic === k ? '' : ' ghost'}`} to={`/ranger/messages?topic=${k}`}>{label}{data.counts[k] ? <> <span className="count-pill">{data.counts[k]}</span></> : null}</Link>
        ))}
      </div>
      {([['Open', data.open], ['Recently Handled', data.handled]] as [string, MessageRow[]][]).map(([heading, rows]) => (
        <div key={heading} className="panel" style={{ marginBottom: 20 }}>
          <div className="panel-head"><h2>{heading} <span className="count-pill">{rows.length}</span></h2>{heading === 'Open' && <span className="muted">oldest first</span>}</div>
          <div className="table-wrap"><table className="table">
            <thead><tr><th>ID</th><th>Received</th><th>From</th><th>Topic</th><th>Message</th><th>{heading === 'Open' ? 'Waiting' : 'Handled'}</th><th>Resolution</th></tr></thead>
            <tbody>
              {rows.map((m) => (
                <tr key={m.id} className="click" onClick={() => navigate(`/ranger/messages/${m.id}`)}>
                  <td className="nowrap"><Link to={`/ranger/messages/${m.id}`}><b>#{m.id}</b></Link></td><td className="nowrap">{shortTime(m.createdAt)}</td>
                  <td><Link to={`/ranger/messages/${m.id}`}><b>{m.name}</b></Link><br /><span className="help">{m.email}</span></td>
                  <td>{m.topic}</td>
                  <td className="wrap">{cut(m.message)}{m.repliedAt && <><br /><span className="help">Replied {age(m.repliedAt)} ago</span></>}</td>
                  <td className="nowrap">{heading === 'Open' ? age(m.createdAt) : <>{new Date(m.handledAt!).toLocaleDateString('en-US', { month: 'numeric', day: 'numeric' })}<br /><span className="help">{m.handledBy}</span></>}</td>
                  <td><ResolutionPill value={m.resolution} /></td>
                </tr>
              ))}
              {rows.length === 0 && <EmptyRow cols={7}>{heading === 'Open' ? 'No new messages.' : 'Nothing handled yet.'}</EmptyRow>}
            </tbody>
          </table></div>
        </div>
      ))}
    </>
  );
}
