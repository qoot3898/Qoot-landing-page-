import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider, strings } from './i18n';
import { ConfirmProvider, ToastProvider } from './ui/feedback';
import { emptyItem, SchemaFields, type FormContext } from './form/SchemaForm';
import { fromLocalInput, toLocalInput } from './pages/NewsEditor';
import type { FieldDef, JsonObject } from './types';

afterEach(cleanup);

function shape(value: unknown): unknown {
  if (typeof value === 'function') return 'fn';
  if (Array.isArray(value)) return value.length;
  if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map((k) => [k, shape((value as Record<string, unknown>)[k])]));
  return typeof value;
}

describe('dashboard wording', () => {
  it('has every string in both languages', () => {
    expect(shape(strings.ar)).toEqual(shape(strings.en));
  });
});

describe('publication dates in Benghazi time', () => {
  it('converts both ways (UTC+2)', () => {
    expect(toLocalInput('2026-10-18T09:00:00Z')).toBe('2026-10-18T11:00');
    expect(fromLocalInput('2026-10-18T11:00')).toBe('2026-10-18T09:00:00.000Z');
    expect(fromLocalInput('2026-01-01T00:30')).toBe('2025-12-31T22:30:00.000Z');
    expect(toLocalInput(null)).toBe('');
    expect(fromLocalInput('nonsense')).toBeNull();
  });
});

const text = (key: string, extra: Partial<FieldDef> = {}): FieldDef => ({
  key, type: 'text', label: { ar: key, en: key }, help: null, localized: true, required: false, maxLength: 80, format: 'none', options: null,
  itemFields: null, itemLabel: null, minItems: 0, maxItems: 50, min: null, max: null, group: null, private: false, allowDrawings: false, ...extra,
});

describe('list items', () => {
  it('start empty, shaped by their fields, with an id the API accepts', () => {
    const item = emptyItem([
      text('label'),
      text('target', { type: 'url', localized: false }),
      text('tone', { type: 'select', localized: false, options: [{ value: 'leaf', label: { ar: 'أ', en: 'Leaf' } }] }),
      text('on', { type: 'bool', localized: false }),
      text('image', { type: 'media', localized: false }),
      text('days', { type: 'days', localized: false }),
    ]);
    expect(item).toMatchObject({ hidden: false, label: { ar: '', en: '' }, target: '', tone: 'leaf', on: false, image: null, days: [] });
    expect(item.id).toMatch(/^[A-Za-z0-9_-]{1,40}$/);
  });
});

function Harness({ fields, initial, ctx, onValue }: { fields: FieldDef[]; initial: JsonObject; ctx?: Partial<FormContext>; onValue: (v: JsonObject) => void }) {
  const [value, setValue] = useState(initial);
  return (
    <SchemaFields
      fields={fields}
      value={value}
      path=""
      ctx={{ errors: {}, missing: new Set(), anchors: [], ...ctx }}
      onChange={(v) => {
        setValue(v);
        onValue(v);
      }}
    />
  );
}

function wrap(ui: React.ReactNode) {
  localStorage.setItem('qoot.admin.lang', 'en');
  return render(
    <I18nProvider>
      <QueryClientProvider client={new QueryClient()}>
        <ToastProvider>
          <ConfirmProvider>{ui}</ConfirmProvider>
        </ToastProvider>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('schema form', () => {
  it('edits Arabic and English separately and shows the API errors on the right field', () => {
    const onValue = vi.fn();
    wrap(
      <Harness
        fields={[text('title', { required: true })]}
        initial={{ title: { ar: 'قوت', en: '' } }}
        ctx={{ errors: { 'title.ar': ['Keep it under 80 characters.'] }, missing: new Set(['title.en']) }}
        onValue={onValue}
      />,
    );
    const inputs = screen.getAllByRole('textbox');
    expect(inputs[0]).toHaveProperty('dir', 'rtl');
    expect(inputs[1]).toHaveProperty('dir', 'ltr');
    expect(screen.getByText('Keep it under 80 characters.')).toBeTruthy();
    expect(screen.getByText('Missing translation')).toBeTruthy();
    fireEvent.change(inputs[1]!, { target: { value: 'Qoot' } });
    expect(onValue).toHaveBeenLastCalledWith({ title: { ar: 'قوت', en: 'Qoot' } });
  });

  it('adds, hides and reorders list items', () => {
    const onValue = vi.fn();
    const list = text('items', { type: 'list', localized: false, itemLabel: 'label', maxItems: 3, itemFields: [text('label')] });
    wrap(<Harness fields={[list]} initial={{ items: [{ id: 'a1', hidden: false, label: { ar: 'أ', en: 'First' } }] }} onValue={onValue} />);
    fireEvent.click(screen.getByRole('button', { name: /Add/ }));
    let items = onValue.mock.lastCall![0].items as JsonObject[];
    expect(items).toHaveLength(2);
    fireEvent.click(screen.getByRole('button', { name: 'Hide on the website: First' }));
    items = onValue.mock.lastCall![0].items as JsonObject[];
    expect(items[0]!.hidden).toBe(true);
    fireEvent.click(screen.getByRole('button', { name: 'Move down: First' }));
    items = onValue.mock.lastCall![0].items as JsonObject[];
    expect(items[1]!.id).toBe('a1');
  });
});
