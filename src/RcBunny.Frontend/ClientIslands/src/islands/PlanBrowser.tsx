import { useEffect, useState } from 'react';

interface Plan {
  id: number;
  name: string;
  internal: string;
  slug: string;
  term: number;
  termLabel: string;
  etfLabel: string;
  tags: string[];
  perks: string[];
  price: number | null;
  featured: boolean;
}

interface Props {
  filter: string;
  filters: { key: string; label: string }[];
  zip: string | null;
  zipNote: string | null;
  plans: Plan[];
}

const matches = (p: Plan, f: string) =>
  f === 'all' ? true : f === 'month' ? p.term === 1 : /^\d+$/.test(f) ? p.term === Number(f) : p.tags.includes(f);

/**
 * The plans grid with instant filtering. Same markup as the server-rendered version; chips stay real links
 * (?filter=…) so the address is shareable, but clicks filter in place and update the URL without reloading.
 */
export default function PlanBrowser({ filter: initial, filters, zip, zipNote, plans }: Props) {
  const [filter, setFilter] = useState(initial);

  useEffect(() => {
    const onPop = () => setFilter(new URLSearchParams(location.search).get('filter') ?? 'all');
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, []);

  const href = (key: string) => {
    const q = new URLSearchParams();
    if (zip) q.set('zip', zip);
    if (key !== 'all') q.set('filter', key);
    const s = q.toString();
    return `/plans${s ? `?${s}` : ''}`;
  };

  const pick = (key: string) => (e: React.MouseEvent) => {
    e.preventDefault();
    setFilter(key);
    history.pushState(null, '', href(key));
  };

  const shown = plans.filter((p) => matches(p, filter));
  const zipQ = zip ? `?zip=${encodeURIComponent(zip)}` : '';

  return (
    <>
      <div className="plans-head">
        <div>
          <span className="eyebrow">Filter Plans</span>
          <h2 style={{ marginBottom: 0 }}>Find your fit</h2>
          {zipNote && <p className="zip-note">{zipNote}</p>}
        </div>
        <div className="filters" role="toolbar" aria-label="Filter plans">
          {filters.map((f) => (
            <a key={f.key} className={`chip${f.key === filter ? ' on' : ''}`} href={href(f.key)} onClick={pick(f.key)} aria-pressed={f.key === filter}>
              {f.label}
            </a>
          ))}
        </div>
      </div>
      <div className="plan-grid">
        {shown.map((p) => (
          <article key={p.id} className={`plan${p.featured ? ' featured' : ''}`}>
            {p.featured && <span className="plan-flag">Popular Pick</span>}
            <div className="plan-top">
              <span className="plan-term">{p.termLabel}</span>
              <h3><a className="plan-name" href={`/plans/${p.slug}${zipQ}`}>{p.name}</a></h3>
              <div className="plan-rate"><strong>{p.price == null ? '—' : `${p.price.toFixed(1)}¢`}</strong><span>per kWh</span></div>
              <p className="plan-rate-note">Avg. price at 1,000 kWh · {p.etfLabel}</p>
            </div>
            <div className="plan-body">
              <ul className="checks">{p.perks.map((x) => <li key={x}>{x}</li>)}</ul>
              <a className="btn btn-block" href={`/contact-us?plan=${encodeURIComponent(p.internal)}`}>Sign Up</a>
              <a className="btn btn-outline btn-block plan-details" href={`/plans/${p.slug}${zipQ}`}>Plan Details</a>
              <div className="plan-links"><a href="#">Electricity Facts Label</a><a href="#">Terms of Service</a><a href="#">YRAC</a></div>
            </div>
          </article>
        ))}
        {shown.length === 0 && <p className="center" style={{ gridColumn: '1/-1' }}>No plans match that filter, pardner. Try another.</p>}
      </div>
    </>
  );
}
