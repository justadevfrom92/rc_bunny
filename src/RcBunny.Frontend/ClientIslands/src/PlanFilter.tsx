import { useState } from 'react';

interface Plan { name: string; term: number; price: number; green: number }

const termLabel = (months: number) => (months <= 1 ? 'Month to month' : `${months} months`);

/** The plans page: the same cards Razor renders, plus buttons to filter by contract length without reloading. */
export default function PlanFilter({ plans }: { plans: Plan[] }) {
  const [term, setTerm] = useState<number | null>(null);
  const terms = [...new Set(plans.map((p) => p.term))].sort((a, b) => a - b);
  const shown = term === null ? plans : plans.filter((p) => p.term === term);

  return (
    <>
      <div className="filters">
        <button className={`chip${term === null ? ' on' : ''}`} onClick={() => setTerm(null)}>All</button>
        {terms.map((t) => (
          <button key={t} className={`chip${term === t ? ' on' : ''}`} onClick={() => setTerm(t)}>{termLabel(t)}</button>
        ))}
      </div>
      <div className="plan-grid">
        {shown.map((p) => (
          <article key={p.name} className="plan">
            <div className="plan-top">
              <span className="plan-term">{termLabel(p.term)}</span>
              <h3>{p.name}</h3>
              <div className="plan-rate"><strong>{p.price.toFixed(1)}¢</strong><span>per kWh</span></div>
              <p className="plan-rate-note">Average price at 1,000 kWh</p>
            </div>
            <div className="plan-body">
              <ul className="checks">
                {p.green > 0 && <li>{p.green}% renewable</li>}
                <li>{p.term <= 1 ? 'No contract' : 'Fixed rate for the whole term'}</li>
              </ul>
              <a className="btn btn-block" href={`/contact?plan=${encodeURIComponent(p.name)}`}>Sign Up</a>
            </div>
          </article>
        ))}
      </div>
    </>
  );
}
