import { useEffect, useLayoutEffect, useRef, useState, type InputHTMLAttributes, type KeyboardEvent } from 'react';
import { createPortal } from 'react-dom';

/*
  ebunny's shared/datepicker.js and shared/password-toggle.js as React components: the same markup, classes and
  CSS (from those scripts), so date and password fields look and work like ebunny's.
*/

const DP_CSS =
  '.dp-wrap{position:relative;display:block;width:100%}.dp-wrap>input{width:100%;padding-right:40px!important}' +
  '.dp-btn{position:absolute;right:6px;top:50%;transform:translateY(-50%);width:32px;height:32px;display:inline-flex;align-items:center;justify-content:center;border:0;border-radius:6px;background:transparent;color:#5b6b80;cursor:pointer;padding:0}' +
  '.dp-btn:hover{background:rgba(16,34,71,.07);color:#102247}.dp-btn::after,.dp-btn::before{display:none!important}' +
  '.dp{position:absolute;z-index:9999;width:292px;background:#fff;border:1px solid #d9e1ec;border-radius:12px;box-shadow:0 14px 34px rgba(16,34,71,.22);padding:12px;font:14px/1.2 system-ui,-apple-system,Segoe UI,sans-serif;color:#102247}' +
  '.dp-head{display:flex;align-items:center;justify-content:space-between;margin-bottom:8px}.dp-head b{font-size:15px}' +
  '.dp-nav{width:32px;height:32px;border:1px solid #d9e1ec;border-radius:8px;background:#fff;cursor:pointer;font-size:18px;line-height:1;color:#102247;padding:0}.dp-nav:hover{background:#eef4fb}' +
  '.dp-grid{display:grid;grid-template-columns:repeat(7,1fr);gap:2px}.dp-dow{font-size:11px;font-weight:700;color:#7a879a;text-align:center;padding:4px 0;text-transform:uppercase}' +
  '.dp-day{height:36px;border:0;border-radius:8px;background:transparent;cursor:pointer;font:inherit;color:#102247;padding:0}' +
  '.dp-day:hover{background:#e6f6fd}.dp-day.out{color:#b4bfcd}.dp-day.today{box-shadow:inset 0 0 0 1px #00aeef;font-weight:700}' +
  '.dp-day.sel{background:#00aeef;color:#fff;font-weight:700}.dp-day.focus{outline:2px solid #102247;outline-offset:-2px}.dp-day:disabled{color:#d3d9e2;cursor:not-allowed;background:transparent}' +
  '.dp-foot{display:flex;justify-content:space-between;margin-top:10px;border-top:1px solid #eef1f5;padding-top:8px}' +
  '.dp-link{border:0;background:none;color:#0093cc;font:600 13px system-ui,sans-serif;cursor:pointer;padding:4px 6px;border-radius:6px}.dp-link:hover{background:#eef4fb}';

const PW_CSS =
  '.pw-wrap{position:relative;display:block;width:100%}.pw-wrap>input{width:100%;padding-right:44px!important}' +
  '.pw-eye{position:absolute;right:6px;top:50%;transform:translateY(-50%);display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;padding:0;margin:0;border:0;border-radius:6px;background:transparent;color:#5b6b80;cursor:pointer}' +
  '.pw-eye:hover,.pw-eye:focus-visible{color:#102247;background:rgba(16,34,71,.07)}.pw-eye::after,.pw-eye::before{display:none!important}';

function useCss(id: string, css: string) {
  useEffect(() => {
    if (document.getElementById(id)) return;
    const s = document.createElement('style');
    s.id = id; s.textContent = css;
    document.head.appendChild(s);
  }, [id, css]);
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const DAYS = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];
const pad = (n: number) => (n < 10 ? '0' : '') + n;
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const parse = (s?: string | null) => { const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s || ''); return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null; };

type DateProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'onChange' | 'value' | 'defaultValue'> & {
  value?: string; defaultValue?: string; onChange?: (value: string) => void;
};

/** A date field (YYYY-MM-DD) with ebunny's calendar. Works as a plain form field (name + defaultValue) or controlled (value + onChange). */
export function DateInput({ value, defaultValue, onChange, min, max, style, ...rest }: DateProps) {
  useCss('dp-css', DP_CSS);
  const [own, setOwn] = useState(defaultValue ?? '');
  const current = value ?? own;
  const [open, setOpen] = useState(false);
  const wrap = useRef<HTMLSpanElement>(null);
  const input = useRef<HTMLInputElement>(null);

  const set = (v: string) => { if (value === undefined) setOwn(v); onChange?.(v); };
  const maxWidth = style?.maxWidth;
  const inputStyle = { ...style, maxWidth: undefined };

  return (
    <span className="dp-wrap" ref={wrap} style={maxWidth ? { maxWidth } : undefined}>
      <input {...rest} ref={input} type="text" inputMode="numeric" autoComplete="off" className={`dp-input${rest.className ? ' ' + rest.className : ''}`}
        placeholder={rest.placeholder ?? 'YYYY-MM-DD'} pattern="\d{4}-\d{2}-\d{2}" value={current} style={inputStyle}
        onChange={(e) => set(e.target.value)} onClick={() => setOpen(true)}
        onKeyDown={(e) => { if (open && (e.key.startsWith('Arrow') || e.key === 'Enter' || e.key === 'Escape' || e.key.startsWith('Page'))) e.preventDefault(); }} />
      <button type="button" className="dp-btn" aria-label="Open calendar" onClick={() => setOpen(!open)}>
        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2" /><path d="M3 10h18M8 3v4M16 3v4" /></svg>
      </button>
      {open && <Calendar anchor={input.current!} wrap={wrap.current!} value={current} min={parse(min as string)} max={parse(max as string)}
        onPick={(v) => { set(v); setOpen(false); input.current?.focus(); }} onClose={() => setOpen(false)} />}
    </span>
  );
}

function Calendar({ anchor, wrap, value, min, max, onPick, onClose }: {
  anchor: HTMLInputElement; wrap: HTMLElement; value: string; min: Date | null; max: Date | null; onPick: (v: string) => void; onClose: () => void;
}) {
  const sel = parse(value);
  const [focus, setFocus] = useState(() => (sel ? new Date(sel) : new Date()));
  const view = new Date(focus.getFullYear(), focus.getMonth(), 1);
  const box = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ top: 0, left: 0 });
  const allowed = (d: Date) => !(min && d < min) && !(max && d > max);

  useLayoutEffect(() => {
    const r = anchor.getBoundingClientRect(), h = box.current?.offsetHeight ?? 0, cw = document.documentElement.clientWidth;
    let top = window.scrollY + r.bottom + 6, left = window.scrollX + r.left;
    if (left + 292 > window.scrollX + cw - 8) left = window.scrollX + cw - 300;
    if (r.bottom + h + 8 > window.innerHeight && r.top > h + 8) top = window.scrollY + r.top - h - 6;
    setPos({ top, left: Math.max(8, left) });
  }, [anchor, focus]);

  useEffect(() => {
    const outside = (e: MouseEvent) => { if (!box.current?.contains(e.target as Node) && !wrap.contains(e.target as Node)) onClose(); };
    const keys = (e: globalThis.KeyboardEvent) => { if (e.target === anchor) onKey(e as unknown as KeyboardEvent); };
    const t = setTimeout(() => document.addEventListener('mousedown', outside, true), 0);
    anchor.addEventListener('keydown', keys);
    window.addEventListener('resize', onClose);
    return () => { clearTimeout(t); document.removeEventListener('mousedown', outside, true); anchor.removeEventListener('keydown', keys); window.removeEventListener('resize', onClose); };
  });

  function onKey(e: KeyboardEvent) {
    const step = ({ ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 } as Record<string, number>)[e.key];
    const next = new Date(focus);
    if (step) next.setDate(next.getDate() + step);
    else if (e.key === 'PageUp' || e.key === 'PageDown') next.setMonth(next.getMonth() + (e.key === 'PageUp' ? -1 : 1));
    else if (e.key === 'Enter') { if (allowed(focus)) onPick(iso(focus)); return; }
    else if (e.key === 'Escape') { onClose(); return; }
    else return;
    setFocus(next);
  }

  const start = new Date(view.getFullYear(), view.getMonth(), 1 - view.getDay());
  const today = iso(new Date());
  return createPortal(
    <div className="dp" role="dialog" aria-label="Choose a date" ref={box} style={{ top: pos.top, left: pos.left }} onKeyDown={onKey}>
      <div className="dp-head">
        <button type="button" className="dp-nav" aria-label="Previous month" onClick={() => setFocus(new Date(view.getFullYear(), view.getMonth() - 1, 1))}>‹</button>
        <b>{MONTHS[view.getMonth()]} {view.getFullYear()}</b>
        <button type="button" className="dp-nav" aria-label="Next month" onClick={() => setFocus(new Date(view.getFullYear(), view.getMonth() + 1, 1))}>›</button>
      </div>
      <div className="dp-grid">
        {DAYS.map((d) => <span key={d} className="dp-dow">{d}</span>)}
        {Array.from({ length: 42 }, (_, i) => {
          const d = new Date(start); d.setDate(start.getDate() + i);
          const k = iso(d);
          const cls = ['dp-day', d.getMonth() !== view.getMonth() && 'out', k === today && 'today', sel && k === iso(sel) && 'sel', k === iso(focus) && 'focus'].filter(Boolean).join(' ');
          return <button key={k} type="button" className={cls} disabled={!allowed(d)} aria-label={d.toDateString()} onClick={() => onPick(k)}>{d.getDate()}</button>;
        })}
      </div>
      <div className="dp-foot">
        <button type="button" className="dp-link" onClick={() => allowed(new Date()) && onPick(today)}>Today</button>
        <button type="button" className="dp-link" onClick={() => onPick('')}>Clear</button>
      </div>
    </div>,
    document.body,
  );
}

const EYE = <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" /><circle cx="12" cy="12" r="3" /></svg>;
const EYE_OFF = <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M3 3l18 18M10.6 5.1A10.6 10.6 0 0 1 12 5c6.5 0 10 7 10 7a17.6 17.6 0 0 1-3.2 4.2M6.6 6.6C3.8 8.4 2 12 2 12s3.5 7 10 7c1.7 0 3.2-.4 4.5-1.1M9.9 9.9a3 3 0 0 0 4.2 4.2" /></svg>;

/** A password field with ebunny's show/hide eye button. */
export function PasswordInput(props: Omit<InputHTMLAttributes<HTMLInputElement>, 'type'>) {
  useCss('pw-toggle-css', PW_CSS);
  const [show, setShow] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  return (
    <span className="pw-wrap">
      <input {...props} ref={input} type={show ? 'text' : 'password'} />
      <button type="button" className="pw-eye" aria-label={show ? 'Hide password' : 'Show password'} aria-pressed={show} onClick={() => { setShow(!show); input.current?.focus(); }}>
        {show ? EYE_OFF : EYE}
      </button>
    </span>
  );
}
