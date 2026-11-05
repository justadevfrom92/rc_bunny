import { dateTime, useLoad } from '../../api';
import { EmptyRow, PageHead } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Sheriff → Control Log (ebunny's sheriff/heartbeat-log): every heartbeat control anyone used. */
export default function ControlLog() {
  const { data, error } = useLoad<any[]>('/api/sheriff/heartbeat/log');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Control Log" sub="Every take down and bring up used on a heartbeat control: who, when and what." />
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>When</th><th>Check</th><th>Control</th><th>Who</th><th>Result</th></tr></thead>
        <tbody>
          {data.map((a) => (
            <tr key={a.id}><td className="nowrap"><b>#{a.id}</b></td><td className="nowrap">{dateTime(a.createdAt)}</td><td><b>Website</b></td>
              <td>{/took .* down/i.test(a.what) ? 'Take Down' : 'Bring Up'}</td><td>{a.who || 'System'}</td>
              <td className="wrap"><span className="pill ok">Done</span><div className="help">{a.what}</div></td></tr>
          ))}
          {data.length === 0 && <EmptyRow cols={6}>No controls used yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}
