import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, date, useLoad } from '../../api';
import { stageLabels } from './crm';
import LeadForm, { type LeadOptions } from './LeadForm';

interface Row { id: number; name: string; company: string | null; kind: string; source: string; stage: string; monthlyKwh: number; partner: string | null; owner: string | null; createdAt: string }

export default function Leads() {
  const [params, setParams] = useSearchParams();
  const stage = params.get('stage') ?? 'open';
  const navigate = useNavigate();
  const { data, error } = useLoad<{ leads: Row[] } & LeadOptions>(`/api/crm/leads?stage=${stage}`);
  const [adding, setAdding] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  const add = (lead: unknown) =>
    api.post<{ id: number }>('/api/crm/leads', lead).then((r) => navigate(`/corral/crm/leads/${r.id}`), (e: Error) => setProblem(e.message));

  return (
    <>
      <div className="row spread"><h1>Leads</h1><button onClick={() => setAdding(!adding)}>Add a lead</button></div>
      {problem && <p className="error">{problem}</p>}
      {adding && <div className="card"><LeadForm options={data} onSave={add} /></div>}
      <div className="row">
        {['open', ...data.stages].map((s) => (
          <button key={s} className={s === stage ? 'primary' : ''} onClick={() => setParams({ stage: s })}>{s === 'open' ? 'Open' : stageLabels[s]}</button>
        ))}
      </div>
      <table>
        <thead><tr><th>Lead</th><th>Kind</th><th>Source</th><th>Stage</th><th className="num">kWh/mo</th><th>Partner</th><th>Added</th></tr></thead>
        <tbody>
          {data.leads.map((l) => (
            <tr key={l.id}>
              <td><Link to={`/corral/crm/leads/${l.id}`}>{l.company ?? l.name}</Link>{l.company && <span className="muted"> · {l.name}</span>}</td>
              <td>{l.kind}</td><td>{l.source}</td><td><span className="tag">{stageLabels[l.stage]}</span></td>
              <td className="num">{l.monthlyKwh.toLocaleString()}</td><td>{l.partner ?? '—'}</td><td>{date(l.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
