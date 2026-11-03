import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { queryClient } from './api';
import { MeProvider, RequirePerm } from './auth';
import { Shell } from './layout/Shell';
import { Login } from './pages/Login';
import { Launcher } from './pages/Launcher';
import { NotPorted } from './pages/NotPorted';
import { Skeleton } from './ui';
import './tokens.css';
import './admin.css';

// Each app's screens are their own chunk: a CSR who only has Corral never downloads Sheriff's code.
const CustomerSearch = lazy(() => import('./pages/corral/CustomerSearch'));
const CustomerDetail = lazy(() => import('./pages/corral/CustomerDetail'));
const EsiidLookup = lazy(() => import('./pages/corral/EsiidLookup'));
const StaffDirectory = lazy(() => import('./pages/sheriff/StaffDirectory'));
const StaffEdit = lazy(() => import('./pages/sheriff/StaffEdit'));
const StaffProfile = lazy(() => import('./pages/sheriff/StaffProfile'));
const Roles = lazy(() => import('./pages/sheriff/Roles'));
const Timeline = lazy(() => import('./pages/sheriff/Timeline'));
const Caches = lazy(() => import('./pages/sheriff/Caches'));
const WebMessages = lazy(() => import('./pages/ranger/WebMessages'));
const AstroPlans = lazy(() => import('./pages/astro/Plans'));

/** A route that needs an app (or right) and loads its code on demand. */
function Guard({ perm }: { perm: string }) {
  return (
    <RequirePerm perm={perm}>
      <Suspense fallback={<div className="panel"><Skeleton rows={8} /></div>}>
        <Outlet />
      </Suspense>
    </RequirePerm>
  );
}

function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<MeProvider><Shell /></MeProvider>}>
        <Route index element={<Launcher />} />

        <Route path="corral" element={<Guard perm="corral" />}>
          <Route index element={<Navigate to="customers" replace />} />
          <Route path="customers" element={<CustomerSearch />} />
          <Route path="customers/:id" element={<CustomerDetail />} />
          <Route path="esiid" element={<EsiidLookup />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="sheriff" element={<Guard perm="sheriff" />}>
          <Route index element={<Navigate to="users" replace />} />
          <Route path="users" element={<StaffDirectory />} />
          <Route path="users/new" element={<StaffEdit />} />
          <Route path="users/:id" element={<StaffProfile />} />
          <Route path="users/:id/edit" element={<StaffEdit />} />
          <Route path="roles" element={<Roles />} />
          <Route path="timeline" element={<Timeline />} />
          <Route path="cache" element={<Caches />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="ranger" element={<Guard perm="ranger" />}>
          <Route path="messages" element={<WebMessages />} />
          <Route path="*" element={<NotPorted />} />
          <Route index element={<NotPorted />} />
        </Route>

        <Route path="astro" element={<Guard perm="astro" />}>
          <Route index element={<Navigate to="plans" replace />} />
          <Route path="plans" element={<AstroPlans />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        {['lando', 'walker', 'caboose', 'bounty', 'rodeo', 'deputy'].map((app) => (
          <Route key={app} path={app} element={<Guard perm={app} />}>
            <Route index element={<NotPorted />} />
            <Route path="*" element={<NotPorted />} />
          </Route>
        ))}

        <Route path="*" element={<div className="panel empty"><h2>Page not found</h2></div>} />
      </Route>
    </Routes>
  );
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
