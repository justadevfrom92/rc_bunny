import { Fragment, useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, NavLink, useLocation } from 'react-router';
import { api } from './api';
import { useAuth } from './auth';

/** A menu: groups of [label, path, options]. also: other paths that belong to the link; badge: the count shown next to it (ebunny's AdminMenu::badge keys). */
export type Menu = { heading?: string; links: [label: string, path: string, options?: { also?: string[]; badge?: string }][] }[];

/** The admin apps: name, launcher text, icon and home page. Same as ebunny's config/admin.php. */
/** perm: the permission that opens it, when it isn't the app's own key (Astro needs "plans"). */
export const apps: Record<string, { name: string; desc: string; icon: string; home: string; perm?: string }> = {
  corral: { name: 'Corral', icon: 'users', home: '/corral', desc: 'Customer service and CRM: search accounts, create orders, work exception queues, the sales pipeline of leads, follow-ups and partners.' },
  ranger: { name: 'Ranger', icon: 'ticket', home: '/ranger', desc: 'Support tickets customers open from My Account and messages from the website contact form: the queue, replies and notes, take over and join in for managers, the ticket team (in-office and 3rd party) and approval ratings.' },
  lando: { name: 'Lando', icon: 'layout', home: '/lando', desc: 'Website CMS: site health and visitors, pages, page templates and content blocks.' },
  astro: { name: 'Astro', icon: 'chart', home: '/astro/plans', perm: 'plans', desc: 'Plans and pricing: plans, plan groups, rates, TDSP fees, term discounts, ETFs and Build Your Own Plan products.' },
  walker: { name: 'Walker', icon: 'file', home: '/walker', desc: 'Files & reporting: ETL feeds, legal documents, customer templates, and reports with their status.' },
  caboose: { name: 'Caboose', icon: 'train', home: '/caboose', desc: 'Finance: payments, credits and debits approval, refunds, deposits, receivables aging, the journal export, MSIDs and promo codes.' },
  bounty: { name: 'Bounty', icon: 'gift', home: '/bounty', desc: 'Rewards: offers, how stars are earned, redemptions to fulfil, member balances and the monthly drawing.' },
  deputy: { name: 'Deputy', icon: 'bolt', home: '/deputy', desc: 'AI agents on open-weight models (Qwen, Kimi, DeepSeek, Llama…): texts and the My Account chat with take-over by a person, models and testing.' },
  sheriff: { name: 'Sheriff', icon: 'star', home: '/sheriff/users', desc: 'Settings: the staff directory (profiles, permissions, log in as), roles, markets, API integrations, scheduled jobs, cron automations and the timeline of everything that ran.' },
};

/** Your Account area (the user menu's pages): Notifications, My Files and Settings, like ebunny's. */
export const account = { name: 'Account', home: '/my/notifications' };

/** The public website (it runs on its own address): set VITE_WEBSITE_URL when building for another server. */
export const websiteUrl: string = import.meta.env.VITE_WEBSITE_URL || 'http://localhost:5100/';

export const env = ['localhost', '127.0.0.1'].includes(location.hostname) ? 'LOCAL' : 'PRODUCTION';

/** The logo and "ENERGY TEXAS / Admin Tools", as at the top of ebunny's sidebar. */
export const Brand = () => (
  <>
    <img src="/shared/img/logo-mark-on-dark.svg" alt="" width={36} height={36} />
    <div><strong>ENERGY TEXAS</strong><span>Admin Tools</span></div>
  </>
);

/**
 * One admin app's page: ebunny's shell. The navy sidebar (logo, environment, Admin button, the app's menu, Website),
 * the top bar (crumbs and your name), and the page in .content. Menu paths are relative to the app.
 */
export default function AppShell({ app, menu, children }: { app: string; menu: Menu; children: ReactNode }) {
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const info = app === 'account' ? account : apps[app];
  // The Account area's pages aren't under one folder (/my/notifications, /files, /my/settings)
  const base = app === 'account' ? '' : `/${app}`;
  useEffect(() => setOpen(false), [pathname]);
  // The counts next to menu links, fresh on every page like ebunny's
  const [badges, setBadges] = useState<Record<string, number>>({});
  useEffect(() => { api.get<Record<string, number>>('/api/menu/badges').then(setBadges, () => {}); }, [pathname]);

  // The menu item for this page: the longest link the address starts with
  // (a link can also own other paths, e.g. Caboose's promos/ pages belong to MSIDs & Promo Codes)
  const links = menu.flatMap((g) => g.links.map(([label, to, o]) => ({ label, url: `${base}/${to}`.replace(/\/$/, ''), also: (o?.also ?? []).map((a) => `${base}/${a}`) })));
  const under = (pathname: string, url: string) => pathname === url || pathname.startsWith(url + '/');
  const active = links.filter((l) => under(pathname, l.url) || l.also.some((a) => under(pathname, a))).sort((a, b) => b.url.length - a.url.length)[0];
  const exact = active && pathname === active.url;

  return (
    <div className="shell">
      <aside className={`sidebar${open ? ' open' : ''}`} id="sidebar">
        <div className="brand"><Brand /></div>
        <span className="env">{env}</span>
        <Link className="admin-home" to="/">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1.5" /><rect x="14" y="3" width="7" height="7" rx="1.5" /><rect x="3" y="14" width="7" height="7" rx="1.5" /><rect x="14" y="14" width="7" height="7" rx="1.5" /></svg>
          <span>Admin<small>{info.name}</small></span>
        </Link>
        <nav className="menu" id="menu" aria-label={`${info.name} menu`}>
          {menu.map((group, i) => (
            <Fragment key={i}>
              {group.heading ? <h4>{group.heading}</h4> : i > 0 && <div className="menu-gap" />}
              {group.links.map(([label, to, o]) => {
                const url = `${base}/${to}`.replace(/\/$/, '');
                const on = active?.url === url;
                const n = o?.badge ? badges[o.badge] : 0;
                return <NavLink key={to} to={url} className={on ? 'on' : ''} aria-current={on ? 'page' : undefined} end>{label}{n ? <span className="count hot">{n}</span> : null}</NavLink>;
              })}
            </Fragment>
          ))}
        </nav>
        <div className="side-foot"><a className="side-website" href={websiteUrl} target="_blank" rel="noopener">Website</a></div>
      </aside>
      <div className="main">
        <header className="topbar">
          <button className="menu-btn" id="menu-btn" aria-label="Open menu" onClick={() => setOpen(!open)}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M3 6h18M3 12h18M3 18h18" /></svg>
          </button>
          <div className="crumbs">
            <Link to={info.home}>{info.name}</Link> /{' '}
            {active && !exact && <><Link to={active.url}>{active.label}</Link> / </>}
            <Link to={pathname} aria-current="page"><b>{exact ? active.label : active ? 'Details' : info.name}</b></Link>
          </div>
          <UserMenu />
        </header>
        <main className="content">{children}</main>
      </div>
    </div>
  );
}

/**
 * Top right (ebunny's partials/user-menu): your avatar (with the number of unread notifications on its corner) and name.
 * Clicking your name opens your latest 5 notifications, My Files, Settings and Logout.
 */
export function UserMenu() {
  const { me, setMe } = useAuth();
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const [data, setData] = useState<{ unread: number; files: number; latest: { id: number; title: string; body?: string; icon: string; readAt?: string; createdAt: string }[] } | null>(null);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const close = (e: MouseEvent) => { if (!box.current?.contains(e.target as Node)) setOpen(false); };
    document.addEventListener('click', close);
    return () => document.removeEventListener('click', close);
  }, []);
  const load = () => api.get<typeof data>('/api/account/menu').then(setData, () => {});
  useEffect(() => { load(); setOpen(false); }, [pathname]);

  // Background reports: a popup says when each one is ready; checks more often while something of yours is running
  const [count, setCount] = useState<number | null>(null);
  const [bump, setBump] = useState(0);
  const [pops, setPops] = useState<Ready[]>([]);
  useEffect(() => {
    let t = 0;
    const check = () => api.get<{ count: number; running: number; items: Ready[] }>('/api/notifications').then((d) => {
      setCount((before) => { if (before != null && d.count > before) setBump((b) => b + 1); return d.count; });
      const seen = notified(), fresh = d.items.filter((it) => !seen.includes(it.id));
      if (fresh.length) {
        remember([...seen, ...fresh.map((it) => it.id)]);
        setPops((p) => [...p, ...fresh.slice(0, 3)]);
        fresh.slice(0, 3).forEach((it) => setTimeout(() => setPops((p) => p.filter((x) => x !== it)), 15000));
      }
      t = window.setTimeout(check, d.running ? 5000 : 20000);
    }, () => { t = window.setTimeout(check, 60000); });
    check();
    return () => clearTimeout(t);
  }, []);
  if (!me) return null;
  const unread = count ?? data?.unread ?? 0;
  const close = () => setOpen(false);
  return (
    <div className="user-menu" ref={box}>
      <button type="button" className="user-chip" aria-haspopup="true" aria-expanded={open} onClick={() => setOpen(!open)}>
        <span className="avatar-wrap"><span className="avatar">{initials(me.name)}</span>
          <span key={bump} className={`files-badge${bump ? ' bump' : ''}`} hidden={!unread} aria-label={`${unread} unread ${unread === 1 ? 'notification' : 'notifications'}`}>{unread}</span></span>
        <span><span>{me.name}</span><small>{me.role}</small></span>
        <svg className="caret" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg>
      </button>
      <div className="user-pop" hidden={!open}>
        <div className="up-head"><b>{me.name}</b><span>{me.email}</span></div>
        <div className="up-label up-label-first"><span>Notifications</span>{unread > 0 && <><span className="up-new">{unread} new</span>
          {/* Clear: marks them all read (they stay in All Notifications) */}
          <form className="up-clear" onSubmit={(e) => { e.preventDefault(); api.post('/api/my/notifications/read-all').then(load); }}><button type="submit">Clear</button></form></>}</div>
        <div className="up-notes">
          {(data?.latest ?? []).map((n) => (
            <Link key={n.id} className={`up-note ${n.readAt ? '' : 'unread'}`} to={`/my/notifications/${n.id}`} onClick={close}><span className="up-ico" aria-hidden="true">{n.icon}</span>
              <span><b>{n.title}</b>{n.body && <small>{n.body.length > 70 ? n.body.slice(0, 67) + '...' : n.body}</small>}<small>{ago(n.createdAt)}</small></span></Link>
          ))}
          {data && data.latest.length === 0 && <p className="up-empty">Nothing yet.</p>}
        </div>
        <Link className="up-item" to="/my/notifications" onClick={close}><span>All Notifications</span></Link>
        <Link className="up-item" to="/files" onClick={close}><span>My Files</span><span className="up-count">{data?.files ?? ''}</span></Link>
        <Link className="up-item" to="/my/settings" onClick={close}><span>Settings</span></Link>
        <button className="up-item up-logout" onClick={() => api.post('/api/logout').then(() => setMe(null))}>Logout</button>
      </div>
      {pops.length > 0 && (
        <div className="notices" aria-live="polite">
          {pops.map((it) => (
            <div key={it.id} className={`notice-pop${it.ok ? '' : ' bad'}`}>
              <div className="np-body"><b>{it.ok ? 'Report ready' : 'Report failed'}</b><div className="np-sub">{it.title}{it.ok && ` · ${it.rows.toLocaleString()} rows · ${it.format}`}</div>
                <div className="np-links">{it.ok && it.download && <a className="btn sm cyan" href={it.download}>Download</a>}<Link className="btn sm ghost" to={it.url}>View</Link></div></div>
              <button type="button" className="np-x" aria-label="Close" onClick={() => setPops((p) => p.filter((x) => x !== it))}>&times;</button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

type Ready = { id: number; title: string; ok: boolean; rows: number; format: string; url: string; download: string | null };
// The runs this browser already popped up for (ebunny keeps them in localStorage too)
const KEY = 'admin.notified';
const notified = (): number[] => { try { return JSON.parse(localStorage.getItem(KEY) || '[]'); } catch { return []; } };
const remember = (ids: number[]) => { try { localStorage.setItem(KEY, JSON.stringify(ids.slice(-100))); } catch { /* private window */ } };

/** "3 hours ago", like Laravel's diffForHumans. */
export function ago(at: string) {
  const s = Math.max(1, (Date.now() - new Date(at).getTime()) / 1000);
  const units: [number, string][] = [[31536000, 'year'], [2592000, 'month'], [604800, 'week'], [86400, 'day'], [3600, 'hour'], [60, 'minute'], [1, 'second']];
  for (const [n, u] of units) if (s >= n) { const k = Math.floor(s / n); return `${k} ${u}${k === 1 ? '' : 's'} ago`; }
  return 'just now';
}

export const initials = (name: string) => name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join('');

/** ebunny's app icons. */
export function Icon({ name }: { name: string }) {
  const p = { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, 'aria-hidden': true } as const;
  switch (name) {
    case 'ticket': return <svg {...p} strokeLinejoin="round"><path d="M3 7a2 2 0 012-2h14a2 2 0 012 2v3a2 2 0 000 4v3a2 2 0 01-2 2H5a2 2 0 01-2-2v-3a2 2 0 000-4z" /><path d="M14 5v14" strokeDasharray="2 2" /></svg>;
    case 'users': return <svg {...p} strokeLinecap="round"><circle cx="9" cy="8" r="3.5" /><path d="M2.5 20a6.5 6.5 0 0113 0M16 4.5a3.5 3.5 0 010 7M18 14a5.5 5.5 0 013.5 6" /></svg>;
    case 'file': return <svg {...p} strokeLinejoin="round"><path d="M6 2h8l5 5v15H6z" /><path d="M14 2v5h5M9 13h6M9 17h6" /></svg>;
    case 'bolt': return <svg {...p} strokeLinejoin="round"><path d="M13 2L4 14h7l-1 8 9-12h-7z" /></svg>;
    case 'megaphone': return <svg {...p} strokeLinejoin="round"><path d="M3 10v4h4l9 5V5L7 10z" /><path d="M7 14l1.5 6h3L10 15.5M19 9a4 4 0 010 6" /></svg>;
    case 'layout': return <svg {...p} strokeLinecap="round"><rect x="3" y="4" width="18" height="16" rx="2" /><path d="M3 9h18M8 13h8M8 16h5" /></svg>;
    case 'chart': return <svg {...p} strokeLinecap="round"><path d="M4 20V10M10 20V4M16 20v-7M22 20H2" /></svg>;
    case 'star': return <svg {...p} strokeLinejoin="round"><path d="M12 2l2.4 5 5.6.6-4.2 3.8 1.2 5.5L12 14l-5 2.9 1.2-5.5L4 7.6 9.6 7z" /><path d="M12 14v8" /></svg>;
    case 'train': return <svg {...p} strokeLinejoin="round"><path d="M3 9h18v8H3zM8 9V5h8v4M10 5V3h4v2" /><path d="M6 12h3M15 12h3M1 17h2M21 17h2" /><circle cx="7" cy="19" r="2" /><circle cx="17" cy="19" r="2" /></svg>;
    case 'gift': return <svg {...p} strokeLinejoin="round"><rect x="3" y="8" width="18" height="4" /><path d="M5 12v9h14v-9M12 8v13M12 8C10 4 6.5 4 7 6.5S12 8 12 8zM12 8c2-4 5.5-4 5-1.5S12 8 12 8z" /></svg>;
    default: return <svg {...p} strokeLinejoin="round"><path d="M3 6h7l2 2h9v11H3z" /></svg>;
  }
}
