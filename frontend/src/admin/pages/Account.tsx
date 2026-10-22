import { useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, ApiError } from '../../shared/api';
import { useCurrentUser } from '../auth';
import { keys } from '../data';
import { useI18n } from '../i18n';
import type { Lang, Me } from '../types';
import { errorText, Field, PageHead, Select, TextInput } from '../ui/controls';
import { useToast } from '../ui/feedback';

/** The signed-in user's own name, dashboard language and password. */
export function AccountPage() {
  const { t, setLang } = useI18n();
  const me = useCurrentUser();
  const client = useQueryClient();
  const toast = useToast();
  const [name, setName] = useState(me.displayName);
  const [language, setLanguage] = useState<Lang>(me.preferredLanguage);
  const [pw, setPw] = useState({ current: '', next: '', confirm: '' });
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState<null | 'profile' | 'password'>(null);

  const saveProfile = async (e: FormEvent) => {
    e.preventDefault();
    setBusy('profile');
    try {
      const fresh = await api<Me>('/api/auth/profile', { method: 'PUT', body: { displayName: name, preferredLanguage: language } });
      client.setQueryData(keys.me, fresh);
      setLang(fresh.preferredLanguage);
      toast.show(t.account.saved);
    } catch (err) {
      if (err instanceof ApiError) setErrors(err.errors);
      toast.show(errorText(err, t), 'error');
    } finally {
      setBusy(null);
    }
  };

  const changePassword = async (e: FormEvent) => {
    e.preventDefault();
    if (pw.next !== pw.confirm) {
      setErrors({ confirm: [t.account.mismatch] });
      return;
    }
    setBusy('password');
    try {
      await api('/api/auth/change-password', { method: 'POST', body: { currentPassword: pw.current, newPassword: pw.next } });
      setPw({ current: '', next: '', confirm: '' });
      setErrors({});
      toast.show(t.account.changed);
    } catch (err) {
      if (err instanceof ApiError) setErrors(err.errors);
      toast.show(errorText(err, t), 'error');
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="page settings">
      <PageHead title={t.account.title} lead={<span dir="ltr">{me.email}</span>} />
      <section className="settings-row" aria-labelledby="acc-profile">
        <div className="settings-label"><h2 id="acc-profile">{t.account.profile}</h2></div>
        <form className="settings-panel" onSubmit={saveProfile} noValidate>
          <div className="fields">
            <Field label={t.account.name} htmlFor="acc-name" error={errors.displayName?.[0]}>
              <TextInput id="acc-name" dir="auto" value={name} onChange={(e) => setName(e.target.value)} autoComplete="name" />
            </Field>
            <Field label={t.account.language} htmlFor="acc-lang">
              <Select id="acc-lang" value={language} onChange={(v) => setLanguage(v as Lang)} options={[{ value: 'ar', label: 'العربية' }, { value: 'en', label: 'English' }]} />
            </Field>
          </div>
          <div className="panel-foot">
            <button type="submit" className="btn btn-primary" disabled={busy !== null || (name === me.displayName && language === me.preferredLanguage)}>{busy === 'profile' ? t.common.saving : t.common.save}</button>
          </div>
        </form>
      </section>
      <section className="settings-row" aria-labelledby="acc-password">
        <div className="settings-label">
          <h2 id="acc-password">{t.account.password}</h2>
          <p className="muted">{t.account.passwordHelp}</p>
        </div>
        <form className="settings-panel" onSubmit={changePassword} noValidate>
          <div className="fields">
            <input type="text" name="username" autoComplete="username" value={me.email} readOnly hidden />
            <Field label={t.account.current} htmlFor="pw-current" error={errors.currentPassword?.[0]}>
              <TextInput id="pw-current" type="password" dir="ltr" autoComplete="current-password" value={pw.current} onChange={(e) => setPw({ ...pw, current: e.target.value })} />
            </Field>
            <Field label={t.account.newPassword} htmlFor="pw-new" error={errors.newPassword?.[0]}>
              <TextInput id="pw-new" type="password" dir="ltr" autoComplete="new-password" value={pw.next} onChange={(e) => setPw({ ...pw, next: e.target.value })} />
            </Field>
            <Field label={t.account.confirm} htmlFor="pw-confirm" error={errors.confirm?.[0]}>
              <TextInput id="pw-confirm" type="password" dir="ltr" autoComplete="new-password" value={pw.confirm} onChange={(e) => setPw({ ...pw, confirm: e.target.value })} />
            </Field>
          </div>
          <div className="panel-foot">
            <button type="submit" className="btn btn-primary" disabled={busy !== null || !pw.current || !pw.next}>{busy === 'password' ? t.common.saving : t.account.change}</button>
          </div>
        </form>
      </section>
    </div>
  );
}
