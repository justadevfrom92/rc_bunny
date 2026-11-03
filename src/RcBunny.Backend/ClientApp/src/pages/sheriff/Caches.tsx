import { useState } from 'react';
import { api } from '../../api';

export default function Caches() {
  const [done, setDone] = useState(false);
  return (
    <>
      <h1>Caches</h1>
      <div className="card">
        <p>What's kept in memory, and for how long:</p>
        <ul>
          <li><b>Admin:</b> each person's role and permissions, 30 seconds. Changing someone's role or sign-in clears theirs straight away.</li>
          <li><b>Website:</b> markets, zip codes and plan prices, 1 minute. The Home and Plans pages, 1 minute. The maintenance switch, 30 seconds.</li>
          <li><b>Your browser:</b> each admin screen shows its last data instantly while it reloads.</li>
        </ul>
        <button onClick={() => api.post('/api/sheriff/caches/clear').then(() => setDone(true))}>Clear the admin cache</button>
        {done && <p>Cleared. Everyone's permissions are re-read on their next click.</p>}
      </div>
    </>
  );
}
