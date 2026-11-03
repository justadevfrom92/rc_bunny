import { useState } from 'react';

interface Props {
  months: { label: string; kwh: number; amount: number }[];
}

/** My Account usage: bars per month with the bill on hover/focus. Data comes from the page (no extra request). */
export default function UsageChart({ months }: Props) {
  const [hover, setHover] = useState<number | null>(null);
  if (!months.length) return null;
  const max = Math.max(...months.map((m) => m.kwh));
  return (
    <div className="usage" role="img" aria-label={`Monthly usage, highest ${max.toLocaleString()} kWh`}>
      {months.map((m, i) => (
        <div
          key={m.label}
          className="usage-bar"
          tabIndex={0}
          onMouseEnter={() => setHover(i)}
          onMouseLeave={() => setHover(null)}
          onFocus={() => setHover(i)}
          onBlur={() => setHover(null)}
        >
          {hover === i && <span className="usage-tip">{m.kwh.toLocaleString()} kWh · ${m.amount.toFixed(2)}</span>}
          <span className="fill" style={{ height: `${Math.max(4, (m.kwh / max) * 100)}%` }} />
          <small>{m.label}</small>
        </div>
      ))}
    </div>
  );
}
