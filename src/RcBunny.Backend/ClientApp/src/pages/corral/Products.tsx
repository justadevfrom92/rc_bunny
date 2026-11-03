import { useState, type FormEvent } from 'react';
import { api, money, useLoad } from '../../api';

interface Product { id: number; sku: string; name: string; category: string; description: string | null; price: number; stock: number; active: boolean }

export default function Products() {
  const { data, error, reload } = useLoad<Product[]>('/api/products');
  const [editing, setEditing] = useState<Product | 'new' | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { sku: f.get('sku'), name: f.get('name'), category: f.get('category'), description: f.get('description'), price: Number(f.get('price')), stock: Number(f.get('stock')), active: f.get('active') === 'on' };
    const call = editing === 'new' ? api.post('/api/products', body) : api.put(`/api/products/${(editing as Product).id}`, body);
    call.then(() => { setEditing(null); setProblem(null); reload(); }, (err: Error) => setProblem(err.message));
  };
  const p = editing === 'new' ? null : editing;

  return (
    <>
      <div className="row spread"><h1>Products</h1><button onClick={() => setEditing('new')}>New product</button></div>
      {(problem || error) && <p className="error">{problem ?? error}</p>}
      {editing && (
        <form key={p?.id ?? 'new'} className="card" onSubmit={save}>
          <div className="row">
            <label>SKU<input name="sku" defaultValue={p?.sku} required /></label>
            <label>Name<input name="name" defaultValue={p?.name} required /></label>
            <label>Category<input name="category" defaultValue={p?.category} /></label>
          </div>
          <div className="row">
            <label>Price<input name="price" type="number" step="0.01" min="0" defaultValue={p?.price} required /></label>
            <label>In stock<input name="stock" type="number" min="0" defaultValue={p?.stock ?? 0} required /></label>
          </div>
          <label>Description<textarea name="description" rows={2} defaultValue={p?.description ?? ''} /></label>
          <label style={{ flexDirection: 'row' }}><input name="active" type="checkbox" defaultChecked={p?.active ?? true} /> For sale</label>
          <div className="row"><button className="primary">Save</button><button type="button" onClick={() => setEditing(null)}>Cancel</button></div>
        </form>
      )}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>SKU</th><th>Name</th><th>Category</th><th className="num">Price</th><th className="num">Stock</th><th /></tr></thead>
          <tbody>
            {data.map((x) => (
              <tr key={x.id} className={x.active ? '' : 'inactive'}>
                <td>{x.sku}</td><td>{x.name}</td><td>{x.category}</td><td className="num">{money(x.price)}</td><td className="num">{x.stock}</td>
                <td><button onClick={() => setEditing(x)}>Edit</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
