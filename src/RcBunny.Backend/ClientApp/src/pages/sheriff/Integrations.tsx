import { useLoad } from '../../api';
import { PageHead } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Sheriff → APIs (ebunny's sheriff/integrations): third-party integrations and whether each is set up. */
export default function Integrations() {
  const { data, error } = useLoad<any[]>('/api/sheriff/integrations');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="APIs" sub="Third-party integrations." />
      <div className="banner-note">Keys and passwords live in the server's settings (<span className="mono">appsettings</span> or environment variables), never in the database or the code. This screen only shows whether each value is set.</div>
      <div className="grid-3">
        {data.map((i) => (
          <div key={i.name} className="panel"><div className="panel-head"><h2>{i.name}</h2><span className={`pill ${i.configured ? 'ok' : 'warn'}`}>{i.configured ? 'Configured' : 'Not configured'}</span></div>
            <div className="panel-body">
              <p className="muted" style={{ marginBottom: 10 }}>{i.usedFor}</p>
              <dl className="kv" style={{ gridTemplateColumns: '1fr auto' }}>
                <dt className="mono">{i.setting}</dt><dd className={i.configured ? 'secret-set' : 'muted'}>{i.configured ? 'set' : '—'}</dd>
              </dl>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}
