import { Link } from 'react-router';
import { useLoad } from '../../api';
import { stageLabels } from './crm';

interface PipelineData {
  stages: { stage: string; leads: { id: number; name: string; company: string | null; kind: string; monthlyKwh: number; owner: string | null }[] }[];
  won: number; lost: number; winRate: number;
}

export default function Pipeline() {
  const { data, error } = useLoad<PipelineData>('/api/crm/pipeline');
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;

  return (
    <>
      <div className="row spread"><h1>Pipeline</h1><Link to="/corral/crm/leads">All leads →</Link></div>
      <div className="stats">
        <div className="card"><span className="muted">Won (90 days)</span><strong>{data.won}</strong></div>
        <div className="card"><span className="muted">Lost (90 days)</span><strong>{data.lost}</strong></div>
        <div className="card"><span className="muted">Win rate</span><strong>{data.winRate}%</strong></div>
      </div>
      <div className="board">
        {data.stages.map((s) => (
          <div key={s.stage} className="card">
            <h2>{stageLabels[s.stage]} <span className="muted">({s.leads.length})</span></h2>
            {s.leads.map((l) => (
              <Link key={l.id} to={`/corral/crm/leads/${l.id}`} className="lead">
                <strong>{l.company ?? l.name}</strong>
                <div className="muted">{l.kind} · {l.monthlyKwh.toLocaleString()} kWh/mo</div>
              </Link>
            ))}
          </div>
        ))}
      </div>
    </>
  );
}
