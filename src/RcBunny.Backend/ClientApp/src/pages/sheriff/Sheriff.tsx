import { Navigate, Route, Routes } from 'react-router';
import Sidebar, { type Menu } from '../../Sidebar';
import Heartbeat from './Heartbeat';
import Checks from './Checks';
import Timeline from './Timeline';
import Staff from './Staff';
import Roles from './Roles';
import Crons from './Crons';
import Caches from './Caches';
import Markets from './Markets';
import Integrations from './Integrations';

const menu: Menu = [
  { heading: 'Heartbeat', links: [['Overview', 'heartbeat'], ['Checks', 'heartbeat/checks'], ['Control Log', 'heartbeat/log']] },
  { heading: 'Access', links: [['Staff Directory', 'staff'], ['Roles', 'roles']] },
  { heading: 'Config', links: [['Timeline', 'timeline'], ['Crons', 'crons'], ['Caches', 'caches']] },
  { heading: 'Markets', links: [['View Markets', 'markets']] },
  { heading: 'APIs', links: [['Integrations', 'integrations']] },
];

export default function Sheriff() {
  return (
    <div className="with-sidebar">
      <Sidebar app="sheriff" menu={menu} />
      <div className="content">
        <Routes>
          <Route index element={<Navigate to="staff" replace />} />
          <Route path="heartbeat" element={<Heartbeat />} />
          <Route path="heartbeat/checks" element={<Checks />} />
          <Route path="heartbeat/log" element={<Timeline category="heartbeat" title="Control Log" />} />
          <Route path="staff" element={<Staff />} />
          <Route path="roles" element={<Roles />} />
          <Route path="timeline" element={<Timeline />} />
          <Route path="crons" element={<Crons />} />
          <Route path="caches" element={<Caches />} />
          <Route path="markets" element={<Markets />} />
          <Route path="integrations" element={<Integrations />} />
        </Routes>
      </div>
    </div>
  );
}
