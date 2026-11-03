import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { useLoad } from '../../api';

interface Row { id: number; account: string; name: string; stars: number; status: string }

export default function Members() {
  const [q, setQ] = useState('');
  const [text, setText] = useState('');
  const { data, error } = useLoad<Row[]>(`/api/bounty/members?q=${encodeURIComponent(q)}`);
  const search = (e: FormEvent) => { e.preventDefault(); setQ(text); };

  return (
    <>
      <h1>Members</h1>
      <form className="row" onSubmit={search}>
        <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Account or name" />
        <button className="primary">Search</button>
      </form>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <table>
          <thead><tr><th>Account</th><th>Name</th><th>Status</th><th className="num">Stars</th></tr></thead>
          <tbody>{data.map((m) => <tr key={m.id}><td><Link to={`/bounty/members/${m.id}`}>{m.account}</Link></td><td>{m.name}</td><td>{m.status}</td><td className="num">{m.stars.toLocaleString()}</td></tr>)}</tbody>
        </table>
      )}
    </>
  );
}
