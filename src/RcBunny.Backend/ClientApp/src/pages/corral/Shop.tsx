import { useState, type FormEvent, type ReactNode } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, money, qs, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, PagerBar, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const orderTone = (s: string) => ({ Delivered: 'ok', Canceled: 'bad', Shipped: 'info' } as Record<string, string>)[s] ?? 'warn';
const items = (o: any) => JSON.parse(o.items) as any[];

/** Corral → Products (ebunny's corral/shop/index): everything in the website Shop. */
export function Products() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const cat = params.get('cat') ?? '', page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/shop/products?${qs({ cat, page })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const count = (k: string) => data.counts.find((c: any) => c.cat === k)?.n ?? 0;
  const total = data.counts.reduce((t: number, c: any) => t + c.n, 0);
  return (
    <>
      <PageHead title="Products" sub="Everything in the website Shop. Open a product to edit it or order it for a customer."
        actions={<><Link className="btn ghost" to="/corral/products/orders">Product Orders</Link><Link className="btn cyan" to="/corral/products/new">New Product</Link></>} />
      <select className="pill-select" aria-label="Category" style={{ marginBottom: 18 }} value={cat} onChange={(e) => navigate(`/corral/products${e.target.value ? `?cat=${e.target.value}` : ''}`)}>
        <option value="">All categories · {total}</option>
        {Object.entries(data.categories).map(([k, label]) => <option key={k} value={k}>{label as string} · {count(k)}</option>)}
      </select>
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>Product</th><th>Category</th><th className="num">Price</th><th className="num">Stars</th><th>Stock</th><th>Shop</th><th className="num">Sold</th></tr></thead>
        <tbody>
          {data.products.map((p: any) => {
            const monthly = data.protection.includes(p.sku);
            return (
              <tr key={p.id} className="click" onClick={() => navigate(`/corral/products/${p.id}`)}><td className="nowrap"><Link to={`/corral/products/${p.id}`}><b>#{p.id}</b></Link></td>
                <td><b>{p.name}</b><div className="help mono">{p.sku}</div></td><td>{data.categories[p.category] ?? p.category}</td>
                <td className="num nowrap">{money(p.price)}{monthly ? <div className="help">{money(p.price / 12)}/mo on the bill</div> : p.was ? <div className="help"><s>{money(p.was)}</s></div> : null}</td>
                <td className="num">{p.stars.toLocaleString()}</td>
                <td><span className={`pill ${p.inStock ? 'ok' : 'bad'}`}>{p.inStock ? 'In stock' : 'Out of stock'}</span></td>
                <td><span className={`pill ${p.active ? 'ok' : ''}`}>{p.active ? 'Shown' : 'Hidden'}</span></td>
                <td className="num">{(data.sold[p.sku] ?? 0).toLocaleString()}</td></tr>
            );
          })}
          {data.products.length === 0 && <EmptyRow cols={8}>No products{cat ? ' in this category' : ''}.</EmptyRow>}
        </tbody>
      </table></div><PagerBar page={page} total={data.total} size={10} onPage={(p) => navigate(`/corral/products?${qs({ cat, page: p })}`)} /></div>
    </>
  );
}

/** New Product / Edit (ebunny's corral/shop/form). */
export function ProductForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data } = useLoad<any>(id ? `/api/corral/shop/products/${id}` : '/api/corral/shop/products?page=1');
  const [run, bar] = useRun();
  if (!data) return <div className="empty">Loading…</div>;
  const p = id ? data.product : { category: Object.keys(data.categories)[0], stars: 0, inStock: true, active: true, icon: data.icons[0] };
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { sku: f.get('sku'), name: f.get('name'), category: f.get('cat'), price: Number(f.get('price')), was: f.get('was') ? Number(f.get('was')) : null,
      stars: Number(f.get('stars')), badge: f.get('badge'), icon: f.get('icon'), blurb: f.get('blurb'), inStock: f.get('stock') === 'on', active: f.get('active') === 'on' };
    run((id ? api.put<any>(`/api/corral/shop/products/${id}`, body) : api.post<any>('/api/corral/shop/products', body)).then((r) => { navigate(`/corral/products/${r.id}`); return r; }));
  };
  return (
    <>
      <PageHead title={id ? `Edit ${p.name}` : 'New Product'} sub="A product in the website Shop. Protection plans (PR-…) are billed monthly; everything else is charged when ordered."
        actions={<Link className="btn ghost btn-back" to={id ? `/corral/products/${id}` : '/corral/products'}>Back</Link>} />
      {bar}
      <form className="panel" onSubmit={save}>
        <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={120} defaultValue={p.name} />
          <label htmlFor="sku">SKU</label><input id="sku" name="sku" required maxLength={20} defaultValue={p.sku} placeholder="e.g. PL-260" />
          <label htmlFor="cat">Category</label><select id="cat" name="cat" defaultValue={p.category}>{Object.entries(data.categories).map(([k, label]) => <option key={k} value={k}>{label as string}</option>)}</select>
          <label htmlFor="price">Price ($)</label><input id="price" type="number" step="0.01" min={0} name="price" required defaultValue={p.price} />
          <label htmlFor="was">Was ($, for a sale)</label><input id="was" type="number" step="0.01" min={0} name="was" defaultValue={p.was ?? ''} placeholder="leave empty if not on sale" />
          <label htmlFor="stars">Rewards stars earned</label><input id="stars" type="number" min={0} name="stars" required defaultValue={p.stars ?? 0} />
          <label htmlFor="badge">Badge</label><input id="badge" name="badge" maxLength={40} defaultValue={p.badge ?? ''} placeholder="e.g. Best Seller" />
          <label htmlFor="icon">Picture</label><select id="icon" name="icon" defaultValue={p.icon}>{data.icons.map((i: string) => <option key={i}>{i}</option>)}</select>
          <label htmlFor="blurb">Description</label><textarea id="blurb" name="blurb" maxLength={300} style={{ minHeight: 70 }} defaultValue={p.blurb ?? ''} />
          <label htmlFor="stock">Stock</label><label className="check"><input id="stock" type="checkbox" name="stock" defaultChecked={p.inStock} /> In stock</label>
          <label htmlFor="active">Website</label><label className="check"><input id="active" type="checkbox" name="active" defaultChecked={p.active} /> Show in the Shop</label>
        </div>
        <div className="panel-body"><button className="btn cyan">{id ? 'Save Product' : 'Add Product'}</button></div>
      </form>
    </>
  );
}

/** One product (ebunny's corral/shop/show): what the website shows, Order for a Customer, and its recent orders. */
export function Product() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/corral/shop/products/${id}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const p = data.product, monthly = data.protection.includes(p.sku);
  return (
    <>
      <PageHead title={p.name} sub={<>Product #{p.id} · <span className="mono">{p.sku}</span> · {data.categories[p.category] ?? p.category}</>}
        actions={<><Link className="btn ghost btn-back" to="/corral/products">Back to Products</Link><Link className="btn cyan" to={`/corral/products/${p.id}/edit`}>Edit Product</Link></>} />
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <div className="grid-4 stat-row">
          <div className="stat"><span>Price</span><strong>{money(p.price)}</strong><small>{monthly ? `${money(p.price / 12)}/mo on the electricity bill` : p.was ? `was ${money(p.was)}` : 'charged today'}</small></div>
          <div className="stat"><span>Rewards Stars</span><strong>{p.stars.toLocaleString()}</strong><small>each</small></div>
          <div className={`stat${p.inStock ? '' : ' alert'}`}><span>Stock</span><strong style={{ fontSize: '1.25rem' }}>{p.inStock ? 'In stock' : 'Out of stock'}</strong></div>
          <div className="stat"><span>Sold</span><strong>{(data.sold ?? 0).toLocaleString()}</strong><small>all orders</small></div>
        </div>
        <div className="panel"><div className="panel-head"><h2>On the Website</h2><span className={`pill ${p.active ? 'ok' : ''}`}>{p.active ? 'Shown in the Shop' : 'Hidden from the Shop'}</span></div><div className="panel-body"><dl className="kv">
          <dt>Name</dt><dd>{p.name}</dd>
          <dt>Description</dt><dd>{p.blurb || '—'}</dd>
          <dt>Badge</dt><dd>{p.badge || '—'}</dd>
          <dt>Rating</dt><dd>{Number(p.rating).toFixed(1)} ★ · {Number(p.reviews).toLocaleString()} reviews</dd>
          <dt>Category</dt><dd>{data.categories[p.category] ?? p.category}</dd>
        </dl></div></div>
        <CustomerFinder id="order-for" title="Order for a Customer" help="Find the customer, then start the order with this product in it, like the customer adding it to their cart on the website."
          button={(c) => <Link className="btn sm cyan" to={`/corral/customers/${c.id}/order-products?sku=${p.sku}`}>Start order with this customer</Link>} />
        <div className="panel"><div className="panel-head"><h2>Recent Orders</h2><Link className="btn sm ghost" to="/corral/products/orders">All Orders</Link></div><div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Order</th><th>Placed</th><th>Account</th><th className="num">Qty</th><th>Status</th></tr></thead>
          <tbody>
            {data.orders.map((o: any) => (
              <tr key={o.id} className="click" onClick={() => navigate(`/corral/products/orders/${o.id}`)}><td className="nowrap"><Link to={`/corral/products/orders/${o.id}`}><b>#{o.id}</b></Link></td><td className="mono">{o.number}</td><td className="nowrap">{dateTime(o.createdAt)}</td>
                <td onClick={(e) => e.stopPropagation()}><AccountNo id={o.customerId} account={o.account} /><div className="help">{o.customer}</div></td>
                <td className="num">{o.qty ?? '—'}</td><td><span className={`pill ${orderTone(o.status)}`}>{o.status}</span></td></tr>
            ))}
            {data.orders.length === 0 && <EmptyRow cols={6}>Not ordered yet.</EmptyRow>}
          </tbody>
        </table></div></div>
      </div>
    </>
  );
}

/** ebunny's partials/customer-finder: search for a customer, then a button for each match. */
function CustomerFinder({ id, title, help, button }: { id: string; title: string; help: string; button: (c: any) => ReactNode }) {
  const [q, setQ] = useState<string | null>(null);
  const { data } = useLoad<any>(q ? `/api/customers?${qs({ q, limit: 10 })}` : null);
  return (
    <div className="panel customer-finder" id={id}>
      <div className="panel-head"><h2>{title}</h2></div>
      <div className="panel-body">
        <form className="adv-bar" role="search" onSubmit={(e) => { e.preventDefault(); setQ(String(new FormData(e.currentTarget).get('find') ?? '')); }}>
          <label className="search-field"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></svg>
            <input type="search" name="find" defaultValue={q ?? ''} placeholder="Name, last name, phone, account number, email or address" aria-label="Find a customer" /></label>
          <button className="btn cyan">Search</button>
        </form>
        <p className="help" style={{ marginTop: 8 }}>{help}</p>
      </div>
      {q !== null && data && (
        <div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Account</th><th>Name</th><th>Phone</th><th>Email</th><th>Status</th><th><span className="sr-only">Action</span></th></tr></thead>
          <tbody>
            {data.rows.map((c: any) => (
              <tr key={c.id}><td className="nowrap"><Link to={`/corral/customers/${c.id}`}><b>#{c.id}</b></Link></td><td><AccountNo id={c.id} account={c.account} /></td>
                <td><b>{c.name}</b><div className="help">{c.city}</div></td><td className="nowrap">{c.phone}</td><td className="wrap">{c.email}</td><td>{c.status}</td>
                <td className="nowrap" style={{ textAlign: 'right' }}>{button(c)}</td></tr>
            ))}
            {data.rows.length === 0 && <EmptyRow cols={7}>No customers match “{q}”.</EmptyRow>}
          </tbody>
        </table></div>
      )}
    </div>
  );
}

/** Corral → Product Orders (ebunny's corral/shop/orders). */
export function ProductOrders() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const status = params.get('status') ?? '', page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/shop/orders?${qs({ status, page })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const list = (o: any) => { const s = items(o).map((i) => `${i.qty}× ${i.name}`).join(', '); return s.length > 80 ? s.slice(0, 77) + '...' : s; };
  return (
    <>
      <PageHead title="Product Orders" sub="Shop orders customers placed in My Account, and orders placed for them by phone." />
      <select className="pill-select" aria-label="Status" style={{ marginBottom: 18 }} value={status} onChange={(e) => navigate(`/corral/products/orders${e.target.value ? `?status=${e.target.value}` : ''}`)}>
        <option value="">All orders</option>{data.statuses.map((s: string) => <option key={s}>{s}</option>)}
      </select>
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>Order</th><th>Placed</th><th>Account</th><th>Items</th><th className="num">Today</th><th className="num">Monthly</th><th>By</th><th>Status</th></tr></thead>
        <tbody>
          {data.orders.map((o: any) => (
            <tr key={o.id} className="click" onClick={() => navigate(`/corral/products/orders/${o.id}`)}><td className="nowrap"><Link to={`/corral/products/orders/${o.id}`}><b>#{o.id}</b></Link></td><td className="mono">{o.number}</td><td className="nowrap">{dateTime(o.createdAt)}</td>
              <td onClick={(e) => e.stopPropagation()}><AccountNo id={o.customerId} account={o.account} /><div className="help">{o.customer}</div></td>
              <td className="wrap">{list(o)}</td><td className="num">{money(o.total)}</td><td className="num">{o.monthly > 0 ? money(o.monthly) : '—'}</td>
              <td>{o.placedBy ?? 'Customer'}<div className="help">{o.placedBy ? 'by phone' : 'My Account'}</div></td>
              <td><span className={`pill ${orderTone(o.status)}`}>{o.status}</span></td></tr>
          ))}
          {data.orders.length === 0 && <EmptyRow cols={9}>No orders{status ? ` ${status.toLowerCase()}` : ''}.</EmptyRow>}
        </tbody>
      </table></div><PagerBar page={page} total={data.total} size={10} onPage={(p) => navigate(`/corral/products/orders?${qs({ status, page: p })}`)} /></div>
    </>
  );
}

/** One Shop order (ebunny's corral/shop/order): items, totals, shipping and payment, and its status. */
export function ProductOrder() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/shop/orders/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const o = data.order, c = data.customer;
  const [name, street, city, state, zip] = o.shipTo.split('|');
  const fromAccount = params.get('from') === 'account';
  return (
    <>
      <PageHead title={`Order ${o.number}`} sub={`Order #${o.id} · placed ${dateTime(o.createdAt)} ${o.placedBy ? `by ${o.placedBy} (by phone)` : 'by the customer in My Account'}`}
        actions={<>{fromAccount ? <Link className="btn ghost btn-back" to={`/corral/customers/${c.id}#shop`}>Back to Account</Link> : <Link className="btn ghost btn-back" to="/corral/products/orders">Back to Product Orders</Link>}
          <Link className="btn ghost" to={`/corral/customers/${c.id}/order-products`}>New Order for This Customer</Link></>} />
      {bar}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <div className="grid-4 stat-row">
          <div className="stat"><span>Charged Today</span><strong>{money(o.total)}</strong><small>{o.payWith === 'bill' ? 'added to the next bill' : `card ${data.method ?? ''}`}</small></div>
          <div className="stat"><span>Monthly</span><strong>{o.monthly > 0 ? money(o.monthly) : '—'}</strong><small>protection plans on the bill</small></div>
          <div className="stat"><span>Stars Earned</span><strong>{o.stars.toLocaleString()}</strong></div>
          <div className="stat"><span>Status</span><strong style={{ fontSize: '1.25rem' }}>{o.status}</strong><small>payment {o.paymentStatus.toLowerCase()}</small></div>
        </div>
        <div className="panel"><div className="panel-head"><h2>Items</h2></div><div className="table-wrap"><table>
          <thead><tr><th>Product</th><th className="num">Qty</th><th className="num">Price</th><th className="num">Line</th><th className="num">Stars</th></tr></thead>
          <tbody>{items(o).map((i) => (
            <tr key={i.sku}><td><b>{i.name}</b><div className="help mono">{i.sku}</div></td><td className="num">{i.qty}</td><td className="num">{money(i.price)}</td>
              <td className="num">{money(i.line)}{i.monthly ? '/mo' : ''}</td><td className="num">{Number(i.stars).toLocaleString()}</td></tr>
          ))}</tbody>
          <tfoot><tr><td colSpan={3} className="num">Subtotal</td><td className="num">{money(o.subtotal)}</td><td /></tr><tr><td colSpan={3} className="num">Tax</td><td className="num">{money(o.tax)}</td><td /></tr>
            <tr><td colSpan={3} className="num"><b>Total today</b></td><td className="num"><b>{money(o.total)}</b></td><td /></tr></tfoot>
        </table></div></div>
        <div className="panel"><div className="panel-head"><h2>Customer &amp; Shipping</h2></div><div className="panel-body"><dl className="kv">
          <dt>Account</dt><dd><AccountNo id={c.id} account={c.account} /> {c.name}</dd>
          <dt>Ship To</dt><dd>{name}, {street}, {city}, {state || 'TX'} {zip}</dd>
          <dt>Paid With</dt><dd>{o.payWith === 'bill' ? 'Added to the next electricity bill' : `Card ${data.method ?? ''}`}</dd>
        </dl></div></div>
        <form className="panel" onSubmit={(e) => { e.preventDefault(); run(api.put(`/api/corral/shop/orders/${o.id}`, { status: new FormData(e.currentTarget).get('status') }), 'Status saved.'); }}>
          <div className="panel-head"><h2>Status</h2></div>
          <div className="panel-body"><div className="form-row"><select name="status" aria-label="Order status" defaultValue={o.status}>{data.statuses.map((s: string) => <option key={s}>{s}</option>)}</select><button className="btn">Save</button></div></div>
        </form>
      </div>
    </>
  );
}

/** Order Products for a customer on the phone (ebunny's corral/shop/new-order): pick items, then pay and ship like checkout. */
export function OrderFor() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { data, error } = useLoad<any>(`/api/corral/shop/order-for/${id}`);
  const [qty, setQty] = useState<Record<string, number>>(params.get('sku') ? { [params.get('sku')!.toUpperCase()]: 1 } : {});
  const [ship, setShip] = useState('service');
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.customer;
  const monthlyOf = (p: any) => data.protection.includes(p.sku);
  let today = 0, monthly = 0, stars = 0;
  for (const p of data.products) { const q = qty[p.sku] ?? 0; if (monthlyOf(p)) monthly += q * Math.round(p.price / 12 * 100) / 100; else today += q * p.price; stars += q * p.stars; }
  today *= 1.0825; // with sales tax, like the website
  const place = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post<{ id: number }>(`/api/corral/shop/order-for/${id}`, {
      qty, payWith: f.get('pay_with'), method: f.get('method') ? Number(f.get('method')) : null, ship, street: f.get('street'), city: f.get('city'), zip: f.get('zip'), agree: f.get('agree') === 'on',
    }).then((r) => { navigate(`/corral/products/orders/${r.id}`); return r; }));
  };
  return (
    <>
      <PageHead title={`Order Products for ${c.name}`}
        sub={<>Account <Link className="mono" to={`/corral/customers/${c.id}`}>{c.account}</Link> · the same products, prices and stars as the website Shop. Protection plans go on the electricity bill monthly.</>}
        actions={<Link className="btn ghost btn-back" to={`/corral/customers/${c.id}#shop`}>Back to Account</Link>} />
      <form onSubmit={place} style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        {bar}
        {Object.entries(data.categories).map(([cat, label]) => {
          const list = data.products.filter((p: any) => p.category === cat);
          if (list.length === 0) return null;
          return (
            <div key={cat} className="panel"><div className="panel-head"><h2>{label as string}</h2></div><div className="table-wrap"><table data-page="0">
              <thead><tr><th>Product</th><th className="num">Price</th><th className="num">Stars</th><th>Stock</th><th className="num">Qty</th></tr></thead>
              <tbody>{list.map((p: any) => {
                const q = qty[p.sku] ?? 0, m = monthlyOf(p);
                return (
                  <tr key={p.sku} className={q > 0 ? 'row-warn' : undefined}><td><b>{p.name}</b>{p.badge && <> <span className="pill info">{p.badge}</span></>}<div className="help">{p.blurb}</div></td>
                    <td className="num nowrap">{money(m ? p.price / 12 : p.price)}{m ? '/mo' : ''}{p.was && !m && <div className="help"><s>{money(p.was)}</s></div>}</td>
                    <td className="num">{p.stars.toLocaleString()}</td>
                    <td><span className={`pill ${p.inStock ? 'ok' : 'bad'}`}>{p.inStock ? 'In stock' : 'Out'}</span></td>
                    <td className="num" style={{ width: 110 }}><input type="number" min={0} max={m ? 1 : 10} value={q} aria-label={`How many ${p.name}`} disabled={!p.inStock}
                      onChange={(e) => setQty({ ...qty, [p.sku]: Math.max(0, Number(e.target.value) || 0) })} style={{ width: 80, textAlign: 'right' }} /></td></tr>
                );
              })}</tbody>
            </table></div></div>
          );
        })}
        <div className="panel"><div className="panel-head"><h2>Checkout</h2></div>
          <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
            <label>Order total</label><div className="order-total"><b>{money(today)}</b> today (with tax) · <b>{money(monthly)}</b>/mo on the bill · {stars.toLocaleString()} stars</div>
            <label htmlFor="pay_with">Pay with</label>
            <select id="pay_with" name="pay_with" defaultValue={data.methods.length ? 'card' : 'bill'}>
              <option value="card" disabled={data.methods.length === 0}>A card on the account</option><option value="bill">Add to the next electricity bill</option>
            </select>
            <label htmlFor="method">Card</label><select id="method" name="method">{data.methods.length === 0 ? <option value="">No cards on the account</option>
              : data.methods.map((m: any) => <option key={m.id} value={m.id}>{m.type} •••• {m.last4}{m.nickname ? ` (${m.nickname})` : ''}</option>)}</select>
            <label htmlFor="ship">Ship to</label>
            <select id="ship" name="ship" value={ship} onChange={(e) => setShip(e.target.value)}><option value="service">Service address: {c.address}, {c.city} {c.zip}</option><option value="other">Another address</option></select>
            <label htmlFor="street" hidden={ship !== 'other'}>Street</label><input id="street" name="street" maxLength={120} hidden={ship !== 'other'} />
            <label htmlFor="city" hidden={ship !== 'other'}>City</label><input id="city" name="city" maxLength={60} hidden={ship !== 'other'} />
            <label htmlFor="zip" hidden={ship !== 'other'}>ZIP</label><input id="zip" name="zip" inputMode="numeric" maxLength={5} hidden={ship !== 'other'} />
            <label htmlFor="agree">Order terms</label><label className="check"><input id="agree" type="checkbox" name="agree" required /> I read the order terms to the customer and they agreed (returns within 30 days; protection plans can be canceled any time)</label>
          </div>
          <div className="panel-body"><button className="btn cyan">Place Order</button></div>
        </div>
      </form>
    </>
  );
}
