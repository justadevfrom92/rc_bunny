import { useState } from 'react';
import { date, money, useLoad } from '../../api';

interface Line { date: string; debit: string; credit: string; amount: number; memo: string }

const iso = (d: Date) => d.toISOString().slice(0, 10);

/** The daily double-entry journal for the accounting system. Download it as CSV to import. */
export default function Journal() {
  const [start, setStart] = useState(iso(new Date(Date.now() - 7 * 864e5)));
  const [end, setEnd] = useState(iso(new Date()));
  const url = `/api/caboose/journal?start=${start}&end=${end}`;
  const { data, error } = useLoad<{ lines: Line[]; total: number }>(url);

  return (
    <>
      <h1>Journal</h1>
      <div className="row">
        <input type="date" value={start} onChange={(e) => setStart(e.target.value)} />
        <input type="date" value={end} onChange={(e) => setEnd(e.target.value)} />
        <a className="button" href={`${url}&format=csv`}>Download CSV</a>
      </div>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : data.lines.length === 0 ? <p className="muted">Nothing in these dates.</p> : (
        <table>
          <thead><tr><th>Date</th><th>Debit</th><th>Credit</th><th className="num">Amount</th><th>Memo</th></tr></thead>
          <tbody>{data.lines.map((l, i) => <tr key={i}><td>{date(l.date)}</td><td>{l.debit}</td><td>{l.credit}</td><td className="num">{money(l.amount)}</td><td>{l.memo}</td></tr>)}</tbody>
          <tfoot><tr><th colSpan={3}>Total</th><th className="num">{money(data.total)}</th><th /></tr></tfoot>
        </table>
      )}
    </>
  );
}
