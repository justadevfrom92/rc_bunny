import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { api, date, useLoad } from '../../api';
import { useAuth } from '../../auth';

interface Entry { id: number; stars: number; reason: string; fulfillment: string | null; by: string; createdAt: string }
interface MemberData {
  id: number; account: string; name: string; stars: number; status: string; inDrawing: boolean;
  history: Entry[]; offers: { id: number; name: string; stars: number }[]; rules: { id: number; name: string; stars: number }[];
}

export default function Member() {
  const { id } = useParams();
  const { can } = useAuth();
  const { data: m, error, reload } = useLoad<MemberData>(`/api/bounty/members/${id}`);
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!m) return <p>Loading…</p>;

  const run = (action: Promise<unknown>, form?: HTMLFormElement) => action.then(() => { setProblem(null); form?.reset(); reload(); }, (e: Error) => setProblem(e.message));
  const byRule = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    run(api.post(`/api/bounty/members/${m.id}/adjust`, { ruleId: Number(new FormData(e.currentTarget).get('rule')), stars: 0, reason: '' }));
  };
  const byHand = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post(`/api/bounty/members/${m.id}/adjust`, { stars: Number(f.get('stars')), reason: f.get('reason') }), e.currentTarget);
  };
  const redeem = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    run(api.post(`/api/bounty/members/${m.id}/redeem`, { offerId: Number(new FormData(e.currentTarget).get('offer')) }));
  };

  return (
    <>
      <p><Link to="/bounty/members">← Members</Link></p>
      <h1>{m.name} <span className="muted">#{m.account}</span></h1>
      <p><b>{m.stars.toLocaleString()}</b> stars · {m.status}{m.inDrawing && ' · in this month\'s drawing'}{can('corral') && <> · <Link to={`/corral/customers/${m.id}`}>Open in Corral</Link></>}</p>
      {problem && <p className="error">{problem}</p>}
      <div className="grid">
        <section className="card">
          <h2>Give stars</h2>
          {m.rules.length > 0 && (
            <form className="row" onSubmit={byRule}>
              <select name="rule">{m.rules.map((r) => <option key={r.id} value={r.id}>{r.name} (+{r.stars})</option>)}</select>
              <button className="primary">Give</button>
            </form>
          )}
          <form className="row" onSubmit={byHand}>
            <input name="stars" type="number" placeholder="Stars (− to take away)" required style={{ maxWidth: 190 }} />
            <input name="reason" placeholder="Reason" required />
            <button>Adjust</button>
          </form>
        </section>
        <section className="card">
          <h2>Redeem for them</h2>
          <form className="row" onSubmit={redeem}>
            <select name="offer">{m.offers.map((o) => <option key={o.id} value={o.id}>{o.name} ({o.stars.toLocaleString()} stars)</option>)}</select>
            <button className="primary">Redeem</button>
          </form>
        </section>
      </div>
      <section className="card">
        <h2>History</h2>
        <table>
          <thead><tr><th>Date</th><th>What</th><th>By</th><th className="num">Stars</th></tr></thead>
          <tbody>{m.history.map((e) => <tr key={e.id}><td>{date(e.createdAt)}</td><td>{e.reason}{e.fulfillment && <span className="tag">{e.fulfillment}</span>}</td><td>{e.by}</td><td className="num">{e.stars > 0 ? '+' : ''}{e.stars.toLocaleString()}</td></tr>)}</tbody>
        </table>
      </section>
    </>
  );
}
