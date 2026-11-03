import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../api';
import { useAuth, type Me } from '../auth';

export default function Login() {
  const { setMe } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [samples, setSamples] = useState<string[]>([]);

  useEffect(() => { api.get<string[]>('/api/demo-logins').then(setSamples, () => {}); }, []);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    api.post<Me>('/api/login', { email, password }).then(setMe, (err: Error) => setError(err.message));
  };

  return (
    <form className="login card" onSubmit={submit}>
      <h1>RC Bunny Admin</h1>
      <label>Email<input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required autoFocus /></label>
      <label>Password<input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required /></label>
      {error && <p className="error">{error}</p>}
      <button className="primary">Sign in</button>
      {samples.length > 0 && (
        <div className="samples">
          <p>Sample sign-ins (password is <code>password</code>):</p>
          {samples.map((s) => <button type="button" key={s} onClick={() => { setEmail(s); setPassword('password'); }}>{s}</button>)}
        </div>
      )}
    </form>
  );
}
