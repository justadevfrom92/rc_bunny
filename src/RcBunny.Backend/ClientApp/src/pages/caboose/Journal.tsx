import { useState } from 'react';
import { useLoad } from '../../api';
import { DateInput } from '../../fields';
import { EmptyRow, PageHead, Panel } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const iso = (d: Date) => d.toISOString().slice(0, 10);
const n2 = (v: number) => v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Caboose → Journal (ebunny's caboose/journal): the daily double-entry journal, with Download CSV. */
export default function Journal() {
  const [start, setStart] = useState(iso(new Date(Date.now() - 7 * 86400000)));
  const [end, setEnd] = useState(iso(new Date()));
  const [range, setRange] = useState({ start, end });
  const { data, error } = useLoad<any>(`/api/caboose/journal?start=${range.start}&end=${range.end}`);
  if (error) return <div className="flash bad">{error}</div>;
  // Each journal entry is two lines: the debit and the credit
  const lines = (data?.lines ?? []).flatMap((l: any, i: number) => {
    const je = `JE-${l.date.replace(/-/g, '')}-${i + 1}`;
    return [{ je, date: l.date, account: l.debit, debit: l.amount, credit: 0, memo: l.memo }, { je, date: l.date, account: l.credit, debit: 0, credit: l.amount, memo: l.memo }];
  });
  const debits = lines.reduce((t: number, l: any) => t + l.debit, 0), credits = lines.reduce((t: number, l: any) => t + l.credit, 0);
  return (
    <>
      <PageHead title="Journal" sub="Daily double-entry journal of bills, payments, deposits, credits and refunds, for import into the accounting system." />
      <div className="banner-note">QuickBooks isn't connected (Sheriff → APIs). Download the journal and import it.</div>
      <form className="panel" onSubmit={(e) => { e.preventDefault(); setRange({ start, end }); }}><div className="panel-body form-row">
        <div className="field"><label htmlFor="start">From</label><DateInput id="start" value={start} onChange={setStart} /></div>
        <div className="field"><label htmlFor="end">To</label><DateInput id="end" value={end} onChange={setEnd} /></div>
        <button className="btn">Show</button>
        <a className="btn ghost" href={`/api/caboose/journal?start=${start}&end=${end}&format=csv`}>Download CSV</a>
      </div></form>
      <Panel title="Entries" right={<span className={Math.abs(debits - credits) < 0.005 ? 'muted' : ''}>Debits ${n2(debits)} · Credits ${n2(credits)}{Math.abs(debits - credits) < 0.005 ? ' · balanced' : ' · NOT BALANCED'}</span>}>
        <div className="table-wrap"><table>
          <thead><tr><th>Entry</th><th>Date</th><th>Account</th><th className="num">Debit</th><th className="num">Credit</th><th>Memo</th></tr></thead>
          <tbody>
            {lines.map((l: any, i: number) => (
              <tr key={i}><td className="nowrap"><b>{l.je}</b></td><td>{l.date}</td><td>{l.account}</td><td className="num">{l.debit ? n2(l.debit) : ''}</td><td className="num">{l.credit ? n2(l.credit) : ''}</td><td className="help">{l.memo}</td></tr>
            ))}
            {data && lines.length === 0 && <EmptyRow cols={6}>No activity in this range.</EmptyRow>}
          </tbody>
        </table></div>
      </Panel>
    </>
  );
}
