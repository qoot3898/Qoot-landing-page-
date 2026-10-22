import { useEffect, useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, ApiError } from '../../shared/api';
import { useCurrentUser } from '../auth';
import { SchemaFields, type FormContext } from '../form/SchemaForm';
import { useUnsavedGuard } from '../form/useUnsavedGuard';
import { keys, useSchemas, useSections, useSettings } from '../data';
import { useI18n } from '../i18n';
import type { JsonObject, SettingsDetail } from '../types';
import { ErrorBlock, errorText, PageHead, SkeletonRows } from '../ui/controls';
import { useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';

/** Site-wide settings, one row per area: what it is on the left, its fields on the right. One save for the page. */
export function SettingsPage() {
  const { t, pick, date } = useI18n();
  const me = useCurrentUser();
  const schemas = useSchemas();
  const settings = useSettings();
  const sections = useSections();
  const client = useQueryClient();
  const toast = useToast();
  const [data, setData] = useState<JsonObject | null>(null);
  const [baseline, setBaseline] = useState('');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState(false);
  const [conflict, setConflict] = useState(false);
  const canEdit = me.role === 'Admin';

  useEffect(() => {
    if (settings.data && data === null) {
      setData(settings.data.data);
      setBaseline(JSON.stringify(settings.data.data));
    }
  }, [settings.data, data]);

  const dirty = data !== null && JSON.stringify(data) !== baseline;
  useUnsavedGuard(dirty);
  const missing = useMemo(() => new Set(settings.data?.missingTranslations ?? []), [settings.data]);
  const anchors = useMemo(() => (sections.data ?? []).filter((s) => s.anchor).map((s) => `#${s.anchor}`), [sections.data]);

  if (schemas.isPending || settings.isPending) return <div className="page"><SkeletonRows rows={8} /></div>;
  if (schemas.error || settings.error) return <div className="page"><ErrorBlock error={schemas.error ?? settings.error} onRetry={() => { void schemas.refetch(); void settings.refetch(); }} /></div>;

  const schema = schemas.data!.settings;
  const ctx: FormContext = { errors, missing, anchors, disabled: !canEdit };

  const save = async () => {
    if (!data) return;
    setBusy(true);
    try {
      const fresh = await api<SettingsDetail>('/api/admin/settings', { method: 'PUT', body: { data, concurrencyStamp: settings.data!.concurrencyStamp } });
      client.setQueryData(keys.settings, fresh);
      setData(fresh.data);
      setBaseline(JSON.stringify(fresh.data));
      setErrors({});
      toast.show(t.settings.saved);
    } catch (e) {
      if (e instanceof ApiError && e.code === 'concurrency_conflict') setConflict(true);
      if (e instanceof ApiError) setErrors(e.errors);
      toast.show(errorText(e, t), 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="page settings">
      <PageHead
        title={t.settings.title}
        lead={<>{t.settings.lead} <span className="num">{t.common.updated(date(settings.data!.updatedAt, true))}</span></>}
        actions={canEdit ? <button type="button" className="btn btn-primary" disabled={!dirty || busy} onClick={() => void save()}>{busy ? t.common.saving : t.common.save}</button> : undefined}
      />
      {!canEdit && <p className="notice"><Icon name="lock" /> {t.settings.readOnly}</p>}
      {conflict && (
        <div className="notice notice-bad" role="alert">
          <Icon name="alert" /> {t.common.conflict}
          <button type="button" className="link" onClick={() => { setData(null); setConflict(false); void settings.refetch(); }}>{t.common.reload}</button>
        </div>
      )}
      {data && (
        <form onSubmit={(e) => { e.preventDefault(); void save(); }} noValidate>
          {schema.groups.map((g) => (
            <section key={g.key} className="settings-row" aria-labelledby={`set-${g.key}`}>
              <div className="settings-label">
                <h2 id={`set-${g.key}`}>{pick(g.label)}</h2>
                {t.settings.groups[g.key] && <p className="muted">{t.settings.groups[g.key]}</p>}
              </div>
              <div className="settings-panel">
                <SchemaFields fields={schema.fields} value={data} onChange={setData} path="" ctx={ctx} group={g.key} />
              </div>
            </section>
          ))}
          {canEdit && (
            <div className="settings-foot">
              <button type="submit" className="btn btn-primary" disabled={!dirty || busy}>{busy ? t.common.saving : t.common.save}</button>
            </div>
          )}
        </form>
      )}
    </div>
  );
}
