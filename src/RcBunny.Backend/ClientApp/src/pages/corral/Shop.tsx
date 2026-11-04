import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, date, money, qs, useLoad } from '../../api';
import { Pager, Status, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Corral → Products: the website Shop's products by category, with how many have sold. */
export function Products() {
  const [params, setParams] = useSearchParams();
  const cat = params.get('cat') ?? '', page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/shop/products?${qs({ cat, page })}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const count = (k: string) => data.counts.find((c: any) => c.cat === k)?.n ?? 0;
  return (
    <>
      <div className="row spread"><h1>Products</h1><Link className="button" to="/corral/products/new">New Product</Link></div>
      <div className="row">
        <button className={cat ? '' : 'primary'} onClick={() => setParams('')}>All</button>
        {Object.entries(data.categories).map(([k, v]) => <button key={k} className={cat === k ? 'primary' : ''} onClick={() => setParams({ cat: k })}>{v as string} ({count(k)})</button>)}
      </div>
      <table>
        <thead><tr><th>SKU</th><th>Product</th><th>Category</th><th className="num">Price</th><th className="num">Stars</th><th className="num">Sold</th><th>In stock</th><th>In the Shop</th></tr></thead>
        <tbody>{data.products.map((p: any) => (
          <tr key={p.id} className={p.active ? '' : 'inactive'}><td className="mono">{p.sku}</td><td><Link to={`/corral/products/${p.id}`}>{p.name}</Link>{p.badge && <div className="muted">{p.badge}</div>}</td>
            <td>{data.categories[p.category]}</td><td className="num">{money(p.price)}{p.was && <div className="muted"><s>{money(p.was)}</s></div>}</td><td className="num">{p.stars}</td>
            <td className="num">{data.sold[p.sku] ?? 0}</td><td>{p.inStock ? 'Yes' : 'Out of stock'}</td><td>{p.active ? 'Yes' : 'Hidden'}</td></tr>
        ))}</tbody>
      </table>
      <Pager page={page} total={data.total} size={10} onPage={(p) => setParams(qs({ cat, page: p }))} />
    </>
  );
}

function ProductFields({ p, categories, icons }: { p: any; categories: Record<string, string>; icons: string[] }) {
  return (
    <>
      <div className="row">
        <label>SKU<input name="sku" defaultValue={p.sku} required maxLength={20} /></label>
        <label>Name<input name="name" defaultValue={p.name} required /></label>
        <label>Category<select name="category" defaultValue={p.category}>{Object.entries(categories).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
      </div>
      <div className="row">
        <label>Price<input name="price" type="number" step="0.01" min="0" defaultValue={p.price} required /></label>
        <label>Was (sale from)<input name="was" type="number" step="0.01" min="0" defaultValue={p.was ?? ''} /></label>
        <label>Stars earned<input name="stars" type="number" min="0" defaultValue={p.stars ?? 0} required /></label>
        <label>Badge<input name="badge" defaultValue={p.badge ?? ''} /></label>
        <label>Icon<select name="icon" defaultValue={p.icon ?? 'plug'}>{icons.map((i) => <option key={i}>{i}</option>)}</select></label>
      </div>
      <label>Blurb<input name="blurb" defaultValue={p.blurb ?? ''} maxLength={300} /></label>
      <div className="row">
        <label style={{ flexDirection: 'row' }}><input type="checkbox" name="inStock" defaultChecked={p.inStock ?? true} /> In stock</label>
        <label style={{ flexDirection: 'row' }}><input type="checkbox" name="active" defaultChecked={p.active ?? true} /> In the website Shop</label>
      </div>
    </>
  );
}

const productBody = (f: FormData) => ({
  sku: f.get('sku'), name: f.get('name'), category: f.get('category'), price: Number(f.get('price')), was: f.get('was') ? Number(f.get('was')) : null,
  stars: Number(f.get('stars')), badge: f.get('badge'), icon: f.get('icon'), blurb: f.get('blurb'), inStock: f.get('inStock') === 'on', active: f.get('active') === 'on',
});

/** New Product. */
export function ProductForm() {
  const navigate = useNavigate();
  const { data } = useLoad<any>('/api/corral/shop/products?page=1');
  const [run, bar] = useRun();
  if (!data) return <p>Loading…</p>;
  const icons = ['thermo', 'sensor', 'ev', 'cable', 'bulb', 'plug', 'meter', 'shield', 'house', 'sun', 'battery', 'tools', 'cap', 'cup', 'bag'];
  return (
    <>
      <h1>New Product</h1>
      {bar}
      <form className="card" onSubmit={(e) => { e.preventDefault(); run(api.post<{ id: number }>('/api/corral/shop/products', productBody(new FormData(e.currentTarget))).then((r) => { navigate(`/corral/products/${r.id}`); return r; })); }}>
        <ProductFields p={{ category: Object.keys(data.categories)[0] }} categories={data.categories} icons={icons} />
        <button className="primary">Save</button>
      </form>
    </>
  );
}

/** One product: edit it, and its recent orders. */
export function Product() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/shop/products/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const p = data.product;
  return (
    <>
      <p><Link to="/corral/products">← Products</Link></p>
      <h1>{p.name} <span className="muted mono">{p.sku}</span></h1>
      <p>{money(p.price)} · {p.stars} stars · {p.rating}★ ({p.reviews} reviews) · {data.sold} sold</p>
      {bar}
      <form className="card" key={JSON.stringify(p)} onSubmit={(e) => { e.preventDefault(); run(api.put(`/api/corral/shop/products/${p.id}`, productBody(new FormData(e.currentTarget)))); }}>
        <ProductFields p={p} categories={data.categories} icons={data.icons} />
        <button className="primary">Save</button>
      </form>
      <section className="card"><h2>Recent orders</h2>
        {data.orders.length === 0 ? <p className="muted">No orders yet.</p> : (
          <table><tbody>{data.orders.map((o: any) => <tr key={o.id}><td><Link to={`/corral/products/orders/${o.id}`}>{o.number}</Link></td><td>{date(o.createdAt)}</td>
            <td><Link to={`/corral/customers/${o.customerId}`}>{o.customer}</Link></td><td className="num">{money(o.total)}</td><td><Status value={o.status} /></td></tr>)}</tbody></table>
        )}
      </section>
    </>
  );
}

/** Product Orders, by status. */
export function ProductOrders() {
  const [params, setParams] = useSearchParams();
  const status = params.get('status') ?? '', page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/shop/orders?${qs({ status, page })}`);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  return (
    <>
      <h1>Product Orders</h1>
      <div className="row">
        <button className={status ? '' : 'primary'} onClick={() => setParams('')}>All</button>
        {data.statuses.map((s: string) => <button key={s} className={status === s ? 'primary' : ''} onClick={() => setParams({ status: s })}>{s}</button>)}
      </div>
      <table>
        <thead><tr><th>ID</th><th>Placed</th><th>Customer</th><th>Items</th><th className="num">Total</th><th>Payment</th><th>Status</th></tr></thead>
        <tbody>{data.orders.map((o: any) => (
          <tr key={o.id}><td><Link to={`/corral/products/orders/${o.id}`}><b>#{o.id}</b></Link><div className="mono muted">{o.number}</div></td>
            <td>{date(o.createdAt)}<div className="muted">{o.placedBy ? `by ${o.placedBy}` : 'My Account'}</div></td>
            <td><Link to={`/corral/customers/${o.customerId}`}>{o.customer}</Link> <span className="muted">#{o.account}</span></td>
            <td>{JSON.parse(o.items).map((i: any) => `${i.qty}× ${i.name}`).join(', ')}</td>
            <td className="num">{money(o.total)}{o.monthly > 0 && <div className="muted">+{money(o.monthly)}/mo</div>}</td><td>{o.paymentStatus}</td><td><Status value={o.status} /></td></tr>
        ))}</tbody>
      </table>
      <Pager page={page} total={data.total} size={10} onPage={(p) => setParams(qs({ status, page: p }))} />
    </>
  );
}

/** One order: items, totals, shipping and payment, and its status. */
export function ProductOrder() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/shop/orders/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const o = data.order, c = data.customer;
  const [name, street, city, state, zip] = o.shipTo.split('|');
  return (
    <>
      <p><Link to="/corral/products/orders">← Product Orders</Link></p>
      <h1>Order {o.number} <Status value={o.status} /></h1>
      <p><Link to={`/corral/customers/${c.id}`}>{c.name} #{c.account}</Link> · placed {date(o.createdAt)} {o.placedBy ? `by ${o.placedBy} on the phone` : 'in My Account'}</p>
      {bar}
      <div className="grid">
        <section className="card"><h2>Items</h2>
          <table>
            <thead><tr><th>Item</th><th className="num">Qty</th><th className="num">Price</th><th className="num">Line</th><th className="num">Stars</th></tr></thead>
            <tbody>{JSON.parse(o.items).map((i: any) => <tr key={i.sku}><td>{i.name} <span className="mono muted">{i.sku}</span></td><td className="num">{i.qty}</td><td className="num">{money(i.price)}</td>
              <td className="num">{money(i.line)}{i.monthly && '/mo'}</td><td className="num">{i.stars}</td></tr>)}</tbody>
          </table>
          <dl><dt>Subtotal</dt><dd>{money(o.subtotal)}</dd><dt>Tax</dt><dd>{money(o.tax)}</dd><dt>Total today</dt><dd><b>{money(o.total)}</b></dd>
            {o.monthly > 0 && <><dt>Monthly</dt><dd>{money(o.monthly)}/mo on the bill</dd></>}<dt>Stars earned</dt><dd>{o.stars}</dd></dl>
        </section>
        <section className="card"><h2>Shipping and payment</h2>
          <dl><dt>Ship to</dt><dd>{name}<br />{street}<br />{city}, {state} {zip}</dd><dt>Pay with</dt><dd>{o.payWith === 'bill' ? 'The electricity bill' : data.method ?? 'Card'}</dd><dt>Payment</dt><dd>{o.paymentStatus}</dd></dl>
          <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.put(`/api/corral/shop/orders/${o.id}`, { status: new FormData(e.currentTarget).get('status') }), 'Status saved.'); }}>
            <select name="status" defaultValue={o.status}>{data.statuses.map((s: string) => <option key={s}>{s}</option>)}</select><button className="primary">Save status</button>
          </form>
        </section>
      </div>
    </>
  );
}

/** Order Products for a customer on the phone: pick products, then check out as they would in My Account. */
export function OrderFor() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { data, error } = useLoad<any>(`/api/corral/shop/order-for/${id}`);
  const [qty, setQty] = useState<Record<string, number>>(params.get('sku') ? { [params.get('sku')!.toUpperCase()]: 1 } : {});
  const [summary, setSummary] = useState<any>(null);
  const [payWith, setPayWith] = useState('card');
  const [ship, setShip] = useState('service');
  const [run, bar] = useRun();

  useEffect(() => {
    if (Object.values(qty).some((q) => q > 0)) api.post<any>(`/api/corral/shop/order-for/${id}/summary`, { qty }).then(setSummary);
    else setSummary(null);
  }, [id, qty]);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const c = data.customer;
  const place = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post<{ id: number }>(`/api/corral/shop/order-for/${id}`, {
      qty, payWith, method: f.get('method') ? Number(f.get('method')) : null, ship, street: f.get('street'), city: f.get('city'), zip: f.get('zip'), agree: f.get('agree') === 'on',
    }).then((r) => { navigate(`/corral/products/orders/${r.id}`); return r; }));
  };

  return (
    <>
      <p><Link to={`/corral/customers/${c.id}#shop`}>← {c.name} #{c.account}</Link></p>
      <h1>Order Products</h1>
      {bar}
      {Object.entries(data.categories).map(([cat, label]) => {
        const products = data.products.filter((p: any) => p.category === cat);
        if (products.length === 0) return null;
        return (
          <section key={cat} className="card"><h2>{label as string}</h2>
            <table><tbody>{products.map((p: any) => (
              <tr key={p.sku} className={p.inStock ? '' : 'inactive'}><td>{p.name}<div className="muted">{p.blurb}</div></td>
                <td className="num">{data.protection.includes(p.sku) ? `${money(p.price / 12)}/mo` : money(p.price)}</td><td className="num">{p.stars} stars</td>
                <td>{p.inStock ? <input type="number" min="0" max={data.protection.includes(p.sku) ? 1 : 10} value={qty[p.sku] ?? 0} style={{ width: 70 }}
                  onChange={(e) => setQty({ ...qty, [p.sku]: Number(e.target.value) })} /> : 'Out of stock'}</td></tr>
            ))}</tbody></table>
          </section>
        );
      })}
      {summary && summary.lines.length > 0 && (
        <form className="card" onSubmit={place}>
          <h2>Check out</h2>
          <table><tbody>{summary.lines.map((l: any) => <tr key={l.sku}><td>{l.qty}× {l.name}</td><td className="num">{money(l.lineTotal)}{l.monthly && '/mo'}</td></tr>)}</tbody></table>
          <dl><dt>Subtotal</dt><dd>{money(summary.subtotal)}</dd><dt>Tax</dt><dd>{money(summary.tax)}</dd><dt>Due today</dt><dd><b>{money(summary.total)}</b></dd>
            {summary.monthly > 0 && <><dt>On the monthly bill</dt><dd>{money(summary.monthly)}/mo</dd></>}<dt>Stars they'll earn</dt><dd>{summary.stars}</dd></dl>
          <div className="row">
            <label style={{ flexDirection: 'row' }}><input type="radio" checked={payWith === 'card'} onChange={() => setPayWith('card')} /> Card on file</label>
            <label style={{ flexDirection: 'row' }}><input type="radio" checked={payWith === 'bill'} onChange={() => setPayWith('bill')} /> Add it to the electricity bill</label>
            {payWith === 'card' && <select name="method">{data.methods.length === 0 && <option value="">No cards on file</option>}{data.methods.map((m: any) => <option key={m.id} value={m.id}>{m.nickname ?? m.type} ending {m.last4}</option>)}</select>}
          </div>
          <div className="row">
            <label style={{ flexDirection: 'row' }}><input type="radio" checked={ship === 'service'} onChange={() => setShip('service')} /> Ship to {c.address}, {c.city}</label>
            <label style={{ flexDirection: 'row' }}><input type="radio" checked={ship === 'other'} onChange={() => setShip('other')} /> Somewhere else</label>
          </div>
          {ship === 'other' && <div className="row"><input name="street" placeholder="Street" required /><input name="city" placeholder="City" required /><input name="zip" placeholder="Zip" maxLength={5} required /></div>}
          <label style={{ flexDirection: 'row' }}><input type="checkbox" name="agree" /> I read the order terms to the customer and they agreed</label>
          <button className="primary">Place order</button>
        </form>
      )}
    </>
  );
}
