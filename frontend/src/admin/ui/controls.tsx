import { useId, useLayoutEffect, useRef, type ChangeEvent, type InputHTMLAttributes, type ReactNode, type TextareaHTMLAttributes } from 'react';
import { ApiError } from '../../shared/api';
import { useI18n, type Strings } from '../i18n';
import type { Bilingual, Lang } from '../types';
import { Icon } from './icons';

export function errorText(error: unknown, t: Strings): string {
  if (error instanceof ApiError) return error.isNetwork ? t.common.networkError : error.message || t.common.errorTitle;
  return t.common.errorTitle;
}

// ------------------------------------------------------------------ fields

export function Field({ label, help, error, htmlFor, children, counter, aside, className }: {
  label: ReactNode;
  help?: ReactNode;
  error?: string | null;
  htmlFor?: string;
  children: ReactNode;
  counter?: ReactNode;
  aside?: ReactNode;
  className?: string;
}) {
  return (
    <div className={`field${error ? ' has-error' : ''}${className ? ` ${className}` : ''}`}>
      <div className="field-label">
        <label htmlFor={htmlFor}>{label}</label>
        {aside}
      </div>
      {children}
      {(help || error || counter) && (
        <div className="field-foot">
          {error ? <p className="field-error" role="alert">{error}</p> : help ? <p className="field-help">{help}</p> : <span />}
          {counter && <span className="counter num">{counter}</span>}
        </div>
      )}
    </div>
  );
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={`input${props.className ? ` ${props.className}` : ''}`} />;
}

/** A textarea that grows with its text (no inner scrollbar while editing). */
export function TextArea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  const ref = useRef<HTMLTextAreaElement>(null);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const fit = () => {
      el.style.height = 'auto';
      el.style.height = `${Math.min(el.scrollHeight + 2, 560)}px`;
    };
    fit();
    // Again once the web fonts are in (Arabic lines change height) and when the column changes width.
    void document.fonts?.ready.then(fit);
    let lastWidth = el.clientWidth;
    const onResize = () => {
      if (el.clientWidth !== lastWidth) {
        lastWidth = el.clientWidth;
        fit();
      }
    };
    const ro = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(onResize) : null;
    ro?.observe(el);
    return () => ro?.disconnect();
  }, [props.value]);
  return <textarea rows={3} {...props} ref={ref} className={`input textarea${props.className ? ` ${props.className}` : ''}`} />;
}

/**
 * One text in both languages, side by side: Arabic right-to-left, English left-to-right. A missing
 * translation is marked; the website falls back to the other language until it is written.
 */
export function BiText({ label, value, onChange, multiline = false, maxLength, required = false, help, errors, missing, inputMode, disabled }: {
  label: string;
  value: Bilingual;
  onChange: (value: Bilingual) => void;
  multiline?: boolean;
  maxLength?: number;
  required?: boolean;
  help?: string | null;
  errors?: Partial<Record<Lang, string>>;
  missing?: Partial<Record<Lang, boolean>>;
  inputMode?: InputHTMLAttributes<HTMLInputElement>['inputMode'];
  disabled?: boolean;
}) {
  const { t } = useI18n();
  const id = useId();
  const langs: Lang[] = ['ar', 'en'];
  return (
    <fieldset className="field bi">
      <legend className="field-label">
        <span>{label}</span>
        {required && <span className="req">{t.common.required}</span>}
      </legend>
      <div className="bi-row">
        {langs.map((lang) => {
          const v = value[lang] ?? '';
          const err = errors?.[lang];
          const props = {
            id: `${id}-${lang}`,
            lang,
            dir: lang === 'ar' ? 'rtl' : 'ltr',
            value: v,
            maxLength: maxLength ? maxLength + 200 : undefined,
            disabled,
            'aria-invalid': err ? true : undefined,
            'aria-describedby': err ? `${id}-${lang}-err` : undefined,
            onChange: (e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => onChange({ ...value, [lang]: e.target.value }),
          } as const;
          const over = maxLength !== undefined && v.length > maxLength;
          return (
            <div key={lang} className={`bi-cell${err ? ' has-error' : ''}${missing?.[lang] && !v ? ' is-missing' : ''}`}>
              <span className="bi-lang">
                <label htmlFor={`${id}-${lang}`} className="lang-tag">{lang === 'ar' ? t.common.arabic : t.common.english}</label>
                {missing?.[lang] && !v && <span className="pill pill-warn">{t.common.missingTranslation}</span>}
              </span>
              {multiline ? <TextArea {...props} /> : <TextInput {...props} inputMode={inputMode} />}
              <div className="field-foot">
                {err ? <p className="field-error" id={`${id}-${lang}-err`} role="alert">{err}</p> : <span />}
                {maxLength !== undefined && (multiline || v.length > maxLength * 0.7) && (
                  <span className={`counter num${over ? ' over' : ''}`}>{t.common.chars(v.length, maxLength)}</span>
                )}
              </div>
            </div>
          );
        })}
      </div>
      {help && <p className="field-help">{help}</p>}
    </fieldset>
  );
}

export function Toggle({ checked, onChange, label, description, disabled, id }: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: ReactNode;
  description?: ReactNode;
  disabled?: boolean;
  id?: string;
}) {
  const auto = useId();
  const inputId = id ?? auto;
  return (
    <div className="toggle-row">
      <div>
        <label htmlFor={inputId} className="toggle-label">{label}</label>
        {description && <p className="field-help">{description}</p>}
      </div>
      <input id={inputId} type="checkbox" role="switch" className="switch" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
    </div>
  );
}

export function Select({ value, onChange, options, placeholder, id, disabled, ariaLabel, className }: {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  placeholder?: string;
  id?: string;
  disabled?: boolean;
  ariaLabel?: string;
  className?: string;
}) {
  return (
    <select id={id} className={`input select${className ? ` ${className}` : ''}`} value={value} disabled={disabled} aria-label={ariaLabel} onChange={(e) => onChange(e.target.value)}>
      {placeholder !== undefined && <option value="">{placeholder}</option>}
      {options.map((o) => (
        <option key={o.value} value={o.value}>{o.label}</option>
      ))}
    </select>
  );
}

// ------------------------------------------------------------------ marks

export type PillKind = 'ok' | 'warn' | 'neutral' | 'bad' | 'brand';
export function Pill({ kind = 'neutral', children, icon }: { kind?: PillKind; children: ReactNode; icon?: Parameters<typeof Icon>[0]['name'] }) {
  return (
    <span className={`pill pill-${kind}`}>
      {icon && <Icon name={icon} size={12} />}
      {children}
    </span>
  );
}

export function Tabs<T extends string>({ value, onChange, items, label }: {
  value: T;
  onChange: (value: T) => void;
  items: { value: T; label: string; count?: number }[];
  label: string;
}) {
  return (
    <div className="tabs" role="tablist" aria-label={label}>
      {items.map((item) => (
        <button
          key={item.value}
          type="button"
          role="tab"
          aria-selected={item.value === value}
          className="tab"
          onClick={() => onChange(item.value)}
        >
          {item.label}
          {item.count !== undefined && item.count > 0 && <span className="tab-count num">{item.count}</span>}
        </button>
      ))}
    </div>
  );
}

export function SearchBox({ value, onChange, placeholder }: { value: string; onChange: (v: string) => void; placeholder: string }) {
  return (
    <div className="search">
      <Icon name="search" />
      <input type="search" className="input" value={value} placeholder={placeholder} aria-label={placeholder} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

export function Pager({ page, totalPages, total, pageSize, onPage }: { page: number; totalPages: number; total: number; pageSize: number; onPage: (p: number) => void }) {
  const { t } = useI18n();
  if (total === 0) return null;
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return (
    <nav className="pager" aria-label={t.common.page(page, totalPages)}>
      <span className="muted num">{t.common.range(from, to, total)}</span>
      {totalPages > 1 && (
        <span className="pager-btns">
          <button type="button" className="btn btn-sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t.common.prev}</button>
          <button type="button" className="btn btn-sm" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>{t.common.next}</button>
        </span>
      )}
    </nav>
  );
}

// ------------------------------------------------------------------ states

export function ErrorBlock({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useI18n();
  return (
    <div className="state-error" role="alert">
      <Icon name="alert" />
      <p>{t.common.loadFailed} {errorText(error, t)}</p>
      {onRetry && <button type="button" className="link" onClick={onRetry}>{t.common.retry}</button>}
    </div>
  );
}

export function Empty({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="empty">
      <p>{children}</p>
      {action}
    </div>
  );
}

/** Rows of grey bars at the size of what is loading; the layout does not move when data lands. */
export function SkeletonRows({ rows = 5, label }: { rows?: number; label?: string }) {
  const { t } = useI18n();
  return (
    <div className="skeleton" role="status" aria-label={label ?? t.common.loading}>
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="sk-row">
          <span className="sk" style={{ width: `${38 + ((i * 17) % 30)}%` }} />
          <span className="sk sk-short" />
        </div>
      ))}
    </div>
  );
}

export function PageHead({ title, lead, actions, back }: { title: ReactNode; lead?: ReactNode; actions?: ReactNode; back?: ReactNode }) {
  return (
    <header className="page-head">
      {back}
      <div className="page-head-row">
        <div className="page-head-text">
          <h1>{title}</h1>
          {lead && <p className="lead">{lead}</p>}
        </div>
        {actions && <div className="page-actions">{actions}</div>}
      </div>
    </header>
  );
}
