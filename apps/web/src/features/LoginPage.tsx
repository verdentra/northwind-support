
import { useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { login, saveAuth } from '../api/auth';

export function LoginPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setError('');

    try {
      const result = await login(email, password);
      saveAuth(result);

      const requested = params.get('returnTo');
      const destination =
        requested?.startsWith('/') && !requested.startsWith('//')
          ? requested
          : '/tickets';

      navigate(destination, { replace: true });
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Login failed.'
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="app__main">
      <h1>Sign in</h1>
      <p>Sign in with your Northwind Support Desk agent account.</p>

      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="login-email">Email</label>
          <input
            id="login-email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            autoComplete="username"
          />
        </div>

        <div>
          <label htmlFor="login-password">Password</label>
          <input
            id="login-password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            autoComplete="current-password"
          />
        </div>

        {error && <p role="alert">{error}</p>}

        <button type="submit" disabled={pending}>
          {pending ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </main>
  );
}
