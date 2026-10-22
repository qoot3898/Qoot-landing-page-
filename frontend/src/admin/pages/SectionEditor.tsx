import { useEffect, useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError } from '../../shared/api';
import { SchemaFields, type FormContext } from '../form/SchemaForm';
import { useUnsavedGuard } from '../form/useUnsavedGuard';
import { keys, useSchemas, useSection, useSections } from '../data';
import { useI18n } from '../i18n';
import type { JsonObject, SectionDetail, SectionSummary } from '../types';
import { ErrorBlock, errorText, Pill, SkeletonRows, Tabs } from '../ui/controls';
import { useConfirm, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';
import { SectionStatusPill } from '../ui/status';
import { NotFoundPage } from './NotFound';

/** One section of the home page: both languages side by side, saved as a draft, then published. */
export function SectionEditorPage() {
  const { key = '' } = useParams();
  const { t, pick, relative } = useI18n();
  const schemas = useSchemas();
  const detail = useSection(key);
  const sections = useSections();
  const client = useQueryClient();
  const toast = useToast();
  const confirm = useConfirm();

  const [draft, setDraft] = useState<JsonObject | null>(null);
  const [baseline, setBaseline] = useState('');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [conflict, setConflict] = useState(false);
  const [busy, setBusy] = useState<null | 'save' | 'publish' | 'discard' | 'visibility'>(null);
  const [group, setGroup] = useState<string | null>(null);
  const [preview, setPreview] = useState<null | 'ar' | 'en'>(null);
  const [previewNonce, setPreviewNonce] = useState(0);

  const schema = schemas.data?.sections.find((s) => s.key === key);
  const dirty = draft !== null && JSON.stringify(draft) !== baseline;
  useUnsavedGuard(dirty);

  // Load the draft into the form (and again after every save, with the server's normalised copy).
  const load = (d: SectionDetail) => {
    setDraft(d.draft);
    setBaseline(JSON.stringify(d.draft));
    setErrors({});
    setConflict(false);
  };
  useEffect(() => {
    if (detail.data && draft === null) {
      setDraft(detail.data.draft);
      setBaseline(JSON.stringify(detail.data.draft));
    }
  }, [detail.data, draft]);
  useEffect(() => {
    setDraft(null);
    setGroup(null);
  }, [key]);
  useEffect(() => {
    if (schema && schema.groups.length && group === null) setGroup(schema.groups[0]!.key);
  }, [schema, group]);

  const anchors = useMemo(
    () => [...(sections.data ?? []).filter((s) => s.anchor && s.isVisible).map((s) => `#${s.anchor}`), '/news/', '/en/'],
    [sections.data],
  );
  const missing = useMemo(() => new Set(detail.data?.missingTranslations ?? []), [detail.data]);

  if (schemas.isPending || detail.isPending) return <div className="page"><SkeletonRows rows={8} /></div>;
  if (detail.error instanceof ApiError && detail.error.status === 404) return <NotFoundPage />;
  if (schemas.error || detail.error) return <div className="page"><ErrorBlock error={schemas.error ?? detail.error} onRetry={() => { void schemas.refetch(); void detail.refetch(); }} /></div>;
  if (!schema) return <NotFoundPage />;

  const d = detail.data!;
  const section = d.section;
  const ctx: FormContext = { errors, missing, anchors };

  const afterChange = (fresh: SectionDetail | SectionSummary) => {
    if ('section' in fresh) {
      client.setQueryData(keys.section(key), fresh);
      load(fresh);
    }
    void client.invalidateQueries({ queryKey: keys.sections });
    void client.invalidateQueries({ queryKey: keys.dashboard });
    setPreviewNonce((n) => n + 1);
  };

  const fail = (error: unknown) => {
    if (error instanceof ApiError && error.code === 'concurrency_conflict') {
      setConflict(true);
      return;
    }
    if (error instanceof ApiError && Object.keys(error.errors).length) {
      setErrors(error.errors);
      const firstGroup = schema.fields.find((f) => Object.keys(error.errors).some((p) => p === f.key || p.startsWith(`${f.key}.`) || p.startsWith(`${f.key}[`)))?.group;
      if (firstGroup) setGroup(firstGroup);
    }
    toast.show(errorText(error, t), 'error');
  };

  const save = async (): Promise<SectionDetail | null> => {
    if (!draft) return null;
    const fresh = await api<SectionDetail>(`/api/admin/sections/${key}/draft`, { method: 'PUT', body: { data: draft, concurrencyStamp: d.concurrencyStamp } });
    afterChange(fresh);
    return fresh;
  };

  const onSave = async () => {
    setBusy('save');
    try {
      await save();
      toast.show(t.sections.draftSaved);
    } catch (e) {
      fail(e);
    } finally {
      setBusy(null);
    }
  };

  const onPublish = async () => {
    setBusy('publish');
    try {
      if (dirty) await save();
      const fresh = await api<SectionDetail>(`/api/admin/sections/${key}/publish`, { method: 'POST' });
      afterChange(fresh);
      toast.show(t.sections.published);
    } catch (e) {
      fail(e);
    } finally {
      setBusy(null);
    }
  };

  const onDiscard = async () => {
    if (!(await confirm({ title: t.sections.discardConfirm, confirm: t.sections.discard, danger: true }))) return;
    setBusy('discard');
    try {
      const fresh = await api<SectionDetail>(`/api/admin/sections/${key}/discard`, { method: 'POST' });
      afterChange(fresh);
      toast.show(t.sections.discarded);
    } catch (e) {
      fail(e);
    } finally {
      setBusy(null);
    }
  };

  const onVisibility = async (visible: boolean) => {
    setBusy('visibility');
    try {
      const fresh = await api<SectionSummary>(`/api/admin/sections/${key}/visibility`, { method: 'PUT', body: { visible } });
      client.setQueryData<SectionDetail>(keys.section(key), (old) => (old ? { ...old, section: fresh } : old));
      afterChange(fresh);
      toast.show(visible ? t.sections.visibilityOn : t.sections.visibilityOff);
    } catch (e) {
      fail(e);
    } finally {
      setBusy(null);
    }
  };

  const reload = async () => {
    const fresh = await detail.refetch();
    if (fresh.data) load(fresh.data);
  };

  const groupHasError = (g: string) => schema.fields.some((f) => f.group === g && Object.keys(errors).some((p) => p === f.key || p.startsWith(`${f.key}.`) || p.startsWith(`${f.key}[`)));
  const previewUrl = (lang: 'ar' | 'en') => `${lang === 'ar' ? '/' : '/en/'}?preview${schema.anchor ? `#${schema.anchor}` : ''}`;

  return (
    <div className={`page editor${preview ? ' with-preview' : ''}`}>
      <div className="editor-main">
        <header className="page-head">
          <Link to="/sections" className="crumb"><Icon name="chevron" className="flip" /> {t.sections.title}</Link>
          <div className="page-head-row">
            <div className="page-head-text">
              <h1>{pick(schema.name)}</h1>
              <p className="lead">{pick(schema.description)}</p>
            </div>
            <div className="page-actions">
              <button type="button" className="btn" onClick={() => void onSave()} disabled={!dirty || busy !== null}>
                {busy === 'save' ? t.common.saving : t.sections.saveDraft}
              </button>
              <button type="button" className="btn btn-primary" onClick={() => void onPublish()} disabled={busy !== null || (!dirty && !section.hasUnpublishedChanges && section.isPublished)}>
                {busy === 'publish' ? t.common.saving : dirty ? t.sections.saveAndPublish : t.sections.publish}
              </button>
            </div>
          </div>
        </header>

        <div className="status-bar">
          <SectionStatusPill section={section} />
          <span className="muted small">
            {section.hasUnpublishedChanges ? t.sections.pendingChanges : section.isPublished ? t.sections.upToDate : t.sections.neverPublished}
            {d.draftUpdatedBy && ` · ${t.common.updated(relative(section.draftUpdatedAt))} ${t.common.by(d.draftUpdatedBy)}`}
          </span>
          {dirty && <Pill kind="warn">{t.common.unsavedTitle.replace(/[?؟]$/, '')}</Pill>}
          <span className="status-bar-end">
            {section.canHide && (
              <label className="switch-inline">
                <span className="small">{t.sections.showOnSite}</span>
                <input type="checkbox" role="switch" className="switch" checked={section.isVisible} disabled={busy !== null} onChange={(e) => void onVisibility(e.target.checked)} />
              </label>
            )}
            <span className="preview-switch" role="group" aria-label={t.common.preview}>
              <button type="button" className="btn btn-sm" aria-pressed={preview === 'ar'} onClick={() => setPreview(preview === 'ar' ? null : 'ar')}>{t.sections.previewAr}</button>
              <button type="button" className="btn btn-sm" aria-pressed={preview === 'en'} onClick={() => setPreview(preview === 'en' ? null : 'en')}>{t.sections.previewEn}</button>
            </span>
            {section.hasUnpublishedChanges && (
              <button type="button" className="btn btn-sm btn-quiet danger" onClick={() => void onDiscard()} disabled={busy !== null}>{t.sections.discard}</button>
            )}
          </span>
        </div>

        {conflict && (
          <div className="notice notice-bad" role="alert">
            <Icon name="alert" /> {t.common.conflict}
            <button type="button" className="link" onClick={() => void reload()}>{t.common.reload}</button>
          </div>
        )}
        {missing.size > 0 && (
          <div className="notice" role="status">
            <Icon name="info" /> {t.sections.missing(missing.size)}. <span className="muted">{t.sections.missingList}: {[...missing].slice(0, 6).join(', ')}{missing.size > 6 ? '…' : ''}</span>
          </div>
        )}

        {schema.groups.length > 0 && group && (
          <Tabs
            label={pick(schema.name)}
            value={group}
            onChange={setGroup}
            items={schema.groups.map((g) => ({ value: g.key, label: pick(g.label) + (groupHasError(g.key) ? ' •' : '') }))}
          />
        )}

        {draft && (
          <form className="editor-form" onSubmit={(e) => { e.preventDefault(); void onSave(); }} noValidate>
            <SchemaFields fields={schema.fields} value={draft} onChange={setDraft} path="" ctx={ctx} group={schema.groups.length ? group : undefined} />
          </form>
        )}
      </div>

      {preview && (
        <aside className="preview" aria-label={t.sections.previewPanel}>
          <header className="preview-head">
            <span className="small">{t.sections.previewPanel} · {preview === 'ar' ? t.common.arabic : t.common.english}</span>
            <span className="preview-actions">
              <button type="button" className="icon-btn" onClick={() => setPreviewNonce((n) => n + 1)} aria-label={t.common.reload}><Icon name="refresh" /></button>
              <a className="icon-btn" href={previewUrl(preview)} target="_blank" rel="noopener" aria-label={t.common.open}><Icon name="external" /></a>
              <button type="button" className="icon-btn" onClick={() => setPreview(null)} aria-label={t.common.close}><Icon name="close" /></button>
            </span>
          </header>
          {dirty && <p className="preview-hint small">{t.sections.previewHint}</p>}
          <iframe key={`${preview}-${previewNonce}`} title={t.sections.previewPanel} src={previewUrl(preview)} />
        </aside>
      )}
    </div>
  );
}
