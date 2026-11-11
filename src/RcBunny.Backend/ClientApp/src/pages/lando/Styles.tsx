import { useEffect, useRef, useState } from 'react';
import { useLoad } from '../../api';
import { PageHead } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

const KEY = 'et-admin-local-styles', DAYS = 5 * 24 * 3600 * 1000;
const hexOf = (v: string) => (/^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(v) ? (v.length === 4 ? '#' + [...v.slice(1)].map((c) => c + c).join('') : v).toLowerCase() : null);
const fileShort: Record<string, string> = { 'shared/css/tokens.css': 'Shared', 'backend/admin.css': 'Admin', 'frontend/css/style.css': 'Website' };

/** Lando → Style Guide (ebunny's lando/styles): every color and style variable, try new colors, copy the changed CSS. */
export default function Styles() {
  const { data, error } = useLoad<any>('/api/lando/styles');
  const [tab, setTab] = useState(() => location.hash.slice(1) || 'colors');
  const [changed, setChanged] = useState<Record<string, string>>(() => {
    try { const s = JSON.parse(localStorage.getItem(KEY) || 'null'); return s?.vars && s.expires > Date.now() ? s.vars : {}; } catch { return {}; }
  });
  const [exporting, setExporting] = useState(false);
  const [flash, setFlash] = useState<string | null>(null);
  const frames = useRef<HTMLIFrameElement[]>([]);

  // Changes apply to every admin page in this browser for 5 days, and to the website samples
  useEffect(() => {
    try { if (Object.keys(changed).length) localStorage.setItem(KEY, JSON.stringify({ vars: changed, expires: Date.now() + DAYS })); else localStorage.removeItem(KEY); } catch { /* blocked */ }
    frames.current.forEach((f) => { try { for (const [n, v] of Object.entries(changed)) f.contentDocument!.documentElement.style.setProperty(n, v); } catch { /* not loaded */ } });
  }, [changed]);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;

  const files: Record<string, string> = Object.fromEntries(data.files.map((f: any) => [f.key, f.label]));
  const pick = (v: any, value: string) => {
    const original = hexOf(v.resolved);
    const next = { ...changed };
    if (value === original) { delete next[v.name]; document.documentElement.style.removeProperty(v.name); frames.current.forEach((f) => f.contentDocument?.documentElement.style.removeProperty(v.name)); }
    else { next[v.name] = value; document.documentElement.style.setProperty(v.name, value); }
    setChanged(next);
  };
  const reset = () => { for (const n of Object.keys(changed)) { document.documentElement.style.removeProperty(n); frames.current.forEach((f) => f.contentDocument?.documentElement.style.removeProperty(n)); } setChanged({}); setExporting(false); };
  const names = Object.keys(changed);
  const byFile: Record<string, string[]> = {};
  for (const n of names) (byFile[data.vars.find((v: any) => v.name === n)?.file ?? 'shared/css/tokens.css'] ??= []).push(`  ${n}: ${changed[n]};`);
  const css = Object.entries(byFile).map(([f, lines]) => `/* public/${f} */\n:root {\n${lines.join('\n')}\n}`).join('\n\n');
  const copy = () => { setExporting(true); navigator.clipboard?.writeText(css).catch(() => {}); };
  const show = (t: string) => { setTab(t); history.replaceState(null, '', '#' + t); };
  const jump = (n: string) => { show('colors'); setTimeout(() => { document.getElementById('var' + n)?.scrollIntoView({ block: 'center' }); setFlash(n); setTimeout(() => setFlash(null), 1200); }, 0); };
  const Chips = ({ list }: { list: string[] }) => (
    <div className="sg-chips" aria-label="Variables used">
      {list.length ? list.map((n) => <button key={n} type="button" className="sg-chip" title={`Show ${n}`} onClick={() => jump(n)}><i style={{ ['--sg' as any]: `var(${n})` }} /><span className="mono">{n}</span></button>) : <span className="muted">No variables</span>}
    </div>
  );
  const sizeFrame = (f: HTMLIFrameElement) => { try { if (f.offsetParent) f.style.height = f.contentDocument!.body.scrollHeight + 34 + 'px'; } catch { /* hidden */ } };
  const colors = data.vars.filter((v: any) => v.kind === 'color');
  const groups = [['Brand colors', 'website + every admin app'], ['Status colors', 'alerts, pills and messages, website + admin'], ['App colors', 'admin apps only']];
  const until = new Date(Date.now() + DAYS).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

  return (
    <>
      <PageHead title="Style Guide"
        sub={<>Every color and style variable used by the website and all admin apps, in one place. Shared colors live in <span className="mono">public/shared/css/tokens.css</span>; pick a swatch to try a new value. Your changes apply to every admin page in this browser for 5 days (nobody else sees them); Copy Changed CSS to make them permanent for everyone.</>}
        actions={<>{names.length > 0 && <span className="muted">{names.length} changed · applied to the admin in this browser until {until}</span>}
          <button type="button" className="btn ghost" disabled={!names.length} onClick={reset}>Reset</button><button type="button" className="btn cyan" disabled={!names.length} onClick={copy}>Copy Changed CSS</button></>} />
      {exporting && names.length > 0 && <div className="panel" style={{ marginBottom: 16 }}>
        <div className="panel-head"><h2>Changed Variables</h2><span className="muted">Paste into the file named in each comment to make the change permanent</span></div>
        <div className="panel-body"><textarea className="sg-out" readOnly aria-label="Changed CSS" value={css} autoFocus onFocus={(e) => e.currentTarget.select()} /></div>
      </div>}
      <div className="panel">
        <div className="tabs" role="tablist">
          {[['colors', `Colors (${colors.length})`], ['components', 'Components'], ['type', 'Fonts & Sizes'], ['hardcoded', `Hard-coded Colors (${[...data.literals].length})`], ['files', 'Files']].map(([k, l]) =>
            <button key={k} type="button" className={tab === k ? 'on' : ''} aria-selected={tab === k} onClick={() => show(k)}>{l}</button>)}
        </div>

        <div hidden={tab !== 'colors'}>
          {groups.map(([group, note]) => (
            <div key={group}>
              <div className="panel-head"><h2>{group}</h2><span className="muted">{note}</span></div>
              <div className="table-wrap"><table className="table" data-page="0">
                <thead><tr><th style={{ width: 56 }}>Color</th><th>Variable</th><th>Value</th><th>Defined In</th><th className="num">Used</th><th>Where</th></tr></thead>
                <tbody>{colors.filter((v: any) => v.group === group).map((v: any) => {
                  const hex = hexOf(v.resolved);
                  return (
                    <tr key={v.name} id={'var' + v.name} className={`sg-row${changed[v.name] ? ' changed' : ''}${flash === v.name ? ' flash' : ''}`}>
                      <td>{hex ? <label className="sg-pick" title={`Try a new ${v.name}`}><span className="sg-swatch" style={{ ['--sg' as any]: `var(${v.name})` }} />
                        <input type="color" value={changed[v.name] ?? hex} onChange={(e) => pick(v, e.target.value)} aria-label={v.name} /></label> : <span className="sg-swatch" style={{ ['--sg' as any]: `var(${v.name})` }} />}</td>
                      <td className="mono"><b>{v.name}</b></td>
                      <td className="mono"><span>{changed[v.name] ?? v.value}</span>{v.value !== v.resolved && <div className="muted">= {v.resolved}</div>}</td>
                      <td className="muted">{files[v.file]}</td>
                      <td className="num">{v.count}</td>
                      <td>{v.count ? <details className="sg-uses"><summary>{Object.keys(v.uses).map((f) => fileShort[f]).join(' + ')}</summary>
                        {Object.entries(v.uses).map(([f, sels]) => <div key={f}><b>{files[f]}</b><ul>{[...new Set(sels as string[])].map((s) => <li key={s} className="mono">{s}</li>)}</ul></div>)}
                      </details> : <span className="muted">not used yet</span>}</td>
                    </tr>
                  );
                })}</tbody>
              </table></div>
            </div>
          ))}
        </div>

        <div hidden={tab !== 'components'}>
          <div className="panel-head"><h2>Admin Apps</h2><span className="muted">public/backend/admin.css · click a variable to jump to it</span></div>
          <div className="panel-body sg-comp">
            {data.admin.map((c: any) => (
              <div key={c.title} className="panel"><div className="panel-head"><h3 style={{ margin: 0 }}>{c.title}</h3></div><div className="panel-body">
                <div dangerouslySetInnerHTML={{ __html: c.html }} /><Chips list={c.vars} />
              </div></div>
            ))}
          </div>
          <div className="panel-head"><h2>Website</h2><span className="muted">public/frontend/css/style.css · shown in frames so the website's own styles apply</span></div>
          <div className="panel-body sg-comp">
            {data.site.map((c: any, i: number) => (
              <div key={c.title} className="panel"><div className="panel-head"><h3 style={{ margin: 0 }}>{c.title}</h3></div><div className="panel-body">
                <iframe className="sg-frame" title={`${c.title} sample`} ref={(f) => { if (f) frames.current[i] = f; }}
                  srcDoc={`<!doctype html><html><head><meta charset="utf-8"><style>${data.siteCss}</style><style>body{padding:16px;background:#fff}.reveal{opacity:1!important;transform:none!important}</style></head><body>${c.html}</body></html>`}
                  onLoad={(e) => { const f = e.currentTarget; for (const [n, v] of Object.entries(changed)) f.contentDocument?.documentElement.style.setProperty(n, v); sizeFrame(f); setTimeout(() => sizeFrame(f), 400); }} />
                <Chips list={c.vars} />
              </div></div>
            ))}
          </div>
        </div>

        <div hidden={tab !== 'type'}>
          <div className="panel-head"><h2>Fonts</h2></div>
          <div className="table-wrap"><table className="table" data-page="0"><thead><tr><th>Variable</th><th>Sample</th><th>Value</th><th className="num">Used</th></tr></thead><tbody>
            {data.vars.filter((v: any) => v.kind === 'font').map((v: any) => <tr key={v.name}><td className="mono"><b>{v.name}</b><div className="muted">{files[v.file]}</div></td>
              <td><span className="sg-font" style={{ fontFamily: `var(${v.name})` }}>Texas-sized service, 1,234 kWh</span></td><td className="mono muted">{v.value}</td><td className="num">{v.count}</td></tr>)}
          </tbody></table></div>
          <div className="panel-head"><h2>Sizes &amp; Effects</h2></div>
          <div className="table-wrap"><table className="table" data-page="0"><thead><tr><th>Variable</th><th>Value</th><th>Defined In</th><th className="num">Used</th></tr></thead><tbody>
            {data.vars.filter((v: any) => v.kind === 'other').map((v: any) => <tr key={v.name}><td className="mono"><b>{v.name}</b></td><td className="mono">{v.value}</td><td className="muted">{files[v.file]}</td><td className="num">{v.count}</td></tr>)}
          </tbody></table></div>
        </div>

        <div hidden={tab !== 'hardcoded'}>
          <div className="panel-body"><p className="muted" style={{ margin: 0 }}>Colors typed straight into a rule instead of using a variable. These don't change when a variable does; mostly white and see-through overlays on navy.</p></div>
          <div className="table-wrap"><table className="table" data-page="0"><thead><tr><th style={{ width: 56 }}>Color</th><th>Value</th><th className="num">Used</th><th>Where</th></tr></thead><tbody>
            {data.literals.map((l: any) => (
              <tr key={l.value}><td><span className="sg-swatch" style={{ background: undefined, backgroundImage: 'linear-gradient(45deg,#ddd 25%,transparent 25%,transparent 75%,#ddd 75%),linear-gradient(45deg,#ddd 25%,transparent 25%,transparent 75%,#ddd 75%)', backgroundSize: '10px 10px', backgroundPosition: '0 0,5px 5px', boxShadow: `inset 0 0 0 40px ${l.value}` }} /></td>
                <td className="mono">{l.value}</td><td className="num">{l.count}</td>
                <td><details className="sg-uses"><summary>{l.count} {l.count === 1 ? 'rule' : 'rules'}</summary><ul>{l.uses.map((u: string) => <li key={u} className="mono">{u}</li>)}</ul></details></td></tr>
            ))}
          </tbody></table></div>
        </div>

        <div hidden={tab !== 'files'}>
          <div className="table-wrap"><table className="table" data-page="0"><thead><tr><th>File</th><th>Used By</th><th className="num">Variables Defined</th><th className="num">Variable Uses</th></tr></thead><tbody>
            {data.files.map((f: any) => <tr key={f.key}><td className="mono">public/{f.key}</td><td>{f.label}</td><td className="num">{data.vars.filter((v: any) => v.file === f.key).length}</td>
              <td className="num">{data.vars.reduce((t: number, v: any) => t + (v.uses[f.key]?.length ?? 0), 0)}</td></tr>)}
          </tbody></table></div>
          <div className="panel-body"><p className="muted" style={{ margin: 0 }}>Every admin app (Corral, Ranger, Lando, Astro, Caboose, Bounty, Sheriff) uses the same admin stylesheet, so a change here applies to all of them.</p></div>
        </div>
      </div>
    </>
  );
}
