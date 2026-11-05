import { Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import Dashboard from './Dashboard';
import Payments, { PaymentForm } from './Payments';
import Ledger, { LedgerShow } from './Ledger';
import Refunds, { RefundForm, RefundShow } from './Refunds';
import Deposits from './Deposits';
import Aging from './Aging';
import Journal from './Journal';
import Channels, { ChannelForm, PromoForm } from './Channels';

const menu: Menu = [
  { heading: 'Finance', links: [['Dashboard', ''], ['Payments', 'payments'], ['Credits & Debits', 'credits-debits', { badge: 'ledger_pending' }], ['Refunds', 'refunds', { badge: 'refunds_pending' }], ['Deposits', 'deposits']] },
  { heading: 'Accounting', links: [['Receivables Aging', 'aging'], ['Journal', 'journal']] },
  { heading: 'Tracking', links: [['MSIDs & Promo Codes', 'channels', { also: ['promos'] }]] },
];

export default function Caboose() {
  return (
    <AppShell app="caboose" menu={menu}>
        <Routes>
          <Route index element={<Dashboard />} />
          <Route path="payments" element={<Payments />} />
          <Route path="payments/new" element={<PaymentForm />} />
          <Route path="credits-debits" element={<Ledger />} />
          <Route path="credits-debits/:id" element={<LedgerShow />} />
          <Route path="refunds" element={<Refunds />} />
          <Route path="refunds/new" element={<RefundForm />} />
          <Route path="refunds/:id" element={<RefundShow />} />
          <Route path="deposits" element={<Deposits />} />
          <Route path="aging" element={<Aging />} />
          <Route path="journal" element={<Journal />} />
          <Route path="channels" element={<Channels />} />
          <Route path="channels/new" element={<ChannelForm />} />
          <Route path="channels/:id/edit" element={<ChannelForm />} />
          <Route path="promos/new" element={<PromoForm />} />
          <Route path="promos/:id/edit" element={<PromoForm />} />
          <Route path="records/:type/:id" element={<Record />} />
        </Routes>
    </AppShell>
  );
}
