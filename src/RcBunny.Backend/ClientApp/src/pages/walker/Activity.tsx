import { Link, useNavigate } from 'react-router';
import { api, useLoad } from '../../api';
import { EmptyRow, PageHead, toast, useRun } from '../../ui';
import { DateInput } from '../../fields';
import { ReportActions, StatePill, when } from './shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const heads: Record<string, [string, string]> = {
  orders: ['Orders Report', 'Pull orders by date range and status. Runs in the background and saves a file you can download.'],
  notes: ['Notes Report', 'Account notes by agent, date, text and priority. "System" notes are the ones written automatically.'],
  phonecalls: ['Phonecalls Report', 'Search calls by date, account, agent id or phone number.'],
};
const headline = (s: string) => s.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());

/** ebunny's corral/reports/_output: export format, summary and Run Report. */
const Output = ({ formats }: { formats: Record<string, string> }) => (
  <>
    <div className="field"><label>Export as</label><div className="chips small">
      {Object.entries(formats).map(([v, l]) => <label key={v}><input type="radio" name="format" value={v} defaultChecked={v === 'xlsx'} /><span>{l}</span></label>)}
    </div></div>
    <label className="check"><input type="checkbox" name="summary" /> Summary only (counts instead of every row)</label>
    <button className="btn">Run Report</button>
    <p className="help">Runs in the background. You'll get a notice at the top right when the file is ready.</p>
  </>
);

/** Walker → Orders, Notes and Phonecalls (ebunny's corral/report and corral/reports/*): pick settings, run, and your runs. */
export default function Activity({ report }: { report: 'orders' | 'notes' | 'phonecalls' }) {
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/walker/activity/${report}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const d = data.defaults;
  const [title, sub] = heads[report];
  const submit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const params: Record<string, unknown> = {};
    for (const [k] of f) if (!['format', 'summary'].includes(k)) params[k] = ['statuses', 'priorities'].includes(k) ? f.getAll(k) : f.get(k);
    for (const k of ['statuses', 'priorities']) if (report === (k === 'statuses' ? 'orders' : 'notes') && !f.has(k)) params[k] = [];
    run(api.post(`/api/walker/reports/${report}`, { params, format: f.get('format'), summary: f.get('summary') === 'on' }));
  };
  const rerun = (id: number) => api.post<{ id: number; message: string }>(`/api/files/${id}/rerun`).then((x) => { toast(x.message); navigate(`/files/${x.id}`); }, (e) => toast(e.message));
  const dates = <><div className="field"><label htmlFor="start">Start</label><DateInput id="start" name="start" defaultValue={d.start ?? ''} /></div>
    <div className="field"><label htmlFor="end">End</label><DateInput id="end" name="end" defaultValue={d.end ?? ''} /></div></>;
  return (
    <>
      <PageHead title={title} sub={sub} />
      {bar}
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={submit}><div className="panel-head"><h2>Search {title}</h2></div>
            {report === 'orders' && (
              <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div className="form-row">{dates}<Output formats={data.formats} /></div>
                <div className="actions">
                  {data.statuses.map((s: string) => <label key={s} className="check" style={{ marginRight: 10 }}><input type="checkbox" name="statuses" value={s} defaultChecked={(d.statuses ?? []).includes(s)} /> {s}</label>)}
                </div>
              </div>
            )}
            {report === 'notes' && (
              <div className="panel-body" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div className="form-row">
                  <div className="field"><label>Username</label><div className="table-wrap pick-list" style={{ maxHeight: 180 }}><table className="table" data-page="0"><tbody>
                    <tr><td style={{ width: 32 }}><input type="radio" id="nu0" name="user" value="" defaultChecked={!d.user} /></td><td><label htmlFor="nu0">Anyone</label></td></tr>
                    {data.users.map((u: any) => <tr key={u.id}><td><input type="radio" id={`nu${u.id}`} name="user" value={u.id} defaultChecked={String(d.user) === String(u.id)} /></td><td><label htmlFor={`nu${u.id}`}>{u.name}</label></td></tr>)}
                  </tbody></table></div></div>
                  {dates}
                  <div className="field grow"><label htmlFor="contains">Note Contains</label><input id="contains" name="contains" defaultValue={d.contains ?? ''} maxLength={100} /></div>
                </div>
                <div className="form-row">
                  <div className="field"><label>Priority</label><div className="actions">
                    {data.priorities.map((p: string) => <label key={p} className="check" style={{ marginRight: 10 }}><input type="checkbox" name="priorities" value={p} defaultChecked={(d.priorities ?? []).includes(p)} /> {p}</label>)}
                  </div></div>
                  <Output formats={data.formats} />
                </div>
              </div>
            )}
            {report === 'phonecalls' && (
              <div className="panel-body"><div className="form-row">
                {dates}
                <div className="field"><label htmlFor="account">Account Number</label><input id="account" name="account" defaultValue={d.account ?? ''} inputMode="numeric" style={{ width: '14ch' }} /></div>
                <div className="field"><label htmlFor="agent">Agent Id</label><input id="agent" name="agent" defaultValue={d.agent ?? ''} style={{ width: '10ch' }} /></div>
                <div className="field"><label htmlFor="phone">Phone Number</label><input id="phone" name="phone" defaultValue={d.phone ?? ''} inputMode="tel" style={{ width: '16ch' }} /></div>
                <Output formats={data.formats} />
              </div></div>
            )}
          </form>
        </aside>
        <div className="search-main">
          <div className="panel" id="report-runs">
            <div className="panel-head"><h2>{data.def.title} — Your Runs</h2><Link className="btn sm ghost" to="/files">All My Files</Link></div>
            <div className="table-wrap"><table>
              <thead><tr><th>ID</th><th>Requested</th><th>Settings</th><th>Format</th><th className="num">Rows</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {data.recent.map((r: any) => (
                  <tr key={r.id} className="click" onClick={() => navigate(`/files/${r.id}`)}>
                    <td style={{ whiteSpace: 'nowrap' }}><Link to={`/files/${r.id}`}><b>#{r.id}</b></Link><div className="help">{r.summary ? 'Summary' : 'Every row'}</div></td>
                    <td className="nowrap">{when(r.createdAt)}</td>
                    <td className="help wrap" style={{ minWidth: 160 }}>{Object.entries(r.params ?? {}).map(([k, v]: [string, any]) => `${headline(k)}: ${Array.isArray(v) ? v.join(', ') : v}`).join(' · ')}</td>
                    <td className="nowrap">{r.format}</td>
                    <td className="num">{r.status === 'done' ? r.rows.toLocaleString() : '…'}</td>
                    <td style={{ whiteSpace: 'nowrap' }}><StatePill state={r.state} /></td>
                    <td className="actions"><ReportActions download={r.file ? `/api/files/${r.id}/download` : null} label={`Download ${r.format}`}
                      why={['queued', 'running'].includes(r.status) ? 'still running' : 'no file'} rerun={() => rerun(r.id)} /></td></tr>
                ))}
                {data.recent.length === 0 && <EmptyRow cols={7}>You haven't run this report yet. Pick the settings on the left and Run Report.</EmptyRow>}
              </tbody>
            </table></div>
          </div>
        </div>
      </div>
    </>
  );
}
