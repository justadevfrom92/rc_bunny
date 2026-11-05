import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router';
import AppShell from './Sidebar';
import { AuthProvider, RequirePermission, useAuth } from './auth';
import Login from './pages/Login';
import Home from './pages/Home';
import Corral from './pages/corral/Corral';
import Plans from './pages/Plans';
import Ranger from './pages/ranger/Ranger';
import Sheriff from './pages/sheriff/Sheriff';
import Caboose from './pages/caboose/Caboose';
import Bounty from './pages/bounty/Bounty';
import Record from './pages/Record';
import './admin.css';
import './styles.css';
import { enhancePages } from './enhance';

// ebunny's admin.js table paging (10 rows at a time)
enhancePages();

function App() {
  const { me } = useAuth();
  if (me === undefined) return <div className="empty">Loading…</div>;
  // Signed out: every page shows the sign-in at /admin/login, then comes back to the page you asked for
  if (me === null) return <Routes><Route path="/login" element={<Login />} /><Route path="*" element={<ToLogin />} /></Routes>;

  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/login" element={<Navigate to={new URLSearchParams(location.search).get('to') ?? '/'} replace />} />
      <Route path="/corral/*" element={<RequirePermission permission="corral"><Corral /></RequirePermission>} />
      <Route path="/astro" element={<Navigate to="/astro/rates" replace />} />
      <Route path="/astro/*" element={<RequirePermission permission="plans"><AppShell app="astro" menu={[{ heading: 'Rates', links: [['View Rates', 'rates']] }]}>
        <Routes><Route path="rates" element={<Plans />} /><Route path="records/:type/:id" element={<Record />} /></Routes></AppShell></RequirePermission>} />
      <Route path="/ranger/*" element={<RequirePermission permission="ranger"><Ranger /></RequirePermission>} />
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
