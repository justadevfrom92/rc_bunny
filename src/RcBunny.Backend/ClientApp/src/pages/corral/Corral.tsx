import { Navigate, Route, Routes } from 'react-router';
import Sidebar, { type Menu } from '../../Sidebar';
import Customers from './Customers';
import Customer from './Customer';
import EsiidLookup from './EsiidLookup';
import CreateOrder from './CreateOrder';
import Texts from './Texts';
import Calls from './Calls';
import Pipeline from './Pipeline';
import Leads from './Leads';
import Lead from './Lead';
import FollowUps from './FollowUps';
import Partners from './Partners';
import Products from './Products';
import ProductOrders from './ProductOrders';
import Ercot from './Ercot';
import Queues from './Queues';

// The Corral menu, in ebunny's order.
const menu: Menu = [
  { links: [['Customer Search', 'customers'], ['ESIID Lookup', 'esiid'], ['Create Order', 'orders/new'], ['Create Order - Biz', 'orders/new-biz'], ['SMS', 'sms'], ['Phone Calls', 'calls']] },
  { heading: 'CRM', links: [['Pipeline', 'crm'], ['Leads', 'crm/leads'], ['Follow-ups', 'crm/follow-ups'], ['Partners & Brokers', 'crm/partners']] },
  { heading: 'Products', links: [['Products', 'products'], ['Product Orders', 'products/orders']] },
  { links: [['ERCOT', 'ercot']] },
  { heading: 'Exceptions', links: [['Queues', 'queues']] },
];

export default function Corral() {
  return (
    <div className="with-sidebar">
      <Sidebar app="corral" menu={menu} />
      <div className="content">
        <Routes>
          <Route index element={<Navigate to="customers" replace />} />
          <Route path="customers" element={<Customers />} />
          <Route path="customers/:id" element={<Customer />} />
          <Route path="esiid" element={<EsiidLookup />} />
          <Route path="orders/new" element={<CreateOrder business={false} />} />
          <Route path="orders/new-biz" element={<CreateOrder business />} />
          <Route path="sms" element={<Texts />} />
          <Route path="calls" element={<Calls />} />
          <Route path="crm" element={<Pipeline />} />
          <Route path="crm/leads" element={<Leads />} />
          <Route path="crm/leads/:id" element={<Lead />} />
          <Route path="crm/follow-ups" element={<FollowUps />} />
          <Route path="crm/partners" element={<Partners />} />
          <Route path="products" element={<Products />} />
          <Route path="products/orders" element={<ProductOrders />} />
          <Route path="ercot" element={<Ercot />} />
          <Route path="queues" element={<Queues />} />
        </Routes>
      </div>
    </div>
  );
}
