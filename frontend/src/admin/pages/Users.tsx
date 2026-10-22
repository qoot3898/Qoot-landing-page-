import { useState, type FormEvent } from 'react';
import { api, ApiError } from '../../shared/api';
import { useCurrentUser } from '../auth';
import { keys, useApiMutation, useUsers } from '../data';
import { useI18n } from '../i18n';
import type { Role, User } from '../types';
import { ErrorBlock, errorText, Field, PageHead, Pill, Select, SkeletonRows, TextInput } from '../ui/controls';
import { Dialog, useConfirm, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';

type Mode = { kind: 'add' } | { kind: 'edit'; user: User } | { kind: 'password'; user: User };

/** Who can sign in. Accounts are only ever created here, by an administrator; the API enforces the same. */
export function UsersPage() {
  const { t, relative } = useI18n();
  const me = useCurrentUser();
  const users = useUsers();
  const toast = useToast();
  const confirm = useConfirm();
  const [mode, setMode] = useState<Mode | null>(null);
  const [form, setForm] = useState({ email: '', displayName: '', role: 'Editor' as Role, password: '' });
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  const mutate = useApiMutation((fn: () => Promise<unknown>) => fn(), () => [keys.users]);
  const run = (fn: () => Promise<unknown>, done: string, close = false) =>
    mutate.mutate(fn, {
      onSuccess: () => {
        toast.show(done);
        if (close) setMode(null);
        setErrors({});
      },
      onError: (e) => {
        if (e instanceof ApiError) setErrors(e.errors);
        toast.show(errorText(e, t), 'error');
      },
    });

  const open = (m: Mode) => {
    setErrors({});
    setMode(m);
    if (m.kind === 'add') setForm({ email: '', displayName: '', role: 'Editor', password: '' });
    if (m.kind === 'edit') setForm({ email: m.user.email, displayName: m.user.displayName, role: m.user.role, password: '' });
    if (m.kind === 'password') setForm((f) => ({ ...f, password: '' }));
  };

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!mode) return;
    if (mode.kind === 'add') run(() => api('/api/admin/users', { method: 'POST', body: form }), t.users.created, true);
    if (mode.kind === 'edit') run(() => api(`/api/admin/users/${mode.user.id}`, { method: 'PUT', body: { displayName: form.displayName, role: form.role, isActive: mode.user.isActive } }), t.users.saved, true);
    if (mode.kind === 'password') run(() => api(`/api/admin/users/${mode.user.id}/reset-password`, { method: 'POST', body: { newPassword: form.password } }), t.users.passwordSet, true);
  };

  const roleOptions = (['Admin', 'Editor'] as Role[]).map((r) => ({ value: r, label: t.users.roles[r] }));

  return (
    <div className="page">
      <PageHead title={t.users.title} lead={t.users.lead} actions={<button type="button" className="btn btn-primary" onClick={() => open({ kind: 'add' })}>{t.users.add}</button>} />
      {users.isPending ? (
        <SkeletonRows rows={4} />
      ) : users.error ? (
        <ErrorBlock error={users.error} onRetry={() => void users.refetch()} />
      ) : (
        <>
          <p className="muted num table-count">{t.users.count(users.data!.length)}</p>
          <table className="table">
            <thead>
              <tr>
                <th scope="col" className="col-id">{t.users.name}</th>
                <th scope="col">{t.users.role}</th>
                <th scope="col">{t.messages.status}</th>
                <th scope="col" className="num hide-tablet">{t.users.lastLogin}</th>
                <th scope="col"><span className="sr-only">{t.common.more}</span></th>
              </tr>
            </thead>
            <tbody>
              {users.data!.map((u) => {
                const self = u.id === me.id;
                return (
                  <tr key={u.id}>
                    <td className="col-id">
                      <span className="cell-text">
                        <span className="cell-title">{u.displayName || u.email}{self && <span className="muted"> · {t.users.you}</span>}</span>
                        <span className="cell-sub" dir="ltr">{u.email}</span>
                      </span>
                    </td>
                    <td>{t.users.roles[u.role]}</td>
                    <td>
                      {u.isLockedOut ? <Pill kind="bad" icon="lock">{t.status.locked}</Pill> : u.isActive ? <Pill kind="ok">{t.status.active}</Pill> : <Pill kind="neutral">{t.status.inactive}</Pill>}
                    </td>
                    <td className="num hide-tablet">{u.lastLoginAt ? relative(u.lastLoginAt) : <span className="muted">{t.users.never}</span>}</td>
                    <td className="row-actions">
                      <button type="button" className="btn btn-sm" onClick={() => open({ kind: 'edit', user: u })}>{t.common.edit}</button>
                      <details className="menu">
                        <summary className="icon-btn" aria-label={`${t.common.more}: ${u.displayName || u.email}`}><Icon name="kebab" /></summary>
                        <div className="menu-list">
                          <button type="button" onClick={() => open({ kind: 'password', user: u })}>{t.users.resetPassword}</button>
                          {u.isLockedOut && <button type="button" onClick={() => run(() => api(`/api/admin/users/${u.id}/unlock`, { method: 'POST' }), t.users.unlocked)}>{t.users.unlock}</button>}
                          {!self && (
                            <button type="button" onClick={() => run(() => api(`/api/admin/users/${u.id}`, { method: 'PUT', body: { displayName: u.displayName, role: u.role, isActive: !u.isActive } }), t.users.saved)}>
                              {u.isActive ? t.users.deactivate : t.users.activate}
                            </button>
                          )}
                          {!self && (
                            <button
                              type="button"
                              className="danger"
                              onClick={async () => {
                                if (await confirm({ title: t.users.deleteConfirm(u.displayName || u.email), confirm: t.common.delete, danger: true })) {
                                  run(() => api(`/api/admin/users/${u.id}`, { method: 'DELETE' }), t.users.deleted);
                                }
                              }}
                            >
                              {t.common.delete}
                            </button>
                          )}
                        </div>
                      </details>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <dl className="role-help">
            {(['Admin', 'Editor'] as Role[]).map((r) => (
              <div key={r}><dt>{t.users.roles[r]}</dt><dd className="muted">{t.users.roleHelp[r]}</dd></div>
            ))}
          </dl>
        </>
      )}

      <Dialog
        open={mode !== null}
        onClose={() => setMode(null)}
        title={mode?.kind === 'add' ? t.users.add : mode?.kind === 'password' ? `${t.users.resetPassword}: ${mode.user.displayName || mode.user.email}` : t.common.edit}
        footer={
          <>
            <button type="button" className="btn" onClick={() => setMode(null)}>{t.common.cancel}</button>
            <button type="submit" form="user-form" className="btn btn-primary" disabled={mutate.isPending}>{mutate.isPending ? t.common.saving : t.common.save}</button>
          </>
        }
      >
        {mode && (
          <form id="user-form" onSubmit={submit} noValidate>
            {mode.kind !== 'password' && (
              <>
                <Field label={t.users.email} htmlFor="u-email" error={errors.email?.[0]}>
                  <TextInput id="u-email" type="email" dir="ltr" autoComplete="off" value={form.email} disabled={mode.kind === 'edit'} onChange={(e) => setForm({ ...form, email: e.target.value })} />
                </Field>
                <Field label={t.users.name} htmlFor="u-name" error={errors.displayName?.[0]}>
                  <TextInput id="u-name" dir="auto" value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
                </Field>
                <Field label={t.users.role} htmlFor="u-role" help={t.users.roleHelp[form.role]} error={errors.role?.[0]}>
                  <Select id="u-role" value={form.role} onChange={(v) => setForm({ ...form, role: v as Role })} options={roleOptions} disabled={mode.kind === 'edit' && mode.user.id === me.id} />
                </Field>
              </>
            )}
            {(mode.kind === 'add' || mode.kind === 'password') && (
              <Field label={t.users.password} htmlFor="u-pass" help={t.users.passwordHelp} error={errors.password?.[0] ?? errors.newPassword?.[0]}>
                <TextInput id="u-pass" type="password" dir="ltr" autoComplete="new-password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
              </Field>
            )}
          </form>
        )}
      </Dialog>
    </div>
  );
}
