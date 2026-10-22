import { useRef, useState } from 'react';
import { api } from '../../shared/api';
import { keys, useApiMutation, useMediaItem, useMediaList } from '../data';
import { useI18n } from '../i18n';
import type { MediaItem } from '../types';
import { Empty, ErrorBlock, errorText, Pager, SearchBox, SkeletonRows } from '../ui/controls';
import { Dialog, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';

export const ACCEPT = 'image/jpeg,image/png,image/webp,image/avif,image/gif';

/** Cream brand files (the footer wordmarks) are drawn for the green: show them on the green. */
export function isLight(url: string): boolean {
  return /-cream\.svg$/i.test(url);
}

export function useUpload() {
  return useApiMutation(
    ({ files, altAr = '', altEn = '' }: { files: File[]; altAr?: string; altEn?: string }) => {
      const form = new FormData();
      files.forEach((f) => form.append('files', f));
      form.append('altAr', altAr);
      form.append('altEn', altEn);
      return api<MediaItem[]>('/api/admin/media', { method: 'POST', form, timeoutMs: 120_000 });
    },
    () => [keys.mediaAll, keys.dashboard],
  );
}

/** A picture field: the chosen picture (or none), and a library to choose or upload from. */
export function MediaField({ value, onChange, allowDrawings = false, label, error, required }: {
  value: number | null;
  onChange: (id: number | null) => void;
  allowDrawings?: boolean;
  label: string;
  error?: string;
  required?: boolean;
}) {
  const { t, lang } = useI18n();
  const [open, setOpen] = useState(false);
  const item = useMediaItem(value);
  const media = item.data;
  return (
    <div className={`field media-field${error ? ' has-error' : ''}`}>
      <div className="field-label">
        <span>{label}</span>
        {required && <span className="req">{t.common.required}</span>}
      </div>
      <div className="media-slot">
        <button type="button" className={`media-thumb${media?.isDrawing ? ' is-drawing' : ''}${media && isLight(media.url) ? ' on-green' : ''}`} onClick={() => setOpen(true)} aria-label={`${label}: ${media ? t.form.replaceImage : t.form.pickImage}`}>
          {value && media ? <img src={media.url} alt="" /> : value && item.isPending ? <span className="sk" /> : <Icon name="media" size={20} />}
        </button>
        <div className="media-slot-text">
          {media ? (
            <>
              <span className="media-name" dir="ltr">{media.fileName}</span>
              <span className="muted small" dir="auto">{(lang === 'ar' ? media.altAr || media.altEn : media.altEn || media.altAr) || t.common.dash}</span>
            </>
          ) : (
            <span className="muted">{t.form.noImage}</span>
          )}
          <span className="media-slot-actions">
            <button type="button" className="btn btn-sm" onClick={() => setOpen(true)}>{value ? t.form.replaceImage : t.form.pickImage}</button>
            {value !== null && !required && (
              <button type="button" className="btn btn-sm btn-quiet" onClick={() => onChange(null)}>{t.form.clearImage}</button>
            )}
          </span>
        </div>
      </div>
      {error && <p className="field-error" role="alert">{error}</p>}
      <MediaPicker
        open={open}
        onClose={() => setOpen(false)}
        allowDrawings={allowDrawings}
        selected={value}
        onPick={(id) => {
          onChange(id);
          setOpen(false);
        }}
      />
    </div>
  );
}

export function MediaPicker({ open, onClose, onPick, allowDrawings, selected }: {
  open: boolean;
  onClose: () => void;
  onPick: (id: number) => void;
  allowDrawings: boolean;
  selected: number | null;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const input = useRef<HTMLInputElement>(null);
  const params = { search, page, pageSize: 24, ...(allowDrawings ? {} : { type: 'photo' }) };
  const list = useMediaList(params);
  const upload = useUpload();

  const onFiles = (files: FileList | null) => {
    if (!files?.length) return;
    upload.mutate(
      { files: Array.from(files) },
      {
        onSuccess: (items) => {
          toast.show(t.media.uploaded(items.length));
          if (items.length === 1) onPick(items[0]!.id);
        },
        onError: (e) => toast.show(errorText(e, t), 'error'),
      },
    );
  };

  return (
    <Dialog open={open} onClose={onClose} title={t.media.pickTitle} wide>
      <div className="toolbar">
        <SearchBox value={search} onChange={(v) => { setSearch(v); setPage(1); }} placeholder={t.media.searchPlaceholder} />
        <input ref={input} type="file" accept={ACCEPT} multiple hidden onChange={(e) => { onFiles(e.target.files); e.target.value = ''; }} />
        <button type="button" className="btn" onClick={() => input.current?.click()} disabled={upload.isPending}>
          <Icon name="upload" /> {upload.isPending ? t.media.uploading : t.media.upload}
        </button>
      </div>
      {list.isPending ? (
        <SkeletonRows rows={4} />
      ) : list.error ? (
        <ErrorBlock error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data!.items.length === 0 ? (
        <Empty>{t.media.empty}</Empty>
      ) : (
        <>
          <ul className="media-grid picker">
            {list.data!.items.map((m) => (
              <li key={m.id}>
                <button type="button" className={`media-tile${m.id === selected ? ' is-selected' : ''}`} onClick={() => onPick(m.id)} aria-pressed={m.id === selected}>
                  <span className={`media-img${m.isDrawing ? ' is-drawing' : ''}${isLight(m.url) ? ' on-green' : ''}`}><img src={m.url} alt="" loading="lazy" /></span>
                  <span className="media-caption" dir="ltr">{m.fileName}</span>
                </button>
              </li>
            ))}
          </ul>
          <Pager page={list.data!.page} totalPages={list.data!.totalPages} total={list.data!.total} pageSize={list.data!.pageSize} onPage={setPage} />
        </>
      )}
    </Dialog>
  );
}
