import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, dateTime, qs, useLoad } from '../../api';
import { Pager, Status, duration, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Corral → Phone Calls, with filters. */
export function Calls() {
  const [params, setParams] = useSearchParams();
  const f = Object.fromEntries(['start', 'end', 'account', 'phone', 'agent', 'user', 'direction', 'issue'].map((k) => [k, params.get(k) ?? '']));
  const page = Number(params.get('page') ?? 1);
  const { data, error } = useLoad<any>(`/api/corral/calls?${qs({ ...f, page })}`);
  const filter = (e: FormEvent<HTMLFormElement>) => { e.preventDefault(); setParams(qs(Object.fromEntries(new FormData(e.currentTarget).entries()) as any)); };

  return (
    <>
      <div className="row spread"><h1>Phone Calls</h1><Link className="button" to="/corral/contacts/new">New Contact</Link></div>
      <form className="row" onSubmit={filter} key={params.toString()}>
        <input name="start" type="date" defaultValue={f.start} /><input name="end" type="date" defaultValue={f.end} />
        <input name="account" placeholder="Account" defaultValue={f.account} style={{ maxWidth: 140 }} />
        <input name="phone" placeholder="Phone" defaultValue={f.phone} style={{ maxWidth: 140 }} />
        <input name="agent" placeholder="Agent ID" defaultValue={f.agent} style={{ maxWidth: 110 }} />
        <select name="user" defaultValue={f.user}><option value="">Anyone</option>{data?.users.map((u: any) => <option key={u.id} value={u.id}>{u.name}</option>)}</select>
        <select name="direction" defaultValue={f.direction}><option value="">In and out</option><option value="inbound">Inbound</option><option value="outbound">Outbound</option></select>
        <select name="issue" defaultValue={f.issue}><option value="">Any issue</option>{data?.issues.map((i: string) => <option key={i}>{i}</option>)}</select>
        <button className="primary">Show</button>
      </form>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : (
        <>
          <p className="muted">{data.total} calls</p>
          <table>
            <thead><tr><th>ID</th><th>Called</th><th>Customer</th><th>Phone</th><th>Issue</th><th className="num">Duration</th><th>Answered by</th><th>Resolution</th></tr></thead>
            <tbody>{data.rows.map((c: any) => (
              <tr key={c.id}><td><Link to={`/corral/calls/${c.id}`}><b>#{c.id}</b></Link></td><td>{dateTime(c.startedAt)}<div className="muted">{c.direction}</div></td>
                <td>{c.customerId ? <Link to={`/corral/customers/${c.customerId}`}>{c.customer}</Link> : '—'}</td><td>{c.phone}</td><td>{c.issue ?? '—'}</td>
                <td className="num">{duration(c.durationSec)}</td><td>{c.answeredBy ?? '—'}{c.agentId && <div className="mono muted">{c.agentId}</div>}</td><td><Status value={c.resolution} /></td></tr>
            ))}</tbody>
          </table>
          <Pager page={page} total={data.total} size={25} onPage={(p) => setParams(qs({ ...f, page: p }))} />
        </>
      )}
    </>
  );
}

/** One call: what was said, and how it ended. */
export function CallTranscript() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/corral/calls/${id}`);
  const [run, bar] = useRun(reload);
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const { call, customer: c } = data;
  return (
    <>
      <p><Link to="/corral/calls">← Phone Calls</Link></p>
      <h1>Call #{call.id}</h1>
      <p>{dateTime(call.startedAt)} · {call.direction} · {call.phone} · {duration(call.durationSec)}{call.issue && ` · ${call.issue}`}{call.answeredBy && ` · answered by ${call.answeredBy}`}{call.agentId && ` (${call.agentId})`}</p>
      {c && <p><Link to={`/corral/customers/${c.id}`}>{c.name} #{c.account}</Link></p>}
      {bar}
      <form className="row" onSubmit={(e) => { e.preventDefault(); run(api.post(`/api/corral/resolution/call/${call.id}`, { resolution: new FormData(e.currentTarget).get('resolution') }), 'Saved.'); }}>
        <label style={{ flexDirection: 'row', margin: 0 }}>Resolution <select name="resolution" defaultValue={call.resolution ?? ''}>{data.resolutions.map((r: string) => <option key={r}>{r}</option>)}</select></label>
        <button>Save</button>{call.resolvedBy && <small>by {call.resolvedBy}</small>}
      </form>
      <section className="card"><h2>Transcript</h2>{call.transcript ? <p className="wrap-text">{call.transcript}</p> : <p className="muted">No transcript.</p>}</section>
    </>
  );
}

/** New Contact: log a call, email, text or chat that happened, and how it ended. */
export function NewContact() {
  const [params] = useSearchParams();
  const [account, setAccount] = useState(params.get('account') ?? '');
  const { data } = useLoad<any>(`/api/corral/contacts/new?${qs({ account: params.get('account') })}`);
  const navigate = useNavigate();
  const [channel, setChannel] = useState('call');
  const [run, bar] = useRun();
  if (!data) return <p>Loading…</p>;

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    run(api.post<{ goTo: string; message: string }>('/api/corral/contacts', {
      account, channel, direction: f.get('direction'), issue: f.get('issue'), resolution: f.get('resolution'), minutes: Number(f.get('minutes') || 0), body: f.get('body'),
    }).then((r) => { navigate(r.goTo); return r; }));
  };

  return (
    <>
      <h1>New Contact</h1>
      {bar}
      <form className="card" onSubmit={submit}>
        <div className="row">
          <label>Account<input value={account} onChange={(e) => setAccount(e.target.value)} required /></label>
          <label>Kind<select value={channel} onChange={(e) => setChannel(e.target.value)}>{Object.entries(data.channels).map(([k, v]) => <option key={k} value={k}>{v as string}</option>)}</select></label>
          <label>Direction<select name="direction"><option value="in">From the customer</option><option value="out">To the customer</option></select></label>
          {channel === 'call' && <label>Minutes<input name="minutes" type="number" min="0" max="600" /></label>}
        </div>
        <div className="row">
          <label>Issue<input name="issue" list="issues" /><datalist id="issues">{data.issues.map((i: string) => <option key={i} value={i} />)}</datalist></label>
          <label>How it ended<select name="resolution">{data.resolutions.map((r: string) => <option key={r}>{r}</option>)}</select></label>
        </div>
        <label>What happened<textarea name="body" rows={4} required /></label>
        <button className="primary">Log it</button>
      </form>
    </>
  );
}
