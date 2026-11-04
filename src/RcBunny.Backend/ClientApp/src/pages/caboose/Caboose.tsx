import { Navigate, Route, Routes } from 'react-router';
import AppShell, { type Menu } from '../../Sidebar';
import Dashboard from './Dashboard';
import Payments from './Payments';
import Ledger from './Ledger';
import Refunds from './Refunds';
import Deposits from './Deposits';
import Aging from './Aging';
import Journal from './Journal';
import Channels from './Channels';

const menu: Menu = [
  { heading: 'Finance', links: [['Dashboard', 'dashboard'], ['Payments', 'payments'], ['Credits & Debits', 'ledger'], ['Refunds', 'refunds'], ['Deposits', 'deposits']] },
  { heading: 'Accounting', links: [['Receivables Aging', 'aging'], ['Journal', 'journal']] },
  { heading: 'Tracking', links: [['MSIDs & Promo Codes', 'channels']] },
];

export default function Caboose() {
  return (
    <AppShell app="caboose" menu={menu}>
        <Routes>
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="payments" element={<Payments />} />
          <Route path="ledger" element={<Ledger />} />
          <Route path="refunds" element={<Refunds />} />
          <Route path="deposits" element={<Deposits />} />
          <Route path="aging" element={<Aging />} />
          <Route path="journal" element={<Journal />} />
          <Route path="channels" element={<Channels />} />
        </Routes>
    </AppShell>
  );
}
