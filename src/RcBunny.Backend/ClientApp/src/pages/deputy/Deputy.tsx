import { useEffect, useState } from 'react';
import { Link, Route, Routes, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import AppShell, { ago, type Menu } from '../../Sidebar';
import { AccountNo, EmptyRow, PageHead, PagerBar, Pill, ResolutionForm, toast, useRun } from '../../ui';
import { DateInput } from '../../fields';
import { when } from '../walker/shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

// ebunny's config/admin.php → deputy menu
const menu: Menu = [
  { heading: 'AI Agents', links: [['Dashboard', ''], ['Conversations', 'conversations', { badge: 'ai_waiting' }]] },
  { heading: 'Setup', links: [['Agents', 'agents'], ['New Agent', 'agents/new'], ['Models', 'models'], ['Add a Model', 'models/new']] },
];

export default function Deputy() {
  return (
    <AppShell app="deputy" menu={menu}>
      <Routes>
        <Route index element={<Dashboard />} />
        <Route path="conversations" element={<Conversations />} />
        <Route path="conversations/:id" element={<Conversation />} />
        <Route path="agents" element={<Agents />} />
        <Route path="agents/new" element={<AgentForm />} />
        <Route path="agents/:id/edit" element={<AgentForm />} />
        <Route path="agents/:id/test" element={<AgentTest />} />
        <Route path="models" element={<Models />} />
        <Route path="models/new" element={<ModelForm />} />
        <Route path="models/:id/edit" element={<ModelForm />} />
      </Routes>
    </AppShell>
  );
}

const outcomeTones: Record<string, string> = { resolved: 'ok', handed_off: 'info', abandoned: 'warn', failed: 'bad' };
const statusTones: Record<string, string> = { bot: 'info', waiting: 'warn', agent: 'ok', closed: '' };
/** ebunny's deputy/_outcome */
const Outcome = ({ c }: { c: any }) => <Pill tone={outcomeTones[c.outcome] ?? ''}>{c.outcomeLabel}</Pill>;
/** ebunny's deputy/_model: a model name with its provider. */
const ModelName = ({ m }: { m: any }) => m ? <span className={`dp-model ${m.local ? 'local' : 'hosted'}`}><b>{m.name}</b><small className="mono">{m.modelId}</small></span> : <span className="muted">—</span>;
const mmss = (sec: number) => `${Math.floor(sec / 60)}:${String(sec % 60).padStart(2, '0')}`;
const temp = (t: number) => String(Number(t.toFixed(2)));
const short = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');

/** Deputy → Dashboard (ebunny's deputy/dashboard). */
function Dashboard() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any>('/api/deputy/dashboard');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="AI Agents" sub="Which open-weight model each agent runs on, and how the agents are doing over the last 30 days. Downloaded models run on your local model server; hosted ones through the open models API."
        actions={<><Link className="btn ghost" to="/deputy/conversations">Conversations</Link><Link className="btn cyan" to="/deputy/agents/new">New Agent</Link></>} />
      {([['hosted', 'Open Models API', 'Integrations:OpenModels:Url'], ['local', 'Local Models', 'Integrations:LocalModels:Url']] as const).map(([k, name, env]) => !data.ready[k] && (
        <div key={k} className="banner-note" style={{ marginBottom: 12 }}><b>{name}</b> isn't set up yet: agents on {k === 'local' ? 'downloaded models' : 'hosted models'} can't answer until <span className="mono">{env}</span> is set in Sheriff → APIs → {name}.</div>
      ))}
      <div className="grid-4 stat-row">
        <Link className="stat" to="/deputy/conversations#needs-person"><span>Needs a Person</span><strong>{(data.live.waiting ?? 0).toLocaleString()}</strong><small>{(data.live.agent ?? 0).toLocaleString()} with a person, {(data.live.bot ?? 0).toLocaleString()} with the bot</small></Link>
        <Link className="stat" to="/deputy/conversations"><span>Texts &amp; Chats</span><strong>{data.total.toLocaleString()}</strong><small>last 30 days</small></Link>
        <div className="stat"><span>Resolved by the Bot</span><strong>{data.resolvedRate}%</strong><small>{data.handed.toLocaleString()} handed to a person · {data.tokens.toLocaleString()} tokens</small></div>
        <Link className="stat" to="/deputy/models"><span>Models</span><strong>{data.models.length}</strong><small>{data.local} downloaded, {data.models.length - data.local} hosted</small></Link>
      </div>
      <div className="panel" style={{ marginBottom: 20 }}>
        <div className="panel-head"><h2>Model by Agent</h2><Link className="btn sm ghost" to="/deputy/agents">Agents</Link></div>
        <div className="table-wrap"><table className="table">
          <thead><tr><th>Agent</th><th>Model</th><th>Fallback</th><th className="num">Conversations</th><th className="num">Resolved</th><th className="num">Avg Length</th><th className="num">Tokens</th></tr></thead>
          <tbody>
            {data.agents.map((a: any) => {
              const s = a.stats;
              return (
                <tr key={a.id} className={a.active ? '' : 'muted'}><td><Link to={`/deputy/agents/${a.id}/edit`}><b>{a.name}</b></Link>
                  <div className="help">{a.activityLabel} · temperature {temp(a.temperature)}{a.active ? '' : ' · Paused'} · <Link to={`/deputy/agents/${a.id}/test`}>Test</Link></div></td>
                  <td><ModelName m={a.model} /></td><td><ModelName m={a.fallback} /></td>
                  <td className="num">{s ? <Link to={`/deputy/conversations?${qs({ agent: a.id })}`}>{s.n.toLocaleString()}</Link> : 0}<div className="help">30 days</div></td>
                  <td className="num">{s?.n ? `${Math.round((100 * s.resolved) / s.n)}%` : '—'}</td>
                  <td className="num">{s ? mmss(s.avgSec) : '—'}</td><td className="num">{s ? s.tokens.toLocaleString() : '—'}</td></tr>
              );
            })}
            {data.agents.length === 0 && <EmptyRow cols={7}>No agents yet.</EmptyRow>}
          </tbody>
        </table></div>
      </div>
      <div className="grid-2">
        <div className="panel"><div className="panel-head"><h2>Models in Use</h2><Link className="btn sm ghost" to="/deputy/models">Models</Link></div>
          <div className="table-wrap"><table>
            <thead><tr><th>Model</th><th className="num">Agents</th><th className="num">Conversations</th><th className="num">Tokens</th></tr></thead>
            <tbody>{data.models.map(({ model: m, agents, stats: u }: any) => (
              <tr key={m.id}><td><ModelName m={m} /><div className="help">{m.providerLabel}{m.status !== 'available' && ` · ${m.status.charAt(0).toUpperCase()}${m.status.slice(1)}`}</div></td>
                <td className="num">{agents}</td><td className="num">{u ? u.n.toLocaleString() : 0}</td><td className="num">{u ? u.tokens.toLocaleString() : 0}</td></tr>
            ))}</tbody>
          </table></div>
        </div>
        <div className="panel"><div className="panel-head"><h2>Latest Conversations</h2><Link className="btn sm ghost" to="/deputy/conversations">All</Link></div>
          <div className="table-wrap"><table><tbody>
            {data.recent.map((c: any) => (
              <tr key={c.id} className="click" onClick={() => navigate(`/deputy/conversations/${c.id}`)}><td className="nowrap"><Link to={`/deputy/conversations/${c.id}`}><b>#{c.id}</b></Link></td>
                <td className="nowrap">{short(c.startedAt)}<div className="help">{c.channelLabel}</div></td>
                <td>{c.topic}<div className="help">{c.agent?.name}</div></td><td><Outcome c={c} /></td></tr>
            ))}
            {data.recent.length === 0 && <tr><td className="empty">No conversations yet.</td></tr>}
          </tbody></table></div>
        </div>
      </div>
    </>
  );
}

/** Deputy → Conversations (ebunny's deputy/conversations): live ones on top, then a search of every conversation. */
function Conversations() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/deputy/conversations?${sp}`);
  const [run, bar] = useRun(reload);
  // Keep Needs a Person and Live Now current without reloading the page
  useEffect(() => { const t = setInterval(() => { if (!document.hidden) reload(); }, 10000); return () => clearInterval(t); }, [reload]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const f = Object.fromEntries(sp);
  const search = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const v = Object.fromEntries([...new FormData(e.currentTarget)].filter(([, x]) => x !== '')) as Record<string, string>;
    navigate(`/deputy/conversations?${qs(v)}`);
  };
  const take = (id: number) => run(api.post(`/api/deputy/conversations/${id}/take-over`).then((x) => { navigate(`/deputy/conversations/${id}`); return x; }));
  return (
    <>
      <PageHead title="Conversations" sub="Texts and My Account chats. The bot answers first; when it hands off, or you take over, you reply here or call the customer." />
      {bar}
      {([['Needs a Person', data.waiting, 'Nobody is waiting on a person right now.', 'needs-person'], ['Live Now', data.answering, 'No other conversations are open.', 'live-now']] as const).map(([heading, list, none, id], i) => (
        <div key={id} className="panel" style={{ marginBottom: 20 }} id={id}>
          <div className="panel-head"><h2>{heading} <span className="count-pill">{list.length}</span></h2>{i === 0 && <span className="muted">oldest first</span>}</div>
          <div className="table-wrap"><table className="table" data-page="0">
            <thead><tr><th>Last Message</th><th>Channel</th><th>Account</th><th>Issue</th><th>Who's Answering</th><th></th></tr></thead>
            <tbody>
              {list.map((c: any) => (
                <tr key={c.id} className="click" onClick={() => navigate(`/deputy/conversations/${c.id}`)}>
                  <td className="nowrap">{c.lastMessageAt && ago(c.lastMessageAt)}</td><td>{c.channelLabel}</td>
                  <td onClick={(e) => e.stopPropagation()}>{c.customer && <><AccountNo id={c.customer.id} account={c.customer.account} /><br /><span className="help">{c.customer.name}</span></>}</td>
                  <td>{c.topic}</td>
                  <td><Pill tone={statusTones[c.status] ?? ''}>{c.statusLabel}</Pill>{c.status === 'agent' && c.handedTo && <><br /><span className="help">{c.handedTo.name}</span></>}</td>
                  <td className="actions" onClick={(e) => e.stopPropagation()}>{c.status !== 'agent' || c.handedToId !== data.me
                    ? <button className="btn sm cyan" onClick={() => take(c.id)}>Take Over</button> : <Link className="btn sm" to={`/deputy/conversations/${c.id}`}>Open</Link>}</td></tr>
              ))}
              {list.length === 0 && <EmptyRow cols={6}>{none}</EmptyRow>}
            </tbody>
          </table></div>
        </div>
      ))}
      <h2 style={{ margin: '4px 0 12px' }}>All Conversations <span className="count-pill">{data.total.toLocaleString()}</span></h2>
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" onSubmit={search} key={sp.toString()}><div className="panel-head"><h2>Search Conversations</h2></div><div className="panel-body">
            <div className="field"><label htmlFor="start">Start</label><DateInput id="start" name="start" defaultValue={f.start ?? ''} /></div>
            <div className="field"><label htmlFor="end">End</label><DateInput id="end" name="end" defaultValue={f.end ?? ''} /></div>
            <div className="field"><label htmlFor="account">Account Number</label><input id="account" name="account" defaultValue={f.account ?? ''} inputMode="numeric" /></div>
            <div className="field"><label htmlFor="agent">Agent</label><select id="agent" name="agent" defaultValue={f.agent ?? ''}><option value="">Any</option>{data.agents.map((a: any) => <option key={a.id} value={a.id}>{a.name}</option>)}</select></div>
            <div className="field"><label htmlFor="model">Model</label><select id="model" name="model" defaultValue={f.model ?? ''}><option value="">Any</option>{data.models.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select></div>
            <div className="field"><label htmlFor="channel">Channel</label><select id="channel" name="channel" defaultValue={f.channel ?? ''}><option value="">Any</option>{Object.entries(data.channels as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></div>
            <div className="field"><label htmlFor="topic">Issue</label><select id="topic" name="topic" defaultValue={f.topic ?? ''}><option value="">Any</option>{data.topics.map((t: string) => <option key={t}>{t}</option>)}</select></div>
            <div className="field"><label htmlFor="outcome">Outcome</label><select id="outcome" name="outcome" defaultValue={f.outcome ?? ''}><option value="">Any</option>{Object.entries(data.outcomes as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></div>
            <button className="btn">Search</button>{Object.values(f).some(Boolean) && <Link className="btn ghost" to="/deputy/conversations">Clear</Link>}
          </div></form>
        </aside>
        <div className="search-main">
          <div className="panel"><div className="table-wrap"><table className="table" data-page="0">
            <thead><tr><th>ID</th><th>Started</th><th>Account</th><th>Issue</th><th className="num">Length</th><th>Agent &amp; Model</th><th>Outcome</th><th>Resolution</th></tr></thead>
            <tbody>
              {data.conversations.map((c: any) => (
                <tr key={c.id} className="click" onClick={() => navigate(`/deputy/conversations/${c.id}`)}>
                  <td className="nowrap"><Link to={`/deputy/conversations/${c.id}`}><b>#{c.id}</b></Link></td><td className="nowrap">{when(c.startedAt)}<br /><span className="help">{c.channelLabel}</span></td>
                  <td onClick={(e) => e.stopPropagation()}>{c.customer ? <><AccountNo id={c.customer.id} account={c.customer.account} /><br /><span className="help">{c.customer.name}</span></> : c.tester ? <span className="help">Test by {c.tester}</span> : <span className="muted">—</span>}</td>
                  <td>{c.topic}</td><td className="num">{mmss(c.durationSec)}</td>
                  <td>{c.agent?.name}<br /><span className="help">{c.model?.name ?? 'No model answered'}</span></td>
                  <td>{c.live ? <Pill tone="warn">{c.statusLabel}</Pill> : <Outcome c={c} />}{c.handedTo && <><br /><span className="help">to {c.handedTo.name}</span></>}</td>
                  <td>{c.resolution ? <Pill tone={({ Resolved: 'ok', Incomplete: 'warn', 'Follow-up Needed': 'info', Escalated: 'bad' } as Record<string, string>)[c.resolution] ?? ''}>{c.resolution}</Pill> : <span className="muted">—</span>}</td></tr>
              ))}
              {data.conversations.length === 0 && <EmptyRow cols={8}>No conversations match.</EmptyRow>}
            </tbody>
          </table></div>
            <PagerBar page={data.page} total={data.total} size={50} onPage={(p) => navigate(`/deputy/conversations?${qs({ ...f, page: p })}`)} /></div>
        </div>
      </div>
    </>
  );
}

/** One conversation (ebunny's deputy/conversation and _live): the transcript updates every few seconds while it's live. */
function Conversation() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<any>(`/api/deputy/conversations/${id}`);
  const [run, bar] = useRun(reload);
  const [dial, setDial] = useState<string | null>(null);
  const [message, setMessage] = useState('');
  const live = data?.conversation.live;
  useEffect(() => { if (!live) return; const t = setInterval(() => { if (!document.hidden) reload(); }, 4000); return () => clearInterval(t); }, [live, reload]);
  const lines = data?.lines.length ?? 0;
  useEffect(() => { const t = document.getElementById('transcript'); if (t) t.scrollTop = t.scrollHeight; }, [lines]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.conversation, mine = data.mine;
  const post = (action: string) => run(api.post(`/api/deputy/conversations/${c.id}/${action}`));
  const call = () => run(api.post<any>(`/api/deputy/conversations/${c.id}/call`).then((r) => { setDial(r.dial); location.href = `tel:${r.dial}`; return r; }));
  const send = (e: React.FormEvent) => { e.preventDefault(); run(api.post(`/api/deputy/conversations/${c.id}/reply`, { message }).then((r) => { setMessage(''); return r; })); };
  const started = new Date(c.startedAt).toLocaleString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(' at ', ' ');
  const hint = mine ? 'The bot is quiet while you have this conversation.' : c.status === 'waiting' ? 'The bot handed this to a person. Sending takes it over.'
    : c.status === 'agent' ? 'Someone else has this conversation. Sending takes it over from them.' : 'The bot is answering. Sending takes it over.';
  return (
    <>
      <PageHead title={`${c.topic}${c.customer ? `: ${c.customer.name}` : ''}`} sub={`${started} · ${c.channelLabel} · ${Math.floor(c.durationSec / 60)} min ${c.durationSec % 60} sec`}
        actions={<><Link className="btn ghost" to="/deputy/conversations">Back to Conversations</Link>{c.agent && <Link className="btn ghost" to={`/deputy/agents/${c.agent.id}/test`}>Test this Agent</Link>}</>} />
      {bar}
      {dial && <div className="banner-note" style={{ marginBottom: 12 }}>Calling <a href={`tel:${dial}`} id="dial">{dial}</a>. If your phone app didn't open, click the number.</div>}
      <div className="grid-2" style={{ alignItems: 'start', gridTemplateColumns: 'minmax(0,1fr) minmax(0,2fr)' }}>
        <div className="panel"><div className="panel-head"><h2>Conversation</h2><Outcome c={c} /></div><div className="panel-body"><dl className="kv">
          <dt>Account</dt><dd>{c.customer?.account ?? '—'}{c.customer && <><br /><span className="help">{c.customer.name}</span></>}</dd>
          <dt>Phone</dt><dd>{c.customer?.phone ?? '—'}</dd>
          <dt>Channel</dt><dd>{c.channelLabel}</dd>
          <dt>Bot</dt><dd>{c.agent ? <><Link to={`/deputy/agents/${c.agent.id}/edit`}>{c.agent.name}</Link><br /><span className="help">{data.agentActivity}</span></> : '—'}</dd>
          <dt>Model</dt><dd><ModelName m={c.model} /></dd>
          <dt>Issue</dt><dd>{c.topic}</dd>
          <dt>Handed to</dt><dd>{c.handedTo?.name ?? '—'}</dd>
          {c.tester && <><dt>Tested by</dt><dd>{c.tester}</dd></>}
          <dt>Tokens</dt><dd>{data.inputTokens.toLocaleString()} in · {data.outputTokens.toLocaleString()} out</dd>
          <dt>Last message</dt><dd>{c.lastMessageAt ? when(c.lastMessageAt) : '—'}</dd>
        </dl></div>
          <div className="panel-body" style={{ borderTop: '1px solid var(--line)' }}><h3>Resolution</h3>
            <ResolutionForm value={c.resolution} by={data.resolvedBy && `${data.resolvedBy}${data.resolvedAt ? `, ${ago(data.resolvedAt)}` : ''}`}
              onSave={(r) => run(api.post(`/api/deputy/conversations/${c.id}/resolution`, { resolution: r }))} /></div>
        </div>
        <div className="panel">
          <div id="live">
            <div className="panel-head" style={{ flexWrap: 'wrap', gap: 10 }}>
              <h2>Transcript</h2>
              <Pill tone={statusTones[c.status] ?? ''}>{c.statusLabel}{c.status === 'agent' && c.handedTo ? `: ${mine ? 'you' : c.handedTo.name}` : ''}</Pill>
              {live && (
                <div className="actions" style={{ marginLeft: 'auto', display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {!mine ? <button className="btn sm cyan" onClick={() => post('take-over')}>Take Over</button> : <button className="btn sm ghost" onClick={() => post('hand-back')}>Hand Back to Bot</button>}
                  {c.customer?.phone && <button className="btn sm" onClick={call}>Call {c.customer.phone}</button>}
                  <button className="btn sm ghost" onClick={() => post('close')}>Close</button>
                </div>
              )}
            </div>
            <div className="panel-body transcript" id="transcript">
              {data.lines.map((l: any, i: number) => (
                <p key={i} className={[l.who === 'Bot' || l.who.toLowerCase() === 'agent' ? 'agent' : '', l.who.endsWith('(agent)') ? 'person' : '', l.who === 'System' ? 'system' : ''].filter(Boolean).join(' ') || undefined}><b>{l.who}</b> {l.text}</p>
              ))}
              {data.lines.length === 0 && <p className="muted">No transcript.</p>}
            </div>
          </div>
          {live && (
            <form className="panel-body reply-box" id="reply" onSubmit={send}>
              <label htmlFor="message" id="reply-label">{c.channel === 'sms' ? 'Text the customer' : 'Reply in the chat'}</label>
              <textarea id="message" name="message" required maxLength={c.channel === 'sms' ? 480 : 2000} rows={3} placeholder="Type your reply…" value={message} onChange={(e) => setMessage(e.target.value)} />
              <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
                <button className="btn cyan">Send</button><span className="help" id="reply-hint">{hint}</span>
              </div>
            </form>
          )}
        </div>
      </div>
    </>
  );
}

/** Deputy → Agents (ebunny's deputy/agents). */
function Agents() {
  const { data, error } = useLoad<any[]>('/api/deputy/agents');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Agents" sub="Each agent does one job and runs on one model, with a fallback model if that one can't answer." actions={<Link className="btn cyan" to="/deputy/agents/new">New Agent</Link>} />
      <div className="panel"><div className="table-wrap"><table className="table">
        <thead><tr><th>Agent</th><th>Kind</th><th>Model</th><th>Fallback</th><th className="num">Conversations</th><th></th></tr></thead>
        <tbody>
          {data.map((a) => (
            <tr key={a.id}><td><Link to={`/deputy/agents/${a.id}/edit`}><b>{a.name}</b></Link> {a.active ? <Pill tone="ok">Active</Pill> : <Pill>Paused</Pill>}<div className="help">{a.activityLabel}{a.description && ` · ${a.description}`}</div></td>
              <td>{a.activity === 'sms' ? 'Texts' : 'My Account chat'}</td>
              <td><ModelName m={a.model} /></td><td><ModelName m={a.fallback} /></td>
              <td className="num"><Link to={`/deputy/conversations?${qs({ agent: a.id })}`}>{a.conversations.toLocaleString()}</Link><div className="help">{a.lastAt ? `last ${new Date(a.lastAt).toLocaleDateString('en-US')}` : 'none yet'}</div></td>
              <td className="actions"><Link className="btn sm ghost" to={`/deputy/agents/${a.id}/edit`}>Edit</Link><Link className="btn sm" to={`/deputy/agents/${a.id}/test`}>Test</Link></td></tr>
          ))}
          {data.length === 0 && <EmptyRow cols={6}>No agents yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

const ModelOptions = ({ models }: { models: any[] }) => {
  const groups = models.reduce((g: Record<string, any[]>, m) => ({ ...g, [m.providerLabel]: [...(g[m.providerLabel] ?? []), m] }), {});
  return <>{(Object.entries(groups) as [string, any[]][]).map(([p, list]) => <optgroup key={p} label={p}>{list.map((m) => <option key={m.id} value={m.id}>{m.name} ({m.modelId}){m.status !== 'available' ? ` — ${m.status}` : ''}</option>)}</optgroup>)}</>;
};

/** New Agent and Edit (ebunny's deputy/agent-form). */
function AgentForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(id ? `/api/deputy/agents/${id}` : '/api/deputy/agents/new');
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const a = data.agent;
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = {
      name: f.get('name'), activity: f.get('activity'), description: f.get('description'), aiModelId: Number(f.get('ai_model_id')),
      fallbackModelId: f.get('fallback_model_id') ? Number(f.get('fallback_model_id')) : null, temperature: Number(f.get('temperature')),
      maxTokens: Number(f.get('max_tokens')), systemPrompt: f.get('system_prompt'), active: f.get('active') === 'on',
    };
    run((id ? api.put(`/api/deputy/agents/${id}`, body) : api.post('/api/deputy/agents', body)).then((r) => { navigate('/deputy/agents'); return r; }));
  };
  return (
    <>
      <PageHead title={id ? `Edit ${a.name}` : 'New Agent'} sub="What the agent does, which model it runs on, and its instructions."
        actions={id ? <Link className="btn ghost" to={`/deputy/agents/${id}/test`}>Test</Link> : undefined} />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={80} defaultValue={a.name} />
          <span className="label">Activity</span>
          <div className="table-wrap"><table className="table pick-table" data-page="0">
            <thead><tr><th></th><th>Activity</th><th>Kind</th></tr></thead>
            <tbody>{Object.entries(data.activities as Record<string, string>).map(([v, l]) => (
              <tr key={v} onClick={(e) => { if ((e.target as HTMLElement).tagName !== 'INPUT') (document.getElementById(`activity-${v}`) as HTMLInputElement).checked = true; }}>
                <td><input type="radio" id={`activity-${v}`} name="activity" value={v} required defaultChecked={(a.activity ?? 'chat') === v} /></td>
                <td><label htmlFor={`activity-${v}`}>{l}</label></td><td className="help">Talks to customers ({v === 'sms' ? 'texts' : 'My Account chat bubble'})</td></tr>
            ))}</tbody>
          </table></div>
          <label htmlFor="description">Description</label><input id="description" name="description" maxLength={250} defaultValue={a.description ?? ''} />
          <label htmlFor="ai_model_id">Model</label><select id="ai_model_id" name="ai_model_id" defaultValue={a.aiModelId ?? ''}><ModelOptions models={data.models} /></select>
          <label htmlFor="fallback_model_id">Fallback model</label><div><select id="fallback_model_id" name="fallback_model_id" defaultValue={a.fallbackModelId ?? ''}><option value="">None</option><ModelOptions models={data.models.map((m: any) => ({ ...m, status: 'available' }))} /></select>
            <div className="help">Used when the main model can't answer (not set up, unreachable or erroring).</div></div>
          <label htmlFor="temperature">Temperature</label><div><input id="temperature" name="temperature" type="number" step="0.05" min="0" max="2" required defaultValue={a.temperature} style={{ maxWidth: '10ch' }} />
            <div className="help">0 gives the same careful answer every time; higher is more varied. 0.2 to 0.4 suits customer service.</div></div>
          <label htmlFor="max_tokens">Max tokens</label><input id="max_tokens" name="max_tokens" type="number" min="256" max="64000" required defaultValue={a.maxTokens} style={{ maxWidth: '14ch' }} />
          <label htmlFor="system_prompt">Instructions</label><textarea id="system_prompt" name="system_prompt" required maxLength={20000} style={{ minHeight: 220, fontFamily: 'inherit' }} defaultValue={a.systemPrompt} />
          <label htmlFor="active">Active</label><label className="check"><input id="active" type="checkbox" name="active" defaultChecked={a.active} /> On (answering customers)</label>
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save Agent' : 'Add Agent'}</button><Link className="btn ghost" to="/deputy/agents">Cancel</Link></div>
      </form>
    </>
  );
}

/** Test an agent (ebunny's deputy/agent-test): each test is a real call to the model, saved as a Test conversation. */
function AgentTest() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/deputy/agents/${id}/test`);
  const [run, bar] = useRun();
  const [message, setMessage] = useState('');
  const [result, setResult] = useState<any>(null);
  const [busy, setBusy] = useState(false);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const a = data.agent;
  const send = (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    run(api.post<any>(`/api/deputy/agents/${id}/test`, { message }).then((r) => { setResult({ ...r, sent: message }); setMessage(''); reload(); }).finally(() => setBusy(false)));
  };
  return (
    <>
      <PageHead title={`Test ${a.name}`} sub={`${a.activityLabel} · runs on ${a.model ?? '—'}${a.fallback ? `, falling back to ${a.fallback}` : ''}. Each test is a real call to the model and is saved as a Test conversation.`}
        actions={<Link className="btn ghost" to={`/deputy/agents/${id}/edit`}>Edit Agent</Link>} />
      {bar}
      {result && (
        <div className="panel" style={{ marginBottom: 20 }}><div className="panel-head"><h2>Reply</h2>
          {result.ok ? <Pill tone="ok">Answered by {result.modelName}</Pill> : <Pill tone="bad">No answer</Pill>}</div>
          <div className="panel-body transcript">
            <p><b>You</b> {result.sent}</p>
            {result.ok && <p className="agent" style={{ whiteSpace: 'pre-wrap' }}><b>{a.name}</b> {result.text}</p>}
            {result.note && <p className="muted" style={{ background: 'none', padding: 0 }}>{result.note}</p>}
            <p className="muted" style={{ background: 'none', padding: 0, fontSize: '.8rem' }}>{result.ms.toLocaleString()} ms · {result.inputTokens.toLocaleString()} tokens in · {result.outputTokens.toLocaleString()} out · <Link to={`/deputy/conversations/${result.conversation}`}>saved conversation</Link></p>
          </div>
        </div>
      )}
      <form className="panel simple-form" style={{ marginBottom: 20 }} onSubmit={send}>
        <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
          <label htmlFor="message">Message to the agent</label>
          <textarea id="message" name="message" required maxLength={4000} style={{ minHeight: 120, fontFamily: 'inherit' }} placeholder="Write what a customer might say, e.g. Why is my bill higher this month?" value={message} onChange={(e) => setMessage(e.target.value)} />
        </div>
        <div className="panel-body actions"><button className="btn cyan" disabled={busy}>{busy ? 'Sending…' : 'Send to Agent'}</button></div>
      </form>
      <div className="panel"><div className="panel-head"><h2>Recent Tests</h2></div><div className="table-wrap"><table>
        <thead><tr><th>ID</th><th>When</th><th>By</th><th>Model</th><th>Outcome</th></tr></thead>
        <tbody>
          {data.tests.map((t: any) => (
            <tr key={t.id} className="click" onClick={() => navigate(`/deputy/conversations/${t.id}`)}><td className="nowrap"><Link to={`/deputy/conversations/${t.id}`}><b>#{t.id}</b></Link></td>
              <td className="nowrap">{when(t.startedAt)}</td><td>{t.tester}</td><td>{t.model?.name ?? '—'}</td><td><Outcome c={t} /></td></tr>
          ))}
          {data.tests.length === 0 && <EmptyRow cols={5}>Not tested yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** Deputy → Models (ebunny's deputy/models). */
function Models() {
  const { data, error, reload } = useLoad<any[]>('/api/deputy/models');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const tones: Record<string, string> = { available: 'ok', downloading: 'info', missing: 'warn' };
  return (
    <>
      <PageHead title="Models" sub="Open-weight models: downloaded to your local model server, or hosted behind the open models API. Agents pick from these."
        actions={<Link className="btn cyan" to="/deputy/models/new">Add a Model</Link>} />
      {bar}
      <div className="panel"><div className="table-wrap"><table className="table">
        <thead><tr><th>Model</th><th>Provider</th><th>Source</th><th className="num">Size</th><th className="num">Context</th><th className="num">Agents</th><th>Status</th><th></th></tr></thead>
        <tbody>
          {data.map(({ model: m, ...x }) => (
            <tr key={m.id}><td><ModelName m={m} /></td><td>{m.providerLabel}</td>
              <td className="wrap">{x.source ?? '—'}{x.quantization && <div className="help">{x.quantization}</div>}</td>
              <td className="num">{x.sizeGb ? `${Number(x.sizeGb).toFixed(1)} GB` : '—'}</td>
              <td className="num">{x.contextWindow ? x.contextWindow.toLocaleString() : '—'}</td>
              <td className="num">{x.agents}{x.asFallback > 0 && <div className="help">+{x.asFallback} as fallback</div>}</td>
              <td><Pill tone={tones[m.status] ?? ''}>{m.status.charAt(0).toUpperCase() + m.status.slice(1)}</Pill>{x.checkedAt && <div className="help">checked {ago(x.checkedAt)}</div>}</td>
              <td className="actions"><button className="btn sm ghost" onClick={() => run(api.post(`/api/deputy/models/${m.id}/check`))}>Check</button><Link className="btn sm ghost" to={`/deputy/models/${m.id}/edit`}>Edit</Link></td></tr>
          ))}
          {data.length === 0 && <EmptyRow cols={8}>No models yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** Add a Model and Edit (ebunny's deputy/model-form). */
function ModelForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(id ? `/api/deputy/models/${id}` : '/api/deputy/models/new');
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const m = data.model;
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = Object.fromEntries(new FormData(e.currentTarget)) as Record<string, string>;
    const body = { ...f, sizeGb: f.sizeGb ? Number(f.sizeGb) : null, contextWindow: f.contextWindow ? Number(f.contextWindow) : null };
    run((id ? api.put(`/api/deputy/models/${id}`, body) : api.post('/api/deputy/models', body)).then((r) => { toast((r as any).message); navigate('/deputy/models'); }));
  };
  return (
    <>
      <PageHead title={id ? `Edit ${m.name}` : 'Add a Model'}
        sub={<>Any open-weight model: Qwen, Kimi, DeepSeek, Llama, Mistral, gpt-oss and so on. For a downloaded model, use its name on the local server (what <span className="mono">ollama list</span> shows, like <span className="mono">qwen3:8b</span>). For a hosted one, use the id the open models API lists (like <span className="mono">moonshotai/Kimi-K2-Instruct</span>).</>} />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={80} defaultValue={m.name} placeholder="Qwen3 8B" />
          <label htmlFor="provider">Provider</label><select id="provider" name="provider" defaultValue={m.provider}>{Object.entries(data.providers as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
          <label htmlFor="model_id">Model id</label><input id="model_id" name="modelId" required maxLength={120} className="mono" defaultValue={m.modelId} placeholder="qwen3:8b" />
          <label htmlFor="source">Downloaded from</label><input id="source" name="source" maxLength={250} defaultValue={m.source ?? ''} placeholder="ollama pull qwen3:8b, a Hugging Face repo, or a file like /models/billing-q4.gguf" />
          <label htmlFor="size_gb">Size (GB)</label><input id="size_gb" name="sizeGb" type="number" step="0.1" min="0" defaultValue={m.sizeGb ?? ''} style={{ maxWidth: '12ch' }} />
          <label htmlFor="quantization">Quantization</label><input id="quantization" name="quantization" maxLength={20} defaultValue={m.quantization ?? ''} placeholder="Q4_K_M" style={{ maxWidth: '16ch' }} />
          <label htmlFor="context_window">Context window</label><input id="context_window" name="contextWindow" type="number" min="512" defaultValue={m.contextWindow ?? ''} placeholder="tokens" style={{ maxWidth: '16ch' }} />
          <label htmlFor="notes">Notes</label><textarea id="notes" name="notes" maxLength={1000} style={{ minHeight: 80, fontFamily: 'inherit' }} defaultValue={m.notes ?? ''} />
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save Model' : 'Add Model'}</button><Link className="btn ghost" to="/deputy/models">Cancel</Link></div>
      </form>
    </>
  );
}
