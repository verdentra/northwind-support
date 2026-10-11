import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiError, toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';

interface LoginLocationState {
  from?: unknown;
}

export function LoginPage() {
  const { session, signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string>();

  if (session !== null) {
    return <Navigate to={getReturnPath(location.state)} replace />;
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(undefined);
    setIsPending(true);

    try {
      await signIn({ email: email.trim(), password });
      navigate(getReturnPath(location.state), { replace: true });
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.status === 401
          ? 'Email or password is incorrect.'
          : toErrorMessage(caught, 'Could not sign in. Please try again.'),
      );
    } finally {
      setIsPending(false);
    }
  };

  return (
    <section className="login-page" aria-labelledby="login-heading">
      <form className="card form login-form" onSubmit={(event) => void handleSubmit(event)}>
        <h1 id="login-heading">Sign in</h1>
        <p className="page-header__subtitle">Sign in with your support agent account.</p>

        <div className="field">
          <label className="field__label" htmlFor="login-email">Email</label>
          <input
            id="login-email"
            name="email"
            type="email"
            autoComplete="username"
            required
            maxLength={256}
            value={email}
            disabled={isPending}
            aria-describedby={error ? 'login-error' : undefined}
            onChange={(event) => {
              setEmail(event.target.value);
              setError(undefined);
            }}
          />
        </div>

        <div className="field">
          <label className="field__label" htmlFor="login-password">Password</label>
          <input
            id="login-password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            minLength={1}
            maxLength={256}
            value={password}
            disabled={isPending}
            aria-describedby={error ? 'login-error' : undefined}
            onChange={(event) => {
              setPassword(event.target.value);
              setError(undefined);
            }}
          />
        </div>

        {error && (
          <p id="login-error" className="field__error" role="alert">
            {error}
          </p>
        )}

        <button className="button button--primary" type="submit" disabled={isPending}>
          {isPending ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </section>
  );
}

function getReturnPath(state: unknown): string {
  if (typeof state !== 'object' || state === null || !('from' in state)) {
    return '/tickets';
  }

  const from = (state as LoginLocationState).from;

  if (
    typeof from !== 'string' ||
    !from.startsWith('/') ||
    from.startsWith('//') ||
    from === '/login'
  ) {
    return '/tickets';
  }

  return from;
}
