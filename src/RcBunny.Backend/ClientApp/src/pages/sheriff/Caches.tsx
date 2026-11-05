import { api } from '../../api';
import { PageHead, useRun } from '../../ui';

/** Sheriff → Cached Tables: what the admin and website keep in memory, and clearing it. */
export default function Caches() {
  const [run, bar] = useRun();
  const rows = [
    ['Admin sign-ins', 'Each person\'s role and permissions', '30 seconds', 'Changing someone\'s role or sign-in clears theirs straight away.'],
    ['Website catalog', 'Markets, zip codes, plans, rates and fees', '1 minute', 'Plan and rate changes show on the website within a minute.'],
    ['Website maintenance switch', 'Sheriff → Heartbeat → Website', '30 seconds', 'Turning the website off or on takes effect within 30 seconds.'],
    ['Admin screens', 'The last data each screen loaded, in your browser', 'Until you leave', 'Screens show it instantly while they reload.'],
  ];
  return (
    <>
      <PageHead title="Cached Tables" sub="What the admin and the website keep in memory so pages open quickly, and for how long."
        actions={<button className="btn cyan" onClick={() => run(api.post('/api/sheriff/caches/clear'), "Cleared. Everyone's permissions are re-read on their next click.")}>Clear the Admin Cache</button>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table className="table">
        <thead><tr><th>Cache</th><th>Holds</th><th>Kept For</th><th>Notes</th></tr></thead>
        <tbody>{rows.map(([name, holds, kept, note]) => (
          <tr key={name}><td><b>{name}</b></td><td>{holds}</td><td className="nowrap">{kept}</td><td className="wrap help">{note}</td></tr>
        ))}</tbody>
      </table></div></div>
    </>
  );
}
