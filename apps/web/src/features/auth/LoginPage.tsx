import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiError, toErrorMessage } from '../../api/client';
import { Field } from '../../components/Field';
import type { LoginLocationState } from './AuthProvider';
import { useAuth } from './useAuth';

interface FormState {
  email: string;
  password: string;
}

type FormErrors = Partial<Record<keyof FormState, string>>;

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function validate(form: FormState): FormErrors {
  const errors: FormErrors = {};

  if (!emailPattern.test(form.email.trim())) {
    errors.email = 'Enter your work email address.';
  }

  if (form.password.length === 0) {
    errors.password = 'Enter your password.';
  }

  return errors;
}

/** Where to return after signing in: the page the agent was sent from, never back to /login. */
function returnPath(state: unknown): string {
  const from = (state as LoginLocationState | null)?.from;

  return from && from.startsWith('/') && !from.startsWith('/login') ? from : '/tickets';
}

export function LoginPage() {
  const { agent, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [form, setForm] = useState<FormState>({ email: '', password: '' });
  const [errors, setErrors] = useState<FormErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  const target = returnPath(location.state);

  if (agent) {
    return <Navigate to={target} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      await login(form.email.trim(), form.password);
      void navigate(target, { replace: true });
    } catch (caught) {
      setSubmitError(
        caught instanceof ApiError && caught.status === 401
          ? 'Email or password is incorrect.'
          : toErrorMessage(caught, 'Could not sign in.'),
      );
      setForm((current) => ({ ...current, password: '' }));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="login">
      <form className="card form login__card" onSubmit={submit} noValidate aria-labelledby="login-heading">
        <h1 id="login-heading">Sign in</h1>
        <p className="page-header__subtitle">Northwind Support Desk - agents only.</p>

        <Field id="login-email" label="Email" error={errors.email}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="email"
              autoComplete="username"
              value={form.email}
              disabled={isSubmitting}
              onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="password"
              autoComplete="current-password"
              value={form.password}
              disabled={isSubmitting}
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
            />
          )}
        </Field>

        {submitError && (
          <p className="field__error" role="alert">
            {submitError}
          </p>
        )}

        <div className="button-row">
          <button type="submit" className="button button--primary" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </div>
      </form>
    </main>
  );
}
