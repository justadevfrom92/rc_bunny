import { useState, type FormEvent } from 'react';

interface Props {
  ranges: [number, number][];
}

/** Home page zip box: Home/Business toggle and an instant "do we serve you?" check, using zip ranges in the page. */
export default function ZipCheck({ ranges }: Props) {
  const [type, setType] = useState<'home' | 'business'>('home');
  const [zip, setZip] = useState('');
  const [msg, setMsg] = useState<{ tone: 'error' | 'ok'; text: string } | null>(null);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!/^\d{5}$/.test(zip)) {
      setMsg({ tone: 'error', text: 'Enter a 5-digit zip code.' });
      return;
    }
    const z = Number(zip);
    if (!ranges.some(([from, to]) => z >= from && z <= to)) {
      setMsg({ tone: 'error', text: "We don't serve that area yet. Try another zip, or call us." });
      return;
    }
    setMsg({ tone: 'ok', text: 'Great, we serve your area!' });
    window.location.assign(type === 'business' ? `/business?zip=${zip}` : `/plans?zip=${zip}`);
  };

  return (
    <form className="zip-card" onSubmit={submit} noValidate>
      <h2>Find your plan</h2>
      <p>Enter your zip code to see rates available at your address.</p>
      <div className="toggle-group" role="group" aria-label="Service type">
        <button type="button" className={type === 'home' ? 'on' : ''} aria-pressed={type === 'home'} onClick={() => setType('home')}>Home</button>
        <button type="button" className={type === 'business' ? 'on' : ''} aria-pressed={type === 'business'} onClick={() => setType('business')}>Business</button>
      </div>
      <div className="field">
        <label htmlFor="zip">Service Zip Code</label>
        <input
          id="zip"
          name="zip"
          inputMode="numeric"
          maxLength={5}
          placeholder="e.g. 77001"
          autoComplete="postal-code"
          value={zip}
          onChange={(e) => { setZip(e.target.value.replace(/\D/g, '')); setMsg(null); }}
        />
      </div>
      <button className="btn btn-block" type="submit">View Plans</button>
      <div className={`form-msg ${msg?.tone ?? ''}`} role="status">{msg?.text}</div>
    </form>
  );
}
