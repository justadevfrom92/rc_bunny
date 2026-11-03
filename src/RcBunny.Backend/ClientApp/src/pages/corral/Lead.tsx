import { useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { api, date, useLoad } from '../../api';
import { stageLabels } from './crm';
import LeadForm, { type LeadOptions, type LeadValues } from './LeadForm';

interface FollowUp { id: number; dueOn: string; task: string; done: boolean }

export default function Lead() {
  const { id } = useParams();
  const { data, error, reload } = useLoad<{ lead: LeadValues & { id: number; createdAt: string; closedAt: string | null }; followUps: FollowUp[] }>(`/api/crm/leads/${id}`);
  const { data: options } = useLoad<LeadOptions>('/api/crm/options');
  const [message, setMessage] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data || !options) return <p>Loading…</p>;
  const { lead, followUps } = data;

  const run = (action: Promise<unknown>, done: string) => action.then(() => { setMessage(done); reload(); }, (e: Error) => setMessage(e.message));
  const save = (values: LeadValues) => run(api.put(`/api/crm/leads/${lead.id}`, values), 'Saved.');
  const addFollowUp = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget, f = new FormData(form);
    run(api.post('/api/crm/follow-ups', { leadId: lead.id, dueOn: f.get('dueOn'), task: f.get('task') }).then(() => form.reset()), 'Follow-up added.');
  };

  return (
    <>
      <p><Link to="/corral/crm/leads">← Leads</Link></p>
      <h1>{lead.company ?? lead.name} <span className="tag">{stageLabels[lead.stage]}</span></h1>
      <p className="muted">Added {date(lead.createdAt)}{lead.closedAt && ` · closed ${date(lead.closedAt)}`}</p>
      {message && <p>{message}</p>}
      <div className="grid">
        <section className="card"><h2>Details</h2><LeadForm key={JSON.stringify(lead)} options={options} lead={lead} onSave={save} /></section>
        <section className="card">
          <h2>Follow-ups</h2>
          <form className="row" onSubmit={addFollowUp}>
            <input name="dueOn" type="date" required style={{ maxWidth: 170 }} />
            <input name="task" placeholder="What needs doing" required />
            <button className="primary">Add</button>
          </form>
          <ul className="notes">
            {followUps.map((f) => (
              <li key={f.id} className="row spread">
                <span style={{ textDecoration: f.done ? 'line-through' : undefined }}><small>{date(f.dueOn)}</small> {f.task}</span>
                {!f.done && <button onClick={() => run(api.post(`/api/crm/follow-ups/${f.id}/done`), 'Done.')}>Done</button>}
              </li>
            ))}
          </ul>
        </section>
      </div>
    </>
  );
}
