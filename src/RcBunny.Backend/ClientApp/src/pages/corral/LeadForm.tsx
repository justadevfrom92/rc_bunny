import type { FormEvent } from 'react';
import { stageLabels } from './crm';

export interface LeadOptions { stages: string[]; kinds: string[]; sources: string[]; partners: { id: number; name: string }[] }
export interface LeadValues {
  name: string; company: string | null; email: string | null; phone: string | null; kind: string; source: string; stage: string;
  monthlyKwh: number; partnerId: number | null; lostReason: string | null; notes: string | null;
}

/** The add / edit lead form. */
export default function LeadForm({ options, lead, onSave }: { options: LeadOptions; lead?: LeadValues; onSave: (values: LeadValues) => void }) {
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const text = (k: string) => (f.get(k) as string) || null;
    onSave({
      name: f.get('name') as string, company: text('company'), email: text('email'), phone: text('phone'),
      kind: f.get('kind') as string, source: f.get('source') as string, stage: f.get('stage') as string,
      monthlyKwh: Number(f.get('monthlyKwh') || 0), partnerId: f.get('partnerId') ? Number(f.get('partnerId')) : null,
      lostReason: text('lostReason'), notes: text('notes'),
    });
  };

  return (
    <form onSubmit={submit}>
      <div className="row">
        <label>Name<input name="name" defaultValue={lead?.name} required /></label>
        <label>Company<input name="company" defaultValue={lead?.company ?? ''} /></label>
      </div>
      <div className="row">
        <label>Email<input name="email" type="email" defaultValue={lead?.email ?? ''} /></label>
        <label>Phone<input name="phone" defaultValue={lead?.phone ?? ''} /></label>
        <label>kWh a month<input name="monthlyKwh" type="number" min="0" defaultValue={lead?.monthlyKwh ?? 0} /></label>
      </div>
      <div className="row">
        <label>Kind<select name="kind" defaultValue={lead?.kind}>{options.kinds.map((k) => <option key={k}>{k}</option>)}</select></label>
        <label>Source<select name="source" defaultValue={lead?.source}>{options.sources.map((s) => <option key={s}>{s}</option>)}</select></label>
        <label>Stage<select name="stage" defaultValue={lead?.stage ?? 'new'}>{options.stages.map((s) => <option key={s} value={s}>{stageLabels[s]}</option>)}</select></label>
        <label>Partner / broker
          <select name="partnerId" defaultValue={lead?.partnerId ?? ''}>
            <option value="">None</option>
            {options.partners.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
        </label>
      </div>
      <label>If lost, why?<input name="lostReason" defaultValue={lead?.lostReason ?? ''} /></label>
      <label>Notes<textarea name="notes" rows={3} defaultValue={lead?.notes ?? ''} /></label>
      <button className="primary">Save</button>
    </form>
  );
}
