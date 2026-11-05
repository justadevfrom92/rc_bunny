import { Navigate, Route, Routes } from 'react-router';
import AppShell, { type Menu } from '../../Sidebar';
import Heartbeat from './Heartbeat';
import Checks from './Checks';
import Timeline from './Timeline';
import Staff, { UserForm, UserHistory, UserShow } from './Staff';
import Roles from './Roles';
import Crons from './Crons';
import Caches from './Caches';
import Markets, { MarketForm } from './Markets';
import ControlLog from './ControlLog';
import Integrations from './Integrations';
import Record from '../Record';

const menu: Menu = [
  { heading: 'Heartbeat', links: [['Overview', 'heartbeat', { badge: 'health_problems' }], ['Checks', 'heartbeat/checks'], ['Control Log', 'heartbeat/log']] },
  { heading: 'Access', links: [['Staff Directory', 'users'], ['Roles', 'roles']] },
  { heading: 'Config', links: [['Timeline', 'timeline'], ['Crons', 'jobs', { badge: 'failed_jobs' }], ['Cached Tables', 'cached-tables']] },
  { heading: 'Markets', links: [['View Markets', 'markets'], ['Add a Market', 'markets/create']] },
  { heading: 'APIs', links: [['All APIs', 'integrations']] },
];

export default function Sheriff() {
  return (
    <AppShell app="sheriff" menu={menu}>
        <Routes>
          <Route index element={<Navigate to="users" replace />} />
          <Route path="heartbeat" element={<Heartbeat />} />
          <Route path="heartbeat/checks" element={<Checks />} />
          <Route path="heartbeat/log" element={<ControlLog />} />
          <Route path="users" element={<Staff />} />
          <Route path="users/new" element={<UserForm />} />
          <Route path="users/:id" element={<UserShow />} />
          <Route path="users/:id/edit" element={<UserForm />} />
          <Route path="users/:id/history" element={<UserHistory />} />
          <Route path="roles" element={<Roles />} />
          <Route path="timeline" element={<Timeline />} />
          <Route path="jobs" element={<Crons />} />
          <Route path="cached-tables" element={<Caches />} />
          <Route path="markets" element={<Markets />} />
          <Route path="markets/create" element={<MarketForm />} />
          <Route path="markets/:id/edit" element={<MarketForm />} />
          <Route path="integrations" element={<Integrations />} />
          <Route path="records/:type/:id" element={<Record />} />
        </Routes>
    </AppShell>
  );
}
