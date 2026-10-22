import { Link } from 'react-router-dom';
import { api } from '../../shared/api';
import { useCurrentUser } from '../auth';
import { keys, useApiMutation, useDashboard, useNewsList, useSections } from '../data';
import { useI18n } from '../i18n';
import type { SectionSummary } from '../types';
import { Bars } from '../ui/Bars';
import { Empty, ErrorBlock, errorText, PageHead, SkeletonRows } from '../ui/controls';
import { useConfirm, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';
import { MessageStatusPill, NewsStatusPill, SectionStatusPill } from '../ui/status';
import { Initials } from './Messages';

/**
 * The overview answers one question: what is waiting? Contact us messages first (the most frequent
 * job is calling someone back), then the news, and on the side the home page itself as a spine of
 * its sections.
 */
export function OverviewPage() {
  const { t, pick, relative, number, bytes, lang, date } = useI18n();
  const me = useCurrentUser();
  const dash = useDashboard();
  const sections = useSections();
  const news = useNewsList({ sort: 'updated', pageSize: 4 });
  const confirm = useConfirm();
  const toast = useToast();
  const publishAll = useApiMutation(() => api('/api/admin/sections/publish-all', { method: 'POST' }), () => [keys.sections, keys.dashboard]);

  const d = dash.data;
  const finding = d ? t.overview.finding(d.messages.new, d.sections.withUnpublishedChanges) : ' ';

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
        title={t.overview.title}
        lead={finding}
        actions={<Link className="btn btn-primary" to="/news/new">{t.overview.writeNews}</Link>}
      />

      <div className="ov-grid">
        <div className="ov-main">
          <section className="card" aria-labelledby="ov-requests">
            <header className="card-head">
              <h2 id="ov-requests">{t.overview.requests}</h2>
              <Link className="link" to="/messages">{t.overview.openInbox}</Link>
            </header>
            {dash.isPending ? (
              <SkeletonRows rows={4} />
            ) : dash.error ? (
              <ErrorBlock error={dash.error} onRetry={() => void dash.refetch()} />
            ) : (
              <>
                <div className="ov-requests">
                  <div className="figure">
                    <span className="figure-value num">{number(d!.messages.new)}</span>
                    <span className="figure-label">{t.overview.requestsWaiting}</span>
                    <span className="figure-note">{t.overview.requests30(d!.messages.last30Days)}</span>
                  </div>
                  <div className="ov-chart">
                    <p className="chart-title">{t.overview.requests14}</p>
                    <Bars data={d!.requestsPerDay} label={t.overview.requests14} />
                  </div>
                </div>
                {d!.recentMessages.length === 0 ? (
                  <Empty>{t.overview.noOpenRequests}</Empty>
                ) : (
                  <ul className="rows">
                    {d!.recentMessages.slice(0, 4).map((m) => (
                      <li key={m.id}>
                        <Link to={`/messages/${m.id}`} className={`row-link${m.isRead ? '' : ' is-unread'}`}>
                          <Initials name={m.name} />
                          <span className="row-main">
                            <span className="row-title" dir="auto">{m.name}</span>
                            <span className="row-sub" dir="auto">{m.message}</span>
                          </span>
                          <span className="row-end">
                            <MessageStatusPill status={m.status} />
                            <span className="muted small">{relative(m.createdAt)}</span>
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                )}
              </>
            )}
          </section>

          <section className="card" aria-labelledby="ov-news">
            <header className="card-head">
              <h2 id="ov-news">{t.overview.news}</h2>
              <Link className="link" to="/news">{t.overview.allNews}</Link>
            </header>
            {d && <p className="card-sub num">{t.overview.newsCounts(d.news.published, d.news.drafts, d.news.scheduled)}</p>}
            {news.isPending ? (
              <SkeletonRows rows={4} />
            ) : news.error ? (
              <ErrorBlock error={news.error} onRetry={() => void news.refetch()} />
            ) : news.data!.items.length === 0 ? (
              <Empty action={<Link className="btn" to="/news/new">{t.overview.writeNews}</Link>}>{t.overview.noNews}</Empty>
            ) : (
              <ul className="rows">
                {news.data!.items.map((a) => (
                  <li key={a.id}>
                    <Link to={`/news/${a.id}`} className="row-link">
                      <span className={`thumb thumb-wide${a.imageUrl ? '' : ' is-empty'}`} aria-hidden="true">{a.imageUrl ? <img src={a.imageUrl} alt="" loading="lazy" /> : <Icon name="news" size={18} />}</span>
                      <span className="row-main">
                        <span className="row-title" dir="auto">{(lang === 'ar' ? a.titleAr || a.titleEn : a.titleEn || a.titleAr) || a.slug}</span>
                        <span className="row-sub">{[a.category ? pick({ ar: a.category.nameAr, en: a.category.nameEn }) : '', a.publishedAt ? date(a.publishedAt) : ''].filter(Boolean).join(' · ') || t.common.dash}</span>
                      </span>
                      <span className="row-end">
                        <NewsStatusPill status={a.status} featured={a.isFeatured} />
                        <span className="muted small">{relative(a.updatedAt)}</span>
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>

        <div className="ov-rail">
          <h2 className="rail-head">{t.overview.website}</h2>
          <section className="card spine-card">
            {sections.isPending ? (
              <SkeletonRows rows={6} />
            ) : sections.error ? (
              <ErrorBlock error={sections.error} onRetry={() => void sections.refetch()} />
            ) : (
              <>
                <ol className="spine">
                  {sections.data!.map((s) => (
                    <li key={s.key}>
                      <Link to={`/sections/${s.key}`} className="spine-row">
                        <span className="spine-mark" data-state={spineState(s)} aria-hidden="true" />
                        <span className="spine-name">{pick(s.name)}</span>
                        <SectionStatusPill section={s} compact />
                      </Link>
                    </li>
                  ))}
                </ol>
                <footer className="card-foot">
                  <span className="muted small num">{t.overview.websiteSub(sections.data!.filter((s) => s.isVisible).length, sections.data!.length)}</span>
                  {sections.data!.some((s) => s.hasUnpublishedChanges) && (
                    <button type="button" className="btn btn-sm" onClick={() => void onPublishAll()} disabled={publishAll.isPending}>
                      {t.overview.publishAll}
                    </button>
                  )}
                </footer>
              </>
            )}
          </section>

          {d && (
            <>
              <h2 className="rail-head">{t.overview.media}</h2>
              <section className="card card-line">
                <span className="num">{t.overview.mediaLine(d.media.uploads, bytes(d.media.uploadBytes))}</span>
                <Link className="link" to="/media">{t.nav.media}</Link>
              </section>
            </>
          )}

          {me.role === 'Admin' && d?.recentActivity && (
            <>
              <h2 className="rail-head">{t.overview.activity}</h2>
              <section className="card">
                <ul className="activity">
                  {d.recentActivity.slice(0, 5).map((a) => (
                    <li key={a.id}>
                      <span className="activity-what" dir="auto">{a.summary}</span>
                      <span className="muted small">{a.userEmail} · {relative(a.at)}</span>
                    </li>
                  ))}
                </ul>
                <footer className="card-foot">
                  <Link className="link" to="/audit">{t.overview.allActivity}</Link>
                </footer>
              </section>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

export function spineState(s: SectionSummary): 'hidden' | 'changes' | 'live' | 'draft' {
  if (!s.isVisible) return 'hidden';
  if (!s.isPublished) return 'draft';
  if (s.hasUnpublishedChanges) return 'changes';
  return 'live';
}
