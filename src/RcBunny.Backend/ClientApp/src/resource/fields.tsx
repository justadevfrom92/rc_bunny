import { useState } from 'react';
import { Link } from 'react-router-dom';
import { date, dateTime, money } from '../ui';
import type { Field } from './api';
import { useOptions } from './api';

/** Where a reference points, when that table has its own screen. */
const REF_SCREENS: Record<string, string> = {
  customers: '/corral/customers', users: '/sheriff/users', support_tickets: '/ranger/tickets',
  plans: '/astro/r/plans', markets: '/sheriff/r/markets', campaigns: '/rodeo/r/campaigns', email_templates: '/rodeo/r/email-templates',
  surveys: '/rodeo/r/surveys', ai_agents: '/deputy/r/ai-agents', ai_models: '/deputy/r/ai-models', pages: '/lando/r/pages', content_blocks: '/lando/r/content-blocks',
  crm_leads: '/corral/r/crm-leads', crm_partners: '/corral/r/crm-partners', reward_offers: '/bounty/r/reward-offers', pdf_templates: '/rodeo/r/pdf-templates',
  plan_groups: '/astro/r/plan-groups', templates: '/lando/r/templates', saved_reports: '/walker/r/saved-reports', report_runs: '/walker/runs',
};

const MONEY = /amount|balance|price|total|subtotal|tax|shipping|credit|deposit|monthly|mrc|value$|was$/i;

/** One value, displayed. */
export function Value({ field, value, labels }: { field: Field; value: unknown; labels?: Record<string, string> }) {
  if (value === null || value === undefined || value === '') return <span className="muted">—</span>;
  switch (field.type) {
    case 'bool':
      return value ? <span className="ok-text">Yes</span> : <span className="muted">No</span>;
    case 'date':
      return <>{date(String(value))}</>;
    case 'datetime':
      return <>{dateTime(String(value))}</>;
    case 'decimal':
      return <span className="num">{MONEY.test(field.prop) ? money(Number(value)) : Number(value).toLocaleString('en-US', { maximumFractionDigits: 4 })}</span>;
    case 'int':
      return <span className="num">{Number(value).toLocaleString('en-US')}</span>;
    case 'list':
      return <>{(value as string[]).join(', ')}</>;
    case 'json':
      return <code className="json">{JSON.stringify(value).slice(0, 140)}</code>;
    case 'ref': {
      const label = labels?.[String(value)] ?? `#${value}`;
      const base = field.ref ? REF_SCREENS[field.ref] : undefined;
      return base ? <Link to={`${base}/${value}`} onClick={(e) => e.stopPropagation()}>{label}</Link> : <>{label}</>;
    }
    case 'text': {
      const s = String(value).replace(/<[^>]+>/g, ' ');
      return <>{s.length > 140 ? s.slice(0, 140) + '…' : s}</>;
    }
    default:
      return <>{String(value)}</>;
  }
}

/** Full value on the record page (long text and JSON shown whole). */
export function FullValue({ field, value, labels }: { field: Field; value: unknown; labels?: Record<string, string> }) {
  if (field.type === 'json' && value != null) return <pre className="json">{JSON.stringify(value, null, 2)}</pre>;
  if (field.type === 'text' && value) return <pre className="text">{String(value)}</pre>;
  return <Value field={field} value={value} labels={labels} />;
}

/** The input for one field. Values are kept as strings/JSON the server understands. */
export function Input({ field, value, onChange, label }: { field: Field; value: unknown; onChange: (v: unknown) => void; label?: string }) {
  const id = `f-${field.name}`;
  const common = { id, 'aria-label': field.label, required: field.required };
  if (field.options) {
    return (
      <select {...common} value={value == null ? '' : String(value)} onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)}>
        <option value="">{field.required ? 'Choose…' : '—'}</option>
        {field.options.map((o) => <option key={o}>{o}</option>)}
        {value != null && value !== '' && !field.options.includes(String(value)) && <option>{String(value)}</option>}
      </select>
    );
  }
  switch (field.type) {
    case 'bool':
      return <input type="checkbox" id={id} aria-label={field.label} checked={!!value} onChange={(e) => onChange(e.target.checked)} />;
    case 'int':
    case 'decimal':
      return <input {...common} type="number" step={field.type === 'int' ? 1 : 'any'} value={value == null ? '' : String(value)} onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)} />;
    case 'date':
      return <input {...common} type="date" value={value ? String(value).slice(0, 10) : ''} onChange={(e) => onChange(e.target.value || null)} />;
    case 'datetime':
      return <input {...common} type="datetime-local" value={value ? String(value).slice(0, 16) : ''} onChange={(e) => onChange(e.target.value || null)} />;
    case 'time':
      return <input {...common} type="time" value={value ? String(value).slice(0, 5) : ''} onChange={(e) => onChange(e.target.value || null)} />;
    case 'text':
      return <textarea {...common} rows={field.prop.toLowerCase().includes('html') || field.prop === 'Body' ? 12 : 4} value={value == null ? '' : String(value)} maxLength={field.maxLength ?? undefined} onChange={(e) => onChange(e.target.value)} />;
    case 'list':
      return <input {...common} value={Array.isArray(value) ? value.join(', ') : ''} placeholder="Comma-separated" onChange={(e) => onChange(e.target.value.split(',').map((s) => s.trim()).filter(Boolean))} />;
    case 'json':
      return <JsonInput field={field} value={value} onChange={onChange} />;
    case 'ref':
      return <RefInput field={field} value={value} onChange={onChange} label={label} />;
    default:
      return <input {...common} value={value == null ? '' : String(value)} maxLength={field.maxLength ?? undefined} onChange={(e) => onChange(e.target.value)} />;
  }
}

function JsonInput({ field, value, onChange }: { field: Field; value: unknown; onChange: (v: unknown) => void }) {
  const [text, setText] = useState(() => (value == null ? '' : JSON.stringify(value, null, 2)));
  const [bad, setBad] = useState(false);
  return (
    <>
      <textarea
        id={`f-${field.name}`}
        className="mono"
        rows={6}
        value={text}
        aria-invalid={bad}
        onChange={(e) => {
          setText(e.target.value);
          try {
            onChange(e.target.value.trim() === '' ? null : JSON.parse(e.target.value));
            setBad(false);
          } catch {
            setBad(true);
          }
        }}
      />
      {bad && <span className="small bad-text">Not valid JSON yet.</span>}
    </>
  );
}

/** Searchable picker for a reference (labels from /api/r/options/{table}). */
function RefInput({ field, value, onChange, label }: { field: Field; value: unknown; onChange: (v: unknown) => void; label?: string }) {
  const [q, setQ] = useState('');
  const [open, setOpen] = useState(false);
  const opts = useOptions(open ? field.ref : null, q);
  const current = value == null || value === '' ? null : String(value);
  return (
    <div className="ref-input">
      <button type="button" className="ref-current" onClick={() => setOpen((o) => !o)} aria-expanded={open} id={`f-${field.name}`}>
        {current ? (label ?? opts.data?.find((o) => o.key === current)?.value ?? `#${current}`) : <span className="muted">Choose…</span>}
      </button>
      {current && !field.required && <button type="button" className="link small" onClick={() => onChange(null)}>clear</button>}
      {open && (
        <div className="ref-pop">
          <input autoFocus placeholder="Search…" value={q} onChange={(e) => setQ(e.target.value)} aria-label={`Search ${field.label}`} />
          <ul>
            {opts.data?.map((o) => (
              <li key={o.key}><button type="button" className="link" onClick={() => { onChange(o.key); setOpen(false); }}>{o.value}</button></li>
            ))}
            {opts.data?.length === 0 && <li className="muted small">Nothing matches.</li>}
          </ul>
        </div>
      )}
    </div>
  );
}
