import { Link } from 'react-router';
import { useAuth } from '../auth';
import { apps, Brand, Icon, UserMenu } from '../Sidebar';

/** The launcher: ebunny's "Howdy" page with a tile for each app you can open. */
export default function Home() {
  const { me, can } = useAuth();
  const mine = Object.entries(apps).filter(([key]) => can(key));
  return (
    <div className="launcher">
      <header className="launcher-top"><div className="in">
        <Link className="brand" to="/" style={{ color: '#fff', textDecoration: 'none' }}><Brand /></Link>
        <UserMenu />
      </div></header>
      <main className="launcher-main">
        <div className="page-head">
          <div>
            <h1>Howdy, {me!.name.split(' ')[0]}</h1>
            <p className="muted">Choose an app. Only the apps you have access to are shown.</p>
          </div>
          <div className="actions"><a className="btn ghost" href="/" target="_blank" rel="noopener">View website</a></div>
        </div>
        <div className="tiles">
          {mine.map(([key, a]) => (
            <Link key={key} className="tile" to={a.home}>
              <span className="ico"><Icon name={a.icon} /></span>
              <h2>{a.name}</h2><p>{a.desc}</p><span className="open">Open {a.name} →</span>
            </Link>
          ))}
          {mine.length === 0 && (
            <div className="panel" style={{ gridColumn: '1/-1' }}><div className="panel-body muted">You don't have access to any admin apps yet. Ask an administrator to set up your access in Sheriff → Users.</div></div>
          )}
        </div>
      </main>
    </div>
  );
}
