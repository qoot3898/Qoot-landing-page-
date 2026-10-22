import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useCategories, useDashboard, useNewsList } from '../data';
import { useI18n } from '../i18n';
import { Empty, ErrorBlock, PageHead, Pager, SearchBox, Select, SkeletonRows, Tabs } from '../ui/controls';
import { Icon } from '../ui/icons';
import { NewsStatusPill } from '../ui/status';

type Tab = 'all' | 'published' | 'scheduled' | 'draft';

/** All articles, newest first: each with its cover, its state and the languages it is written in. Filters live in the address. */
export function NewsPage() {
  const { t, pick, date, lang } = useI18n();
  const [params, setParams] = useSearchParams();
  const tab = (params.get('status') as Tab | null) ?? 'all';
  const categoryId = params.get('category') ?? '';
  const page = Number(params.get('page')) || 1;
  const [search, setSearch] = useState(params.get('q') ?? '');
  const dash = useDashboard();
  const categories = useCategories();

  const update = (changes: Record<string, string>) =>
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [k, v] of Object.entries(changes)) {
          if (v) next.set(k, v);
          else next.delete(k);
        }
        return next;
      },
      { replace: true },
    );

  // Search as you type, a moment after the last key.
  useEffect(() => {
    if (search === (params.get('q') ?? '')) return;
    const id = window.setTimeout(() => update({ q: search, page: '' }), 300);
    return () => window.clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const list = useNewsList({
    page,
    pageSize: 20,
    ...(tab !== 'all' ? { status: tab } : {}),
    ...(categoryId ? { categoryId } : {}),
    ...(params.get('q') ? { search: params.get('q')! } : {}),
  });
  const counts = dash.data?.news;
  const filtered = tab !== 'all' || !!categoryId || !!params.get('q');
  const tabs: { value: Tab; label: string; count?: number }[] = [
    { value: 'all', label: t.news.tabs.all, count: counts?.total },
    { value: 'published', label: t.news.tabs.published, count: counts?.published },
    { value: 'draft', label: t.news.tabs.draft, count: counts?.drafts },
  ];
  if ((counts?.scheduled ?? 0) > 0 || tab === 'scheduled') tabs.push({ value: 'scheduled', label: t.news.tabs.scheduled, count: counts?.scheduled });

  return (
    <div className="page">
      <PageHead
        title={t.news.title}
        lead={t.news.lead}
        actions={
          <>
            <Link className="btn" to="/news/categories">{t.news.categories}</Link>
            <Link className="btn btn-primary" to="/news/new"><Icon name="plus" /> {t.news.write}</Link>
          </>
        }
      />
      <div className="toolbar">
        <Tabs<Tab> label={t.news.title} value={tab} onChange={(v) => update({ status: v === 'all' ? '' : v, page: '' })} items={tabs} />
        <span className="toolbar-end">
          <SearchBox value={search} onChange={setSearch} placeholder={t.news.searchPlaceholder} />
          {(categories.data?.length ?? 0) > 0 && (
            <Select
              ariaLabel={t.news.category}
              value={categoryId}
              onChange={(v) => update({ category: v, page: '' })}
              placeholder={t.news.allCategories}
              options={(categories.data ?? []).map((c) => ({ value: String(c.id), label: pick({ ar: c.nameAr, en: c.nameEn }) }))}
            />
          )}
        </span>
      </div>

      {list.isPending ? (
        <SkeletonRows rows={6} />
      ) : list.error ? (
        <ErrorBlock error={list.error} onRetry={() => void list.refetch()} />
      ) : list.data!.items.length === 0 ? (
        <Empty action={!filtered ? <Link className="btn btn-primary" to="/news/new"><Icon name="plus" /> {t.news.write}</Link> : undefined}>{filtered ? t.news.empty : t.news.emptyFirst}</Empty>
      ) : (
        <>
          <p className="muted small num table-count">{t.news.count(list.data!.total)}</p>
          <ul className="news-rows">
            {list.data!.items.map((a) => {
              const title = (lang === 'ar' ? a.titleAr || a.titleEn : a.titleEn || a.titleAr) || t.news.untitled;
              const other = lang === 'ar' ? a.titleEn : a.titleAr;
              return (
                <li key={a.id}>
                  <Link to={`/news/${a.id}`} className="news-row">
                    <span className={`thumb${a.imageUrl ? '' : ' is-empty'}`} aria-hidden="true">
                      {a.imageUrl ? <img src={a.imageUrl} alt="" loading="lazy" /> : <Icon name="media" size={20} />}
                    </span>
                    <span className="news-row-main">
                      <span className="news-row-title" dir="auto">{title}</span>
                      <span className="news-row-sub">
                        {other && other !== title && <span dir="auto" className="truncate">{other}</span>}
                        <span>{a.category ? pick({ ar: a.category.nameAr, en: a.category.nameEn }) : t.news.noCategory}</span>
                        <span className="langs" aria-label={t.news.colLangs}>
                          <span className={a.hasArabic ? 'lang-tag' : 'lang-tag is-missing'} title={a.hasArabic ? t.common.arabic : `${t.common.arabic}: ${t.news.langMissing}`}>{t.common.ar}</span>
                          <span className={a.hasEnglish ? 'lang-tag' : 'lang-tag is-missing'} title={a.hasEnglish ? t.common.english : `${t.common.english}: ${t.news.langMissing}`}>EN</span>
                        </span>
                      </span>
                    </span>
                    <span className="news-row-end">
                      <NewsStatusPill status={a.status} featured={a.isFeatured} />
                      <span className="muted small num">{a.publishedAt ? date(a.publishedAt) : t.common.dash}</span>
                    </span>
                  </Link>
                </li>
              );
            })}
          </ul>
          <Pager page={list.data!.page} totalPages={list.data!.totalPages} total={list.data!.total} pageSize={list.data!.pageSize} onPage={(p) => update({ page: p > 1 ? String(p) : '' })} />
        </>
      )}
    </div>
  );
}
