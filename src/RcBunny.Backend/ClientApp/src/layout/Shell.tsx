import { useState } from 'react';
import { NavLink, Outlet, useLocation, Link } from 'react-router-dom';
import { returnToSelf, signOut, useMe } from '../auth';
import { useMeta } from '../queries';
import type { MenuItem } from '../types';

/** Sidebar (current app's menu), top bar (app switcher, person menu) and the page. */
export function Shell() {
  const me = useMe();
  const { data: meta } = useMeta();
  const { pathname, search } = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);

  const appKey = pathname.split('/')[1];
  const app = meta?.apps.find((a) => a.key === appKey);
  const myApps = meta?.apps.filter((a) => me.apps.includes(a.key)) ?? [];

  // The menu item for this page: an exact match (address and query) first, otherwise the longest path that contains it.
  const items = app?.menu.flatMap((g) => g.items) ?? [];
  const here = pathname + search;
  const active = items.find((i) => i.path === here)
    ?? items.filter((i) => !i.path.includes('?') && (pathname === i.path || pathname.startsWith(i.path + '/'))).sort((a, b) => b.path.length - a.path.length)[0];
  const isOn = (item: MenuItem) => item === active;

  return (
    <div className={`shell ${menuOpen ? 'menu-open' : ''}`}>
      <aside className="sidebar" onClick={() => setMenuOpen(false)}>
        <Link to="/" className="brand">
          <span className="logo-mark" aria-hidden="true">RC</span>
          <span>
            <strong>{app?.name ?? 'Admin'}</strong>
            <small>RC Bunny</small>
          </span>
        </Link>
        <nav className="menu" aria-label={app ? `${app.name} menu` : 'Apps'}>
          {app ? (
            app.menu.map((g, i) => (
              <div key={i}>
                {g.heading && <h4>{g.heading}</h4>}
                {g.items.map((item) => (
                  <NavLink key={item.path} to={item.path} className={() => (isOn(item) ? 'on' : '')} end>
                    {item.label}
                    {!item.ported && <span className="soon" title="Not ported from ebunny yet">soon</span>}
                  </NavLink>
                ))}
              </div>
            ))
          ) : (
            <>
              <h4>Your apps</h4>
              {myApps.map((a) => <NavLink key={a.key} to={a.home}>{a.name}</NavLink>)}
            </>
          )}
        </nav>
        <div className="side-foot">
          <a href={import.meta.env.VITE_WEBSITE_URL ?? 'http://localhost:5100'} target="_blank" rel="noreferrer">Website ↗</a>
        </div>
      </aside>

      <div className="main">
        {me.impersonatedBy && (
          <div className="impersonating">
            You're logged in as <strong>{me.name}</strong> and see exactly what they see.
            <button className="btn small" onClick={() => void returnToSelf()}>Back to me</button>
          </div>
        )}
        <header className="topbar">
          <button className="menu-toggle" aria-label="Menu" onClick={() => setMenuOpen((o) => !o)}>☰</button>
          <nav className="switcher" aria-label="Apps">
            <NavLink to="/" end>Launcher</NavLink>
            {myApps.map((a) => (
              <NavLink key={a.key} to={a.home} className={() => (a.key === appKey ? 'active' : '')}>{a.name}</NavLink>
            ))}
          </nav>
          <details className="person">
            <summary>
              <span className="avatar">{me.name.split(' ').map((w) => w[0]).slice(0, 2).join('')}</span>
              <span className="who">
                {me.name}
                <small>{me.company ? `${me.company} · ` : ''}{me.role}</small>
              </span>
            </summary>
            <div className="person-menu">
              <p className="muted small">{me.email}</p>
              {me.accessEndsOn && <p className="small">Access ends {me.accessEndsOn}</p>}
              <p className="small">Access: {me.perms.join(', ') || 'none'}</p>
              <button className="btn outline small" onClick={() => void signOut()}>Sign out</button>
            </div>
          </details>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
