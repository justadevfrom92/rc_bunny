import { useState } from 'react';
import { useNavigate } from 'react-router';
import { api, date, useLoad } from '../../api';

interface Message { id: number; name: string; email: string; phone: string | null; topic: string; message: string; createdAt: string }

export default function Messages() {
  const [handled, setHandled] = useState(false);
  const { data, error, reload } = useLoad<Message[]>(`/api/messages?handled=${handled}`);
  const navigate = useNavigate();
  const makeTicket = (m: Message) => api.post<{ id: number }>(`/api/messages/${m.id}/ticket`).then((r) => navigate(`/ranger/tickets/${r.id}`));

  return (
    <>
      <h1>Web Messages</h1>
      <div className="row">
        <button className={handled ? '' : 'primary'} onClick={() => setHandled(false)}>New</button>
        <button className={handled ? 'primary' : ''} onClick={() => setHandled(true)}>Handled</button>
      </div>
      {error && <p className="error">{error}</p>}
      {!data ? <p>Loading…</p> : data.length === 0 ? <p className="muted">Nothing here.</p> : data.map((m) => (
        <div key={m.id} className="card">
          <strong>{m.topic}</strong> · {m.name} · <a href={`mailto:${m.email}`}>{m.email}</a>{m.phone && ` · ${m.phone}`}
          <span className="muted"> · {date(m.createdAt)}</span>
          <p>{m.message}</p>
          {!handled && (
            <div className="row">
              <button className="primary" onClick={() => makeTicket(m)}>Make a ticket</button>
              <button onClick={() => api.post(`/api/messages/${m.id}/handled`).then(reload)}>Mark handled</button>
            </div>
          )}
        </div>
      ))}
    </>
  );
}
