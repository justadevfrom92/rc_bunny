import { Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import Status, { Run } from './Status';
import Library, { Report, Saved, SavedShow } from './Reports';
import Activity from './Activity';
import Uploads, { Etl, EtlFeed, Upload, UploadForm } from './Files';

// ebunny's config/admin.php → walker menu
const menu: Menu = [
  { heading: 'Reports', links: [['Report Status', '', { also: ['runs'], badge: 'report_problems' }], ['Run a Report', 'reports'], ['Saved Reports', 'saved-reports'],
    ['Orders', 'activity/orders'], ['Notes', 'activity/notes'], ['Phonecalls', 'activity/phonecalls']] },
  { heading: 'Files', links: [['All Files', 'uploads'], ['ETL Feeds', 'etl', { badge: 'etl_failed' }], ['ETL Files', 'files/etl'], ['Legal Documents', 'files/legal'],
    ['Customer Templates', 'files/templates'], ['Finance', 'files/finance'], ['Utility / ERCOT', 'files/ercot'], ['Vendor', 'files/vendor']] },
];

export default function Walker() {
  return (
    <AppShell app="walker" menu={menu}>
      <Routes>
        <Route index element={<Status />} />
        <Route path="runs/:id" element={<Run />} />
        <Route path="activity/orders" element={<Activity report="orders" key="orders" />} />
        <Route path="activity/notes" element={<Activity report="notes" key="notes" />} />
        <Route path="activity/phonecalls" element={<Activity report="phonecalls" key="phonecalls" />} />
        <Route path="reports" element={<Library />} />
        <Route path="reports/:key" element={<Report />} />
        <Route path="saved-reports" element={<Saved />} />
        <Route path="saved-reports/:id" element={<SavedShow />} />
        <Route path="uploads" element={<Uploads />} />
        <Route path="uploads/new" element={<UploadForm />} />
        <Route path="uploads/:id" element={<Upload />} />
        <Route path="files/:folder" element={<Uploads />} />
        <Route path="etl" element={<Etl />} />
        <Route path="etl/:feed" element={<EtlFeed />} />
        <Route path="records/:type/:id" element={<Record />} />
      </Routes>
    </AppShell>
  );
}
