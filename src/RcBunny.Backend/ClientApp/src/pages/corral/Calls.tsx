import { type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, qs, useLoad } from '../../api';
import { AccountNo, EmptyRow, Field, PageHead, PagerBar, Panel, PhoneActions, ResolutionForm, ResolutionPill, duration, longTime, resolutions, useRun } from '../../ui';
import { DateInput } from '../../fields';

/* eslint-disable @typescript-eslint/no-explicit-any */

const cap = (s?: string) => (s ? s[0].toUpperCase() + s.slice(1) : '');

/** Corral → Phone Calls (ebunny's corral/calls/index): search on the left, recent calls on the right. */
export function Calls() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['start', 'end', 'account', 'phone', 'issue', 'user', 'agent', 'direction'].map((k) => [k, params.get(k) ?? '']));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/calls?${qs({ ...f, page })}`);
  const navigate = useNavigate();
  const filter = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs(Object.fromEntries(new FormData(e.currentTarget).entries()) as any)); };

  return (
    <>
      <PageHead title="Phone Calls" sub="Recent calls with customers: the issue, how long it took, who answered, how it ended and the transcript."
        actions={<Link className="btn cyan" to={`/corral/contacts/new?${qs({ account: f.account, channel: 'call' })}`}>New Contact</Link>} />
      {error && <div className="flash bad">{error}</div>}
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={filter} key={params.toString()}><div className="panel-head"><h2>Search Calls</h2></div><div className="panel-body">
            <Field label="Start" htmlFor="start"><DateInput id="start" name="start" defaultValue={f.start} /></Field>
            <Field label="End" htmlFor="end"><DateInput id="end" name="end" defaultValue={f.end} /></Field>
            <Field label="Account Number" htmlFor="account"><input id="account" name="account" defaultValue={f.account} inputMode="numeric" /></Field>
            <Field label="Phone Number" htmlFor="phone"><input id="phone" name="phone" defaultValue={f.phone} inputMode="tel" /></Field>
            <Field label="Issue" htmlFor="issue"><select id="issue" name="issue" defaultValue={f.issue}><option value="">Any</option>{data?.issues.map((i: string) => <option key={i}>{i}</option>)}</select></Field>
            <Field label="Answered By" htmlFor="user"><select id="user" name="user" defaultValue={f.user}><option value="">Anyone</option>{data?.users.map((u: any) => <option key={u.id} value={u.id}>{u.name}</option>)}</select></Field>
            <Field label="Agent Id" htmlFor="agent"><input id="agent" name="agent" defaultValue={f.agent} /></Field>
            <Field label="Direction" htmlFor="direction"><select id="direction" name="direction" defaultValue={f.direction}><option value="">Both</option><option value="inbound">Inbound</option><option value="outbound">Outbound</option></select></Field>
            <button className="btn">Search</button>{Object.values(f).some(Boolean) && <Link className="btn ghost" to="/corral/calls">Clear</Link>}
          </div></form>
        </aside>
        <div className="search-main">
          <Panel title="Recent Calls" right={<span className="muted">{data?.total.toLocaleString()}</span>}>
            <div className="table-wrap"><table>
              <thead><tr><th>ID</th><th>Called</th><th>Account</th><th>Issue</th><th className="num">Duration</th><th>Answered By</th><th>Resolution</th></tr></thead>
              <tbody>
                {data?.rows.map((p: any) => (
                  <tr key={p.id} className="click" onClick={() => navigate(`/corral/calls/${p.id}/transcript`)}>
                    <td className="nowrap"><Link to={`/corral/calls/${p.id}/transcript`}><b>#{p.id}</b></Link></td>
                    <td className="nowrap">{dateTime(p.startedAt)}<br /><span className="help">{cap(p.direction)}</span></td>
                    <td>{p.customerId && <><Link to={`/corral/customers/${p.customerId}`}>{p.account}</Link><br /><span className="help">{p.customer}</span><br /></>}<span className="help mono">{p.phone}</span></td>
                    <td>{p.issue ?? '—'}</td><td className="num">{duration(p.durationSec)}</td>
                    <td>{p.answeredBy ?? '—'}{p.agentId && <><br /><span className="help mono">{p.agentId}</span></>}</td>
                    <td><ResolutionPill value={p.resolution} /></td>
                  </tr>
                ))}
                {data?.rows.length === 0 && <EmptyRow cols={7}>No calls match.</EmptyRow>}
              </tbody>
            </table></div>
            {data && <PagerBar page={page} total={data.total} size={25} onPage={(p) => setParams(qs({ ...f, page: p }))} />}
          </Panel>
        </div>
      </div>
    </>
  );
}

/** One call (ebunny's corral/calls/transcript): the call and how it ended, and what was said. */
export function CallTranscript() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/calls/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const { call, customer: c } = data;
  const lines = String(call.transcript ?? '').split(/\r?\n/).filter(Boolean);
  return (
    <>
      <PageHead title={`Phone Call${c ? `: ${c.name}` : ''}`}
        sub={`${longTime(call.startedAt)} · ${cap(call.direction)} · ${Math.floor(call.durationSec / 60)} min ${call.durationSec % 60} sec`}
        actions={<>
          <Link className="btn ghost" to="/corral/calls">Back to Phone Calls</Link>
          {c && <Link className="btn ghost" to={`/corral/customers/${c.id}`}>Back to account</Link>}
          <Link className="btn cyan" to={`/corral/contacts/new?${qs({ account: c?.account, channel: 'call' })}`}>New Contact</Link>
        </>} />
      {bar}
      <div className="grid-2" style={{ alignItems: 'start', gridTemplateColumns: 'minmax(0,1fr) minmax(0,2fr)' }}>
        <div className="panel"><div className="panel-head"><h2>Call</h2></div>
          <div className="panel-body"><dl className="kv">
            <dt>Account</dt><dd><AccountNo id={c?.id} account={c?.account} /><br /><span className="help">{c?.name}</span></dd>
            <dt>Phone</dt><dd className="mono"><PhoneActions phone={call.phone} account={c?.account} /></dd>
            <dt>Issue</dt><dd>{call.issue ?? '—'}</dd>
            <dt>Answered by</dt><dd>{call.answeredBy ?? '—'} <span className="mono muted">{call.agentId}</span></dd>
            <dt>Duration</dt><dd>{duration(call.durationSec)}</dd>
            <dt>Resolution</dt><dd><ResolutionPill value={call.resolution} /></dd>
          </dl></div>
          <div className="panel-body" style={{ borderTop: '1px solid var(--line)' }}><h3>How did it end?</h3>
            <ResolutionForm value={call.resolution} by={call.resolvedBy} onSave={(r) => run(api.post(`/api/corral/resolution/call/${call.id}`, { resolution: r }), 'Resolution saved')} />
          </div>
        </div>
        <div className="panel"><div className="panel-head"><h2>Transcript</h2></div><div className="panel-body transcript">
          {lines.map((line, i) => {
            const [who, said] = line.includes(':') ? [line.slice(0, line.indexOf(':')).trim(), line.slice(line.indexOf(':') + 1).trim()] : ['', line];
            return <p key={i} className={who.toLowerCase() === 'agent' ? 'agent' : ''}><b>{who}</b> {said}</p>;
          })}
          {lines.length === 0 && <p className="muted">No transcript for this call.</p>}
        </div></div>
      </div>
    </>
  );
}

/** New Contact (ebunny's corral/contact-form): log a call, email, text or chat that happened, and how it ended. */
export function NewContact() {
  const [params] = useSearchParams();
  const { data } = useLoad<any>(`/api/corral/contacts/new?${qs({ account: params.get('account') })}`);
  const navigate = useNavigate();
  const [run, bar] = useRun();
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.customer;

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post<{ goTo: string; message: string }>('/api/corral/contacts', {
      account: f.get('account'), channel: f.get('channel'), direction: f.get('direction'), issue: f.get('issue'), resolution: f.get('resolution'),
      minutes: Number(f.get('minutes') || 0), body: f.get('body'),
    }).then((r) => { navigate(r.goTo, { state: r }); return r; }));
  };

  return (
    <>
      <PageHead title="New Contact" sub="Log a call, email, text or chat with a customer, and how it ended. It goes on the account's contact history and notes."
        actions={c ? <Link className="btn ghost" to={`/corral/customers/${c.id}`}>Back to account</Link> : undefined} />
      {bar}
      <form className="panel simple-form" onSubmit={submit}>
        <div className="panel-body form-grid">
          <label htmlFor="ct-account">Account</label><div><input id="ct-account" name="account" required className="mono" defaultValue={c?.account ?? params.get('account') ?? ''} style={{ maxWidth: '20ch' }} />
            {c && <div className="help">{c.name} · {c.phone} · {c.email}</div>}</div>
          <label htmlFor="sel-channel">Kind</label>
          <select name="channel" id="sel-channel" style={{ maxWidth: 320 }} defaultValue={params.get('channel') ?? 'call'}>{Object.entries(data.channels).map(([k, l]) => <option key={k} value={k}>{l as string}</option>)}</select>
          <label>Direction</label>
          <div className="chips small"><label><input type="radio" name="direction" value="in" defaultChecked /><span>From the customer</span></label><label><input type="radio" name="direction" value="out" /><span>To the customer</span></label></div>
          <label htmlFor="ct-issue">Issue</label><div><input id="ct-issue" name="issue" maxLength={60} placeholder="e.g. Balance Inquiry" />
            {/* ebunny's pills.js shows more than 3 pill buttons as one dropdown */}
            <select className="pill-select" style={{ marginTop: 6 }} defaultValue="" onChange={(e) => { (document.getElementById('ct-issue') as HTMLInputElement).value = e.target.value; e.target.value = ''; }}>
              <option value="" disabled>Pick one…</option>{data.issues.slice(0, 12).map((i: string) => <option key={i} value={i}>{i}</option>)}
            </select></div>
          <label htmlFor="ct-min">Minutes</label><div><input id="ct-min" name="minutes" type="number" min="0" max="600" style={{ maxWidth: '10ch' }} /><div className="help">Phone calls only.</div></div>
          <label htmlFor="ct-body">What happened</label><textarea id="ct-body" name="body" required maxLength={5000} style={{ minHeight: 120 }} />
          <label htmlFor="ct-res">Resolution</label><select id="ct-res" name="resolution" required defaultValue="Resolved">{Object.keys(resolutions).map((r) => <option key={r}>{r}</option>)}</select>
        </div>
        <div className="panel-body actions"><button className="btn cyan">Save Contact</button><Link className="btn ghost" to={c ? `/corral/customers/${c.id}` : '/corral/calls'}>Cancel</Link></div>
      </form>
    </>
  );
}
