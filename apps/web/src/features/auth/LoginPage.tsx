import { useState, type FormEvent } from 'react';
import {
  useLocation,
  useNavigate,
} from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import type { LoginRequest } from '../../types/api';
import { useAuth } from './useAuth';

interface LoginLocationState {
  from?: {
    pathname: string;
    search: string;
    hash: string;
  };
}

interface LoginFieldErrors {
  email?: string;
  password?: string;
}

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] =
    useState<LoginFieldErrors>({});
  const [errorMessage, setErrorMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  function validate(): LoginFieldErrors {
    const errors: LoginFieldErrors = {};

    if (!email.trim()) {
      errors.email = 'Email is required.';
    } else if (!email.includes('@')) {
      errors.email = 'Enter a valid email address.';
    }

    if (!password) {
      errors.password = 'Password is required.';
    }

    return errors;
  }

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>,
  ): Promise<void> {
    event.preventDefault();

    setErrorMessage('');

    const errors = validate();
    setFieldErrors(errors);

    if (Object.keys(errors).length > 0) {
      return;
    }

    const credentials: LoginRequest = {
      email: email.trim(),
      password,
    };

    setIsSubmitting(true);

    try {
      await login(credentials);

      const state =
        location.state as LoginLocationState | null;

      const destination = state?.from;

      if (destination) {
        navigate(
          `${destination.pathname}${destination.search}${destination.hash}`,
          { replace: true },
        );
      } else {
        navigate('/tickets', { replace: true });
      }
    } catch (error: unknown) {
      setErrorMessage(
        toErrorMessage(
          error,
          'Unable to sign in. Please check your credentials.',
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="login-page">
      <section
        className="login-page__panel card"
        aria-labelledby="login-heading"
      >
        <h1 id="login-heading">Sign in</h1>

        <p className="login-page__description">
          Sign in to access the Northwind Support Desk.
        </p>

        <form onSubmit={handleSubmit} noValidate>
          <div className="field">
            <label className="field__label" htmlFor="email">
              Email address
            </label>

            <input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
              }}
              aria-invalid={Boolean(fieldErrors.email)}
              aria-describedby={
                fieldErrors.email
                  ? 'email-error'
                  : undefined
              }
            />

            {fieldErrors.email && (
              <p id="email-error" className="field__error" role="alert">
                {fieldErrors.email}
              </p>
            )}
          </div>

          <div className="field">
            <label className="field__label" htmlFor="password">
              Password
            </label>

            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => {
                setPassword(event.target.value);
              }}
              aria-invalid={Boolean(fieldErrors.password)}
              aria-describedby={
                fieldErrors.password
                  ? 'password-error'
                  : undefined
              }
            />

            {fieldErrors.password && (
              <p id="password-error" className="field__error" role="alert">
                {fieldErrors.password}
              </p>
            )}
          </div>

          {errorMessage && (
            <p className="error" role="alert">
              {errorMessage}
            </p>
          )}

          <button
            className="button button--primary login-page__submit"
            type="submit"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
