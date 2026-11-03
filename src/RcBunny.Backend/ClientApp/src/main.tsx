import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router';
import { api } from './api';
import { AuthProvider, RequirePermission, useAuth } from './auth';
import Login from './pages/Login';
import Home from './pages/Home';
import Corral from './pages/corral/Corral';
import Plans from './pages/Plans';
import Ranger from './pages/ranger/Ranger';
import Sheriff from './pages/sheriff/Sheriff';
import Caboose from './pages/caboose/Caboose';
import Bounty from './pages/bounty/Bounty';
import './styles.css';

const menu = [
  { to: '/corral', label: 'Corral', permission: 'corral' },
  { to: '/plans', label: 'Plans & Rates', permission: 'plans' },
  { to: '/ranger', label: 'Ranger', permission: 'ranger' },
  { to: '/caboose', label: 'Caboose', permission: 'caboose' },
  { to: '/bounty', label: 'Bounty', permission: 'bounty' },
  { to: '/sheriff', label: 'Sheriff', permission: 'sheriff' },
];

function App() {
  const { me, setMe, can } = useAuth();
  if (me === undefined) return <p className="notice">Loading…</p>;
  if (me === null) return <Login />;

  const signOut = () => api.post('/api/logout').then(() => setMe(null));

  return (
    <>
      <header className="top">
        <strong>RC Bunny Admin</strong>
        <nav>
          <NavLink to="/" end>Home</NavLink>
          {menu.filter((m) => can(m.permission)).map((m) => <NavLink key={m.to} to={m.to}>{m.label}</NavLink>)}
        </nav>
        <span className="who">{me.name} · {me.role}</span>
        <button onClick={signOut}>Sign out</button>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/corral/*" element={<RequirePermission permission="corral"><Corral /></RequirePermission>} />
          <Route path="/plans" element={<RequirePermission permission="plans"><Plans /></RequirePermission>} />
          <Route path="/ranger/*" element={<RequirePermission permission="ranger"><Ranger /></RequirePermission>} />
          <Route path="/caboose/*" element={<RequirePermission permission="caboose"><Caboose /></RequirePermission>} />
          <Route path="/bounty/*" element={<RequirePermission permission="bounty"><Bounty /></RequirePermission>} />
          <Route path="/sheriff/*" element={<RequirePermission permission="sheriff"><Sheriff /></RequirePermission>} />
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </main>
    </>
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
