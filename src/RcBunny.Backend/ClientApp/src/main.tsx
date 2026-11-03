import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router';
import { api } from './api';
import { AuthProvider, RequirePermission, useAuth } from './auth';
import Login from './pages/Login';
import Home from './pages/Home';
import Corral from './pages/corral/Corral';
import Plans from './pages/Plans';
import Messages from './pages/Messages';
import Staff from './pages/Staff';
import './styles.css';

const menu = [
  { to: '/corral', label: 'Corral', permission: 'corral' },
  { to: '/plans', label: 'Plans & Rates', permission: 'plans' },
  { to: '/messages', label: 'Web Messages', permission: 'messages' },
  { to: '/staff', label: 'Staff', permission: 'staff' },
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
          <Route path="/messages" element={<RequirePermission permission="messages"><Messages /></RequirePermission>} />
          <Route path="/staff" element={<RequirePermission permission="staff"><Staff /></RequirePermission>} />
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
