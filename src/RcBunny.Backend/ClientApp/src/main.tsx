import { StrictMode, Suspense, lazy, type ComponentType, type LazyExoticComponent } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Navigate, Outlet, Route, Routes, useParams } from 'react-router-dom';
import { queryClient } from './api';
import { MeProvider, RequirePerm } from './auth';
import { Shell } from './layout/Shell';
import { Login } from './pages/Login';
import { Launcher } from './pages/Launcher';
import { NotPorted } from './pages/NotPorted';
import { Skeleton } from './ui';
import { ResourceDetail, ResourceForm, ResourceList } from './resource/pages';
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
const Caches = lazy(() => import('./pages/sheriff/Caches'));
const AstroPlans = lazy(() => import('./pages/astro/Plans'));

/** A named export from an app module, lazily loaded. */
function lazyNamed<M extends Record<string, unknown>>(load: () => Promise<M>, name: keyof M): LazyExoticComponent<ComponentType> {
  return lazy(() => load().then((m) => ({ default: m[name] as ComponentType })));
}
const corral = () => import('./pages/apps/corral');
const ranger = () => import('./pages/apps/ranger');
const site = () => import('./pages/apps/site');
const money = () => import('./pages/apps/money');
const more = () => import('./pages/apps/more');

const P = {
  Queues: lazyNamed(corral, 'Queues'), Pipeline: lazyNamed(corral, 'Pipeline'), CreateOrder: lazyNamed(corral, 'CreateOrder'),
  RangerDash: lazyNamed(ranger, 'Dashboard'), Tickets: lazyNamed(ranger, 'Tickets'), Ticket: lazyNamed(ranger, 'Ticket'), Satisfaction: lazyNamed(ranger, 'Satisfaction'), Employees: lazyNamed(ranger, 'Employees'),
  LandoDash: lazyNamed(site, 'LandoDashboard'), Health: lazyNamed(site, 'SiteHealth'), Sitemap: lazyNamed(site, 'Sitemap'), Styles: lazyNamed(site, 'StyleGuide'),
  DataIndex: lazyNamed(site, 'DataIndex'), DataTable: lazyNamed(site, 'DataTable'), Rates: lazyNamed(site, 'Rates'), RateEditor: lazyNamed(site, 'RateEditor'), Terms: lazyNamed(site, 'Terms'),
  ReportStatus: lazyNamed(money, 'ReportStatus'), ReportsIndex: lazyNamed(money, 'ReportsIndex'), ReportForm: lazyNamed(money, 'ReportForm'), ReportColumns: lazyNamed(money, 'ReportColumns'),
  RunPage: lazyNamed(money, 'RunPage'), Upload: lazyNamed(money, 'Upload'), Etl: lazyNamed(money, 'Etl'),
  CabooseDash: lazyNamed(money, 'CabooseDashboard'), Deposits: lazyNamed(money, 'Deposits'), Aging: lazyNamed(money, 'Aging'), Journal: lazyNamed(money, 'Journal'),
  BountyDash: lazyNamed(money, 'BountyDashboard'), Drawing: lazyNamed(money, 'Drawing'),
  RodeoDash: lazyNamed(more, 'RodeoDashboard'), NewCampaign: lazyNamed(more, 'NewCampaign'), Emails: lazyNamed(more, 'EmailOverview'), SurveyEmails: lazyNamed(more, 'SurveyEmailsPage'),
  SurveyResults: lazyNamed(more, 'SurveyResults'), TemplateTest: lazyNamed(more, 'TemplateTest'), DeputyDash: lazyNamed(more, 'DeputyDashboard'),
  Heartbeat: lazyNamed(more, 'Heartbeat'), Jobs: lazyNamed(more, 'Jobs'), Apis: lazyNamed(more, 'Apis'), Timeline: lazyNamed(more, 'TimelineAll'),
  Notifications: lazyNamed(more, 'Notifications'), MyFiles: lazyNamed(more, 'MyFiles'), Settings: lazyNamed(more, 'Settings'),
};

const Wait = <div className="panel"><Skeleton rows={8} /></div>;

/** A route that needs an app (or right) and loads its code on demand. */
function Guard({ perm }: { perm?: string }) {
  const body = <Suspense fallback={Wait}><Outlet /></Suspense>;
  return perm ? <RequirePerm perm={perm}>{body}</RequirePerm> : body;
}

/** /:app/r/... and /:app/data/... — guarded by the app in the address (the server checks the screen's own app too). */
function AppGuard() {
  const { app = '' } = useParams();
  return <Guard perm={app} />;
}

function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<MeProvider><Shell /></MeProvider>}>
        <Route index element={<Launcher />} />
        <Route path=":app/r" element={<AppGuard />}>
          <Route path=":key" element={<ResourceList />} />
          <Route path=":key/new" element={<ResourceForm />} />
          <Route path=":key/:id" element={<ResourceDetail />} />
          <Route path=":key/:id/edit" element={<ResourceForm />} />
        </Route>
        <Route path=":app/data" element={<AppGuard />}>
          <Route path=":key" element={<P.DataTable />} />
        </Route>

        <Route path="account" element={<Guard />}>
          <Route index element={<Navigate to="notifications" replace />} />
          <Route path="notifications" element={<P.Notifications />} />
          <Route path="files" element={<P.MyFiles />} />
          <Route path="settings" element={<P.Settings />} />
        </Route>

        <Route path="corral" element={<Guard perm="corral" />}>
          <Route index element={<Navigate to="customers" replace />} />
          <Route path="customers" element={<CustomerSearch />} />
          <Route path="customers/:id" element={<CustomerDetail />} />
          <Route path="esiid" element={<EsiidLookup />} />
          <Route path="ercot" element={<Navigate to="/corral/r/ercot-transactions" replace />} />
          <Route path="queues" element={<P.Queues />} />
          <Route path="crm" element={<P.Pipeline />} />
          <Route path="orders/new" element={<P.CreateOrder />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="ranger" element={<Guard perm="ranger" />}>
          <Route index element={<P.RangerDash />} />
          <Route path="tickets" element={<P.Tickets />} />
          <Route path="tickets/:id" element={<P.Ticket />} />
          <Route path="satisfaction" element={<P.Satisfaction />} />
          <Route path="employees" element={<P.Employees />} />
          <Route path="messages" element={<Navigate to="/ranger/r/contact-messages" replace />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="lando" element={<Guard perm="lando" />}>
          <Route index element={<P.LandoDash />} />
          <Route path="health" element={<P.Health />} />
          <Route path="sitemap" element={<P.Sitemap />} />
          <Route path="styles" element={<P.Styles />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="astro" element={<Guard perm="astro" />}>
          <Route index element={<Navigate to="plans" replace />} />
          <Route path="plans" element={<AstroPlans />} />
          <Route path="rates" element={<P.Rates />} />
          <Route path="rates/edit" element={<P.RateEditor />} />
          <Route path="terms" element={<P.Terms />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="walker" element={<Guard perm="walker" />}>
          <Route index element={<P.ReportStatus />} />
          <Route path="reports" element={<P.ReportsIndex />} />
          <Route path="reports/:key" element={<P.ReportForm />} />
          <Route path="reports/:key/columns" element={<P.ReportColumns />} />
          <Route path="runs/:id" element={<P.RunPage />} />
          <Route path="upload" element={<P.Upload />} />
          <Route path="etl" element={<P.Etl />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="caboose" element={<Guard perm="caboose" />}>
          <Route index element={<P.CabooseDash />} />
          <Route path="deposits" element={<P.Deposits />} />
          <Route path="aging" element={<P.Aging />} />
          <Route path="journal" element={<P.Journal />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="bounty" element={<Guard perm="bounty" />}>
          <Route index element={<P.BountyDash />} />
          <Route path="drawing" element={<P.Drawing />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="rodeo" element={<Guard perm="rodeo" />}>
          <Route index element={<P.RodeoDash />} />
          <Route path="campaigns/new" element={<P.NewCampaign />} />
          <Route path="emails" element={<P.Emails />} />
          <Route path="survey-emails" element={<P.SurveyEmails />} />
          <Route path="surveys/:id/results" element={<P.SurveyResults />} />
          <Route path="templates/:id/test" element={<P.TemplateTest />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="deputy" element={<Guard perm="deputy" />}>
          <Route index element={<P.DeputyDash />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

        <Route path="sheriff" element={<Guard perm="sheriff" />}>
          <Route index element={<Navigate to="users" replace />} />
          <Route path="users" element={<StaffDirectory />} />
          <Route path="users/new" element={<StaffEdit />} />
          <Route path="users/:id" element={<StaffProfile />} />
          <Route path="users/:id/edit" element={<StaffEdit />} />
          <Route path="roles" element={<Roles />} />
          <Route path="timeline" element={<P.Timeline />} />
          <Route path="cache" element={<Caches />} />
          <Route path="heartbeat" element={<P.Heartbeat />} />
          <Route path="jobs" element={<P.Jobs />} />
          <Route path="apis" element={<P.Apis />} />
          <Route path="sitemap" element={<P.Sitemap />} />
          <Route path="data" element={<P.DataIndex />} />
          <Route path="*" element={<NotPorted />} />
        </Route>

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
