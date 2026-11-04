import { Navigate, Route, Routes } from 'react-router';
import AppShell, { type Menu } from '../../Sidebar';
import Dashboard from './Dashboard';
import Offers from './Offers';
import Rules from './Rules';
import Redemptions from './Redemptions';
import Members from './Members';
import Member from './Member';
import Drawing from './Drawing';
import Winners from './Winners';

const menu: Menu = [
  { heading: 'Rewards', links: [['Dashboard', 'dashboard'], ['Offers', 'offers'], ['Earning Rules', 'rules']] },
  { heading: 'Members', links: [['Redemptions', 'redemptions'], ['Members', 'members'], ['Monthly Drawing', 'drawing'], ['Drawing Winners', 'winners']] },
];

export default function Bounty() {
  return (
    <AppShell app="bounty" menu={menu}>
        <Routes>
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="offers" element={<Offers />} />
          <Route path="rules" element={<Rules />} />
          <Route path="redemptions" element={<Redemptions />} />
          <Route path="members" element={<Members />} />
          <Route path="members/:id" element={<Member />} />
          <Route path="drawing" element={<Drawing />} />
          <Route path="winners" element={<Winners />} />
        </Routes>
    </AppShell>
  );
}
