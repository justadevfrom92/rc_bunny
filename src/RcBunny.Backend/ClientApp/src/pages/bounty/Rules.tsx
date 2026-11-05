import { useState } from 'react';
import { api, dateTime, useLoad } from '../../api';
import { PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Bounty → Earning Rules (ebunny's bounty/rules): how customers earn stars; Save Rules; Award Stars Now. */
export default function Rules() {
  const { data, error, reload } = useLoad<any>('/api/bounty/rules');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <RulesForm data={data} reload={reload} key={JSON.stringify(data)} />;
}

function RulesForm({ data, reload }: { data: any; reload: () => void }) {
  const [rows, setRows] = useState<any[]>(data.rules.map((r: any) => ({ ...r })));
  const [run, bar] = useRun(reload);
  const set = (i: number, k: string, v: unknown) => setRows(rows.map((r, j) => (j === i ? { ...r, [k]: v } : r)));
  const last = data.last;
  return (
    <>
      <form style={{ display: 'flex', flexDirection: 'column', gap: 20 }} onSubmit={(e) => { e.preventDefault();
        run(Promise.all(rows.map((r) => api.put(`/api/bounty/rules/${r.id}`, { name: r.name, stars: Number(r.stars), active: r.active }))), 'Rules saved'); }}>
        <PageHead title="Earning Rules" sub={'How customers earn stars. The "Reward stars" job (Sheriff → Crons, nightly) awards them; each award is only given once.'}
          actions={<button className="btn cyan">Save Rules</button>} />
        {bar}
        <div className="panel"><div className="table-wrap"><table>
          <thead><tr><th>Rule</th><th style={{ width: '40%' }}>Name on the Star History</th><th>Stars</th><th>Per</th><th>Active</th></tr></thead>
          <tbody>{rows.map((r, i) => (
            <tr key={r.id}><td className="mono">{r.event}</td>
              <td><input className="cell-input" value={r.name} required aria-label={`${r.event} label`} style={{ width: '100%', minWidth: 220 }} onChange={(e) => set(i, 'name', e.target.value)} /></td>
              <td><input type="number" min="1" value={r.stars} required aria-label={`${r.event} stars`} style={{ width: '10ch', textAlign: 'right' }} onChange={(e) => set(i, 'stars', e.target.value)} /></td>
              <td className="nowrap muted">{r.event === 'bill_paid' ? 'each bill paid' : 'each time'}</td>
              <td><label className="check"><input type="checkbox" checked={r.active} onChange={(e) => set(i, 'active', e.target.checked)} /> Active</label></td></tr>
          ))}</tbody>
        </table></div></div>
      </form>
      <div className="panel"><div className="panel-body form-row" style={{ alignItems: 'center' }}>
        <span>Last award run: {last ? `${dateTime(last.startedAt)} · ${last.ok ? 'OK' : 'Failed'} — ${last.message}` : 'never'}</span>
        <button className="btn" onClick={() => run(api.post('/api/bounty/award'))}>Award Stars Now</button>
      </div></div>
    </>
  );
}
