import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, qs } from '../../api';
import type { CustomerRow } from '../../types';
import { ErrorBox, Panel, Skeleton } from '../../ui';
import { CustomerTable } from './CustomerSearch';

export default function EsiidLookup() {
  const [input, setInput] = useState('');
  const [esiid, setEsiid] = useState('');
  const q = useQuery({
    queryKey: ['corral', 'esiid', esiid],
    queryFn: () => api<CustomerRow[]>(`/api/corral/esiid${qs({ esiid })}`),
    enabled: esiid.length >= 6,
    staleTime: 5 * 60_000,
  });
  return (
    <>
      <h1>ESIID Lookup</h1>
      <Panel>
        <form className="filters" onSubmit={(e) => { e.preventDefault(); setEsiid(input.replace(/\D/g, '')); }}>
          <input inputMode="numeric" placeholder="ESIID (at least 6 digits)" value={input} onChange={(e) => setInput(e.target.value)} aria-label="ESIID" autoFocus />
          <button className="btn">Look up</button>
        </form>
      </Panel>
      {esiid && (
        <Panel title="Accounts on this ESIID">
          <ErrorBox error={q.error} />
          {q.isFetching && !q.data ? <Skeleton rows={3} cols={6} /> : q.data && <CustomerTable rows={q.data} />}
        </Panel>
      )}
    </>
  );
}
