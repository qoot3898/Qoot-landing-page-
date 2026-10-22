import { useEffect, useRef, useState, type DragEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../../shared/api';
import { ACCEPT, useUpload, isLight } from '../form/MediaPicker';
import { keys, useMediaItem, useMediaList, useMediaUsage } from '../data';
import { useI18n } from '../i18n';
import type { MediaItem } from '../types';
import { BiText, Empty, ErrorBlock, errorText, Field, PageHead, Pager, SearchBox, Select, SkeletonRows, Tabs, TextInput } from '../ui/controls';
import { Dialog, useConfirm, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';

type Kind = 'all' | 'upload' | 'bundled';

/** The picture library: upload (button or drop), describe in both languages, replace, see where used, delete. */
export function MediaPage() {
  const { t, bytes, number } = useI18n();
  const toast = useToast();
  const [kind, setKind] = useState<Kind>('all');
  const [type, setType] = useState('');
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<number | null>(null);
  const [dragging, setDragging] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const upload = useUpload();

  useEffect(() => {
    const id = window.setTimeout(() => { setQuery(search); setPage(1); }, 300);
    return () => window.clearTimeout(id);
  }, [search]);

  const list = useMediaList({ page, pageSize: 30, ...(kind !== 'all' ? { kind } : {}), ...(type ? { type } : {}), ...(query ? { search: query } : {}) });

  const onFiles = (files: FileList | File[] | null) => {
    const arr = files ? Array.from(files).filter((f) => f.type.startsWith('image/')) : [];
    if (!arr.length) return;
    upload.mutate(
      { files: arr },
      {
        onSuccess: (items) => {
          toast.show(t.media.uploaded(items.length));
          if (items.length === 1) setSelected(items[0]!.id);
        },
        onError: (e) => toast.show(errorText(e, t), 'error'),
      },
    );
  };

  const onDrop = (e: DragEvent) => {
    e.preventDefault();
    setDragging(false);
    onFiles(e.dataTransfer.files);
  };

  return (
    <div
      className={`page${dragging ? ' is-dragging' : ''}`}
      onDragOver={(e) => { if (e.dataTransfer.types.includes('Files')) { e.preventDefault(); setDragging(true); } }}
      onDragLeave={(e) => { if (e.currentTarget === e.target) setDragging(false); }}
      onDrop={onDrop}
    >
      <PageHead
        title={t.media.title}
        lead={t.media.lead}
        actions={
          <>
            <input ref={input} type="file" accept={ACCEPT} multiple hidden onChange={(e) => { onFiles(e.target.files); e.target.value = ''; }} />
            <button type="button" className="btn btn-primary" onClick={() => input.current?.click()} disabled={upload.isPending}>
              <Icon name="upload" /> {upload.isPending ? t.media.uploading : t.media.upload}
            </button>
          </>
        }
      />
      <Tabs<Kind>
        label={t.media.title}
        value={kind}
        onChange={(v) => { setKind(v); setPage(1); }}
        items={[{ value: 'all', label: t.media.tabs.all }, { value: 'upload', label: t.media.tabs.upload }, { value: 'bundled', label: t.media.tabs.bundled }]}
      />
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} placeholder={t.media.searchPlaceholder} />
        <Select ariaLabel={t.media.types.all} value={type} onChange={(v) => { setType(v); setPage(1); }} options={[{ value: '', label: t.media.types.all }, { value: 'photo', label: t.media.types.photo }, { value: 'drawing', label: t.media.types.drawing }]} />
        <span className="toolbar-end">{list.data && <span className="muted num">{t.media.count(list.data.total)}</span>}</span>
      </div>
      <button type="button" className="dropzone" onClick={() => input.current?.click()}>
        <Icon name="upload" /> {t.media.drop}
      </button>

      {list.isPending ? (
        <SkeletonRows rows={6} />
      ) : list.error ? (
        <ErrorBlock error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data!.items.length === 0 ? (
        <Empty>{t.media.empty}</Empty>
      ) : (
        <>
          <ul className="media-grid">
            {list.data!.items.map((m) => (
              <li key={m.id}>
                <button type="button" className="media-tile" onClick={() => setSelected(m.id)}>
                  <span className={`media-img${m.isDrawing ? ' is-drawing' : ''}${isLight(m.url) ? ' on-green' : ''}`}><img src={m.url} alt="" loading="lazy" /></span>
                  <span className="media-caption" dir="ltr">{m.fileName}</span>
                  <span className="media-meta num">
                    {m.kind === 'bundled' ? t.media.tabs.bundled : bytes(m.sizeBytes)}
                    {m.width > 0 && ` · ${number(m.width)}×${number(m.height)}`}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <Pager page={list.data!.page} totalPages={list.data!.totalPages} total={list.data!.total} pageSize={list.data!.pageSize} onPage={setPage} />
        </>
      )}
      <MediaDetails id={selected} onClose={() => setSelected(null)} />
    </div>
  );
}

function MediaDetails({ id, onClose }: { id: number | null; onClose: () => void }) {
  const { t, pick, date, bytes, number } = useI18n();
  const item = useMediaItem(id);
  const usage = useMediaUsage(id);
  const client = useQueryClient();
  const toast = useToast();
  const confirm = useConfirm();
  const [alt, setAlt] = useState({ ar: '', en: '' });
  const [fileName, setFileName] = useState('');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState(false);
  const replaceInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (item.data) {
      setAlt({ ar: item.data.altAr, en: item.data.altEn });
      setFileName(item.data.fileName);
      setErrors({});
    }
  }, [item.data]);

  const refresh = (fresh?: MediaItem) => {
    if (fresh) client.setQueryData(keys.mediaItem(fresh.id), fresh);
    void client.invalidateQueries({ queryKey: keys.mediaAll });
  };

  const m = item.data;
  const bundled = m?.kind === 'bundled';
  const inUse = (usage.data?.length ?? 0) > 0;
  const dirty = !!m && (alt.ar !== m.altAr || alt.en !== m.altEn || fileName !== m.fileName);

  const save = async () => {
    if (!m) return;
    setBusy(true);
    try {
      refresh(await api<MediaItem>(`/api/admin/media/${m.id}`, { method: 'PUT', body: { altAr: alt.ar, altEn: alt.en, fileName } }));
      toast.show(t.media.saved);
    } catch (e) {
      if (e instanceof ApiError) setErrors(e.errors);
      toast.show(errorText(e, t), 'error');
    } finally {
      setBusy(false);
    }
  };

  const replace = async (file: File | undefined) => {
    if (!m || !file) return;
    setBusy(true);
    try {
      const form = new FormData();
      form.append('file', file);
      refresh(await api<MediaItem>(`/api/admin/media/${m.id}/replace`, { method: 'POST', form, timeoutMs: 120_000 }));
      toast.show(t.media.replaced);
    } catch (e) {
      toast.show(errorText(e, t), 'error');
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!m || !(await confirm({ title: t.media.deleteConfirm(m.fileName), confirm: t.common.delete, danger: true }))) return;
    setBusy(true);
    try {
      await api(`/api/admin/media/${m.id}`, { method: 'DELETE' });
      client.removeQueries({ queryKey: keys.mediaItem(m.id) });
      void client.invalidateQueries({ queryKey: keys.mediaAll });
      void client.invalidateQueries({ queryKey: keys.dashboard });
      toast.show(t.media.deleted);
      onClose();
    } catch (e) {
      toast.show(errorText(e, t), 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={id !== null}
      onClose={onClose}
      title={t.media.details}
      wide
      footer={
        m && (
          <>
            {!bundled && (
              <button type="button" className="btn btn-quiet danger push-start" disabled={busy || inUse || usage.isPending} onClick={() => void remove()} title={inUse ? t.media.deleteInUse : undefined}>
                <Icon name="trash" /> {t.common.delete}
              </button>
            )}
            <button type="button" className="btn" onClick={onClose}>{t.common.close}</button>
            <button type="button" className="btn btn-primary" disabled={!dirty || busy} onClick={() => void save()}>{busy ? t.common.saving : t.common.save}</button>
          </>
        )
      }
    >
      {item.isPending ? (
        <SkeletonRows rows={4} />
      ) : item.error ? (
        <ErrorBlock error={item.error} onRetry={() => void item.refetch()} />
      ) : m ? (
        <div className="media-details">
          <figure className={`media-preview${m.isDrawing ? ' is-drawing' : ''}${isLight(m.url) ? ' on-green' : ''}`}>
            <img src={m.url} alt={pick({ ar: m.altAr, en: m.altEn })} />
          </figure>
          <div className="media-info">
            <dl className="kv">
              <div><dt>{t.media.size}</dt><dd className="num">{bundled ? t.common.dash : bytes(m.sizeBytes)}</dd></div>
              <div><dt>{t.media.dimensions}</dt><dd className="num">{m.width ? `${number(m.width)} × ${number(m.height)}` : t.common.dash}</dd></div>
              <div><dt>{t.media.added}</dt><dd className="num">{date(m.createdAt)}</dd></div>
              <div><dt>URL</dt><dd><code dir="ltr" className="truncate">{m.url}</code></dd></div>
            </dl>
            {bundled && <p className="notice"><Icon name="lock" /> {t.media.bundled}</p>}
            <BiText label={pick({ ar: 'الوصف', en: 'Description' })} help={t.media.altHelp} value={alt} onChange={setAlt} maxLength={300} errors={{ ar: errors.altAr?.[0], en: errors.altEn?.[0] }} />
            <Field label={t.media.fileName} htmlFor="media-name" error={errors.fileName?.[0]}>
              <TextInput id="media-name" dir="ltr" value={fileName} onChange={(e) => setFileName(e.target.value)} disabled={bundled} />
            </Field>
            {!bundled && (
              <>
                <input ref={replaceInput} type="file" accept={ACCEPT} hidden onChange={(e) => { void replace(e.target.files?.[0]); e.target.value = ''; }} />
                <button type="button" className="btn btn-sm" disabled={busy} onClick={() => replaceInput.current?.click()}><Icon name="refresh" /> {t.media.replace}</button>
              </>
            )}
            <div className="usage">
              <h3>{t.media.usage}</h3>
              {usage.isPending ? (
                <SkeletonRows rows={2} />
              ) : usage.data && usage.data.length > 0 ? (
                <>
                  <ul>
                    {usage.data.map((u) => (
                      <li key={`${u.area}-${u.key}`}>
                        <Link className="link" to={u.area === 'news' ? `/news/${u.key}` : u.area === 'settings' ? '/settings' : `/sections/${u.key}`} onClick={onClose}>
                          {pick({ ar: u.labelAr, en: u.labelEn })}
                        </Link>
                      </li>
                    ))}
                  </ul>
                  {!bundled && <p className="field-help">{t.media.deleteInUse}</p>}
                </>
              ) : (
                <p className="muted">{t.media.notUsed}</p>
              )}
            </div>
          </div>
        </div>
      ) : null}
    </Dialog>
  );
}
