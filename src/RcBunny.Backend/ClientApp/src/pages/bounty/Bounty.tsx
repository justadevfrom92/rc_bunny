import { Route, Routes } from 'react-router';
import AppShell, { type Menu } from '../../Sidebar';
import Dashboard from './Dashboard';
import Offers from './Offers';
import Rules from './Rules';
import Redemptions from './Redemptions';
import Members from './Members';
import Drawing, { Draw, Winner } from './Drawing';
import Winners from './Winners';
import Record from '../Record';

const menu: Menu = [
  { heading: 'Rewards', links: [['Dashboard', ''], ['Offers', 'offers'], ['Earning Rules', 'rules']] },
  { heading: 'Members', links: [['Redemptions', 'redemptions'], ['Members', 'members'], ['Monthly Drawing', 'drawing'], ['Drawing Winners', 'winners']] },
];

export default function Bounty() {
  return (
    <AppShell app="bounty" menu={menu}>
        <Routes>
          <Route index element={<Dashboard />} />
          <Route path="offers" element={<Offers />} />
          <Route path="rules" element={<Rules />} />
          <Route path="redemptions" element={<Redemptions />} />
          <Route path="members" element={<Members />} />
          <Route path="drawing" element={<Drawing />} />
          <Route path="drawing/draw" element={<Draw />} />
          <Route path="winners" element={<Winners />} />
          <Route path="winners/:id" element={<Winner />} />
          <Route path="records/:type/:id" element={<Record />} />
        </Routes>
    </AppShell>
  );
}
