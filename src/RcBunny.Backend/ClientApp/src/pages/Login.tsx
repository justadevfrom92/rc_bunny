import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../api';
import { useAuth, type Me } from '../auth';
import { apps, Brand, initials, websiteUrl } from '../Sidebar';
import { PasswordInput } from '../fields';

interface TestUser { email: string; name: string; role: string; permissions: string[] }

/** Admin sign in, as ebunny's: the navy page, the white card, and the test sign-ins (development only). */
export default function Login() {
  const { setMe } = useAuth();
  const [error, setError] = useState('');
  const [testUsers, setTestUsers] = useState<TestUser[]>([]);

  useEffect(() => { api.get<TestUser[]>('/api/demo-logins').then(setTestUsers, () => {}); }, []);

  const signIn = (email: string, password: string) => {
    setError('');
    // After signing in, the /login route sends you on to the page you were going to (?to=…)
    api.post<Me>('/api/login', { email, password }).then(setMe, (err: Error) => setError(err.message));
  };
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    signIn(String(f.get('email')), String(f.get('password')));
  };

  return (
    <div className="login-wrap">
      <form className="login" onSubmit={submit}>
        <div className="brand"><Brand /></div>
        <div className="field"><label htmlFor="email">Email</label><input id="email" name="email" type="email" autoComplete="username" required autoFocus /></div>
        <div className="field"><label htmlFor="password">Password</label><PasswordInput id="password" name="password" autoComplete="current-password" required /></div>
        <label className="check"><input type="checkbox" name="remember" value="1" /> Keep me signed in</label>
        <div className="err" role="alert">{error}</div>
        <button className="btn cyan">Sign In</button>
        <a href={websiteUrl} className="muted" style={{ textAlign: 'center', fontSize: '.85rem' }}>← Back to website</a>
      </form>
      {testUsers.length > 0 && (
        <div className="login test-logins">
          <h2>Test Sign-Ins</h2>
          <p className="muted">For testing only (never shown in production): sign in as any sample admin to see what their role can do.</p>
          {testUsers.map((u) => (
            <button key={u.email} type="button" className="test-user" onClick={() => signIn(u.email, 'password')}>
              <span className="avatar">{initials(u.name)}</span>
              <span><b>{u.name}</b><small>{u.role} · {Object.entries(apps).filter(([k, a]) => u.permissions.includes(a.perm ?? k)).map(([, a]) => a.name).join(', ') || 'no apps'}</small></span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
