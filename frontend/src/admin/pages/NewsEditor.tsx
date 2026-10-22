import { useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode, type TextareaHTMLAttributes } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, ApiError } from '../../shared/api';
import { MediaPicker } from '../form/MediaPicker';
import { useUnsavedGuard } from '../form/useUnsavedGuard';
import { keys, useArticle, useCategories, useMediaItem } from '../data';
import { useI18n } from '../i18n';
import { blockNumber, blocksToText, textToBlocks, wordCount } from '../news-text';
import type { Block, Lang, NewsDetail } from '../types';
import { BiText, ErrorBlock, errorText, Field, Select, SkeletonRows, TextInput, Toggle } from '../ui/controls';
import { useConfirm, useToast } from '../ui/feedback';
import { Icon, QootMark, type IconName } from '../ui/icons';
import { NewsStatusPill } from '../ui/status';
import { NotFoundPage } from './NotFound';

interface Draft {
  slug: string;
  titleAr: string;
  titleEn: string;
  summaryAr: string;
  summaryEn: string;
  /** The article text as it is written (see news-text.ts); turned into blocks when saved. */
  textAr: string;
  textEn: string;
  metaTitleAr: string;
  metaTitleEn: string;
  metaDescriptionAr: string;
  metaDescriptionEn: string;
  featuredImageId: number | null;
  categoryId: number | null;
  isFeatured: boolean;
  priority: number;
  /** datetime-local value in Benghazi time, or empty. */
  publishedAt: string;
}

const EMPTY: Draft = {
  slug: '', titleAr: '', titleEn: '', summaryAr: '', summaryEn: '', textAr: '', textEn: '', metaTitleAr: '', metaTitleEn: '',
  metaDescriptionAr: '', metaDescriptionEn: '', featuredImageId: null, categoryId: null, isFeatured: false, priority: 0, publishedAt: '',
};

const ZONE = 'Africa/Tripoli';

/** ISO (UTC) → "YYYY-MM-DDTHH:mm" in Benghazi time, for <input type="datetime-local">. */
export function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const p = Object.fromEntries(
    new Intl.DateTimeFormat('en-GB', { timeZone: ZONE, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
      .formatToParts(d)
      .map((x) => [x.type, x.value]),
  );
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`;
}

/** "YYYY-MM-DDTHH:mm" in Benghazi time → ISO (UTC). */
export function fromLocalInput(value: string): string | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
  if (!m) return null;
  const asUtc = Date.UTC(+m[1]!, +m[2]! - 1, +m[3]!, +m[4]!, +m[5]!);
  // The zone's offset at that moment (Libya has no daylight saving today, but do not assume it).
  const back = toLocalInput(new Date(asUtc).toISOString());
  const m2 = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(back)!;
  const offset = Date.UTC(+m2[1]!, +m2[2]! - 1, +m2[3]!, +m2[4]!, +m2[5]!) - asUtc;
  return new Date(asUtc - offset).toISOString();
}

function fromDetail(d: NewsDetail, pictureWord: string): Draft {
  return {
    slug: d.slug, titleAr: d.titleAr, titleEn: d.titleEn, summaryAr: d.summaryAr, summaryEn: d.summaryEn,
    textAr: blocksToText(d.bodyAr ?? [], pictureWord), textEn: blocksToText(d.bodyEn ?? [], pictureWord),
    metaTitleAr: d.metaTitleAr, metaTitleEn: d.metaTitleEn, metaDescriptionAr: d.metaDescriptionAr, metaDescriptionEn: d.metaDescriptionEn,
    featuredImageId: d.featuredImageId, categoryId: d.categoryId, isFeatured: d.isFeatured, priority: d.priority, publishedAt: toLocalInput(d.publishedAt),
  };
}

const L = (lang: Lang) => (lang === 'ar' ? 'Ar' : 'En');

/**
 * One article, written like a document: a cover picture, then the headline, a short summary and the
 * text, in one language at a time (a switch at the top changes language). Publishing, the category
 * and "show it first" sit beside it; the web address, pin order and search texts under More options.
 */
export function NewsEditorPage() {
  const params = useParams();
  const id = params.id ? Number(params.id) : null;
  const isNew = id === null;
  const { t, pick, date, lang: uiLang } = useI18n();
  const navigate = useNavigate();
  const client = useQueryClient();
  const toast = useToast();
  const confirm = useConfirm();
  const article = useArticle(Number.isFinite(id) ? id : null);
  const categories = useCategories();

  const [draft, setDraft] = useState<Draft | null>(isNew ? EMPTY : null);
  const [baseline, setBaseline] = useState(isNew ? JSON.stringify(EMPTY) : '');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState<null | 'save' | 'publish' | 'unpublish' | 'delete'>(null);
  const [lang, setLang] = useState<Lang>(uiLang);
  const [conflict, setConflict] = useState(false);
  const [justPublished, setJustPublished] = useState(false);

  useEffect(() => {
    if (article.data && draft === null) {
      const d = fromDetail(article.data, t.news.pictureWord);
      setDraft(d);
      setBaseline(JSON.stringify(d));
      // Open the article in a language it is written in.
      if (!d[`title${L(uiLang)}`] && d[`title${L(uiLang === 'ar' ? 'en' : 'ar')}`]) setLang(uiLang === 'ar' ? 'en' : 'ar');
    }
  }, [article.data, draft, t.news.pictureWord, uiLang]);

  const dirty = draft !== null && JSON.stringify(draft) !== baseline;
  const allowLeave = useUnsavedGuard(dirty && busy !== 'delete');

  if (!isNew && (id === null || !Number.isFinite(id))) return <NotFoundPage />;
  if (!isNew && article.isPending) return <div className="page"><SkeletonRows rows={8} /></div>;
  if (!isNew && article.error instanceof ApiError && article.error.status === 404) return <NotFoundPage />;
  if (!isNew && article.error) return <div className="page"><ErrorBlock error={article.error} onRetry={() => void article.refetch()} /></div>;
  if (!draft) return <div className="page"><SkeletonRows rows={8} /></div>;

  const current = article.data;
  const set = <K extends keyof Draft>(k: K, v: Draft[K]) => setDraft((d) => (d ? { ...d, [k]: v } : d));
  const err = (path: string) => errors[path]?.[0];
  const bodyError = (l: Lang) => {
    const key = Object.keys(errors).find((k) => k.startsWith(`body${L(l)}`));
    if (!key) return undefined;
    const n = blockNumber(key);
    return n ? t.news.bodyProblem(n, errors[key]![0] ?? '') : errors[key]![0];
  };
  const written = (l: Lang) => !!(draft[`title${L(l)}`].trim() || draft[`text${L(l)}`].trim());

  const accept = (fresh: NewsDetail) => {
    client.setQueryData(keys.article(fresh.id), fresh);
    const d = fromDetail(fresh, t.news.pictureWord);
    setDraft(d);
    setBaseline(JSON.stringify(d));
    setErrors({});
    setConflict(false);
    void client.invalidateQueries({ queryKey: keys.newsAll });
    void client.invalidateQueries({ queryKey: keys.dashboard });
    void client.invalidateQueries({ queryKey: keys.categories });
  };

  const fail = (e: unknown) => {
    if (e instanceof ApiError && e.code === 'concurrency_conflict') setConflict(true);
    if (e instanceof ApiError && Object.keys(e.errors).length) {
      setErrors(e.errors);
      // Show the language the first problem is in.
      const first = Object.keys(e.errors).find((k) => /(Ar|En)(\[|$)/.test(k));
      if (first) setLang(/Ar(\[|$)/.test(first) ? 'ar' : 'en');
    }
    toast.show(errorText(e, t), 'error');
  };

  const payload = () => {
    const { textAr, textEn, ...rest } = draft;
    return {
      ...rest,
      bodyAr: textToBlocks(textAr),
      bodyEn: textToBlocks(textEn),
      publishedAt: draft.publishedAt ? fromLocalInput(draft.publishedAt) : null,
      concurrencyStamp: current?.concurrencyStamp ?? null,
    };
  };

  const save = async (): Promise<NewsDetail> => {
    if (isNew) {
      const fresh = await api<NewsDetail>('/api/admin/news', { method: 'POST', body: payload() });
      accept(fresh);
      return fresh;
    }
    const fresh = await api<NewsDetail>(`/api/admin/news/${id}`, { method: 'PUT', body: payload() });
    accept(fresh);
    return fresh;
  };

  const onSave = async () => {
    setBusy('save');
    setJustPublished(false);
    try {
      const fresh = await save();
      toast.show(isNew ? t.news.created : t.news.saved);
      if (isNew) {
        allowLeave();
        navigate(`/news/${fresh.id}`, { replace: true });
      }
    } catch (e) {
      fail(e);
    } finally {
      setBusy(null);
    }
  };

  const onPublish = async () => {
    setBusy('publish');
    try {
      const saved = dirty || isNew ? await save() : current!;
      const publishAt = draft.publishedAt ? fromLocalInput(draft.publishedAt) : null;
      const fresh = await api<NewsDetail>(`/api/admin/news/${saved.id}/publish`, { method: 'POST', body: { publishAt } });
      accept(fresh);
      setJustPublished(fresh.status === 'published');
      toast.show(fresh.status === 'scheduled' ? t.news.scheduledFor(date(fresh.publishedAt, true)) : t.news.publishedNow);
      if (isNew) {
        allowLeave();
        navigate(`/news/${fresh.id}`, { replace: true });
      }
    } catch (e) {
      fail(e);
    } finally {
      setBusy(null);
    }
  };

  const onUnpublish = async () => {
    if (!(await confirm({ title: t.news.unpublishConfirm, confirm: t.news.unpublish, danger: true }))) return;
    setBusy('unpublish');
    setJustPublished(false);
    try {
      accept(await api<NewsDetail>(`/api/admin/news/${id}/unpublish`, { method: 'POST' }));
      toast.show(t.news.unpublished);
    } catch (e) {
      fail(e);
    } finally {
      setBusy(null);
    }
  };

  const onDelete = async () => {
    const name = draft.titleAr || draft.titleEn || draft.slug;
    if (!(await confirm({ title: t.news.deleteConfirm(name), confirm: t.common.delete, danger: true }))) return;
    setBusy('delete');
    try {
      await api(`/api/admin/news/${id}`, { method: 'DELETE' });
      setBaseline(JSON.stringify(draft));
      client.removeQueries({ queryKey: keys.article(id!) });
      void client.invalidateQueries({ queryKey: keys.newsAll });
      void client.invalidateQueries({ queryKey: keys.dashboard });
      toast.show(t.news.deleted);
      allowLeave();
      navigate('/news', { replace: true });
    } catch (e) {
      fail(e);
      setBusy(null);
    }
  };

  const status = current?.status ?? 'draft';
  const futureDate = draft.publishedAt && (fromLocalInput(draft.publishedAt) ?? '') > new Date().toISOString();
  const previewHref = (l: Lang) => `${l === 'ar' ? '/news/' : '/en/news/'}?preview&article=${id}`;
  const liveHref = (l: Lang) => `${l === 'ar' ? '/news/' : '/en/news/'}${encodeURIComponent(current?.slug ?? '')}`;
  const other: Lang = lang === 'ar' ? 'en' : 'ar';
  const dir = lang === 'ar' ? 'rtl' : 'ltr';
  const langName = (l: Lang) => (l === 'ar' ? t.common.arabic : t.common.english);
  const heading = pick({ ar: draft.titleAr, en: draft.titleEn });
  const siteLang: Lang = draft.titleAr ? 'ar' : 'en';

  return (
    <div className="page news-editor">
      <header className="page-head">
        <Link to="/news" className="crumb"><Icon name="chevron" className="flip" /> {t.news.title}</Link>
        <div className="page-head-row">
          <div className="page-head-text">
            <h1 dir="auto">{isNew ? t.news.newTitle : heading || t.news.untitled}</h1>
            <p className="lead">{t.news.statusLine[status]}{current && ` · ${t.common.updated(date(current.updatedAt, true))}`}</p>
          </div>
          <div className="page-actions">
            {status === 'published' && !dirty && (
              <a className="btn" href={liveHref(siteLang)} target="_blank" rel="noopener"><Icon name="external" /> {t.news.viewOnSite}</a>
            )}
            <button type="button" className="btn" onClick={() => void onSave()} disabled={busy !== null || (!dirty && !isNew)}>
              {busy === 'save' ? t.common.saving : status === 'draft' ? t.news.saveDraft : t.news.saveChanges}
            </button>
            {(status === 'draft' || isNew) && (
              <button type="button" className="btn btn-primary" onClick={() => void onPublish()} disabled={busy !== null}>
                {busy === 'publish' ? t.common.saving : futureDate ? t.news.schedule : t.news.publish}
              </button>
            )}
          </div>
        </div>
      </header>

      {conflict && (
        <div className="notice notice-bad" role="alert">
          <Icon name="alert" /> {t.common.conflict}
          <button type="button" className="link" onClick={() => { setDraft(null); void article.refetch(); setConflict(false); }}>{t.common.reload}</button>
        </div>
      )}
      {justPublished && current && (
        <div className="notice notice-ok" role="status">
          <Icon name="check" /> {t.news.publishedBanner}
          <a className="link push-end" href={liveHref(siteLang)} target="_blank" rel="noopener">{t.news.viewOnSite}</a>
        </div>
      )}

      <div className="editor-columns">
        <form className="doc" onSubmit={(e) => { e.preventDefault(); void onSave(); }} noValidate>
          <Cover value={draft.featuredImageId} onChange={(v) => set('featuredImageId', v)} error={err('featuredImageId')} />
          <div className="doc-body">
            <div className="doc-langs">
              <div className="lang-switch" role="group" aria-label={t.news.writingIn}>
                {(['ar', 'en'] as Lang[]).map((l) => (
                  <button key={l} type="button" aria-pressed={lang === l} onClick={() => setLang(l)} lang={l}>
                    <span className={`lang-dot${written(l) ? ' is-done' : ''}`} aria-hidden="true" />
                    {langName(l)}
                    <span className="sr-only">{written(l) ? t.news.written : t.news.notWritten}</span>
                  </button>
                ))}
              </div>
              <span className="muted small">{written(lang) ? t.news.words(wordCount(draft[`text${L(lang)}`])) : t.news.notWritten}</span>
            </div>
            {!written(lang) && written(other) && <p className="doc-note">{t.news.fallbackNote(langName(other))}</p>}

            <Grow
              key={`title-${lang}`}
              className="doc-title"
              lang={lang}
              dir={dir}
              rows={1}
              maxLength={260}
              aria-label={`${t.news.titlePlaceholder} (${langName(lang)})`}
              aria-invalid={err(`title${L(lang)}`) ? true : undefined}
              placeholder={t.news.titlePlaceholder}
              value={draft[`title${L(lang)}`]}
              onChange={(e) => set(`title${L(lang)}`, e.target.value.replace(/\n/g, ' '))}
            />
            {err(`title${L(lang)}`) && <p className="doc-field-error" role="alert">{err(`title${L(lang)}`)}</p>}
            {!err(`title${L(lang)}`) && draft[`title${L(lang)}`].trim().length > 60 && <p className="doc-hint muted small">{t.news.titleLong(draft[`title${L(lang)}`].trim().length)}</p>}
            <Grow
              key={`summary-${lang}`}
              className="doc-summary"
              lang={lang}
              dir={dir}
              rows={1}
              maxLength={800}
              aria-label={`${t.news.summaryLabel} (${langName(lang)})`}
              placeholder={t.news.summaryPlaceholder}
              value={draft[`summary${L(lang)}`]}
              onChange={(e) => set(`summary${L(lang)}`, e.target.value)}
            />
            {err(`summary${L(lang)}`) && <p className="doc-field-error" role="alert">{err(`summary${L(lang)}`)}</p>}

            <Writer
              key={`text-${lang}`}
              lang={lang}
              value={draft[`text${L(lang)}`]}
              onChange={(v) => set(`text${L(lang)}`, v)}
              error={bodyError(lang)}
            />
          </div>
        </form>

        <aside className="editor-side">
          <section className="panel" aria-labelledby="publishing">
            <div className="panel-row">
              <h2 id="publishing">{t.news.publishing}</h2>
              <NewsStatusPill status={status} featured={current?.isFeatured} />
            </div>
            {current?.publishedAt && <p className="muted small num">{date(current.publishedAt, true)}</p>}
            <Field label={t.news.date} help={t.news.dateHelp} htmlFor="news-date" error={err('publishedAt')}>
              <TextInput id="news-date" type="datetime-local" dir="ltr" className="num" value={draft.publishedAt} onChange={(e) => set('publishedAt', e.target.value)} />
            </Field>
            {!isNew && (
              <div className="panel-actions">
                <a className="btn btn-sm" href={previewHref('ar')} target="_blank" rel="noopener"><Icon name="eye" /> {t.news.previewAr}</a>
                <a className="btn btn-sm" href={previewHref('en')} target="_blank" rel="noopener"><Icon name="eye" /> {t.news.previewEn}</a>
              </div>
            )}
            {!isNew && status !== 'draft' && (
              <button type="button" className="btn btn-sm danger-outline" onClick={() => void onUnpublish()} disabled={busy !== null}>{t.news.unpublish}</button>
            )}
          </section>

          <section className="panel">
            <Field label={t.news.category} htmlFor="news-category" error={err('categoryId')} aside={<Link className="link small" to="/news/categories">{t.news.categories}</Link>}>
              <Select
                id="news-category"
                value={draft.categoryId ? String(draft.categoryId) : ''}
                onChange={(v) => set('categoryId', v ? Number(v) : null)}
                placeholder={t.news.noCategory}
                options={(categories.data ?? []).map((c) => ({ value: String(c.id), label: pick({ ar: c.nameAr, en: c.nameEn }) }))}
              />
            </Field>
            <Toggle label={t.news.showFirst} description={t.news.showFirstHelp} checked={draft.isFeatured} onChange={(v) => set('isFeatured', v)} />
          </section>

          <details className="panel-details" open={!!(err('slug') || err('priority') || Object.keys(errors).some((k) => k.startsWith('meta')))}>
            <summary>{t.news.moreOptions}<Icon name="chevronDown" /></summary>
            <Field label={t.news.slug} help={t.news.slugHelp} htmlFor="news-slug" error={err('slug')}>
              <TextInput id="news-slug" dir="ltr" spellCheck={false} value={draft.slug} onChange={(e) => set('slug', e.target.value.toLowerCase())} />
            </Field>
            <Field label={t.news.priority} help={t.news.priorityHelp} htmlFor="news-priority" error={err('priority')}>
              <TextInput id="news-priority" type="number" className="input-num num" min={-100} max={100} value={draft.priority} onChange={(e) => set('priority', Number(e.target.value) || 0)} />
            </Field>
            <p className="field-help">{t.news.metaHelp}</p>
            <BiText label={t.news.metaTitle} value={{ ar: draft.metaTitleAr, en: draft.metaTitleEn }} onChange={(v) => setDraft({ ...draft, metaTitleAr: v.ar, metaTitleEn: v.en })} maxLength={200} errors={{ ar: err('metaTitleAr'), en: err('metaTitleEn') }} />
            <BiText label={t.news.metaDescription} multiline value={{ ar: draft.metaDescriptionAr, en: draft.metaDescriptionEn }} onChange={(v) => setDraft({ ...draft, metaDescriptionAr: v.ar, metaDescriptionEn: v.en })} maxLength={320} errors={{ ar: err('metaDescriptionAr'), en: err('metaDescriptionEn') }} />
          </details>

          {!isNew && (
            <button type="button" className="btn btn-quiet danger" onClick={() => void onDelete()} disabled={busy !== null}>
              <Icon name="trash" /> {t.common.delete}
            </button>
          )}
        </aside>
      </div>
    </div>
  );
}

// ------------------------------------------------------------------ parts

/** A textarea without a box that grows with its text: the headline and the summary. */
function Grow(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  const ref = useRef<HTMLTextAreaElement>(null);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const fit = () => {
      el.style.height = 'auto';
      el.style.height = `${el.scrollHeight}px`;
    };
    fit();
    void document.fonts?.ready.then(fit);
    const ro = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(fit) : null;
    ro?.observe(el);
    return () => ro?.disconnect();
  }, [props.value]);
  return <textarea {...props} ref={ref} />;
}

/** The cover picture: a wide slot at the top of the article, as it will lead the news card. */
function Cover({ value, onChange, error }: { value: number | null; onChange: (id: number | null) => void; error?: string }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const item = useMediaItem(value);
  return (
    <>
      <div className="doc-cover-wrap" style={{ position: 'relative' }}>
        <button type="button" className="doc-cover" onClick={() => setOpen(true)} aria-label={value ? `${t.news.cover}: ${t.form.replaceImage}` : t.news.addCover}>
          {value && item.data ? (
            <img src={item.data.url} alt="" />
          ) : (
            <span className="doc-cover-empty">
              <QootMark size={40} />
              <span>{t.news.addCover}</span>
              <small>{t.news.coverHelp}</small>
            </span>
          )}
        </button>
        {value !== null && (
          <span className="doc-cover-actions">
            <button type="button" className="btn btn-sm" onClick={() => setOpen(true)}><Icon name="media" /> {t.form.replaceImage}</button>
            <button type="button" className="btn btn-sm" onClick={() => onChange(null)}><Icon name="trash" /> {t.form.clearImage}</button>
          </span>
        )}
      </div>
      {error && <p className="field-error" role="alert" style={{ padding: '8px 32px 0' }}>{error}</p>}
      <MediaPicker open={open} onClose={() => setOpen(false)} allowDrawings={false} selected={value} onPick={(pid) => { onChange(pid); setOpen(false); }} />
    </>
  );
}

/** The article text in one language: a plain writing box with a few buttons, and a preview. */
function Writer({ lang, value, onChange, error }: { lang: Lang; value: string; onChange: (v: string) => void; error?: string }) {
  const { t } = useI18n();
  const area = useRef<HTMLTextAreaElement>(null);
  const [mode, setMode] = useState<'write' | 'preview'>('write');
  const [picking, setPicking] = useState(false);
  const pending = useRef<[number, number] | null>(null);
  const dir = lang === 'ar' ? 'rtl' : 'ltr';

  useLayoutEffect(() => {
    const el = area.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${Math.max(el.scrollHeight + 2, 340)}px`;
    if (pending.current) {
      el.focus();
      el.setSelectionRange(pending.current[0], pending.current[1]);
      pending.current = null;
    }
  }, [value, mode]);

  /** Applies a change around the selection and puts the cursor where it belongs. */
  const edit = (fn: (text: string, start: number, end: number) => { text: string; start: number; end: number }) => {
    const el = area.current;
    const start = el?.selectionStart ?? value.length;
    const end = el?.selectionEnd ?? value.length;
    const next = fn(value, start, end);
    pending.current = [next.start, next.end];
    onChange(next.text);
  };
  const lineStart = (text: string, at: number) => text.lastIndexOf('\n', at - 1) + 1;
  const prefixLines = (prefix: string, numbered = false) =>
    edit((text, start, end) => {
      const from = lineStart(text, start);
      const lines = text.slice(from, end).split('\n');
      const done = lines.map((l, i) => (numbered ? `${i + 1}. ` : prefix) + l.replace(/^(#{2,3}\s|[-•*]\s|\d+[.)]\s|>\s?)/, ''));
      const block = done.join('\n');
      return { text: text.slice(0, from) + block + text.slice(end), start: from + block.length, end: from + block.length };
    });
  const wrap = (before: string, after: string, placeholder: string) =>
    edit((text, start, end) => {
      const inner = text.slice(start, end) || placeholder;
      const out = before + inner + after;
      return { text: text.slice(0, start) + out + text.slice(end), start: start + before.length, end: start + before.length + inner.length };
    });
  const insertBlock = (line: string) =>
    edit((text, start) => {
      const before = text.slice(0, start).replace(/\s*$/, '');
      const after = text.slice(start).replace(/^\s*/, '');
      const head = before ? `${before}\n\n` : '';
      const out = `${head}${line}\n\n${after}`;
      const cursor = head.length + line.length + 2;
      return { text: out.replace(/\n{3,}$/, '\n'), start: cursor, end: cursor };
    });

  const blocks = useMemo(() => (mode === 'preview' ? textToBlocks(value) : []), [mode, value]);
  return (
    <div className={`writer${error ? ' has-error' : ''}`}>
      <div className="writer-bar" role="toolbar" aria-label={t.news.body}>
        <Tool icon="heading" label={t.news.tools.heading} disabled={mode === 'preview'} onClick={() => prefixLines('## ')} />
        <Tool icon="list" label={t.news.tools.list} disabled={mode === 'preview'} onClick={() => prefixLines('- ')} />
        <Tool icon="quote" label={t.news.tools.quote} disabled={mode === 'preview'} onClick={() => prefixLines('> ')} />
        <span className="sep" aria-hidden="true" />
        <Tool icon="bold" label={t.news.tools.bold} disabled={mode === 'preview'} onClick={() => wrap('**', '**', lang === 'ar' ? 'نص عريض' : 'bold text')} />
        <Tool icon="link" label={t.news.tools.link} disabled={mode === 'preview'} onClick={() => wrap('[', '](https://)', lang === 'ar' ? 'نص الرابط' : 'link text')} />
        <Tool icon="media" label={t.news.tools.picture} disabled={mode === 'preview'} onClick={() => setPicking(true)} />
        <span className="mode" role="group" aria-label={t.news.body}>
          <button type="button" aria-pressed={mode === 'write'} onClick={() => setMode('write')}>{t.news.writeTab}</button>
          <button type="button" aria-pressed={mode === 'preview'} onClick={() => setMode('preview')}>{t.news.preview}</button>
        </span>
      </div>
      {mode === 'write' ? (
        <textarea
          ref={area}
          lang={lang}
          dir={dir}
          value={value}
          aria-label={`${t.news.body} (${lang === 'ar' ? t.common.arabic : t.common.english})`}
          aria-invalid={error ? true : undefined}
          placeholder={t.news.bodyPlaceholder}
          onChange={(e) => onChange(e.target.value)}
        />
      ) : (
        <div className="writer-preview" lang={lang} dir={dir}>
          {blocks.length === 0 ? <p className="muted">{t.news.previewEmpty}</p> : blocks.map((b, i) => <PreviewBlock key={i} block={b} />)}
        </div>
      )}
      <div className="writer-foot">
        {error ? <span className="field-error" role="alert">{error}</span> : <span>{t.news.bodyHelp}</span>}
      </div>
      <MediaPicker
        open={picking}
        onClose={() => setPicking(false)}
        allowDrawings={false}
        selected={null}
        onPick={(mediaId) => {
          setPicking(false);
          setMode('write');
          insertBlock(`[[${t.news.pictureWord}:${mediaId}]]`);
        }}
      />
    </div>
  );
}

function Tool({ icon, label, disabled, onClick }: { icon: IconName; label: string; disabled: boolean; onClick: () => void }) {
  return (
    <button type="button" className="tool" onClick={onClick} disabled={disabled} title={label}>
      <Icon name={icon} size={15} /> <span className="hide-phone">{label}</span>
    </button>
  );
}

/** **bold** and [text](link), as the website shows them. */
function Inline({ text }: { text: string }) {
  const parts: ReactNode[] = [];
  const re = /\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)/g;
  let last = 0;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    if (m.index > last) parts.push(text.slice(last, m.index));
    parts.push(m[1] !== undefined ? <strong key={m.index}>{m[1]}</strong> : <a key={m.index} href={m[3]} target="_blank" rel="noopener noreferrer">{m[2]}</a>);
    last = m.index + m[0].length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return <>{parts}</>;
}

function PreviewBlock({ block }: { block: Block }) {
  switch (block.type) {
    case 'paragraph':
      return <p>{block.text.split('\n').map((line, i) => <span key={i}>{i > 0 && <br />}<Inline text={line} /></span>)}</p>;
    case 'heading':
      return block.level === 3 ? <h3>{block.text}</h3> : <h2>{block.text}</h2>;
    case 'list': {
      const items = block.items.map((item, i) => <li key={i}><Inline text={item} /></li>);
      return block.style === 'number' ? <ol>{items}</ol> : <ul>{items}</ul>;
    }
    case 'quote':
      return <blockquote><p><Inline text={block.text} /></p>{block.cite && <cite>{block.cite}</cite>}</blockquote>;
    case 'image':
      return <PreviewImage id={block.mediaId} caption={block.caption} />;
    default:
      return null;
  }
}

function PreviewImage({ id, caption }: { id: number | null; caption: string }) {
  const item = useMediaItem(id);
  return (
    <figure>
      {item.data ? <img src={item.data.url} alt="" /> : <span className="sk" style={{ height: 160 }} />}
      {caption && <figcaption>{caption}</figcaption>}
    </figure>
  );
}
