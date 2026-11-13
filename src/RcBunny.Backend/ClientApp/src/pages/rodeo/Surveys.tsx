import { useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import { ago } from '../../Sidebar';
import { AccountNo, BarChart, EmptyRow, PageHead, PagerBar, Pill, useRun } from '../../ui';
import { DateInput } from '../../fields';
import { when } from '../walker/shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const stateTones: Record<string, string> = { open: 'ok', scheduled: 'info', ended: '', closed: '' };
const Dot = ({ color, size }: { color: string; size?: number }) => <span className="sv-dot" style={{ ['--dot' as string]: color, ...(size ? { width: size, height: size } : {}) }} />;
const Cat = ({ name, color }: { name: string; color: string }) => <span className="sv-cat"><Dot color={color} />{name}</span>;
const band = (n: number) => (n >= 9 ? 'pro' : n >= 7 ? 'pas' : 'det');
const Opts = ({ list }: { list: string[] }) => <div className="sv-opts">{list.map((o) => <span key={o} className="sv-opt">{o}</span>)}</div>;

/** Rodeo → All Surveys (ebunny's rodeo/surveys/index). */
export function Surveys() {
  const [sp] = useSearchParams();
  const state = sp.get('state') ?? '';
  const { data, error } = useLoad<any>(`/api/rodeo/surveys?${qs({ state: state || null })}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.counts;
  return (
    <>
      <PageHead title="Surveys" sub={<>Customer surveys answered on the website. Send one by email: put <span className="mono">{'{{survey:slug}}'}</span> in an email template and each customer gets their own link.</>}
        actions={<><Link className="btn ghost" to="/rodeo/surveys/responses">All Responses</Link><Link className="btn cyan" to="/rodeo/surveys/new">New Survey</Link></>} />
      <div className="stat-row" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(170px,1fr))', gap: 12, marginBottom: 20 }}>
        <div className="stat"><span>Open Surveys</span><strong>{c.open ?? 0}</strong><small>{c.scheduled ?? 0} scheduled · {(c.closed ?? 0) + (c.ended ?? 0)} closed</small></div>
        <div className="stat"><span>Responses, last 30 days</span><strong>{data.recent.toLocaleString()}</strong><small>{data.all.toLocaleString()} in all</small></div>
        <div className="stat"><span>NPS, last 30 days</span><strong>{data.nps ?? '—'}</strong><small>promoters minus detractors</small></div>
        <div className="stat"><span>Questions in the Bank</span><strong>{data.bank}</strong><small><Link to="/rodeo/surveys/bank">Question Bank</Link> · <Link to="/rodeo/surveys/answer-sets">Answer Sets</Link></small></div>
      </div>
      <div className="panel">
        <div className="panel-head"><h2>All Surveys</h2>
          <div className="actions">{[['', 'All'], ['open', 'Open'], ['scheduled', 'Scheduled'], ['ended', 'Ended'], ['closed', 'Closed']].map(([v, l]) => (
            <Link key={v} className={`btn sm ${state === v ? '' : 'ghost'}`} to={`/rodeo/surveys${v ? `?state=${v}` : ''}`}>{l}</Link>))}</div></div>
        <div className="table-wrap"><table className="table">
          <thead><tr><th>Survey</th><th>Status</th><th>Questions by Category</th><th className="num">Responses</th><th className="num">NPS</th><th>Last Response</th><th></th></tr></thead>
          <tbody>
            {data.surveys.map((s: any) => (
              <tr key={s.id}><td className="wrap" style={{ minWidth: 160 }}><Link to={`/rodeo/surveys/${s.id}`}><b>{s.title}</b></Link><div className="mono help">/survey/{s.slug}</div></td>
                <td><Pill tone={stateTones[s.state]}>{s.stateLabel}</Pill>{s.closesOn && s.state === 'open' && <div className="help">until {new Date(s.closesOn + 'T00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</div>}</td>
                <td className="wrap"><div className="sv-cats">{s.categories.map((g: any) => <span key={g.name} className="sv-cat"><Dot color={g.color} />{g.name} · {g.n}</span>)}</div></td>
                <td className="num">{s.responses.toLocaleString()}</td><td className="num">{s.nps ?? '—'}</td><td className="nowrap">{s.lastAt ? ago(s.lastAt) : '—'}</td>
                <td className="actions"><Link className="btn sm" to={`/rodeo/surveys/${s.id}/results`}>Results</Link><Link className="btn sm ghost" to={`/rodeo/surveys/responses?survey=${s.id}`}>Responses</Link>
                  <Link className="btn sm ghost" to={`/rodeo/surveys/${s.id}`}>Edit</Link><a className="btn sm ghost" href={`${data.website}/survey/${s.slug}`} target="_blank" rel="noopener">Open</a></td></tr>
            ))}
            {data.surveys.length === 0 && <EmptyRow cols={7}>No surveys {state ? 'with that status' : 'yet'}.</EmptyRow>}
          </tbody>
        </table></div>
      </div>
    </>
  );
}

type Q = { key: number; id?: number; bank?: number | null; category?: number | null; question?: string; type?: string; answerSet?: number | null; options?: string; required?: boolean; help?: string | null };

/** New Survey and the survey builder (ebunny's rodeo/surveys/builder): questions picked from the Question Bank. */
export function SurveyBuilder() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/rodeo/surveys/${id ?? 'new'}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <Builder data={data} id={id} key={id ?? 'new'} />;
}

let nextKey = 1;
function Builder({ data, id }: { data: any; id?: string }) {
  const navigate = useNavigate();
  const s = data.survey, o = data.options;
  const [run, bar] = useRun();
  const [f, setF] = useState({ title: s.title ?? '', intro: s.intro ?? '', thankYou: s.thankYou ?? '', status: s.status ?? 'active', opensOn: s.opensOn ?? '', closesOn: s.closesOn ?? '' });
  const [qs_, setQs] = useState<Q[]>(() => data.questions.map((q: any) => ({ ...q, key: nextKey++ })));
  const colors: Record<number, string> = Object.fromEntries(o.categories.map((c: any) => [c.id, c.color]));
  const names: Record<number, string> = Object.fromEntries(o.categories.map((c: any) => [c.id, c.name]));
  const sets: Record<number, any> = Object.fromEntries(o.sets.map((x: any) => [x.id, x]));
  const bankBy: Record<string, any[]> = o.bank.reduce((g: Record<string, any[]>, b: any) => ({ ...g, [b.categoryName ?? 'Uncategorized']: [...(g[b.categoryName ?? 'Uncategorized'] ?? []), b] }), {});
  const set = (key: number, patch: Partial<Q>) => setQs((l) => l.map((q) => (q.key === key ? { ...q, ...patch } : q)));
  // Picking a bank question fills in its wording, category, answer type, answers and help
  const pick = (key: number, v: string) => {
    if (v === 'own') return;
    const b = o.bank.find((x: any) => String(x.id) === v);
    set(key, b ? { bank: b.id, question: b.question, category: b.category, type: b.type, answerSet: b.set, help: b.help, options: '' } : { bank: null, question: '', category: null, type: undefined, answerSet: null, help: null, options: '' });
  };
  const move = (i: number, d: number) => setQs((l) => { const n = [...l]; const j = i + d; if (j < 0 || j >= n.length) return l; [n[i], n[j]] = [n[j], n[i]]; return n; });
  const save = (e: React.FormEvent) => {
    e.preventDefault();
    // Every question must be picked before saving
    const empty = qs_.find((q) => !q.question);
    if (empty) { document.getElementById(`q-${empty.key}`)?.focus(); return; }
    const body = { ...f, opensOn: f.opensOn || null, closesOn: f.closesOn || null, questions: qs_.map(({ key: _k, ...q }) => q) };
    run((id ? api.put<any>(`/api/rodeo/surveys/${id}`, body) : api.post<any>('/api/rodeo/surveys', body)).then((r) => { if (!id) navigate(`/rodeo/surveys/${r.id}`); return r; }));
  };
  const required = qs_.filter((q) => q.required).length;
  // Outline: categories in the order they first appear, with their question numbers
  const outline: { id: number; nums: number[] }[] = [];
  qs_.forEach((q, n) => { const c = q.category ?? 0; const g = outline.find((x) => x.id === c); if (g) g.nums.push(n + 1); else outline.push({ id: c, nums: [n + 1] }); });
  const url = `${data.website}/survey/${s.slug}`;
  return (
    <>
      <PageHead title={id ? s.title : 'New Survey'}
        sub={id ? <>Customers answer at <span className="mono">{url}</span> · {data.responses.toLocaleString()} {data.responses === 1 ? 'response' : 'responses'}</> : 'Pick each question from the Question Bank; its category and answers come with it.'}
        actions={id ? <><Link className="btn ghost" to={`/rodeo/surveys/${id}/results`}>Results</Link><a className="btn ghost" href={url} target="_blank" rel="noopener">Open Survey</a>
          <button className="btn ghost" onClick={() => run(api.post<any>(`/api/rodeo/surveys/${id}/copy`).then((r) => { navigate(`/rodeo/surveys/${r.id}`); return r; }))}>Copy</button></> : undefined} />
      {bar}
      <form id="sv-form" onSubmit={save}>
        <div className="sv-builder">
          <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 20, minWidth: 0 }}>
            <div className="panel">
              <div className="panel-head"><h2>Details</h2>{id && <Pill tone={s.state === 'open' ? 'ok' : ''}>{s.stateLabel}</Pill>}</div>
              <div className="panel-body form-grid">
                <label htmlFor="title">Title</label><input id="title" required value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} />
                <label htmlFor="intro">Intro</label><textarea id="intro" maxLength={1000} style={{ minHeight: 60, fontFamily: 'inherit' }} placeholder="Shown at the top of the survey" value={f.intro} onChange={(e) => setF({ ...f, intro: e.target.value })} />
                <label htmlFor="thank_you">Thank-you message</label><textarea id="thank_you" maxLength={1000} style={{ minHeight: 60, fontFamily: 'inherit' }} placeholder="Shown after someone submits" value={f.thankYou} onChange={(e) => setF({ ...f, thankYou: e.target.value })} />
                <label htmlFor="status">Status</label><select id="status" value={f.status} onChange={(e) => setF({ ...f, status: e.target.value })}><option value="active">Active (taking responses between the dates below)</option><option value="closed">Closed</option></select>
                <label htmlFor="opens_on">Opens</label><DateInput id="opens_on" value={f.opensOn} onChange={(v) => setF({ ...f, opensOn: v })} style={{ maxWidth: '20ch' }} />
                <label htmlFor="closes_on">Closes</label><DateInput id="closes_on" value={f.closesOn} onChange={(v) => setF({ ...f, closesOn: v })} style={{ maxWidth: '20ch' }} />
                <span /><p className="help" style={{ margin: 0 }}>Leave the dates empty to keep it open until you close it.</p>
              </div>
            </div>
            <div className="panel">
              <div className="panel-head"><h2>Questions</h2><span className="muted" id="sv-count">{qs_.length} {qs_.length === 1 ? 'question' : 'questions'} · {required} required</span></div>
              <div className="panel-body">
                {data.responses > 0 && <div className="banner-note" style={{ marginBottom: 12 }}>This survey has {data.responses} {data.responses === 1 ? 'response' : 'responses'}. Rewording and reordering is safe; changing a question's type or answers changes how its past answers read in results.</div>}
                <div id="sv-rows">{qs_.map((q, i) => {
                  const answers: string[] = q.answerSet && sets[q.answerSet] ? sets[q.answerSet].options : q.options ? q.options.split('\n').filter(Boolean) : [];
                  return (
                    <div key={q.key} className="sv-qcard" style={{ ['--dot' as string]: (q.category && colors[q.category]) || '#D5D9DA' }}>
                      <div className="sv-qhead">
                        <span className="num">Q{i + 1}</span><span className="sv-dot" />
                        <select id={`q-${q.key}`} className="sv-pick" aria-label="Question" required value={q.bank ? String(q.bank) : q.question ? 'own' : ''} onChange={(e) => pick(q.key, e.target.value)}>
                          <option value="">Choose a question…</option>
                          {q.question && !q.bank && <option value="own">{q.question.length > 90 ? q.question.slice(0, 89) + '…' : q.question} (this survey's own)</option>}
                          {Object.entries(bankBy).map(([cat, items]) => <optgroup key={cat} label={cat}>{items.map((b) => <option key={b.id} value={b.id}>{b.question.length > 90 ? b.question.slice(0, 89) + '…' : b.question}</option>)}</optgroup>)}
                        </select>
                        <label className="check sv-req"><input type="checkbox" checked={!!q.required} onChange={(e) => set(q.key, { required: e.target.checked })} /> Required</label>
                        <button type="button" className="btn sm ghost" aria-label="Move up" onClick={() => move(i, -1)}>↑</button>
                        <button type="button" className="btn sm ghost" aria-label="Move down" onClick={() => move(i, 1)}>↓</button>
                        <button type="button" className="btn sm ghost" onClick={() => setQs((l) => l.filter((x) => x.key !== q.key))}>Remove</button>
                      </div>
                      <div className="sv-qinfo">{q.question ? <>
                        <Cat name={(q.category && names[q.category]) || 'Uncategorized'} color={(q.category && colors[q.category]) || '#96999d'} /> <span className="sv-type">{o.types[q.type!] ?? q.type}</span>
                        {answers.length > 0 && <> <span className="sv-opts">{answers.map((a) => <span key={a} className="sv-opt">{a}</span>)}</span></>}
                        {q.help && <div className="muted" style={{ fontSize: '.8rem', marginTop: 4 }}>{q.help}</div>}
                      </> : <span className="muted">Pick a question from the Question Bank.</span>}</div>
                    </div>
                  );
                })}</div>
                {qs_.length === 0 && <p className="empty" id="sv-empty">No questions yet. Add one and pick it from the Question Bank.</p>}
                <div className="actions" style={{ marginTop: 12 }}><button type="button" className="btn ghost" id="sv-add" onClick={() => setQs((l) => [...l, { key: nextKey++, required: false }])}>+ Add a Question</button>
                  <Link className="help" to="/rodeo/surveys/bank/new">Need a question that isn't there? Add it to the Question Bank</Link></div>
              </div>
            </div>
          </div>
          <aside className="sv-side" style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 16 }}>
            <div className="panel"><div className="panel-body" style={{ display: 'grid', gap: 10 }}>
              <button className="btn cyan">Save Survey</button>
              {id && !data.responses && <button type="button" className="btn ghost red-text" onClick={() => confirm('Delete this survey?\nIt has no responses, so nothing else is lost.') && run(api.del(`/api/rodeo/surveys/${id}`).then((r) => { navigate('/rodeo/surveys'); return r; }))}>Delete Survey</button>}
            </div></div>
            <div className="panel"><div className="panel-head"><h3 style={{ margin: 0 }}>Outline</h3></div>
              <div className="panel-body"><ul className="sv-outline" id="sv-outline">
                {outline.map((g) => <li key={g.id}><Dot color={colors[g.id] || '#96999d'} /><span>{names[g.id] || 'Uncategorized'}</span><span>Q{g.nums.join(', Q')}</span></li>)}
                {outline.length === 0 && <li className="muted">Nothing yet</li>}
              </ul></div></div>
          </aside>
        </div>
      </form>
    </>
  );
}

/** One question's results card (ebunny's rodeo/surveys/results). */
function QuestionResult({ s, dot }: { s: any; dot: string }) {
  const q = s.basics;
  const mx = Math.max(1, ...(s.rows ?? []).map((r: any) => r.count));
  return (
    <div className="panel sv-q" style={{ ['--dot' as string]: dot }}>
      <div className="panel-head"><h3>{q.question}</h3><span className="sv-type">{q.typeLabel}</span></div>
      <div className="panel-body" style={{ display: 'grid', gap: 12 }}>
        <div className="muted" style={{ fontSize: '.8rem' }}>{q.count} answered · {q.skipped} skipped</div>
        {q.type === 'rating' ? (() => {
          const b = s.bands.map((x: any) => x.n), tot = Math.max(1, b[0] + b[1] + b[2]);
          return <>
            <div style={{ display: 'flex', gap: 18, alignItems: 'center' }}><div><div className="sv-big">{s.nps ?? '—'}</div><div className="help">NPS</div></div><div><div className="sv-big" style={{ fontSize: '1.4rem' }}>{s.avg ?? '—'}</div><div className="help">average</div></div></div>
            <div className="sv-nps" title="Detractors, passives, promoters"><span className="det" style={{ width: `${(b[0] / tot) * 100}%` }} /><span className="pas" style={{ width: `${(b[1] / tot) * 100}%` }} /><span className="pro" style={{ width: `${(b[2] / tot) * 100}%` }} /></div>
            <div className="sv-legend">{s.bands.map((x: any, i: number) => <span key={x.label}><i style={{ background: `var(--${['red', 'gold', 'ok'][i]})` }} />{x.label}: {x.n}</span>)}</div>
            <div className="sv-cols">{s.rows.map((r: any) => (
              <div key={r.label} tabIndex={0} data-tip={`Score ${r.label}: ${r.count} ${r.count === 1 ? 'response' : 'responses'}`}><span className="v">{r.count}</span>
                <b style={{ height: `${Math.round((r.count / mx) * 80)}%`, background: `var(--${+r.label <= 6 ? 'red' : +r.label <= 8 ? 'gold' : 'ok'})` }} />{r.label}</div>
            ))}</div>
          </>;
        })() : q.type === 'text' ? <>
          {s.texts.map((t: any) => <div key={t.id} className="sv-quote">“{t.text}”<small>{t.name ?? 'Anonymous'} · {new Date(t.at).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })} · <Link to={`/rodeo/surveys/responses/${t.id}`}>View response</Link></small></div>)}
          {s.texts.length === 0 && <p className="muted" style={{ margin: 0 }}>No written answers yet.</p>}
          {s.total > 4 && <Link to={`/rodeo/surveys/responses?${qs({ survey: q.surveyId, comments: 1 })}`}>All {s.total} written answers</Link>}
        </> : <>
          {q.type === 'scale' && s.avg && <div style={{ display: 'flex', gap: 18, alignItems: 'center' }}><div><div className="sv-big" style={{ fontSize: '1.5rem' }}>{s.avg} <span className="muted" style={{ fontSize: '1rem' }}>/ {s.of}</span></div><div className="help">average score</div></div><div><div className="sv-big" style={{ fontSize: '1.5rem' }}>{s.top}%</div><div className="help">top two answers</div></div></div>}
          <div className="sv-bars">{s.rows.map((r: any) => (
            <div key={r.label} className="sv-bar" tabIndex={0} data-tip={`${r.label}: ${r.count} (${r.pct}%)`}><span>{r.label}</span><span className="track"><span className="fill" style={{ width: `${r.pct}%` }} /></span><span className="n">{r.count} · {r.pct}%</span></div>
          ))}</div>
          {q.type === 'multi' && <p className="help" style={{ margin: 0 }}>Percent of people who answered; they could pick more than one.</p>}
        </>}
      </div>
    </div>
  );
}

/** A survey's results (ebunny's rodeo/surveys/results). */
export function Results() {
  const { id } = useParams();
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const f = { from: sp.get('from') ?? '', to: sp.get('to') ?? '', source: sp.get('source') ?? '' };
  const { data, error } = useLoad<any>(`/api/rodeo/surveys/${id}/results?${sp}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const s = data.survey, sc = data.scale;
  const filter = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    navigate(`/rodeo/surveys/${id}/results?${qs(Object.fromEntries([...new FormData(e.currentTarget)].filter(([, v]) => v !== '')) as Record<string, string>)}`);
  };
  const npsWord = data.nps == null ? 'no 0–10 question' : data.nps >= 50 ? 'excellent' : data.nps >= 20 ? 'good' : data.nps >= 0 ? 'fair' : 'needs work';
  return (
    <>
      <PageHead title={s.title} sub={<>Results · {s.stateLabel} · <span className="mono">/survey/{s.slug}</span></>}
        actions={<><Link className="btn ghost" to={`/rodeo/surveys/responses?survey=${id}`}>Responses</Link><a className="btn ghost" href={`/api/rodeo/surveys/${id}/export?${sp}`}>Export CSV</a><Link className="btn ghost" to={`/rodeo/surveys/${id}`}>Edit Survey</Link></>} />
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" style={{ marginBottom: 16 }} onSubmit={filter} key={sp.toString()}><div className="panel-head"><h2>Filter Results</h2></div><div className="panel-body form-row">
            <div className="field"><label htmlFor="f-from">From</label><DateInput id="f-from" name="from" defaultValue={f.from} /></div>
            <div className="field"><label htmlFor="f-to">To</label><DateInput id="f-to" name="to" defaultValue={f.to} /></div>
            <div className="field"><label htmlFor="f-source">Came From</label><select id="f-source" name="source" defaultValue={f.source}><option value="">Anywhere</option>{Object.entries(data.sources as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></div>
            <button className="btn">Apply</button>{(f.from || f.to || f.source) && <Link className="btn ghost" to={`/rodeo/surveys/${id}/results`}>Clear</Link>}
          </div></form>
        </aside>
        <div className="search-main">
          <div className="stat-row" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(170px,1fr))', gap: 12, marginBottom: 20 }}>
            <div className="stat"><span>Responses</span><strong>{data.responses.toLocaleString()}</strong><small>{data.known} from known customers</small></div>
            <div className="stat"><span>Net Promoter Score</span><strong>{data.nps ?? '—'}</strong><small>{npsWord}</small></div>
            <div className="stat"><span>{sc ? (sc.basics.question.length > 32 ? sc.basics.question.slice(0, 29) + '...' : sc.basics.question) : 'Satisfaction'}</span><strong>{sc?.avg ? `${sc.avg} / ${sc.of}` : '—'}</strong><small>{sc && sc.top != null ? `${sc.top}% picked the top two` : 'no scale question'}</small></div>
            <div className="stat"><span>Answered Everything</span><strong>{data.complete == null ? '—' : `${data.complete}%`}</strong><small>{s.questions} questions · {s.required} required</small></div>
          </div>
          <div className="panel" style={{ marginBottom: 8 }}><div className="panel-head"><h2>Responses by Week</h2><span className="muted">last 12 weeks, all sources</span></div>
            <div className="panel-body"><BarChart bars={data.weeks.map((w: any) => ({ label: w.label, value: w.count, tip: `Week of ${w.label}` }))} unit="responses" /></div></div>
          {data.groups.map((g: any, i: number) => {
            const dot = g.category?.color ?? '#96999d';
            return (
              <div key={i}>
                <div className="sv-cat-head" style={{ ['--dot' as string]: dot }}><Dot color={dot} size={14} /><h2>{g.category?.name ?? 'Uncategorized'}</h2><span className="muted">{g.questions.length} {g.questions.length === 1 ? 'question' : 'questions'}{g.category?.description && ` · ${g.category.description}`}</span></div>
                <div className="sv-grid">{g.questions.map((q: any) => <QuestionResult key={q.basics.id} s={{ ...q, basics: { ...q.basics, surveyId: id } }} dot={dot} />)}</div>
              </div>
            );
          })}
          {data.groups.length === 0 && <div className="panel"><div className="panel-body empty">This survey has no questions yet.</div></div>}
        </div>
      </div>
    </>
  );
}

/** Rodeo → Responses (ebunny's rodeo/surveys/responses). */
export function Responses() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/surveys/responses?${sp}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const f = Object.fromEntries(sp);
  const filter = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const v = Object.fromEntries([...new FormData(e.currentTarget)].filter(([, x]) => x !== '')) as Record<string, string>;
    if (v.comments === 'on') v.comments = 'true';
    navigate(`/rodeo/surveys/responses?${qs(v)}`);
  };
  return (
    <>
      <PageHead title="Survey Responses" sub="Every answer customers have sent, newest first. Open one to see all its answers by category." />
      <div className="search-layout">
        <aside className="search-side">
          <form className="panel" style={{ marginBottom: 16 }} onSubmit={filter} key={sp.toString()}><div className="panel-head"><h2>Search Responses</h2></div><div className="panel-body form-row">
            <div className="field grow"><label htmlFor="f-survey">Survey</label><select id="f-survey" name="survey" defaultValue={f.survey ?? ''}><option value="">All surveys</option>{data.surveys.map((s: any) => <option key={s.id} value={s.id}>{s.title}</option>)}</select></div>
            <div className="field"><label htmlFor="f-band">Recommend Score</label><select id="f-band" name="band" defaultValue={f.band ?? ''}><option value="">Any</option><option value="promoter">Promoters (9–10)</option><option value="passive">Passives (7–8)</option><option value="detractor">Detractors (0–6)</option></select></div>
            <div className="field"><label htmlFor="f-source">Came From</label><select id="f-source" name="source" defaultValue={f.source ?? ''}><option value="">Anywhere</option>{Object.entries(data.sources as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></div>
            <div className="field"><label htmlFor="f-account">Account</label><input id="f-account" name="account" defaultValue={f.account ?? ''} inputMode="numeric" style={{ width: '14ch' }} /></div>
            <div className="field"><label htmlFor="f-from">From</label><DateInput id="f-from" name="from" defaultValue={f.from ?? ''} /></div>
            <div className="field"><label htmlFor="f-to">To</label><DateInput id="f-to" name="to" defaultValue={f.to ?? ''} /></div>
            <label className="check" style={{ marginBottom: 8 }}><input type="checkbox" name="comments" defaultChecked={f.comments === 'true' || f.comments === '1'} /> With a written answer</label>
            <button className="btn">Filter</button>{Object.values(f).some(Boolean) && <Link className="btn ghost" to="/rodeo/surveys/responses">Clear</Link>}
          </div></form>
        </aside>
        <div className="search-main">
          <div className="panel">
            <div className="panel-head"><h2>{data.total.toLocaleString()} {data.total === 1 ? 'Response' : 'Responses'}</h2>{f.survey && <Link className="btn sm ghost" to={`/rodeo/surveys/${f.survey}/results`}>Results for this survey</Link>}</div>
            <div className="table-wrap"><table className="table" data-page="0">
              <thead><tr><th>ID</th><th>Date</th><th>Survey</th><th>Customer</th><th className="num">Score</th><th>Written Answer</th><th>Came From</th></tr></thead>
              <tbody>
                {data.rows.map((r: any) => (
                  <tr key={r.id} className="click" onClick={() => navigate(`/rodeo/surveys/responses/${r.id}`)}>
                    <td className="nowrap"><Link to={`/rodeo/surveys/responses/${r.id}`}><b>#{r.id}</b></Link></td><td className="nowrap">{when(r.createdAt)}</td><td>{r.survey}</td>
                    <td onClick={(e) => e.stopPropagation()}>{r.customer ? <>{r.customer.name}<div className="help"><AccountNo id={r.customer.id} account={r.customer.account} /></div></> : <span className="muted">Anonymous</span>}</td>
                    <td className="num">{r.score != null ? <span className={`sv-band ${band(r.score)}`}>{r.score}</span> : '—'}</td>
                    <td className="wrap">{r.comment ? (r.comment.length > 90 ? r.comment.slice(0, 87) + '...' : r.comment) : ''}</td><td>{r.source}</td></tr>
                ))}
                {data.rows.length === 0 && <EmptyRow cols={7}>No responses match.</EmptyRow>}
              </tbody>
            </table></div>
            <PagerBar page={data.page} total={data.total} size={25} onPage={(p) => navigate(`/rodeo/surveys/responses?${qs({ ...f, page: p })}`)} />
          </div>
        </div>
      </div>
    </>
  );
}

/** One response (ebunny's rodeo/surveys/response): every answer by category. */
export function Response() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/rodeo/surveys/responses/${id}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const r = data.response, c = data.customer, n = r.score;
  const at = new Date(r.createdAt).toLocaleString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(' at ', ' ');
  return (
    <>
      <PageHead title={`${data.survey.title} · Response #${r.id}`} sub={`${at} · came from ${r.source}`}
        actions={<>{data.prev && <Link className="btn ghost" to={`/rodeo/surveys/responses/${data.prev}`}>‹ Older</Link>}{data.next && <Link className="btn ghost" to={`/rodeo/surveys/responses/${data.next}`}>Newer ›</Link>}
          <Link className="btn ghost" to={`/rodeo/surveys/${data.survey.id}/results`}>Survey Results</Link></>} />
      <div className="stat-row" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(200px,1fr))', gap: 12, marginBottom: 20 }}>
        <div className="stat"><span>Customer</span><strong style={{ fontSize: '1.1rem' }}>{c?.name ?? 'Anonymous'}</strong><small>{c ? <><AccountNo id={c.id} account={c.account} /> · {c.status}</> : 'answered without a customer link'}</small></div>
        <div className="stat"><span>Plan</span><strong style={{ fontSize: '1.1rem' }}>{c?.plan ?? '—'}</strong><small>{c?.type}</small></div>
        <div className="stat"><span>Recommend Score</span><strong>{n != null ? <span className={`sv-band ${band(n)}`}>{n}</span> : '—'}</strong><small>{n == null ? 'not answered' : n >= 9 ? 'Promoter' : n >= 7 ? 'Passive' : 'Detractor'}</small></div>
        <div className="stat"><span>Answered</span><strong>{data.answered} / {data.questions}</strong><small>questions</small></div>
      </div>
      {data.groups.map((g: any, i: number) => {
        const dot = g.category?.color ?? '#96999d';
        return (
          <div key={i}>
            <div className="sv-cat-head" style={{ ['--dot' as string]: dot }}><Dot color={dot} size={14} /><h2>{g.category?.name ?? 'Uncategorized'}</h2></div>
            <div className="panel sv-q" style={{ ['--dot' as string]: dot }}><div className="table-wrap"><table className="table" data-page="0"><tbody>
              {g.questions.map((q: any) => (
                <tr key={q.question}><td style={{ width: '46%' }}><b>{q.question}</b><div className="sv-type">{q.typeLabel}{q.required ? ' · required' : ''}</div></td>
                  <td>{q.answer == null ? <span className="muted">Skipped</span>
                    : Array.isArray(q.answer) ? <Opts list={q.answer} />
                      : q.type === 'rating' ? <><span className={`sv-band ${band(+q.answer)}`} style={{ fontSize: '1.2rem' }}>{q.answer}</span> <span className="muted">/ 10</span></>
                        : q.type === 'scale' ? <>{q.answer}{q.position > 0 && <span className="muted"> · {q.position} of {q.of}</span>}</>
                          : q.type === 'text' ? <div className="sv-quote">{q.answer}</div> : q.answer}</td></tr>
              ))}
            </tbody></table></div></div>
          </div>
        );
      })}
    </>
  );
}

/** Rodeo → Question Bank (ebunny's rodeo/surveys/bank). */
export function Bank() {
  const [sp] = useSearchParams();
  const only = sp.get('category');
  const { data, error, reload } = useLoad<any>('/api/rodeo/surveys/bank');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const Group = ({ title, desc, dot, items }: { title: string; desc?: string; dot: string; items: any[] }) => (
    <>
      <div className="sv-cat-head" style={{ ['--dot' as string]: dot }}><Dot color={dot} size={14} /><h2>{title}</h2><span className="muted">{items.length} {items.length === 1 ? 'question' : 'questions'}{desc && ` · ${desc}`}</span></div>
      <div className="panel sv-q" style={{ ['--dot' as string]: dot, marginBottom: 8 }}><div className="table-wrap"><table className="table" data-page="0">
        <thead><tr><th>Question</th><th style={{ minWidth: 220 }}>Answers</th><th className="num">In Surveys</th><th></th></tr></thead>
        <tbody>
          {items.map((b) => (
            <tr key={b.id}><td><b>{b.question}</b><div className="sv-type" style={{ marginTop: 3 }}>{b.typeLabel}</div>{b.help && <div className="help">{b.help}</div>}</td>
              <td>{b.set ? <><div className="help" style={{ marginBottom: 3 }}>{b.set.name}</div><Opts list={b.set.options} /></> : <span className="help">{b.type === 'rating' ? '0 to 10' : b.type === 'yes_no' ? 'Yes · No' : 'Written'}</span>}</td>
              <td className="num">{b.uses}</td>
              <td className="actions nowrap"><Link className="btn sm ghost" to={`/rodeo/surveys/bank/${b.id}/edit`}>Edit</Link>
                <button className="btn sm ghost" onClick={() => confirm('Remove this question from the bank?\nSurveys that use it keep their own copy.') && run(api.del(`/api/rodeo/surveys/bank/${b.id}`))}>Remove</button></td></tr>
          ))}
          {items.length === 0 && <EmptyRow cols={4}>No questions in this category yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
  return (
    <>
      <PageHead title="Question Bank" sub="Ready-made questions, filed by category, to add to any survey from the survey builder. A survey keeps its own copy, so editing the bank never changes past surveys."
        actions={<Link className="btn cyan" to={`/rodeo/surveys/bank/new${only ? `?category=${only}` : ''}`}>New Question</Link>} />
      {bar}
      <div className="actions" style={{ marginBottom: 14 }}><Link className={`btn sm ${only ? 'ghost' : ''}`} to="/rodeo/surveys/bank">All</Link>
        {data.categories.map((c: any) => <Link key={c.id} className={`btn sm ${only === String(c.id) ? '' : 'ghost'}`} to={`/rodeo/surveys/bank?category=${c.id}`}><span className="sv-dot" style={{ ['--dot' as string]: c.color, marginRight: 6 }} />{c.name}</Link>)}</div>
      {data.categories.filter((c: any) => !only || String(c.id) === only).map((c: any) => <Group key={c.id} title={c.name} desc={c.description} dot={c.color} items={c.items} />)}
      {!only && data.uncategorized.length > 0 && <Group title="Uncategorized" dot="#96999d" items={data.uncategorized} />}
    </>
  );
}

/** New Question / Edit Question for the bank (ebunny's rodeo/surveys/bank-form). */
export function BankForm() {
  const { id } = useParams();
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/surveys/bank/form?${qs({ id, category: sp.get('category') })}`);
  const [run, bar] = useRun();
  const [type, setType] = useState<string | null>(null);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const q = data.question, t = type ?? q.type, listed = ['scale', 'single', 'multi'].includes(t);
  const groups = data.sets.reduce((g: Record<string, any[]>, s: any) => ({ ...g, [s.group]: [...(g[s.group] ?? []), s] }), {});
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { question: f.get('question'), type: f.get('type'), help: f.get('help'), surveyCategoryId: f.get('cat') ? Number(f.get('cat')) : null, surveyAnswerSetId: f.get('set') ? Number(f.get('set')) : null };
    run((id ? api.put(`/api/rodeo/surveys/bank/${id}`, body) : api.post('/api/rodeo/surveys/bank', body)).then((r) => { navigate('/rodeo/surveys/bank'); return r; }));
  };
  return (
    <>
      <PageHead title={id ? 'Edit Question' : 'New Question'} sub="A ready-made question for the Question Bank." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="question">Question</label><textarea id="question" name="question" rows={3} required maxLength={250} style={{ minHeight: 70, fontFamily: 'inherit' }} defaultValue={q.question ?? ''} />
          <label htmlFor="cat">Category</label><select id="cat" name="cat" defaultValue={q.surveyCategoryId ?? ''}><option value="">Uncategorized</option>{data.categories.map((c: any) => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
          <label htmlFor="bk-type">Answer type</label><select id="bk-type" name="type" value={t} onChange={(e) => setType(e.target.value)}>{Object.entries(data.types as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
          <label htmlFor="set" hidden={!listed}>Answer set</label><select id="set" name="set" hidden={!listed} defaultValue={q.surveyAnswerSetId ?? ''}><option value="">Choose…</option>
            {Object.entries(groups as Record<string, any[]>).map(([g, list]) => <optgroup key={g} label={g}>{list.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</optgroup>)}</select>
          <label htmlFor="help">Help text</label><input id="help" name="help" maxLength={200} defaultValue={q.help ?? ''} placeholder="Optional hint under the question" />
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save Question' : 'Add to Bank'}</button><Link className="btn ghost" to="/rodeo/surveys/bank">Cancel</Link></div>
      </form>
    </>
  );
}

/** Rodeo → Answer Sets (ebunny's rodeo/surveys/answers). */
export function AnswerSets() {
  const { data, error, reload } = useLoad<any[]>('/api/rodeo/surveys/answer-sets');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Answer Sets" sub="Reusable lists of answers, grouped by kind. Scales are ordered and scored 1 to n in results. Changing a set updates the questions that use it."
        actions={<Link className="btn cyan" to="/rodeo/surveys/answer-sets/new">New Answer Set</Link>} />
      {bar}
      {data.map((g) => (
        <div key={g.group}>
          <div className="sv-cat-head"><h2>{g.group}</h2><span className="muted">{g.sets.length} {g.sets.length === 1 ? 'set' : 'sets'}</span></div>
          <div className="sv-grid">{g.sets.map((s: any) => (
            <div key={s.id} className="panel">
              <div className="panel-head"><h3 style={{ margin: 0 }}>{s.name}</h3><span className="sv-type">{s.typeLabel}</span></div>
              <div className="panel-body" style={{ display: 'grid', gap: 10 }}>
                {s.type === 'scale' ? <ol style={{ margin: '0 0 0 18px', fontSize: '.88rem' }}>{s.options.map((o: string, i: number) => <li key={o}>{o} <span className="muted">· scores {i + 1}</span></li>)}</ol> : <Opts list={s.options} />}
                <div className="muted" style={{ fontSize: '.8rem' }}>Used by {s.questions} survey {s.questions === 1 ? 'question' : 'questions'} and {s.bank} in the bank</div>
                <div className="actions"><Link className="btn sm ghost" to={`/rodeo/surveys/answer-sets/${s.id}/edit`}>Edit</Link>
                  {!s.questions && !s.bank && <button className="btn sm ghost" onClick={() => confirm('Delete this answer set?\nNothing uses it.') && run(api.del(`/api/rodeo/surveys/answer-sets/${s.id}`))}>Delete</button>}</div>
              </div>
            </div>
          ))}</div>
        </div>
      ))}
      {data.length === 0 && <div className="panel"><div className="panel-body empty">No answer sets yet.</div></div>}
    </>
  );
}

/** New / Edit Answer Set (ebunny's rodeo/surveys/answer-form). */
export function AnswerSetForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/surveys/answer-sets/form${id ? `?id=${id}` : ''}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const s = data.set;
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const body = Object.fromEntries(new FormData(e.currentTarget));
    run((id ? api.put(`/api/rodeo/surveys/answer-sets/${id}`, body) : api.post('/api/rodeo/surveys/answer-sets', body)).then((r) => { navigate('/rodeo/surveys/answer-sets'); return r; }));
  };
  return (
    <>
      <PageHead title={id ? 'Edit Answer Set' : 'New Answer Set'} sub="A reusable list of answers. For a scale, list answers from lowest to highest." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={80} defaultValue={s.name ?? ''} placeholder="e.g. Satisfaction (5-point)" />
          <label htmlFor="group">Kind</label><input id="group" name="group" required maxLength={40} list="sv-groups" defaultValue={s.group ?? ''} placeholder="Satisfaction, Agreement…" />
          <datalist id="sv-groups">{data.groups.map((g: string) => <option key={g} value={g} />)}</datalist>
          <label htmlFor="type">Type</label><select id="type" name="type" defaultValue={s.type}>{Object.entries(data.types as Record<string, string>).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
          <label htmlFor="options">Answers, one per line</label><textarea id="options" name="options" rows={6} required style={{ minHeight: 140, fontFamily: 'inherit' }} defaultValue={s.options} />
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save Answer Set' : 'Add Answer Set'}</button><Link className="btn ghost" to="/rodeo/surveys/answer-sets">Cancel</Link></div>
      </form>
    </>
  );
}

/** Rodeo → Survey Categories (ebunny's rodeo/surveys/categories). */
export function SurveyCategories() {
  const { data, error, reload } = useLoad<any[]>('/api/rodeo/surveys/categories');
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Survey Categories" sub="Topics that questions are filed under. Results and responses are grouped by these, in this order."
        actions={<Link className="btn cyan" to="/rodeo/surveys/categories/new">New Category</Link>} />
      {bar}
      <div className="panel" style={{ minWidth: 0 }}><div className="table-wrap"><table className="table">
        <thead><tr><th className="num">Order</th><th>Category</th><th>Description</th><th className="num">In the Bank</th><th className="num">In Surveys</th><th></th></tr></thead>
        <tbody>
          {data.map((c) => (
            <tr key={c.id}><td className="num">{c.position}</td><td><Cat name={c.name} color={c.color} /></td><td className="wrap">{c.description}</td>
              <td className="num"><Link to={`/rodeo/surveys/bank?category=${c.id}`}>{c.bank}</Link></td><td className="num">{c.questions}</td>
              <td className="actions"><Link className="btn sm ghost" to={`/rodeo/surveys/categories/${c.id}/edit`}>Edit</Link>
                <button className="btn sm ghost" onClick={() => confirm(`Delete ${c.name}?\nIts ${c.bank + c.questions} questions become uncategorized; nothing else changes.`) && run(api.del(`/api/rodeo/surveys/categories/${c.id}`))}>Delete</button></td></tr>
          ))}
          {data.length === 0 && <EmptyRow cols={6}>No categories yet.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

/** New / Edit survey category (ebunny's rodeo/surveys/category-form). */
export function SurveyCategoryForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/surveys/categories/form${id ? `?id=${id}` : ''}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const c = data.category;
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const body = { name: f.get('name'), description: f.get('description'), color: f.get('color'), position: f.get('position') ? Number(f.get('position')) : null };
    run((id ? api.put(`/api/rodeo/surveys/categories/${id}`, body) : api.post('/api/rodeo/surveys/categories', body)).then((r) => { navigate('/rodeo/surveys/categories'); return r; }));
  };
  return (
    <>
      <PageHead title={id ? 'Edit Category' : 'New Category'} sub="A topic survey questions are filed under." />
      {bar}
      <form className="panel simple-form" onSubmit={save}>
        <div className="panel-body form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={60} defaultValue={c.name ?? ''} />
          <label htmlFor="description">Description</label><input id="description" name="description" maxLength={200} defaultValue={c.description ?? ''} />
          <label htmlFor="color">Color</label><input id="color" type="color" name="color" defaultValue={c.color} style={{ height: 38, padding: 2, maxWidth: 80 }} />
          <label htmlFor="position">Order</label><input id="position" type="number" name="position" min={0} max={999} defaultValue={c.position ?? ''} placeholder="Last" style={{ maxWidth: '10ch' }} />
        </div>
        <div className="panel-body actions"><button className="btn cyan">{id ? 'Save Category' : 'Add Category'}</button><Link className="btn ghost" to="/rodeo/surveys/categories">Cancel</Link></div>
      </form>
    </>
  );
}

/** Rodeo → Survey Emails (ebunny's rodeo/survey-emails): the 90-day check-in and farewell survey, sent daily. */
export function SurveyEmails() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/rodeo/survey-emails?${qs({ page: sp.get('page') })}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const tones: Record<string, string> = { sent: 'ok', queued: 'info', 'not sent': 'warn' };
  return (
    <>
      <PageHead title="Survey Emails" sub="Sent automatically every day (the Survey emails job in Sheriff → Crons). Each customer gets each one once; suppressed emails are skipped."
        actions={<button className="btn cyan" onClick={() => confirm('Send the survey emails now?\nEveryone due gets theirs now instead of with the daily run.') && run(api.post('/api/rodeo/survey-emails/send'))}>Send Now</button>} />
      {bar}
      <div className="grid-2">
        {data.stats.map((s: any) => (
          <div key={s.key} className="panel"><div className="panel-head"><h2>{s.name}</h2><Pill tone={s.due ? 'warn' : 'ok'}>{s.due ? `${s.due} due` : 'Up to date'}</Pill></div>
            <div className="panel-body"><dl className="kv">
              <dt>Who gets it</dt><dd>{s.who}</dd>
              <dt>Email</dt><dd>{s.template ? <><Link to={`/rodeo/templates/${s.template.id}`}>{s.template.name}</Link><div className="help">“{s.template.subject}”</div></> : <span className="muted">Template “{s.templateName}” is missing</span>}</dd>
              <dt>Survey</dt><dd>{s.survey ? <><Link to={`/rodeo/surveys/${s.survey.id}`}>{s.survey.title}</Link> · <Link to={`/rodeo/surveys/${s.survey.id}/results`}>Results</Link></> : <span className="muted">Survey “{s.slug}” is missing</span>}</dd>
            </dl></div>
            <div className="grid-3 stat-row panel-body" style={{ borderTop: '1px solid var(--line)' }}>
              <div className="stat"><span>Sent</span><strong>{s.sent30.toLocaleString()}</strong><small>last 30 days · {s.total.toLocaleString()} in all</small></div>
              <div className="stat"><span>Answered</span><strong>{s.answered.toLocaleString()}</strong><small>customers who filled it in</small></div>
              <div className="stat"><span>Response Rate</span><strong>{s.rate == null ? '—' : `${s.rate}%`}</strong><small>of everyone sent one</small></div>
            </div>
          </div>
        ))}
      </div>
      <div className="panel"><div className="panel-head"><h2>Sent</h2><span className="muted">newest first</span></div><div className="table-wrap"><table data-page="0">
        <thead><tr><th>ID</th><th>Sent</th><th>Customer</th><th>Email</th><th>Status</th><th>Answered</th></tr></thead>
        <tbody>
          {data.sent.map((l: any) => (
            <tr key={l.id} className="click" onClick={() => navigate(`/rodeo/emails/sent/${l.id}`)}><td className="nowrap"><Link to={`/rodeo/emails/sent/${l.id}`}><b>#{l.id}</b></Link></td><td className="nowrap">{when(l.at)}</td>
              <td onClick={(e) => e.stopPropagation()}>{l.customer ? <><AccountNo id={l.customer.id} account={l.customer.account} /> {l.customer.name}<div className="help">{l.customer.email} · {l.customer.status}</div></> : '—'}</td>
              <td>{l.email}</td><td><Pill tone={tones[l.status] ?? ''}>{l.status.charAt(0).toUpperCase() + l.status.slice(1)}</Pill>{l.openedAt && <div className="help">opened {ago(l.openedAt)}</div>}</td>
              <td>{l.answered ? '✓ Yes' : '—'}</td></tr>
          ))}
          {data.sent.length === 0 && <EmptyRow cols={6}>None sent yet.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={data.page} total={data.total} size={10} onPage={(p) => navigate(`/rodeo/survey-emails?page=${p}`)} /></div>
    </>
  );
}
