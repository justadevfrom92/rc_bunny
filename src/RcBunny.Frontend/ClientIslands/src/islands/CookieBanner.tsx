import { useState } from 'react';

/** Cookie banner: shown until the visitor answers; the answer is stored (Lando → Cookie Consents) and in a cookie. */
export default function CookieBanner() {
  const [custom, setCustom] = useState(false);
  const [analytics, setAnalytics] = useState(true);
  const [marketing, setMarketing] = useState(false);
  const [gone, setGone] = useState(false);
  if (gone) return null;

  const answer = (choice: 'all' | 'necessary' | 'custom') => {
    setGone(true);
    void fetch('/site/consent', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-RCB': '1' },
      body: JSON.stringify({ choice, analytics, marketing, page: location.pathname }),
    });
  };

  return (
    <div className="cookie-banner" role="region" aria-label="Cookies"><div className="wrap">
      <div><h2>Cookies</h2><p>We use cookies to run this site and, with your OK, to measure and improve it.</p>
      {custom && (
        <div className="cb-choose">
          <label><input type="checkbox" checked disabled /> Necessary</label>
          <label><input type="checkbox" checked={analytics} onChange={(e) => setAnalytics(e.target.checked)} /> Analytics</label>
          <label><input type="checkbox" checked={marketing} onChange={(e) => setMarketing(e.target.checked)} /> Marketing</label>
        </div>
      )}
      </div>
      <div className="cb-actions">
        {custom ? <button className="btn btn-sm" onClick={() => answer('custom')}>Save choices</button> : <button className="btn btn-sm" onClick={() => answer('all')}>Accept all</button>}
        <button className="btn btn-outline btn-sm" onClick={() => answer('necessary')}>Necessary only</button>
        {!custom && <button className="btn btn-outline btn-sm" onClick={() => setCustom(true)}>Choose</button>}
      </div>
    </div></div>
  );
}
