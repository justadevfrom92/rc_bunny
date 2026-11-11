import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { dateTime, qs, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, PagerBar, age, secondsTime } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const choiceTone: Record<string, string> = { all: 'ok', necessary: 'warn', custom: 'info' };
const catTone: Record<string, string> = { necessary: '', analytics: 'info', marketing: 'warn' };
const cap = (s: string) => s[0].toUpperCase() + s.slice(1);
const pct = (n: number, of: number) => (of ? Math.round((100 * n) / of) : 0);

/** Lando → Cookie Consents (ebunny's lando/consents): every choice made in the website's cookie banner. */
export default function Consents() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/lando/consents?${params}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const who = params.get('who') ?? '', choice = params.get('choice') ?? '', q = params.get('q') ?? '';
  const url = (set: Record<string, string>) => `/lando/cookie-consents?${qs({ who, choice, q, ...set })}`;
  const t = (k: string) => data.totals[k] ?? 0;
  return (
    <>
      <PageHead title="Cookie Consents" sub={`Every choice made in the website's cookie banner, from signed-in customers, employees and anonymous visitors. The latest choice for a person or browser is the one in force (policy version ${data.policy}).`} />
      <div className="grid-4 stat-row">
        <div className="stat"><span>Choices</span><strong>{data.all.toLocaleString()}</strong><small>last 30 days</small></div>
        <div className="stat"><span>Accepted All</span><strong>{pct(t('all'), data.all)}%</strong><small>{t('all').toLocaleString()}</small></div>
        <div className="stat"><span>Necessary Only</span><strong>{pct(t('necessary'), data.all)}%</strong><small>{t('necessary').toLocaleString()}</small></div>
        <div className="stat"><span>Allow Analytics / Marketing</span><strong>{data.analytics.toLocaleString()} / {data.marketing.toLocaleString()}</strong><small>{t('custom').toLocaleString()} chose their own</small></div>
      </div>
      <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>Cookies</h2><span className="muted">allowed in the last 30 days</span></div><div className="table-wrap"><table className="table" data-page="0">
        <thead><tr><th>Cookie</th><th>Category</th><th>What it's for</th><th>Lasts</th><th className="num">Said Yes</th></tr></thead>
        <tbody>{data.cookies.map((c: any) => {
          const yes = c.category === 'necessary' ? data.all : data[c.category];
          return <tr key={c.name}><td className="mono"><b>{c.name}</b></td><td><span className={`pill ${catTone[c.category]}`}>{cap(c.category)}</span></td>
            <td className="wrap">{c.purpose}</td><td>{c.lasts}</td><td className="num">{yes.toLocaleString()} <span className="help">{pct(yes, data.all)}%</span></td></tr>;
        })}</tbody>
      </table></div></div>
      <div className="panel">
        <form onSubmit={(e) => { e.preventDefault(); navigate(url({ q: String(new FormData(e.currentTarget).get('q') ?? ''), page: '' })); }}>
          <div className="panel-head" style={{ flexWrap: 'wrap', gap: 10 }} id="consent-chips">
            <h2>Choices</h2><span className="help">click one to see that person's cookies</span>
            <nav className="chips small" aria-label="Who">{[['', 'Everyone'], ['customer', 'Customers'], ['employee', 'Employees'], ['anonymous', 'Anonymous']].map(([v, l]) =>
              <Link key={v} className={`chip-link${who === v ? ' on' : ''}`} to={url({ who: v, page: '' })}>{l}</Link>)}</nav>
            <nav className="chips small" aria-label="Choice">{[['', 'Any choice'], ['all', 'Accepted all'], ['necessary', 'Necessary only'], ['custom', 'Chose']].map(([v, l]) =>
              <Link key={v} className={`chip-link${choice === v ? ' on' : ''}`} to={url({ choice: v, page: '' })}>{l}</Link>)}</nav>
          </div>
          <div className="panel-body search-row">
            <label className="search-field"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" strokeWidth="2" /><path d="M20 20l-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg>
              <input type="search" name="q" defaultValue={q} key={q} placeholder="Search by account, name, IP or visitor id" aria-label="Search choices" autoComplete="off" /></label>
          </div>
        </form>
        <div className="table-wrap"><table className="table">
          <thead><tr><th>ID</th><th>When</th><th>Who</th><th>Choice</th><th>Cookies Allowed</th><th>Page</th><th>IP</th><th>Visitor</th></tr></thead>
          <tbody>
            {data.consents.map((k: any) => (
              <tr key={k.id} className="click" onClick={() => navigate(`/lando/cookie-consents/${k.id}`)}><td className="nowrap"><Link to={`/lando/cookie-consents/${k.id}`}><b>#{k.id}</b></Link></td><td className="nowrap">{dateTime(k.createdAt)}</td>
                <td onClick={(e) => e.stopPropagation()}>{k.customerId ? <><AccountNo id={k.customerId} account={k.account} /><br /><span className="help">{k.customer}</span></> : k.user ? <>{k.user}<br /><span className="help">Employee</span></> : <span className="muted">Anonymous</span>}</td>
                <td><span className={`pill ${choiceTone[k.choice] ?? ''}`}>{k.label}</span></td>
                <td className="wrap" style={{ minWidth: 220 }}><span className="cookie-list">{k.allowed.map((c: string) => <span key={c} className={`mono cookie-${data.cookies.find((x: any) => x.name === c)?.category}`}>{c}</span>)}</span></td>
                <td className="mono">{k.page ?? '—'}</td><td className="mono">{k.ip}</td><td className="mono help">{k.visitor.slice(0, 10)}…</td></tr>
            ))}
            {data.consents.length === 0 && <EmptyRow cols={8}>No choices match.</EmptyRow>}
          </tbody>
        </table></div>
        <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => navigate(url({ page: String(p) }))} />
      </div>
    </>
  );
}

/** One cookie banner choice (ebunny's lando/consent): who, what it allows, the cookies their browser actually had. */
export function Consent() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/lando/consents/${id}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const k = data.consent, c = data.customer;
  const who = c ? c.name : data.user ? `${data.user} (employee)` : 'Anonymous visitor';
  const jarCount = Object.keys(data.jar).length;
  const why: Record<string, string> = { necessary: 'Always on: the site needs them', analytics: 'How people use the site', marketing: 'Ads that brought them here' };
  return (
    <>
      <PageHead title={`${k.label} — ${who}`} sub={`Cookie choice #${k.id} · ${dateTime(k.createdAt)} · policy ${k.policyVersion}${k.current ? ' (current)' : " (old: they'll be asked again)"}`}
        actions={<Link className="btn ghost" to="/lando/cookie-consents">All Choices</Link>} />
      <div className="grid-4 stat-row">
        <div className="stat"><span>Choice</span><strong style={{ fontSize: '1.3rem' }}>{k.label}</strong><small>{k.analytics ? 'analytics on' : 'analytics off'} · {k.marketing ? 'marketing on' : 'marketing off'}</small></div>
        <div className="stat"><span>Cookies Allowed</span><strong>{data.allowed.length}</strong><small>of {data.cookies.length} the site uses</small></div>
        <div className="stat"><span>In Their Browser</span><strong>{jarCount}</strong><small>{k.cookiesSeenAt ? `last seen ${age(k.cookiesSeenAt)} ago` : 'not seen yet'}</small></div>
        <div className={`stat${data.unexpected.length ? ' alert' : ''}`}><span>Not Allowed but Present</span><strong>{data.unexpected.length}</strong><small>{data.unexpected.length ? data.unexpected.join(', ') : 'none'}</small></div>
      </div>
      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0,1fr) minmax(0,1fr)', alignItems: 'start' }}>
        <div className="panel"><div className="panel-head"><h2>Who &amp; Where</h2></div><div className="panel-body"><dl className="kv">
          <dt>Who</dt><dd>{c ? <><AccountNo id={c.id} account={c.account} /> {c.name}</> : data.user ? <>{data.user} <span className="help">employee</span></> : <span className="muted">Anonymous</span>}</dd>
          <dt>Chose on</dt><dd className="mono">{k.page ?? '—'}</dd>
          <dt>IP</dt><dd className="mono">{k.ip ?? '—'}</dd>
          <dt>Browser</dt><dd className="help" style={{ overflowWrap: 'anywhere' }}>{k.userAgent ?? '—'}</dd>
          <dt>Visitor id</dt><dd className="mono" style={{ overflowWrap: 'anywhere' }}>{k.visitor}</dd>
          <dt>When</dt><dd>{secondsTime(k.createdAt)}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>Cookies by Category</h2></div><div className="panel-body"><dl className="kv">
          {['necessary', 'analytics', 'marketing'].map((cat) => {
            const on = cat === 'necessary' || k[cat];
            return [<dt key={cat + 'a'}>{cap(cat)}</dt>, <dd key={cat + 'b'}><span className={`pill ${on ? 'ok' : 'bad'}`}>{on ? 'Allowed' : 'Blocked'}</span> <span className="help">{why[cat]}</span>
              <div className="help mono">{data.cookies.filter((x: any) => x.category === cat).map((x: any) => x.name).join(', ')}</div></dd>];
          })}
        </dl></div></div>
      </div>
      <div className="panel"><div className="panel-head"><h2>Cookie Cache</h2><span className="muted">{k.cookiesSeenAt ? `what their browser sent, ${dateTime(k.cookiesSeenAt)}` : 'not seen yet'}</span></div>
        <div className="table-wrap"><table className="table cookie-jar" data-page="0">
          <thead><tr><th>Cookie</th><th>Category</th><th>This Choice</th><th>In Their Browser</th><th>What It's For</th><th>Lasts</th></tr></thead>
          <tbody>
            {data.cookies.map((x: any) => {
              const ok = data.allowed.includes(x.name), has = x.name in data.jar;
              return (
                <tr key={x.name} className={has && !ok ? 'row-warn' : undefined}><td className="mono"><b>{x.name}</b></td>
                  <td><span className={`pill ${catTone[x.category]}`}>{cap(x.category)}</span></td><td>{ok ? '✓ Allowed' : '✗ Blocked'}</td>
                  <td className="mono">{has ? <>{data.jar[x.name] !== '' ? data.jar[x.name] : '(empty)'}{!ok && <div className="help" style={{ color: 'var(--bad)' }}>present but not allowed</div>}</> : <span className="muted">—</span>}</td>
                  <td className="wrap">{x.purpose}</td><td className="nowrap">{x.lasts}</td></tr>
              );
            })}
            {Object.entries(data.unknown).map(([name, value]) => (
              <tr key={name} className="row-warn"><td className="mono"><b>{name}</b></td><td><span className="pill bad">Not on our list</span></td><td>—</td>
                <td className="mono">{(value as string) !== '' ? (value as string) : '(empty)'}</td><td className="wrap">Not one of the site's cookies. Set by another script, an old version of the site or a browser extension.</td><td>—</td></tr>
            ))}
          </tbody>
        </table></div>
        <div className="panel-body help" style={{ borderTop: '1px solid var(--line)' }}>Sign-in and form tokens are cut short here: enough to tell them apart, never enough to use. The cache refreshes as the visitor browses (at most every 10 minutes).</div>
      </div>
      <div className="grid-2" style={{ alignItems: 'start' }}>
        <div className="panel"><div className="panel-head"><h2>Their Other Choices</h2><span className="muted">{data.others.length}</span></div><div className="table-wrap"><table data-page="10">
          <thead><tr><th>ID</th><th>When</th><th>Choice</th><th>Policy</th></tr></thead>
          <tbody>
            {data.others.map((o: any) => <tr key={o.id} className="click" onClick={() => navigate(`/lando/cookie-consents/${o.id}`)}><td className="nowrap"><Link to={`/lando/cookie-consents/${o.id}`}><b>#{o.id}</b></Link></td><td className="nowrap">{dateTime(o.createdAt)}</td>
              <td><span className={`pill ${choiceTone[o.choice] ?? ''}`}>{({ all: 'Accepted all', necessary: 'Necessary only', custom: 'Chose' } as Record<string, string>)[o.choice]}</span></td><td className="mono">{o.policyVersion}</td></tr>)}
            {data.others.length === 0 && <EmptyRow cols={4}>This is the only choice from this browser{c ? ' or customer' : ''}.</EmptyRow>}
          </tbody>
        </table></div></div>
        <div className="panel"><div className="panel-head"><h2>Pages Visited</h2><span className="muted">from this browser</span></div><div className="table-wrap"><table data-page="10">
          <thead><tr><th>ID</th><th>When</th><th>Page</th><th>IP</th></tr></thead>
          <tbody>
            {data.visits.map((v: any) => <tr key={v.id} className="click" onClick={() => navigate(`/lando/records/site_visit/${v.id}`)}><td className="nowrap"><Link to={`/lando/records/site_visit/${v.id}`}><b>#{v.id}</b></Link></td>
              <td className="nowrap">{dateTime(v.createdAt)}</td><td className="mono">/{v.path === '/' ? '' : v.path}</td><td className="mono">{v.ip}</td></tr>)}
            {data.visits.length === 0 && <EmptyRow cols={4}>No visits recorded from this browser.</EmptyRow>}
          </tbody>
        </table></div></div>
      </div>
    </>
  );
}
