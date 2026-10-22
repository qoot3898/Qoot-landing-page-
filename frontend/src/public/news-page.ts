import './styles/site.css';
import './styles/additions.css';
import { api, ApiError } from '../shared/api';
import type { Article, BodyBlock, Lang, NewsCard, NewsPage, NewsSectionData, SitePayload } from '../shared/types';
import { html, inlineText, type Markup, picture } from './html';
import { type UiStrings } from './i18n';
import { initHeader, initLanguageSwitch, initReveal } from './interactions';
import { arrive, articleHref, cardLabels, early, emptyState, errorState, initCards, newsCard, newsGrid, newsMeta, storyPicture, storyTone } from './news-ui';
import { ARROW, newsSkeleton, sectionData } from './render';
import { applySeo } from './seo';
import { chrome, context, loadSite, previewBanner, redirectIfLanguageOff, setup, showLoadError, showLoading } from './shell';

/** /news/ (the list) and /news/{slug} (one article), in both languages. */

const PAGE_SIZE = 9;

function route(): { slug: string | null } {
  const match = /^\/(?:en\/)?news\/([^/?#]+)\/?$/.exec(location.pathname);
  return { slug: match?.[1] ? decodeURIComponent(match[1]) : null };
}

async function start(): Promise<void> {
  const { lang, ui, preview, app } = setup();
  const { slug } = route();
  const previewArticle = preview ? Number(new URLSearchParams(location.search).get('article')) || null : null;
  try {
    const { site, previewDenied } = await loadSite(lang, preview);
    if (redirectIfLanguageOff(site, lang)) return;
    const newsHome = lang === 'ar' ? '/news/' : '/en/news/';
    const ctx = context(site, lang, 'news', (other) => {
      const base = other === 'ar' ? '/news/' : '/en/news/';
      return slug ? base + encodeURIComponent(slug) : base;
    });
    const { header, footer } = chrome(site, ctx);
    const banner = preview ? previewBanner(ui, previewDenied, location.pathname) : html``;
    app.innerHTML = html`${banner}${header}<main id="main" class="news-main"><div class="page-state" role="status"><span class="page-state-q" aria-hidden="true"></span><p>${ui.loading}</p></div></main>${footer}`.value;
    initHeader();
    initLanguageSwitch();
    const main = document.getElementById('main')!;

    if (previewArticle && !previewDenied) {
      await showArticle(main, () => api<Article>(`/api/admin/preview/news/${previewArticle}?lang=${lang}`, { lang }), lang, ui, newsHome, site, true);
    } else if (slug) {
      await showArticle(main, () => early<Article>(`/api/public/news/${encodeURIComponent(slug)}?lang=${lang}`, lang), lang, ui, newsHome, site, preview);
    } else {
      await showList(main, lang, ui, site, preview);
    }
  } catch {
    showLoadError(app, ui, () => {
      showLoading(app, ui);
      void start();
    });
  }
}

// ------------------------------------------------------------------ list

async function showList(main: HTMLElement, lang: Lang, ui: UiStrings, site: SitePayload, preview: boolean): Promise<void> {
  const section = sectionData(site, 'news')?.data as NewsSectionData | undefined;
  const params = new URLSearchParams(location.search);
  let category = params.get('category') ?? '';
  let page = 0;
  let total = 0;
  let shown = 0;
  const labels = cardLabels(ui, section?.readMoreLabel);
  const listPath = (p: number) => `/api/public/news?lang=${lang}&page=${p}&pageSize=${PAGE_SIZE}${category ? `&category=${encodeURIComponent(category)}` : ''}`;

  // The categories and the first page are asked for together (boot.js may have started both).
  const firstPage = early<NewsPage>(listPath(1), lang);
  firstPage.catch(() => undefined);
  const categories = await early<{ items: { slug: string; name: string }[] }>(`/api/public/news/categories?lang=${lang}`, lang).catch(() => ({ items: [] }));
  main.innerHTML = html`<section class="news-page on-cream" aria-labelledby="news-page-title" data-reveal>
<div class="news-page-head"><h1 id="news-page-title"><span class="m"><span>${section?.title || ui.allNews}</span></span></h1>${
    section?.intro ? html`<p class="news-intro" data-r style="--i:1">${section.intro}</p>` : ''
  }</div>
${
  categories.items.length
    ? html`<div class="news-filter" role="group" aria-label="${ui.filterBy}" data-r style="--i:2"><button type="button" class="chip" data-cat="" aria-pressed="${category === '' ? 'true' : 'false'}">${ui.allCategories}</button>${categories.items.map(
        (c) => html`<button type="button" class="chip" data-cat="${c.slug}" aria-pressed="${category === c.slug ? 'true' : 'false'}">${c.name}</button>`,
      )}</div>`
    : ''
}
<div class="news-index" aria-live="polite">${newsSkeleton(ui, 3)}</div>
<div class="news-more"><p class="news-count"></p><button class="btn" type="button" hidden><span>${ui.showMore}</span></button></div>
</section>`.value;
  initReveal(main);

  const list = main.querySelector<HTMLElement>('.news-index')!;
  const more = main.querySelector<HTMLButtonElement>('.news-more button')!;
  const count = main.querySelector<HTMLElement>('.news-count')!;
  initCards(list);

  // Page one is drawn whole; "Show more" adds cards to the grid without redrawing what is there.
  const draw = (added: NewsCard[], reset: boolean) => {
    if (reset) shown = 0;
    if (reset && !added.length) {
      list.innerHTML = emptyState(category ? ui.noStories : section?.emptyText || ui.noStories).value;
    } else if (reset) {
      list.innerHTML = newsGrid(added, lang, labels, { level: 2, id: 'news', eager: 3 }).value;
    } else {
      const grid = list.querySelector('.nc-grid');
      grid?.insertAdjacentHTML('beforeend', html`${added.map((c, k) => newsCard(c, lang, labels, { level: 2, id: `news-${shown + k}`, index: k }))}`.value);
      grid?.setAttribute('data-n', String(Math.min(shown + added.length, 3)));
    }
    shown += added.length;
    initCards(list);
    count.textContent = shown && total > shown ? ui.showing(shown, total) : '';
    more.hidden = shown >= total;
  };

  const load = async (reset: boolean, first?: Promise<NewsPage>) => {
    if (reset) {
      page = 0;
      if (!first) list.innerHTML = newsSkeleton(ui, 3).value;
    }
    more.disabled = true;
    list.setAttribute('aria-busy', 'true');
    try {
      const result = await (first ?? api<NewsPage>(listPath(page + 1), { lang }));
      page = result.page;
      total = result.total;
      if (reset) applySeo(result.seo, { noindex: preview || category !== '' });
      draw(result.items, reset);
    } catch (error) {
      list.innerHTML = errorState(error instanceof ApiError && error.isNetwork ? `${ui.newsError} ${ui.pageErrorHint}` : ui.newsError, ui.retry).value;
      list.querySelector('[data-retry]')?.addEventListener('click', () => void load(true), { once: true });
    } finally {
      more.disabled = false;
      list.removeAttribute('aria-busy');
    }
  };

  more.addEventListener('click', () => void load(false));
  main.querySelectorAll<HTMLButtonElement>('.news-filter .chip').forEach((chip) =>
    chip.addEventListener('click', () => {
      category = chip.dataset.cat ?? '';
      main.querySelectorAll('.news-filter .chip').forEach((c) => c.setAttribute('aria-pressed', c === chip ? 'true' : 'false'));
      const url = new URL(location.href);
      if (category) url.searchParams.set('category', category);
      else url.searchParams.delete('category');
      history.replaceState(null, '', url);
      void load(true);
    }),
  );
  await load(true, firstPage);
}

// ------------------------------------------------------------------ article

function block(b: BodyBlock): Markup {
  switch (b.type) {
    case 'paragraph':
      return html`<p>${b.text.split('\n').map((line, i) => html`${i > 0 ? html`<br>` : ''}${inlineText(line)}`)}</p>`;
    case 'heading':
      return b.level === 3 ? html`<h3>${b.text}</h3>` : html`<h2>${b.text}</h2>`;
    case 'list': {
      const items = b.items.map((i) => html`<li>${inlineText(i)}</li>`);
      return b.style === 'number' ? html`<ol>${items}</ol>` : html`<ul>${items}</ul>`;
    }
    case 'quote':
      return html`<blockquote><p>${inlineText(b.text)}</p>${b.cite ? html`<cite>${b.cite}</cite>` : ''}</blockquote>`;
    case 'image':
      return html`<figure class="article-image">${picture(b.image, { sizes: '(max-width: 720px) 100vw, 760px' })}${b.caption ? html`<figcaption>${b.caption}</figcaption>` : ''}</figure>`;
    default:
      return html``;
  }
}

async function showArticle(
  main: HTMLElement, fetchArticle: () => Promise<Article>, lang: Lang, ui: UiStrings, newsHome: string, site: SitePayload, preview: boolean,
): Promise<void> {
  let article: Article;
  try {
    article = await fetchArticle();
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      document.title = `${ui.notFoundTitle} | ${site.settings.siteName}`;
      main.innerHTML = html`<section class="article-missing on-cream"><h1>${ui.notFoundTitle}</h1><p>${ui.notFoundText}</p><a class="btn" href="${newsHome}"><span>${ui.backToNews}</span>${ARROW}</a></section>`.value;
      return;
    }
    main.innerHTML = errorState(ui.newsError, ui.retry).value;
    main.querySelector('[data-retry]')?.addEventListener('click', () => void showArticle(main, fetchArticle, lang, ui, newsHome, site, preview), { once: true });
    return;
  }

  applySeo(article.seo, { noindex: preview || article.preview === true });
  const bodyDir = article.contentLang === 'ar' ? 'rtl' : 'ltr';
  const tone = storyTone(article);
  main.innerHTML = html`<article class="article on-cream" aria-labelledby="article-title">
<nav class="article-top" aria-label="${ui.breadcrumb}"><a class="go u back" href="${newsHome}">${ARROW} ${ui.backToNews}</a></nav>
<header class="article-hero" data-tone="${tone}" data-arrive><div class="story-text"><div class="story-words"><h1 class="story-title" id="article-title" dir="auto" data-a style="--i:1">${article.title}</h1>${
    article.summary ? html`<p class="article-lede" dir="auto" data-a style="--i:2">${article.summary}</p>` : ''
  }<div class="story-foot" data-a style="--i:3">${newsMeta(article, lang, { exact: true })}</div></div></div><figure class="story-photo${article.image ? '' : ' is-drawn'}" data-a="photo">${
    article.image ? picture(article.image, { sizes: '(max-width: 720px) 100vw, 56vw', priority: true }) : storyPicture(article, '56vw')
  }</figure></header>
${article.contentLang !== lang ? html`<p class="article-note" lang="${lang}">${ui.readingIn}</p>` : ''}
<div class="article-body" lang="${article.contentLang}" dir="${bodyDir}" data-arrive>${article.body.map(block)}</div>
<div class="article-foot"><button class="btn btn-quiet" type="button" data-copy><span>${ui.copyLink}</span></button><a class="go u" href="${newsHome}">${ui.backToNews} ${ARROW}</a></div>
</article>
${
  article.related?.length
    ? html`<aside class="article-more" aria-labelledby="more-title"><div class="article-more-in"><h2 id="more-title">${ui.moreNews}</h2>${newsGrid(article.related, lang, cardLabels(ui), { level: 3, id: 'more' })}</div></aside>`
    : ''
}`.value;
  arrive(main);
  const moreNews = main.querySelector<HTMLElement>('.article-more');
  if (moreNews) initCards(moreNews);

  const copy = main.querySelector<HTMLButtonElement>('[data-copy]');
  copy?.addEventListener('click', async () => {
    const url = article.seo?.canonical ?? location.origin + articleHref(article.slug, lang);
    try {
      if (navigator.share && matchMedia('(pointer: coarse)').matches) {
        await navigator.share({ title: article.title, url });
        return;
      }
      await navigator.clipboard.writeText(url);
      copy.querySelector('span')!.textContent = ui.copied;
      window.setTimeout(() => (copy.querySelector('span')!.textContent = ui.copyLink), 2400);
    } catch {
      /* the visitor cancelled sharing, or the clipboard is unavailable */
    }
  });
}

void start();
