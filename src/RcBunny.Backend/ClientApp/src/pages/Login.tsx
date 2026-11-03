import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api, ApiError, post, queryClient } from '../api';
import { ErrorBox } from '../ui';

interface TestLogin {
  id: number;
  name: string;
  email: string;
  role: string;
  company: string | null;
}

export function Login() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  // Development only: the server answers 404 elsewhere and the list stays hidden.
  const tests = useQuery({
    queryKey: ['test-logins'],
    queryFn: () => api<TestLogin[]>('/api/auth/test-logins'),
    retry: false,
    staleTime: Infinity,
  });

  const done = () => {
    queryClient.clear(); // never show one person's cached data to the next
    const next = params.get('next');
    navigate(next && next.startsWith('/') && !next.startsWith('//') ? next : '/', { replace: true });
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await post('/api/auth/login', { email, password });
      done();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  };

  const testLogin = async (id: number) => {
    try {
      await post(`/api/auth/test-logins/${id}`);
      done();
    } catch (err) {
      setError(err);
    }
  };

  return (
    <div className="login">
      <form className="panel login-card" onSubmit={submit}>
        <span className="logo-mark big" aria-hidden="true">RC</span>
        <h1>Admin sign in</h1>
        <ErrorBox error={error instanceof ApiError ? error : null} />
        <label>
          Email
          <input type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
        </label>
        <label>
          Password
          <input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        <button className="btn block" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>

      {tests.data && (
        <div className="panel login-card tests">
          <h2>Sample sign-ins</h2>
          <p className="muted small">Development only. Each has different access, so you can see what they see.</p>
          <ul>
            {tests.data.map((u) => (
              <li key={u.id}>
                <button className="link" onClick={() => void testLogin(u.id)}>{u.name}</button>
                <span className="muted small"> {u.role}{u.company ? ` · ${u.company}` : ''}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
