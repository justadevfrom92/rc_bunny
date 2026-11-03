import { useState } from 'react';
import { useLoad } from '../../api';

interface Entry { id: number; who: string; category: string; what: string; createdAt: string }
const categories = ['', 'sign-in', 'access', 'heartbeat', 'job', 'market'];

/** Sheriff → Timeline (everything), or the Control Log when given category="heartbeat". */
export default function Timeline({ category, title = 'Timeline' }: { category?: string; title?: string }) {
  const [filter, setFilter] = useState(category ?? '');
  const { data, error } = useLoad<Entry[]>(`/api/sheriff/timeline${filter ? `?category=${filter}` : ''}`);

  return (
    <>
      <h1>{title}</h1>
      {!category && (
        <div className="row">{categories.map((c) => <button key={c} className={c === filter ? 'primary' : ''} onClick={() => setFilter(c)}>{c || 'Everything'}</button>)}</div>
      )}
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : data.length === 0 ? <p className="muted">Nothing yet.</p> : (
        <table>
          <thead><tr><th>When</th><th>Who</th><th>What</th></tr></thead>
          <tbody>{data.map((e) => <tr key={e.id}><td>{new Date(e.createdAt).toLocaleString('en-US')}</td><td>{e.who}</td><td><span className="tag">{e.category}</span> {e.what}</td></tr>)}</tbody>
        </table>
      )}
    </>
  );
}
