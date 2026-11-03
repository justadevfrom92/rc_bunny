import { useEffect, useRef, useState, type FormEvent } from 'react';

type Line = { who: string; text: string };
type State = { status: string; lines: Line[] };

const post = (url: string, body?: unknown) =>
  fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-RCB': '1' }, body: JSON.stringify(body ?? {}) }).then((r) => {
    if (!r.ok) throw new Error(r.status === 429 ? 'Slow down a little and try again.' : 'Something went wrong.');
    return r.json() as Promise<State>;
  });

/** My Account chat: the virtual assistant answers until a person takes over. Opens on demand; polls only while open. */
export default function ChatBubble() {
  const [open, setOpen] = useState(false);
  const [state, setState] = useState<State>({ status: 'none', lines: [] });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const end = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const load = () => fetch('/myaccount/chat').then((r) => r.json()).then(setState).catch(() => {});
    load();
    const t = window.setInterval(load, 8000);
    return () => window.clearInterval(t);
  }, [open]);
  useEffect(() => { end.current?.scrollIntoView({ block: 'end' }); }, [state.lines.length]);

  const send = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const input = e.currentTarget.elements.namedItem('message') as HTMLInputElement;
    const message = input.value.trim();
    if (!message) return;
    setBusy(true);
    setError(null);
    setState((s) => ({ ...s, lines: [...s.lines, { who: 'Customer', text: message }] }));
    input.value = '';
    try { setState(await post('/myaccount/chat', { message })); } catch (err) { setError((err as Error).message); } finally { setBusy(false); }
  };

  if (!open) return <button className="chat-fab" onClick={() => setOpen(true)} aria-label="Chat with us">💬 Chat</button>;
  return (
    <div className="chat-panel" role="dialog" aria-label="Chat">
      <div className="chat-top">
        <strong>{state.status === 'waiting' ? 'Getting a person…' : state.status === 'human' ? 'Chatting with our team' : 'Virtual assistant'}</strong>
        <button onClick={() => setOpen(false)} aria-label="Close chat">×</button>
      </div>
      <div className="chat-lines" aria-live="polite">
        {state.lines.length === 0 && <p className="chat-line">Ask about your bill, payments, plan, rewards or moving.</p>}
        {state.lines.map((l, i) => (
          <div key={i} className={`chat-line ${l.who === 'Customer' ? 'me' : ''}`}><small>{l.who === 'Customer' ? 'You' : l.who}</small><p>{l.text}</p></div>
        ))}
        <div ref={end} />
      </div>
      {error && <div className="form-msg error">{error}</div>}
      <form className="chat-form" onSubmit={send}>
        <input name="message" maxLength={1000} placeholder="Type a message" autoComplete="off" disabled={busy} />
        <button className="btn btn-sm" disabled={busy}>Send</button>
      </form>
      {state.status === 'bot' && (
        <button className="chat-person" onClick={() => post('/myaccount/chat/person').then(setState).catch((e) => setError(e.message))}>Talk to a person</button>
      )}
    </div>
  );
}
