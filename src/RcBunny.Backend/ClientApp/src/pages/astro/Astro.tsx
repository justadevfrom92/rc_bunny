import { Navigate, Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import Plans, { GroupForm, Groups, PlanForm } from './Plans';
import Rates, { Fees, RatesEdit } from './Rates';
import { ByopUpload, DiscountLimits, Etfs, ProductForm, Products, Terms } from './Modifiers';

// ebunny's config/admin.php → astro menu
const menu: Menu = [
  { heading: 'Plans', links: [['View Plans', 'plans'], ['Add a Plan', 'plans/create'], ['Plan Groups', 'groups'], ['Add a Group', 'groups/create']] },
  { heading: 'Rates', links: [['View Rates', 'rates'], ['Update Rates', 'rates/edit'], ['TDSP Fees', 'fees']] },
  { heading: 'Modifiers', links: [['Term', 'terms'], ['ETF', 'etfs'], ['Products', 'modifiers/products']] },
  { heading: 'Build Your Own', links: [['Build Your Own', 'products'], ['New Product', 'products/new'], ['BYOP Discounts', 'uploads/byop']] },
];

export default function Astro() {
  return (
    <AppShell app="astro" menu={menu}>
      <Routes>
        <Route index element={<Navigate to="plans" replace />} />
        <Route path="plans" element={<Plans />} />
        <Route path="plans/create" element={<PlanForm />} />
        <Route path="plans/:id/edit" element={<PlanForm />} />
        <Route path="groups" element={<Groups />} />
        <Route path="groups/create" element={<GroupForm />} />
        <Route path="groups/:id/edit" element={<GroupForm />} />
        <Route path="rates" element={<Rates />} />
        <Route path="rates/edit" element={<RatesEdit />} />
        <Route path="fees" element={<Fees />} />
        <Route path="terms" element={<Terms />} />
        <Route path="etfs" element={<Etfs />} />
        <Route path="modifiers/products" element={<DiscountLimits />} />
        <Route path="products" element={<Products />} />
        <Route path="products/new" element={<ProductForm />} />
        <Route path="products/:id/edit" element={<ProductForm />} />
        <Route path="uploads/byop" element={<ByopUpload />} />
        <Route path="records/:type/:id" element={<Record />} />
      </Routes>
    </AppShell>
  );
}
