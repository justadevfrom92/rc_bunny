import { type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { qs, useLoad } from '../../api';
import { Pager } from '../../ui';
import { TicketTable, type Lists, type Row } from './shared';

interface Data {
  view: string; page: number; total: number; tickets: Row[]; counts: Record<string, number>;
  people: { id: number; name: string }[]; customerName: string | null; manager: boolean; lists: Lists;
}

const views: [string, string][] = [['active', 'Active'], ['mine', 'Mine'], ['unassigned', 'Unassigned'], ['manager', 'Manager Requests'], ['watching', 'Joined'], ['done', 'Solved & Closed'], ['all', 'All']];
const filters = ['q', 'in', 'status', 'priority', 'topic', 'channel', 'from', 'to', 'team', 'owner', 'handledBy', 'customer'];

/** The ticket lists with their counts, and the search (words or a ticket number, where to look, and more filters). */
export default function Tickets() {
  const [params, setParams] = useSearchParams();
  const url = `/api/ranger/tickets?${params.toString()}`;
  const { data, error } = useLoad<Data>(url);
  const get = (k: string) => params.get(k) ?? '';

  const search = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const values: Record<string, string> = {};
    for (const k of filters) if (k !== 'in' && f.get(k)) values[k] = String(f.get(k));
    const where = f.getAll('in').join(',');
    if (where) values.in = where;
    if (params.get('view') && !values.q) values.view = params.get('view')!;
    setParams(values);
  };
  const go = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(changes)) if (v === null) next.delete(k); else next.set(k, v);
    setParams(next);
  };

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const l = data.lists;
  const where = get('in').split(',').filter(Boolean);

  return (
    <>
      <h1>Tickets</h1>
      {data.customerName && <p className="notice">Tickets for <b>{data.customerName}</b> · <Link to="/ranger/tickets">Show everyone's</Link></p>}
      <div className="row">
        {views.map(([k, label]) => (
          <button key={k} className={k === data.view ? 'primary' : ''} onClick={() => go({ view: k, page: null })}>{label} <span className="muted">{data.counts[k]}</span></button>
        ))}
      </div>

      <form className="card" onSubmit={search} key={params.toString()}>
        <div className="row">
          <label style={{ flex: 2 }}>Search<input name="q" defaultValue={get('q')} placeholder="Words, a name, phone, account or RT-10023" /></label>
          <span className="row">Look in
            {[['text', 'Messages'], ['notes', 'Notes'], ['people', 'People']].map(([k, label]) => (
              <label key={k} style={{ flexDirection: 'row', margin: 0 }}><input type="checkbox" name="in" value={k} defaultChecked={where.includes(k)} /> {label}</label>
            ))}
          </span>
        </div>
        <div className="row">
          <label>Status<select name="status" defaultValue={get('status')}><option value="">Any</option>{Object.entries(l.statuses).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></label>
          <label>Priority<select name="priority" defaultValue={get('priority')}><option value="">Any</option>{Object.entries(l.priorities).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></label>
          <label>Topic<select name="topic" defaultValue={get('topic')}><option value="">Any</option>{Object.entries(l.topics).map(([k, s]) => <option key={k} value={k}>{s}</option>)}</select></label>
          <label>Came in by<select name="channel" defaultValue={get('channel')}><option value="">Any</option>{Object.keys(l.channels).map((k) => <option key={k}>{k}</option>)}</select></label>
          <label>Team<select name="team" defaultValue={get('team')}><option value="">Any</option>{Object.entries(l.teams).map(([k, s]) => <option key={k} value={k}>{s}</option>)}</select></label>
          <label>Owner<select name="owner" defaultValue={get('owner')}><option value="">Anyone</option>{data.people.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
          <label>Handled by<select name="handledBy" defaultValue={get('handledBy')}><option value="">Anyone</option>{data.people.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
          <label>Opened from<input type="date" name="from" defaultValue={get('from')} /></label>
          <label>to<input type="date" name="to" defaultValue={get('to')} /></label>
          {get('customer') && <input type="hidden" name="customer" value={get('customer')} />}
        </div>
        <div className="row"><button className="primary">Search</button><Link to={`/ranger/tickets?${qs({ view: data.view })}`}>Clear</Link></div>
      </form>

      <p className="muted">{data.total} {data.total === 1 ? 'ticket' : 'tickets'}</p>
      <TicketTable rows={data.tickets} lists={l} />
      <Pager page={data.page} total={data.total} size={25} onPage={(p) => go({ page: String(p) })} />
    </>
  );
}
