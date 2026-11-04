import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
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

function App() {
  const { me } = useAuth();
  if (me === undefined) return <div className="empty">Loading…</div>;
  if (me === null) return <Login />;

  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/corral/*" element={<RequirePermission permission="corral"><Corral /></RequirePermission>} />
      <Route path="/plans" element={<RequirePermission permission="plans"><AppShell app="plans" menu={[{ heading: 'Rates', links: [['View Rates', '']] }]}><Plans /></AppShell></RequirePermission>} />
      <Route path="/ranger/*" element={<RequirePermission permission="ranger"><Ranger /></RequirePermission>} />
      <Route path="/caboose/*" element={<RequirePermission permission="caboose"><Caboose /></RequirePermission>} />
      <Route path="/bounty/*" element={<RequirePermission permission="bounty"><Bounty /></RequirePermission>} />
      <Route path="/sheriff/*" element={<RequirePermission permission="sheriff"><Sheriff /></RequirePermission>} />
      <Route path="/records/:type/:id" element={<AppShell app="corral" menu={[]}><Record /></AppShell>} />
      <Route path="*" element={<Navigate to="/" />} />
    </Routes>
  );
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
);
