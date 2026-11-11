import { Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import Dashboard, { BlockForm, Blocked, Health, Visitors } from './Site';
import Consents, { Consent } from './Consents';
import Pages, { Activity, PageForm } from './Pages';
import { BlockForm as ContentBlockForm, Blocks, CategoryForm, TemplateForm, Templates } from './Content';
import Styles from './Styles';

// ebunny's config/admin.php → lando menu
const menu: Menu = [
  { heading: 'Site', links: [['Dashboard', ''], ['Site Health', 'health'], ['Visitors', 'visitors'], ['Blocked Areas', 'blocked-areas'], ['Blocked IPs', 'blocked-ips'], ['Cookie Consents', 'cookie-consents']] },
  { heading: 'Pages', links: [['List Pages', 'pages'], ['Add a Page', 'pages/create'], ['Page Templates', 'templates']] },
  { heading: 'Content', links: [['Content Blocks', 'blocks', { also: ['block-categories'] }], ['Add a Content Block', 'blocks/create']] },
  { heading: 'Design', links: [['Style Guide', 'styles']] },
];

export default function Lando() {
  return (
    <AppShell app="lando" menu={menu}>
      <Routes>
        <Route index element={<Dashboard />} />
        <Route path="health" element={<Health />} />
        <Route path="visitors" element={<Visitors />} />
        <Route path="cookie-consents" element={<Consents />} />
        <Route path="cookie-consents/:id" element={<Consent />} />
        {(['ips', 'areas'] as const).map((k) => [
          <Route key={k} path={`blocked-${k}`} element={<Blocked kind={k} key={k} />} />,
          <Route key={k + 'n'} path={`blocked-${k}/new`} element={<BlockForm kind={k} key={k + 'n'} />} />,
          <Route key={k + 'e'} path={`blocked-${k}/:id/edit`} element={<BlockForm kind={k} key={k + 'e'} />} />,
        ])}
        <Route path="pages" element={<Pages />} />
        <Route path="pages/create" element={<PageForm />} />
        <Route path="pages/:id/edit" element={<PageForm />} />
        <Route path="pages/:id/activity" element={<Activity />} />
        <Route path="templates" element={<Templates />} />
        <Route path="templates/create" element={<TemplateForm />} />
        <Route path="templates/:id/edit" element={<TemplateForm />} />
        <Route path="blocks" element={<Blocks />} />
        <Route path="blocks/create" element={<ContentBlockForm />} />
        <Route path="blocks/:id/edit" element={<ContentBlockForm />} />
        <Route path="block-categories/new" element={<CategoryForm />} />
        <Route path="styles" element={<Styles />} />
        <Route path="records/:type/:id" element={<Record />} />
      </Routes>
    </AppShell>
  );
}
