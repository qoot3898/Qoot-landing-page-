import { useI18n } from '../i18n';
import type { NewsStatus, SectionSummary, SubmissionStatus } from '../types';
import { Pill } from './controls';

/** One status mark per row, in pale pairs. */

export function SectionStatusPill({ section, compact = false }: { section: SectionSummary; compact?: boolean }) {
  const { t } = useI18n();
  if (!section.isVisible) return <Pill kind="neutral" icon="eyeOff">{t.status.hidden}</Pill>;
  if (!section.isPublished) return <Pill kind="neutral">{t.status.draft}</Pill>;
  if (section.hasUnpublishedChanges) return <Pill kind="warn">{t.status.changes}</Pill>;
  return compact ? null : <Pill kind="ok">{t.status.published}</Pill>;
}

export function NewsStatusPill({ status, featured = false }: { status: NewsStatus; featured?: boolean }) {
  const { t } = useI18n();
  const pill = status === 'published' ? <Pill kind="ok">{t.status.published}</Pill> : status === 'scheduled' ? <Pill kind="warn" icon="calendar">{t.status.scheduled}</Pill> : <Pill kind="neutral">{t.status.draft}</Pill>;
  return featured ? <span className="pills">{pill}<Pill kind="brand" icon="star">{t.status.featured}</Pill></span> : pill;
}

export function MessageStatusPill({ status }: { status: SubmissionStatus }) {
  const { t } = useI18n();
  const kind = status === 'new' ? 'brand' : status === 'in_progress' ? 'warn' : status === 'done' ? 'ok' : 'neutral';
  return <Pill kind={kind}>{t.status[status]}</Pill>;
}
