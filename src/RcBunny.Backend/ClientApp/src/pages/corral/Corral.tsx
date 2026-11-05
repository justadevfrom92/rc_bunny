import { Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import Customers from './Customers';
import Customer from './Customer';
import { ActionForm, ApiLog, ContactLog, History, PanelLog, Stars } from './AccountPages';
import FilePage from './FilePage';
import EsiidLookup from './EsiidLookup';
import CreateOrder from './CreateOrder';
import { Sms, SmsMessage, SmsNew } from './Sms';
import { Calls, CallTranscript, NewContact } from './Calls';
import Ercot from './Ercot';
import { Queue, Queues } from './Queues';
import { OrderFor, Product, ProductForm, ProductOrder, ProductOrders, Products } from './Shop';
import { FollowUps, Lead, LeadForm, Leads, Partner, PartnerForm, Partners, Pipeline } from './Crm';

// The Corral menu, in ebunny's order. Exceptions lists every work queue.
const menu: Menu = [
  { links: [['Customer Search', ''], ['ESIID Lookup', 'esiid'], ['Create Order', 'orders/create'], ['Create Order - Biz', 'orders/create-biz'], ['SMS', 'sms', { badge: 'sms' }], ['Phone Calls', 'calls']] },
  { heading: 'CRM', links: [['Pipeline', 'crm'], ['Leads', 'crm/leads'], ['Follow-ups', 'crm/follow-ups', { badge: 'crm_due' }], ['Partners & Brokers', 'crm/partners']] },
  { heading: 'Products', links: [['Products', 'products'], ['New Product', 'products/new'], ['Product Orders', 'products/orders']] },
  { links: [['ERCOT', 'ercot']] },
  {
    heading: 'Exceptions',
    links: [['All Exceptions', 'queues', { badge: 'queues' }], ...([
      ['ERCOT Exceptions', 'ercot-exceptions'], ['Duplicate IPs', 'duplicate-ips'], ['Duplicated Payments', 'duplicated-payments'],
      ['Unapplied Deposits', 'unapplied-deposits'], ['Unapplied Payments', 'unapplied-payments'], ['Unapplied Credits', 'unapplied-credits'],
      ['Unapplied Debits', 'unapplied-debits'], ['Unapplied Plan Changes', 'unapplied-plan-changes'], ['Unapplied Transfers', 'unapplied-transfers'],
      ['Unbilled Orders', 'unbilled-orders'], ['Unprocessed Autopays', 'unprocessed-autopays'], ['Unprocessed Bills', 'unprocessed-bills'],
      ['Unprocessed Orders', 'unprocessed-orders'],
    ] as [string, string][]).map(([label, key]) => [label, `queues/${key}`, { badge: `queue:${key}` }] as [string, string, { badge: string }])],
  },
];

export default function Corral() {
  return (
    <AppShell app="corral" menu={menu}>
        <Routes>
          <Route index element={<Customers />} />
          <Route path="customers/:id" element={<Customer />} />
          <Route path="customers/:id/actions/:key" element={<ActionForm />} />
          <Route path="customers/:id/panel-log/:panel" element={<PanelLog />} />
          <Route path="customers/:id/contact-log" element={<ContactLog />} />
          <Route path="customers/:id/api-log" element={<ApiLog />} />
          <Route path="customers/:id/stars" element={<Stars />} />
          <Route path="customers/:id/ticket" element={<History />} />
          <Route path="customers/:id/logs/:log" element={<History />} />
          <Route path="customers/:id/bills/:bill/ticket" element={<History />} />
          <Route path="customers/:id/order-products" element={<OrderFor />} />
          <Route path="files/:id/page" element={<FilePage />} />
          <Route path="esiid" element={<EsiidLookup />} />
          <Route path="orders/create" element={<CreateOrder business={false} />} />
          <Route path="orders/create-biz" element={<CreateOrder business />} />
          <Route path="sms" element={<Sms />} />
          <Route path="sms/new" element={<SmsNew />} />
          <Route path="sms/:id" element={<SmsMessage />} />
          <Route path="calls" element={<Calls />} />
          <Route path="calls/:id/transcript" element={<CallTranscript />} />
          <Route path="contacts/new" element={<NewContact />} />
          <Route path="crm" element={<Pipeline />} />
          <Route path="crm/leads" element={<Leads />} />
          <Route path="crm/leads/new" element={<LeadForm />} />
          <Route path="crm/leads/:id" element={<Lead />} />
          <Route path="crm/leads/:id/edit" element={<LeadForm />} />
          <Route path="crm/follow-ups" element={<FollowUps />} />
          <Route path="crm/partners" element={<Partners />} />
          <Route path="crm/partners/new" element={<PartnerForm />} />
          <Route path="crm/partners/:id" element={<Partner />} />
          <Route path="crm/partners/:id/edit" element={<PartnerForm />} />
          <Route path="products" element={<Products />} />
          <Route path="products/new" element={<ProductForm />} />
          <Route path="products/orders" element={<ProductOrders />} />
          <Route path="products/orders/:id" element={<ProductOrder />} />
          <Route path="products/:id" element={<Product />} />
          <Route path="ercot" element={<Ercot />} />
          <Route path="queues" element={<Queues />} />
          <Route path="queues/:key" element={<Queue />} />
          <Route path="records/:type/:id" element={<Record />} />
        </Routes>
    </AppShell>
  );
}
