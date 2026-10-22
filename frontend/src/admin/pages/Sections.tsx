import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '../../shared/api';
import { keys, useApiMutation, useSections } from '../data';
import { useI18n } from '../i18n';
import type { SectionSummary } from '../types';
import { ErrorBlock, errorText, PageHead, Pill, SkeletonRows } from '../ui/controls';
import { useConfirm, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';
import { SectionStatusPill } from '../ui/status';
import { spineState } from './Overview';

/** Every section of the home page, in page order: edit, show or hide, move, publish. */
export function SectionsPage() {
  const { t, pick, relative } = useI18n();
  const sections = useSections();
  const client = useQueryClient();
  const toast = useToast();
  const confirm = useConfirm();

  const reorder = useApiMutation((order: string[]) => api<SectionSummary[]>('/api/admin/sections/order', { method: 'PUT', body: { keys: order } }), () => [keys.sections, keys.dashboard]);
  const visibility = useApiMutation(
    ({ key, visible }: { key: string; visible: boolean }) => api<SectionSummary>(`/api/admin/sections/${key}/visibility`, { method: 'PUT', body: { visible } }),
    (v) => [keys.sections, keys.section(v.key), keys.dashboard],
  );
  const publishAll = useApiMutation(() => api('/api/admin/sections/publish-all', { method: 'POST' }), () => [keys.sections, keys.dashboard]);

  const list = sections.data ?? [];
  const movable = list.filter((s) => !s.pinned);
  const pending = list.filter((s) => s.hasUnpublishedChanges).length;

  const move = (key: string, delta: -1 | 1) => {
    const order = movable.map((s) => s.key);
    const from = order.indexOf(key);
    const to = from + delta;
    if (from < 0 || to < 0 || to >= order.length) return;
    [order[from], order[to]] = [order[to]!, order[from]!];
    // Show the new order at once; the server's answer replaces it.
    client.setQueryData<SectionSummary[]>(keys.sections, (old) =>
      old ? [...old].sort((a, b) => rank(a, order) - rank(b, order)) : old,
    );
    reorder.mutate(order, {
      onSuccess: (fresh) => {
        client.setQueryData(keys.sections, fresh);
        toast.show(t.sections.orderSaved);
      },
      onError: (e) => {
        toast.show(errorText(e, t), 'error');
        void client.invalidateQueries({ queryKey: keys.sections });
      },
    });
  };

  const onPublishAll = async () => {
    if (!(await confirm({ title: t.sections.publishAllConfirm, confirm: t.sections.publishAll }))) return;
    publishAll.mutate(undefined, {
      onSuccess: () => toast.show(t.sections.publishedAll),
      onError: (e) => toast.show(errorText(e, t), 'error'),
    });
  };

  return (
    <div className="page">
      <PageHead
        title={t.sections.title}
        lead={t.sections.lead}
        actions={
          <button type="button" className="btn btn-primary" disabled={pending === 0 || publishAll.isPending} onClick={() => void onPublishAll()}>
            {t.sections.publishAll}{pending > 0 && <span className="num"> ({pending})</span>}
          </button>
        }
      />
      {sections.isPending ? (
        <SkeletonRows rows={10} />
      ) : sections.error ? (
        <ErrorBlock error={sections.error} onRetry={() => void sections.refetch()} />
      ) : (
        <ol className="section-list">
          {list.map((s) => {
            const index = movable.findIndex((m) => m.key === s.key);
            return (
              <li key={s.key} className={`section-row${s.isVisible ? '' : ' is-hidden'}`}>
                <span className="spine-mark" data-state={spineState(s)} aria-hidden="true" />
                <div className="section-main">
                  <Link to={`/sections/${s.key}`} className="section-name">{pick(s.name)}</Link>
                  <p className="section-desc">{pick(s.description)}</p>
                </div>
                <div className="section-meta">
                  <SectionStatusPill section={s} />
                  {s.missingTranslations > 0 && <Pill kind="warn">{t.sections.missing(s.missingTranslations)}</Pill>}
                  <span className="muted small">{t.common.updated(relative(s.draftUpdatedAt))}</span>
                </div>
                <div className="section-controls">
                  {s.canHide ? (
                    <VisibilitySwitch
                      checked={s.isVisible}
                      label={`${t.sections.showOnSite}: ${pick(s.name)}`}
                      title={s.isVisible ? t.common.visible : t.common.hidden}
                      onToggle={(visible, undo) =>
                        visibility.mutate(
                          { key: s.key, visible },
                          {
                            onSuccess: (r) => toast.show(r.isVisible ? t.sections.visibilityOn : t.sections.visibilityOff),
                            onError: (err) => {
                              undo();
                              toast.show(errorText(err, t), 'error');
                            },
                          },
                        )
                      }
                    />
                  ) : (
                    <span className="switch-placeholder" />
                  )}
                  {s.pinned ? (
                    <span className="muted small fixed">{t.sections.fixed}</span>
                  ) : (
                    <span className="move">
                      <button type="button" className="icon-btn" disabled={index <= 0 || reorder.isPending} onClick={() => move(s.key, -1)} aria-label={`${t.common.moveUp}: ${pick(s.name)}`}>
                        <Icon name="up" />
                      </button>
                      <button type="button" className="icon-btn" disabled={index < 0 || index >= movable.length - 1 || reorder.isPending} onClick={() => move(s.key, 1)} aria-label={`${t.common.moveDown}: ${pick(s.name)}`}>
                        <Icon name="down" />
                      </button>
                    </span>
                  )}
                  <Link to={`/sections/${s.key}`} className="btn btn-sm">{t.common.edit}</Link>
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </div>
  );
}

function rank(s: SectionSummary, order: string[]): number {
  if (s.pinned) return s.sortOrder === 0 ? -1 : 100_000;
  return order.indexOf(s.key);
}

/** The switch moves at once; it goes back if the server refuses. */
function VisibilitySwitch({ checked, label, title, onToggle }: { checked: boolean; label: string; title: string; onToggle: (visible: boolean, undo: () => void) => void }) {
  const [on, setOn] = useState(checked);
  useEffect(() => setOn(checked), [checked]);
  return (
    <label className="switch-inline" title={title}>
      <input
        type="checkbox"
        role="switch"
        className="switch"
        checked={on}
        onChange={(e) => {
          const next = e.target.checked;
          setOn(next);
          onToggle(next, () => setOn(!next));
        }}
      />
      <span className="sr-only">{label}</span>
    </label>
  );
}
