import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router';
import { AuthProvider, RequirePermission, useAuth } from './auth';
import Login from './pages/Login';
import Home from './pages/Home';
import Corral from './pages/corral/Corral';
import Astro from './pages/astro/Astro';
import Lando from './pages/lando/Lando';
import Ranger from './pages/ranger/Ranger';
import Sheriff from './pages/sheriff/Sheriff';
import Caboose from './pages/caboose/Caboose';
import Bounty from './pages/bounty/Bounty';
import Walker from './pages/walker/Walker';
import { AccountShell, File, Files, Notifications, OpenNotification, Settings } from './pages/account/Account';
import './admin.css';
import './styles.css';
import { enhancePages } from './enhance';

// ebunny's admin.js table paging (10 rows at a time)
enhancePages();

// Style Guide changes an employee made, kept in this browser for 5 days and applied to every admin page here (ebunny's partials/head)
try {
  const s = JSON.parse(localStorage.getItem('et-admin-local-styles') || 'null');
  if (s?.vars && s.expires < Date.now()) localStorage.removeItem('et-admin-local-styles');
  else if (s?.vars) for (const [n, v] of Object.entries(s.vars)) document.documentElement.style.setProperty(n, v as string);
} catch { /* storage blocked */ }

function App() {
  const { me } = useAuth();
  if (me === undefined) return <div className="empty">Loading…</div>;
  // Signed out: every page shows the sign-in at /admin/login, then comes back to the page you asked for
  if (me === null) return <Routes><Route path="/login" element={<Login />} /><Route path="*" element={<ToLogin />} /></Routes>;

  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/login" element={<Navigate to={new URLSearchParams(location.search).get('to') ?? '/'} replace />} />
      {/* Your Account area (the user menu): Notifications, My Files and Settings */}
      <Route path="/my/notifications" element={<AccountShell><Notifications /></AccountShell>} />
      <Route path="/my/notifications/:id" element={<AccountShell><OpenNotification /></AccountShell>} />
      <Route path="/my/settings" element={<AccountShell><Settings /></AccountShell>} />
      <Route path="/files" element={<AccountShell><Files /></AccountShell>} />
      <Route path="/files/:id" element={<AccountShell><File /></AccountShell>} />
      <Route path="/corral/*" element={<RequirePermission permission="corral"><Corral /></RequirePermission>} />
      <Route path="/lando/*" element={<RequirePermission permission="lando"><Lando /></RequirePermission>} />
      <Route path="/astro/*" element={<RequirePermission permission="plans"><Astro /></RequirePermission>} />
      <Route path="/ranger/*" element={<RequirePermission permission="ranger"><Ranger /></RequirePermission>} />
      <Route path="/walker/*" element={<RequirePermission permission="walker"><Walker /></RequirePermission>} />
      <Route path="/caboose/*" element={<RequirePermission permission="caboose"><Caboose /></RequirePermission>} />
      <Route path="/bounty/*" element={<RequirePermission permission="bounty"><Bounty /></RequirePermission>} />
      <Route path="/sheriff/*" element={<RequirePermission permission="sheriff"><Sheriff /></RequirePermission>} />
      <Route path="*" element={<div className="launcher"><main className="launcher-main"><div className="panel empty">Page not found. <a href="/admin">Back to the apps</a></div></main></div>} />
    </Routes>
  );
}

/** Signed out on any page: go to /admin/login, remembering where you were going. */
function ToLogin() {
  const { pathname, search } = useLocation();
  return <Navigate to={`/login${pathname === '/' ? '' : `?to=${encodeURIComponent(pathname + search)}`}`} replace />;
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter basename="/admin">
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
);
