import { useId, useState } from 'react';
import { SECTOR_ICONS } from '../../public/drawings';
import { useI18n } from '../i18n';
import type { Bilingual, FieldDef, Json, JsonObject } from '../types';
import { BiText, Field, Select, TextArea, TextInput, Toggle } from '../ui/controls';
import { useConfirm } from '../ui/feedback';
import { Icon } from '../ui/icons';
import { MediaField } from './MediaPicker';

/**
 * Builds the editing form for a section (or the site settings) from its schema, the same schema the
 * API validates against. Every text is edited in both languages side by side; lists can be added
 * to, reordered, hidden and removed. Nothing here accepts HTML.
 */
export interface FormContext {
  errors: Record<string, string[]>;
  missing: Set<string>;
  anchors: string[];
  disabled?: boolean;
}

export function SchemaFields({ fields, value, onChange, path, ctx, group }: {
  fields: FieldDef[];
  value: JsonObject;
  onChange: (value: JsonObject) => void;
  path: string;
  ctx: FormContext;
  group?: string | null;
}) {
  const shown = group === undefined ? fields : fields.filter((f) => (f.group ?? null) === group);
  return (
    <div className="fields">
      {shown.map((field) => (
        <FieldControl
          key={field.key}
          field={field}
          value={value[field.key] ?? null}
          onChange={(v) => onChange({ ...value, [field.key]: v })}
          path={path ? `${path}.${field.key}` : field.key}
          ctx={ctx}
        />
      ))}
    </div>
  );
}

function firstError(ctx: FormContext, path: string): string | undefined {
  return ctx.errors[path]?.[0];
}

function asBilingual(v: Json): Bilingual {
  if (v && typeof v === 'object' && !Array.isArray(v)) return { ar: String(v.ar ?? ''), en: String(v.en ?? '') };
  return { ar: typeof v === 'string' ? v : '', en: '' };
}

function FieldControl({ field, value, onChange, path, ctx }: { field: FieldDef; value: Json; onChange: (v: Json) => void; path: string; ctx: FormContext }) {
  const { t, pick } = useI18n();
  const id = useId();
  const label = pick(field.label);
  const help = field.help ? pick(field.help) : field.private ? t.form.privateField : null;
  const err = firstError(ctx, path);

  switch (field.type) {
    case 'text':
    case 'textArea': {
      if (field.localized) {
        return (
          <BiText
            label={label}
            value={asBilingual(value)}
            onChange={(v) => onChange({ ar: v.ar, en: v.en })}
            multiline={field.type === 'textArea'}
            maxLength={field.maxLength}
            required={field.required}
            help={help}
            disabled={ctx.disabled}
            errors={{ ar: firstError(ctx, `${path}.ar`) ?? err, en: firstError(ctx, `${path}.en`) }}
            missing={{ ar: ctx.missing.has(`${path}.ar`), en: ctx.missing.has(`${path}.en`) }}
          />
        );
      }
      const text = typeof value === 'string' ? value : '';
      const ltr = field.format !== 'none';
      const type = field.format === 'email' ? 'email' : field.format === 'phone' ? 'tel' : field.format === 'absoluteUrl' ? 'url' : 'text';
      return (
        <Field label={<>{label}{field.required && <span className="req">{t.common.required}</span>}</>} help={help} error={err} htmlFor={id} counter={field.type === 'textArea' ? t.common.chars(text.length, field.maxLength) : undefined}>
          {field.type === 'textArea' ? (
            <TextArea id={id} value={text} dir="auto" disabled={ctx.disabled} aria-invalid={err ? true : undefined} onChange={(e) => onChange(e.target.value)} />
          ) : (
            <TextInput id={id} type={type} value={text} dir={ltr ? 'ltr' : 'auto'} disabled={ctx.disabled} aria-invalid={err ? true : undefined} onChange={(e) => onChange(e.target.value)} />
          )}
        </Field>
      );
    }
    case 'url': {
      const text = typeof value === 'string' ? value : '';
      const listId = `${id}-targets`;
      return (
        <Field label={<>{label}{field.required && <span className="req">{t.common.required}</span>}</>} help={help} error={err} htmlFor={id}>
          <TextInput id={id} value={text} dir="ltr" list={listId} disabled={ctx.disabled} aria-invalid={err ? true : undefined} onChange={(e) => onChange(e.target.value)} spellCheck={false} />
          <datalist id={listId}>
            {ctx.anchors.map((a) => <option key={a} value={a} />)}
          </datalist>
        </Field>
      );
    }
    case 'media':
      return (
        <MediaField
          label={label}
          value={typeof value === 'number' ? value : null}
          onChange={(v) => onChange(v)}
          allowDrawings={field.allowDrawings}
          required={field.required}
          error={err}
        />
      );
    case 'select':
      return (
        <Field label={label} help={help} error={err} htmlFor={id}>
          <Select
            id={id}
            value={typeof value === 'string' ? value : ''}
            onChange={(v) => onChange(v)}
            disabled={ctx.disabled}
            placeholder={field.required ? undefined : t.form.selectPlaceholder}
            options={(field.options ?? []).map((o) => ({ value: o.value, label: pick(o.label) }))}
          />
        </Field>
      );
    case 'icon': {
      const current = typeof value === 'string' ? value : '';
      return (
        <Field label={label} help={help} error={err} htmlFor={id}>
          <div className="icon-select">
            <span className="icon-preview" aria-hidden="true">{current && SECTOR_ICONS[current] ? <img src={iconDataUri(SECTOR_ICONS[current]!)} alt="" /> : null}</span>
            <Select id={id} value={current} onChange={(v) => onChange(v)} disabled={ctx.disabled} options={(field.options ?? []).map((o) => ({ value: o.value, label: pick(o.label) }))} />
          </div>
        </Field>
      );
    }
    case 'multiSelect': {
      const chosen = Array.isArray(value) ? value.map(String) : [];
      return (
        <fieldset className={`field${err ? ' has-error' : ''}`}>
          <legend className="field-label">{label}</legend>
          <div className="checks">
            {(field.options ?? []).map((o) => (
              <label key={o.value} className="check">
                <input
                  type="checkbox"
                  checked={chosen.includes(o.value)}
                  disabled={ctx.disabled}
                  onChange={(e) => onChange(e.target.checked ? [...chosen, o.value] : chosen.filter((c) => c !== o.value))}
                />
                <span>{pick(o.label)}</span>
              </label>
            ))}
          </div>
          {err ? <p className="field-error">{err}</p> : help ? <p className="field-help">{help}</p> : null}
        </fieldset>
      );
    }
    case 'bool':
      return <Toggle label={label} description={help} checked={value === true} disabled={ctx.disabled} onChange={(v) => onChange(v)} />;
    case 'number':
      return (
        <Field label={label} help={help} error={err} htmlFor={id}>
          <TextInput
            id={id}
            type="number"
            inputMode="numeric"
            className="input-num num"
            min={field.min ?? undefined}
            max={field.max ?? undefined}
            value={typeof value === 'number' ? value : ''}
            disabled={ctx.disabled}
            onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}
          />
        </Field>
      );
    case 'time':
      return (
        <Field label={label} help={help} error={err} htmlFor={id}>
          <TextInput id={id} type="time" dir="ltr" className="input-time num" value={typeof value === 'string' ? value : ''} disabled={ctx.disabled} onChange={(e) => onChange(e.target.value)} />
        </Field>
      );
    case 'days': {
      const days = Array.isArray(value) ? value.map(Number) : [];
      return (
        <fieldset className={`field${err ? ' has-error' : ''}`}>
          <legend className="field-label">{label}</legend>
          <div className="day-chips">
            {t.form.daysShort.map((name, day) => (
              <label key={day} className="day-chip">
                <input
                  type="checkbox"
                  checked={days.includes(day)}
                  disabled={ctx.disabled}
                  onChange={(e) => onChange(e.target.checked ? [...days, day].sort((a, b) => a - b) : days.filter((d) => d !== day))}
                />
                <span title={t.form.days[day]}>{name}</span>
              </label>
            ))}
          </div>
          {err ? <p className="field-error">{err}</p> : help ? <p className="field-help">{help}</p> : null}
        </fieldset>
      );
    }
    case 'list':
      return <ListField field={field} value={Array.isArray(value) ? (value as JsonObject[]) : []} onChange={onChange} path={path} ctx={ctx} />;
    default:
      return null;
  }
}

function iconDataUri(inner: string): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" fill="none" stroke="#004949" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">${inner}</svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

/** A new, empty item for a list, shaped by its fields. */
export function emptyItem(fields: FieldDef[]): JsonObject {
  const item: JsonObject = { id: newId(), hidden: false };
  for (const f of fields) {
    switch (f.type) {
      case 'text':
      case 'textArea':
        item[f.key] = f.localized ? { ar: '', en: '' } : '';
        break;
      case 'url':
      case 'time':
        item[f.key] = '';
        break;
      case 'select':
      case 'icon':
        item[f.key] = f.options?.[0]?.value ?? '';
        break;
      case 'bool':
        item[f.key] = false;
        break;
      case 'number':
        item[f.key] = f.min ?? 0;
        break;
      case 'media':
        item[f.key] = null;
        break;
      default:
        item[f.key] = [];
    }
  }
  return item;
}

function newId(): string {
  const bytes = new Uint8Array(6);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}

function itemTitle(field: FieldDef, item: JsonObject, index: number, pick: (v: Bilingual) => string): string {
  const key = field.itemLabel;
  const raw = key ? item[key] : null;
  const option = key ? field.itemFields?.find((f) => f.key === key)?.options?.find((o) => o.value === raw) : undefined;
  if (option) return pick(option.label);
  const text = raw && typeof raw === 'object' && !Array.isArray(raw) ? pick(asBilingual(raw)) : typeof raw === 'string' ? raw : '';
  return text || `#${index + 1}`;
}

function ListField({ field, value, onChange, path, ctx }: { field: FieldDef; value: JsonObject[]; onChange: (v: Json) => void; path: string; ctx: FormContext }) {
  const { t, pick } = useI18n();
  const confirm = useConfirm();
  const [open, setOpen] = useState<Set<string>>(() => new Set());
  const listError = firstError(ctx, path);
  const fields = field.itemFields ?? [];

  const update = (index: number, item: JsonObject) => onChange(value.map((v, i) => (i === index ? item : v)));
  const move = (index: number, delta: number) => {
    const next = [...value];
    const target = index + delta;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target]!, next[index]!];
    onChange(next);
  };
  const toggle = (key: string) =>
    setOpen((s) => {
      const n = new Set(s);
      if (n.has(key)) n.delete(key);
      else n.add(key);
      return n;
    });

  return (
    <fieldset className={`field list-field${listError ? ' has-error' : ''}`}>
      <legend className="field-label">
        <span>{pick(field.label)}</span>
        <span className="muted small num">{t.form.itemsCount(value.length, field.maxItems)}</span>
      </legend>
      {field.help && <p className="field-help">{pick(field.help)}</p>}
      {listError && <p className="field-error" role="alert">{listError}</p>}
      {value.length === 0 && <p className="empty-line">{t.form.emptyList}</p>}
      <ol className="items">
        {value.map((item, index) => {
          const key = typeof item.id === 'string' ? item.id : String(index);
          const itemPath = `${path}[${index}]`;
          const hasError = Object.keys(ctx.errors).some((p) => p.startsWith(`${itemPath}.`) || p.startsWith(`${itemPath}[`));
          const hasMissing = [...ctx.missing].some((p) => p.startsWith(`${itemPath}.`));
          const isOpen = open.has(key) || hasError;
          const hidden = item.hidden === true;
          const title = itemTitle(field, item, index, pick);
          return (
            <li key={key} className={`item${isOpen ? ' is-open' : ''}${hidden ? ' is-hidden' : ''}${hasError ? ' has-error' : ''}`}>
              <div className="item-head">
                <button type="button" className="item-toggle" aria-expanded={isOpen} onClick={() => toggle(key)}>
                  <Icon name="chevron" className="chev" />
                  <span className="item-index num">{index + 1}</span>
                  <span className="item-title" dir="auto">{title}</span>
                  {hidden && <span className="pill pill-neutral"><Icon name="eyeOff" size={12} />{t.form.itemHidden}</span>}
                  {hasMissing && !hasError && <span className="pill pill-warn">{t.common.missingTranslation}</span>}
                  {hasError && <span className="pill pill-bad">{t.sections.fieldsNeedAttention}</span>}
                </button>
                <span className="item-actions">
                  <button type="button" className="icon-btn" disabled={index === 0 || ctx.disabled} onClick={() => move(index, -1)} aria-label={`${t.common.moveUp}: ${title}`}>
                    <Icon name="up" />
                  </button>
                  <button type="button" className="icon-btn" disabled={index === value.length - 1 || ctx.disabled} onClick={() => move(index, 1)} aria-label={`${t.common.moveDown}: ${title}`}>
                    <Icon name="down" />
                  </button>
                  <button type="button" className="icon-btn" disabled={ctx.disabled} onClick={() => update(index, { ...item, hidden: !hidden })} aria-label={`${hidden ? t.form.showItem : t.form.hideItem}: ${title}`} title={hidden ? t.form.showItem : t.form.hideItem}>
                    <Icon name={hidden ? 'eye' : 'eyeOff'} />
                  </button>
                  <button
                    type="button"
                    className="icon-btn danger"
                    disabled={ctx.disabled}
                    aria-label={`${t.common.remove}: ${title}`}
                    title={t.common.remove}
                    onClick={async () => {
                      if (await confirm({ title: t.form.removeItemConfirm, text: title, confirm: t.common.remove, danger: true })) {
                        onChange(value.filter((_, i) => i !== index));
                      }
                    }}
                  >
                    <Icon name="trash" />
                  </button>
                </span>
              </div>
              {isOpen && (
                <div className="item-body">
                  <SchemaFields fields={fields} value={item} onChange={(v) => update(index, v)} path={itemPath} ctx={ctx} />
                </div>
              )}
            </li>
          );
        })}
      </ol>
      {value.length < field.maxItems && !ctx.disabled && (
        <button
          type="button"
          className="btn btn-sm add-item"
          onClick={() => {
            const item = emptyItem(fields);
            onChange([...value, item]);
            setOpen((s) => new Set(s).add(item.id as string));
          }}
        >
          <Icon name="plus" /> {t.form.addItem}
        </button>
      )}
    </fieldset>
  );
}
