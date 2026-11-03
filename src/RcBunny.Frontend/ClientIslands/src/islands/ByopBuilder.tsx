import { useMemo, useRef, useState } from 'react';

interface Product {
  key: string;
  name: string;
  type: string;
  step: string;
  description: string | null;
  adj: number;
  monthly: number;
}

interface Props {
  steps: string[];
  terms: number[];
  base12: number;
  baseMonthly: number;
  termAdj: Record<string, number>;
  etfs: Record<string, number>;
  products: Product[];
  defaults: string[];
}

const priceLabel = (p: Product) =>
  p.type === 'Included' ? 'Included'
    : p.monthly ? `+$${p.monthly.toFixed(2)}/mo`
      : p.adj ? `${p.adj < 0 ? '−' : '+'}${Math.abs(p.adj).toFixed(1)}¢` : 'Free';

/** Build Your Own Plan: steps, live price. Every number comes from the catalog the server put in the page. */
export default function ByopBuilder({ steps, terms, base12, baseMonthly, termAdj, etfs, products, defaults }: Props) {
  const allSteps = [...steps, 'Thank You'];
  const [step, setStep] = useState(0);
  const [termIdx, setTermIdx] = useState(Math.max(0, terms.indexOf(12)));
  const [chosen, setChosen] = useState<Set<string>>(() => new Set(defaults.filter((k) => products.some((p) => p.key === k))));
  const top = useRef<HTMLDivElement>(null);

  const term = terms[termIdx];
  const termText = term === 1 ? 'Month-to-Month' : `${term} months`;

  const totals = useMemo(() => {
    let rate = base12 + (termAdj[term] ?? 0);
    let monthly = baseMonthly;
    let extras = 0;
    for (const p of products) {
      if (!chosen.has(p.key)) continue;
      rate += p.adj;
      monthly += p.monthly;
      if (p.type !== 'Included') extras++;
    }
    return { rate, monthly, extras, bill: Math.round(rate * 10 + monthly) };
  }, [base12, baseMonthly, termAdj, term, products, chosen]);

  const toggle = (key: string) =>
    setChosen((s) => {
      const n = new Set(s);
      if (n.has(key)) n.delete(key); else n.add(key);
      return n;
    });

  const go = (i: number) => {
    setStep(Math.max(0, Math.min(allSteps.length - 1, i)));
    top.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  return (
    <div ref={top}>
      <ol className="steps">
        {allSteps.map((s, i) => (
          <li key={s} className={i === step ? 'active on' : i < step ? 'done' : ''}><span className="lbl">{s}</span></li>
        ))}
      </ol>
      <div className="byop">
        <div className="byop-panel">
          {steps.map((s, i) => (
            <div key={s} className={`byop-tab${i === step ? ' on' : ''}`}>
              <h2>{s === "Protectin'" ? "Protectin' What Matters" : s === 'Get Started' ? "Let's Get Started" : s}</h2>
              {i === 0 && (
                <div className="range-wrap">
                  <h3>First, choose your plan length:</h3>
                  <div className="range-value">{termText}</div>
                  <input type="range" min={0} max={terms.length - 1} step={1} value={termIdx} onChange={(e) => setTermIdx(Number(e.target.value))} aria-label="Plan length" />
                  <div className="range-labels">{terms.map((t) => <span key={t}>{t}</span>)}</div>
                </div>
              )}
              {products.filter((p) => p.step === s).map((p) => (
                <label className="opt" key={p.key}>
                  <input type="checkbox" checked={chosen.has(p.key)} disabled={p.type === 'Included'} onChange={() => toggle(p.key)} />
                  <span><strong>{p.name}</strong>{p.description && <small>{p.description}</small>}</span>
                  <span className="price">{priceLabel(p)}</span>
                </label>
              ))}
            </div>
          ))}
          <div className={`byop-tab center${step === steps.length ? ' on' : ''}`}>
            <div className="stars-row" style={{ justifyContent: 'center' }} aria-hidden="true">★ ★ ★</div>
            <h2>Thank You, Pardner!</h2>
            <p className="lead">Your custom plan is ready to go. Review your summary, then continue to sign up.</p>
            <a className="btn" href="/contact-us?plan=BYOP">Continue to Sign Up</a>
          </div>
          <div className="byop-nav">
            <button className="btn btn-outline btn-back" type="button" style={{ visibility: step === 0 ? 'hidden' : 'visible' }} onClick={() => go(step - 1)}>Back</button>
            {step < allSteps.length - 1 && <button className="btn" type="button" onClick={() => go(step + 1)}>Next Step</button>}
          </div>
        </div>

        <aside className="summary" aria-live="polite">
          <h3>Your Plan Summary</h3>
          <dl>
            <dt>Plan length</dt><dd>{termText}</dd>
            <dt>Extras</dt><dd>{totals.extras ? `${totals.extras} selected` : 'None'}</dd>
            <dt>Monthly charges</dt><dd>${totals.monthly.toFixed(2)}</dd>
            <dt>Early termination fee</dt><dd>${etfs[term] ?? 0}</dd>
            <dt>Est. monthly bill*</dt><dd>${totals.bill}</dd>
          </dl>
          <div className="total"><span>Est. avg. price</span><strong>{totals.rate.toFixed(1)}¢</strong></div>
          <p style={{ fontSize: '.75rem', opacity: 0.7, margin: '14px 0 0' }}>*Estimate at 1,000 kWh/month. Your final rate is shown on the Electricity Facts Label at sign-up.</p>
        </aside>
      </div>
    </div>
  );
}
