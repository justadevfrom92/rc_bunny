import { type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { useLoad } from '../../api';
import { DateInput } from '../../fields';
import { EmptyRow, PageHead, PagerBar } from '../../ui';
import { TicketRow, type Lists, type Row } from './shared';

interface Data {
  view: string; page: number; total: number; tickets: Row[]; counts: Record<string, number>;
  people: { id: number; name: string }[]; customerName: string | null; manager: boolean; lists: Lists;
}

const advanced = ['in', 'status', 'priority', 'topic', 'channel', 'from', 'to', 'handledBy', 'owner'];

/** Ranger → Tickets (ebunny's ranger/tickets): the search with Advanced search, the view chips, and the list. */
export default function Tickets() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<Data>(`/api/ranger/tickets?${params.toString()}`);
  const get = (k: string) => params.get(k) ?? '';
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const l = data.lists, view = data.view;
  const views: [string, string][] = [['active', 'Open'], ['mine', 'Mine'], ['unassigned', 'Unassigned'], ...(data.manager ? [['manager', 'Manager Requests'] as [string, string]] : []), ['watching', 'Joined'], ['done', 'Solved & Closed'], ['all', 'All']];
  const url = (set: Record<string, string | null>) => {
    const next = new URLSearchParams(params);
    next.set('view', view);
    for (const [k, v] of Object.entries(set)) if (v === null || v === '') next.delete(k); else next.set(k, v);
    return `/ranger/tickets?${next}`;
  };
  const search = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), next = new URLSearchParams();
    for (const [k, v] of f.entries()) if (k !== 'in' && v) next.set(k, String(v));
    const where = f.getAll('in').join(',');
    if (where) next.set('in', where);
    setParams(next);
  };
  const adv = advanced.some((k) => get(k));
  const searching = adv || !!get('q');
  const where = get('in').split(',').filter(Boolean);
  const owner = get('owner') ? data.people.find((p) => String(p.id) === get('owner')) : null;
  const clear = new URLSearchParams(Object.entries({ view: get('view'), customer: get('customer') }).filter(([, v]) => v));

  return (
    <>
      <PageHead title={data.customerName ? `${data.customerName} — Ticket History` : owner ? `${owner.name}'s Tickets` : view === 'unassigned' ? 'Unassigned Tickets' : 'Tickets'}
        sub={get('q') ? `Tickets matching “${get('q')}”.` : data.customerName ? 'Every ticket this account has opened, newest first.' : view === 'unassigned' ? 'Nobody has these yet. Urgent and high first, then the longest waiting. Click one to pick it up.' : 'Urgent and high first, then the longest waiting. Click a ticket to open it.'}
        actions={(owner || data.customerName) ? <Link className="btn ghost" to={`/ranger/tickets?view=${view}`}>Everyone's Tickets</Link> : undefined} />

      <form className="panel adv-search" role="search" onSubmit={search} key={params.toString()}>
        <div className="panel-body">
          {get('view') && <input type="hidden" name="view" value={view} />}
          {get('customer') && <input type="hidden" name="customer" value={get('customer')} />}
          <div className="adv-bar">
            <label className="search-field"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></svg>
              <input type="search" name="q" defaultValue={get('q')} placeholder="Search tickets: words, notes, names, phone, email, account or ticket number" aria-label="Search tickets" /></label>
            <button className="btn cyan">Search</button>
            {searching && <Link className="btn ghost" to={`/ranger/tickets${clear.size ? `?${clear}` : ''}`}>Clear</Link>}
          </div>
          <details className="adv-more" open={adv}>
            <summary>Advanced search</summary>
            <div className="adv-fields">
              <div className="field"><span className="label">Look in</span>
                <div className="adv-checks">
                  {[['text', 'Subject & messages'], ['notes', 'Notes'], ['people', 'Customer & owner (names, email, phone, account)']].map(([k, label]) => (
                    <label key={k}><input type="checkbox" name="in" value={k} defaultChecked={where.includes(k)} /> {label}</label>
                  ))}
                </div><span className="help">None ticked looks everywhere.</span></div>
              <div className="field"><label htmlFor="adv-status">Status</label><select id="adv-status" name="status" defaultValue={get('status')}><option value="">Any</option>{Object.entries(l.statuses).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></div>
              <div className="field"><label htmlFor="adv-priority">Priority</label><select id="adv-priority" name="priority" defaultValue={get('priority')}><option value="">Any</option>{Object.entries(l.priorities).map(([k, s]) => <option key={k} value={k}>{s.label}</option>)}</select></div>
              <div className="field"><label htmlFor="adv-topic">Topic</label><select id="adv-topic" name="topic" defaultValue={get('topic')}><option value="">Any</option>{Object.entries(l.topics).map(([k, s]) => <option key={k} value={k}>{s}</option>)}</select></div>
              <div className="field"><label htmlFor="adv-channel">Came in by</label><select id="adv-channel" name="channel" defaultValue={get('channel')}><option value="">Any</option>{Object.keys(l.channels).map((k) => <option key={k}>{k}</option>)}</select></div>
              <div className="field"><label htmlFor="adv-owner">Owner</label><select id="adv-owner" name="owner" defaultValue={get('owner')}><option value="">Anyone</option>{data.people.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></div>
              <div className="field"><label htmlFor="adv-handled">Handled by</label><select id="adv-handled" name="handledBy" defaultValue={get('handledBy')}><option value="">Anyone</option>{data.people.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select><span className="help">Owned, replied to or wrote a note on it.</span></div>
              <div className="field"><label htmlFor="adv-from">Opened from</label><DateInput id="adv-from" name="from" defaultValue={get('from')} /></div>
              <div className="field"><label htmlFor="adv-to">Opened to</label><DateInput id="adv-to" name="to" defaultValue={get('to')} /></div>
              <div><button className="btn cyan">Search</button></div>
            </div>
          </details>
        </div>
      </form>

      <div className="chips small" style={{ marginBottom: 12 }}>
        {views.map(([k, label]) => <Link key={k} className={`chip-link${view === k ? ' on' : ''}`} to={url({ view: k, page: null })}>{label} · {data.counts[k]}</Link>)}
      </div>

      <div className="panel"><div className="panel-head"><h2>{data.total.toLocaleString()} {data.total === 1 ? 'ticket' : 'tickets'}</h2></div>
        <div className="table-wrap"><table>
          <thead><tr><th>Ticket</th><th>Subject</th><th>Customer</th><th>Status</th><th>Owner</th><th>Rating</th></tr></thead>
          <tbody>
            {data.tickets.map((t) => <TicketRow key={t.id} t={t} lists={l} />)}
            {data.tickets.length === 0 && <EmptyRow cols={6}>No tickets here.</EmptyRow>}
          </tbody>
        </table></div>
        <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => navigate(url({ page: String(p) }))} />
      </div>
    </>
  );
}
