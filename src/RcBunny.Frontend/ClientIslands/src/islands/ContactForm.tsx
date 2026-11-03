import { useState, type FormEvent } from 'react';

interface Props {
  topics: { value: string; label: string }[];
  topic: string | null;
  message: string | null;
}

/** Contact Us: validates in the browser and sends in the background, so the page never reloads. */
export default function ContactForm({ topics, topic, message }: Props) {
  const [status, setStatus] = useState<{ tone: 'ok' | 'error'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget;
    if (!form.checkValidity()) {
      form.reportValidity();
      setStatus({ tone: 'error', text: 'Please fill in the required fields.' });
      return;
    }
    setBusy(true);
    setStatus(null);
    try {
      const res = await fetch('/contact-us', { method: 'POST', body: new FormData(form), headers: { Accept: 'application/json' } });
      if (res.ok) {
        form.reset();
        setStatus({ tone: 'ok', text: "Thanks! We got your message and will be in touch soon." });
      } else {
        const body = await res.json().catch(() => null);
        setStatus({ tone: 'error', text: res.status === 429 ? 'Too many messages. Please wait a minute.' : body?.message ?? 'Something went wrong. Please call us.' });
      }
    } catch {
      setStatus({ tone: 'error', text: "Couldn't send. Check your connection and try again." });
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="byop-panel" onSubmit={submit} noValidate>
      <h2>Send us a message</h2>
      <div className="form-2">
        <div className="field"><label htmlFor="fn">First Name *</label><input id="fn" name="Form.First" required maxLength={100} /></div>
        <div className="field"><label htmlFor="ln">Last Name *</label><input id="ln" name="Form.Last" required maxLength={100} /></div>
      </div>
      <div className="form-2" style={{ marginTop: 16 }}>
        <div className="field"><label htmlFor="em">Email *</label><input id="em" name="Form.Email" type="email" required maxLength={200} /></div>
        <div className="field"><label htmlFor="ph">Phone</label><input id="ph" name="Form.Phone" type="tel" maxLength={40} /></div>
      </div>
      <div className="field" style={{ marginTop: 16 }}>
        <label htmlFor="topic">Topic</label>
        <select id="topic" name="Form.Topic" defaultValue={topic ?? 'general'}>
          {topics.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
        </select>
      </div>
      <div className="field" style={{ marginTop: 16 }}>
        <label htmlFor="msg">Message *</label>
        <textarea id="msg" name="Form.Message" required maxLength={5000} defaultValue={message ?? ''} />
      </div>
      <button className="btn" type="submit" style={{ marginTop: 20 }} disabled={busy}>{busy ? 'Sending…' : 'Send Message'}</button>
      <div className={`form-msg ${status?.tone ?? ''}`} role="status">{status?.text}</div>
    </form>
  );
}
