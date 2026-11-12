import { Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import { Campaign, Campaigns, Dashboard, Heatmap } from './Campaigns';

// ebunny's config/admin.php → rodeo menu
const menu: Menu = [
  { heading: 'Marketing', links: [['Dashboard', ''], ['Campaigns', 'campaigns']] },
];

export default function Rodeo() {
  return (
    <AppShell app="rodeo" menu={menu}>
      <Routes>
        <Route index element={<Dashboard />} />
        <Route path="campaigns" element={<Campaigns />} />
        <Route path="campaigns/new" element={<Campaign key="new" />} />
        <Route path="campaigns/:id" element={<Campaign />} />
        <Route path="campaigns/:id/heatmap" element={<Heatmap kind="campaigns" />} />
        <Route path="templates/:id/heatmap" element={<Heatmap kind="templates" />} />
        <Route path="records/:type/:id" element={<Record />} />
      </Routes>
    </AppShell>
  );
}
