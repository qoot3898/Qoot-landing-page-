import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Navigate, Outlet, useLocation, useNavigate, useOutletContext, useSearchParams } from 'react-router-dom';
import { api, ApiError, resetAntiforgery } from '../shared/api';
import { keys } from './data';
import { useI18n } from './i18n';
import type { Me, Role } from './types';
import { errorText, Field, TextInput } from './ui/controls';
import { Icon, QootMark, Wordmark } from './ui/icons';

export interface AuthContext { me: Me }

/** Every dashboard route sits behind this: no session, no screen (the API enforces the same on every call). */
export function AuthGate() {
  const location = useLocation();
  const { t, setLang } = useI18n();
  const me = useQuery({ queryKey: keys.me, queryFn: () => api<Me>('/api/auth/me'), retry: false, staleTime: 5 * 60_000, meta: { quiet401: true } });

  useEffect(() => {
    // The interface follows the user's saved language the first time it loads on this device.
    if (me.data) {
      try {
        if (!localStorage.getItem('qoot.admin.lang')) setLang(me.data.preferredLanguage);
      } catch {
        setLang(me.data.preferredLanguage);
      }
    }
  }, [me.data, setLang]);

  if (me.isPending) {
    return (
      <div className="boot" role="status">
        <QootMark size={36} />
        <span>{t.common.loading}</span>
      </div>
    );
  }
  if (me.error instanceof ApiError && me.error.status === 401) {
    return <Navigate to={`/login?next=${encodeURIComponent(location.pathname + location.search)}`} replace />;
  }
  if (me.error || !me.data) {
    return (
      <div className="boot" role="alert">
        <QootMark size={36} />
        <p>{errorText(me.error, t)}</p>
        <button type="button" className="btn" onClick={() => void me.refetch()}>{t.common.retry}</button>
      </div>
    );
  }
  return <Outlet context={{ me: me.data } satisfies AuthContext} />;
}

export function useCurrentUser(): Me {
  return useOutletContext<AuthContext>().me;
}

export function RoleGate({ role, children }: { role: Role; children: ReactNode }) {
  const me = useCurrentUser();
  const { t } = useI18n();
  if (role === 'Admin' && me.role !== 'Admin') {
    return (
      <div className="page">
        <div className="notice" role="alert"><Icon name="lock" /> {t.common.noPermission}</div>
      </div>
    );
  }
  return <>{children}</>;
}

export function useSignOut() {
  const client = useQueryClient();
  const navigate = useNavigate();
  return async () => {
    try {
      await api('/api/auth/logout', { method: 'POST' });
    } finally {
      resetAntiforgery();
      client.clear();
      navigate('/login?out=1', { replace: true });
    }
  };
}

/** Only redirect inside the dashboard after signing in (no open redirects). */
function safeNext(next: string | null): string {
  if (!next || !next.startsWith('/admin/') || next.startsWith('//') || next.startsWith('/admin/login')) return '/';
  return next.slice('/admin'.length) || '/';
}

export function LoginPage() {
  const { t, lang, setLang } = useI18n();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const client = useQueryClient();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const next = safeNext(params.get('next'));

  // Already signed in: go straight to the dashboard.
  const me = useQuery({ queryKey: keys.me, queryFn: () => api<Me>('/api/auth/me'), retry: false, meta: { quiet401: true } });
  if (me.data) return <Navigate to={next} replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      resetAntiforgery();
      const user = await api<Me>('/api/auth/login', { method: 'POST', body: { email, password, rememberMe: remember } });
      // The antiforgery token is tied to the signed-in user: get a fresh one for the next requests.
      resetAntiforgery();
      client.setQueryData(keys.me, user);
      navigate(next, { replace: true });
    } catch (err) {
      setError(errorText(err, t));
      setPassword('');
    } finally {
      setBusy(false);
    }
  };

  const notice = params.get('expired') ? t.login.expired : params.get('out') ? t.login.signedOut : null;
  return (
    <main className="login">
      <section className="login-art" aria-hidden="true">
        <Wordmark lang={lang} height={56} />
        <div className="login-window">
          <span className="q-photo" style={{ backgroundImage: 'url(/assets/img/qoot-produce-packing-960.webp)' }} />
          <img className="q-leaf" src="/assets/logo/qoot-q-leaf.svg" width="290" height="355" alt="" />
        </div>
        <div className="login-art-text">
          <h2>{t.login.brandTitle}</h2>
          <p>{t.login.brandText}</p>
        </div>
        <div className="login-band"><span /></div>
      </section>
      <div className="login-main">
        <div className="login-card">
          <div className="login-top">
            <span className="login-product"><QootMark size={22} /><strong>{t.product}</strong> · {t.productSub}</span>
            <button type="button" className="btn btn-sm btn-quiet login-lang" onClick={() => setLang(lang === 'ar' ? 'en' : 'ar')} lang={lang === 'ar' ? 'en' : 'ar'}>
              <Icon name="globe" /> {t.top.language}
            </button>
          </div>
          <h1>{t.login.title}</h1>
          <p className="lead">{t.login.lead}</p>
          {notice && <p className="notice" role="status"><Icon name="info" /> {notice}</p>}
          <form onSubmit={submit} noValidate>
            <Field label={t.login.email} htmlFor="login-email">
              <TextInput id="login-email" type="email" autoComplete="username" dir="ltr" required value={email} onChange={(e) => setEmail(e.target.value)} autoFocus />
            </Field>
            <Field label={t.login.password} htmlFor="login-password">
              <span className="password-wrap">
                <TextInput id="login-password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" dir="ltr" required value={password} onChange={(e) => setPassword(e.target.value)} />
                <button type="button" className="icon-btn" onClick={() => setShowPassword((v) => !v)} aria-label={showPassword ? t.login.hide : t.login.show} aria-pressed={showPassword}>
                  <Icon name={showPassword ? 'eyeOff' : 'eye'} />
                </button>
              </span>
            </Field>
            <label className="check">
              <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
              <span>{t.login.remember}</span>
            </label>
            {error && <p className="form-error" role="alert"><Icon name="alert" /> {error}</p>}
            <button type="submit" className="btn btn-primary btn-block" disabled={busy || !email || !password}>
              {busy ? t.common.loading : t.login.submit}
            </button>
          </form>
        </div>
      </div>
    </main>
  );
}
