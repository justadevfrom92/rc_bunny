import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, post } from '../../api';
import { dateTime, ErrorBox, Panel, Skeleton } from '../../ui';

interface Message {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  topic: string;
  message: string;
  createdAt: string;
  handledAt: string | null;
}

/** The website's Contact Us inbox. New messages show up within 30 s (polling while the page is open). */
export default function WebMessages() {
  const [handled, setHandled] = useState(false);
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ['ranger', 'messages', handled],
    queryFn: () => api<Message[]>(`/api/ranger/messages?handled=${handled}`),
    refetchInterval: 30_000,
  });
  const done = useMutation({
    mutationFn: (id: number) => post(`/api/ranger/messages/${id}/handled`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ranger', 'messages'] }),
  });
  return (
    <>
      <h1>Web Messages</h1>
      <div className="tabs">
        <button className={handled ? '' : 'on'} onClick={() => setHandled(false)}>Open</button>
        <button className={handled ? 'on' : ''} onClick={() => setHandled(true)}>Handled</button>
      </div>
      <Panel>
        <ErrorBox error={q.error ?? done.error} />
        {q.isPending ? <Skeleton rows={6} /> : q.data?.length === 0 ? <p className="muted">No messages.</p> : (
          <ul className="notes">
            {q.data?.map((m) => (
              <li key={m.id}>
                <div className="muted small">
                  {dateTime(m.createdAt)} · {m.firstName} {m.lastName} · <a href={`mailto:${m.email}`}>{m.email}</a>{m.phone && ` · ${m.phone}`} · {m.topic}
                </div>
                <p>{m.message}</p>
                {!m.handledAt && <button className="btn outline small" onClick={() => done.mutate(m.id)}>Mark handled</button>}
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </>
  );
}
