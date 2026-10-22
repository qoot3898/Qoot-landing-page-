import { api, ApiError } from '../shared/api';
import type { Lang, NewsCard, NewsPage } from '../shared/types';
import { html, inlineText, type Markup, paragraphs, picture } from './html';
import { formatDate, type UiStrings } from './i18n';
import { CHEVRON, newsSkeleton } from './render';

/**
 * The news, shared by the home section, the news page and "More news" under an article.
 *
 * Every story is a card: its picture, its headline, its category and date, and its words. A long
 * story is folded to a few lines with a "Read more" button that opens it in place, so a visitor
 * reads the news without opening one story after another. A category keeps its colour everywhere.
 */

export function articleHref(slug: string, lang: Lang): string {
  return (lang === 'ar' ? '/news/' : '/en/news/') + encodeURIComponent(slug);
}

/** How many stories the home page asks for (the most the dashboard allows); it shows its own count. */
export const HOME_PAGE_SIZE = 6;

export function homeNewsPath(lang: Lang): string {
  return `/api/public/news?lang=${lang}&page=1&pageSize=${HOME_PAGE_SIZE}`;
}

/**
 * A request boot.js started while the page's scripts were still downloading (the home page's
 * news, the news list, an article), used once if the path matches exactly; otherwise a normal
 * request.
 */
export async function early<T>(path: string, lang: Lang): Promise<T> {
  const pending = window.__qootEarly?.[path];
  if (pending) {
    delete window.__qootEarly![path];
    try {
      const response = await pending;
      if (response.ok) return (await response.json()) as T;
    } catch {
      /* fall through to a normal request */
    }
  }
  return api<T>(path, { lang });
}

/** Qoot's food tones, all light enough for the green ink (site.css: --leaf, --t-*). */
export const TONES = ['leaf', 'ochre', 'sea', 'wheat', 'coral', 'aqua'] as const;
export type Tone = (typeof TONES)[number];
const DRAWINGS = ['tomato', 'croissant', 'fish', 'milk', 'sack', 'chicken', 'water', 'bag'] as const;

function hash(text: string): number {
  let h = 0;
  for (const ch of text) h = (h * 31 + (ch.codePointAt(0) ?? 0)) >>> 0;
  return h;
}

/** A category always gets the same colour; a story without one gets a colour of its own. */
export function storyTone(card: Pick<NewsCard, 'slug' | 'category'>): Tone {
  return TONES[hash(card.category?.slug ?? card.slug) % TONES.length]!;
}

function drawing(slug: string): string {
  return `/assets/logo/qoot-draw-${DRAWINGS[(hash(slug) >>> 3) % DRAWINGS.length]}.svg`;
}

const DAY = 86_400_000;

/** The calendar day of a moment in Benghazi, as a day number (for counting days between dates). */
function benghaziDay(ms: number): number {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Tripoli', year: 'numeric', month: '2-digit', day: '2-digit' }).format(ms);
  return Math.round(Date.parse(parts + 'T00:00:00Z') / DAY);
}

/**
 * When a story happened, as a reader says it: "Today", "Yesterday", "3 days ago" during its first
 * week, then the date. Always computed at the moment the page is read.
 */
export function whenLabel(iso: string | null | undefined, lang: Lang, now: number = Date.now()): string {
  if (!iso) return '';
  const at = Date.parse(iso);
  if (Number.isNaN(at)) return '';
  const days = benghaziDay(now) - benghaziDay(at);
  if (days < 0 || days > 6) return formatDate(iso, lang);
  const text = new Intl.RelativeTimeFormat(lang === 'ar' ? 'ar-LY-u-nu-latn' : 'en-GB', { numeric: 'auto' }).format(-days, 'day');
  return lang === 'en' ? text.charAt(0).toUpperCase() + text.slice(1) : text;
}

/** The category (with its colour) and when; the full date is in the <time> element's title. */
export function newsMeta(card: Pick<NewsCard, 'publishedAt' | 'category' | 'slug'>, lang: Lang, options: { exact?: boolean } = {}): Markup {
  const date = formatDate(card.publishedAt, lang);
  const when = options.exact ? date : whenLabel(card.publishedAt, lang);
  return html`<span class="news-meta">${card.category ? html`<span class="news-cat" data-tone="${storyTone(card)}">${card.category.name}</span>` : ''}${
    when ? html`<time datetime="${card.publishedAt ?? ''}"${when !== date ? html` title="${date}"` : ''}>${when}</time>` : ''
  }</span>`;
}

/** The story's picture, or one of Qoot's food drawings on the story's colour. */
export function storyPicture(card: NewsCard, sizes: string, eager = false): Markup {
  if (card.image) return picture(card.image, { sizes, loading: eager ? 'eager' : 'lazy', decorative: true });
  return html`<span class="story-drawing"><img src="${drawing(card.slug)}" alt="" width="120" height="120" loading="lazy" decoding="async"></span>`;
}

/** The words on a card: the story's text (summary first), or the summary alone from an older API. */
export function cardParagraphs(card: Pick<NewsCard, 'text' | 'summary'>): string[] {
  return paragraphs(card.text?.trim() || card.summary || '');
}

function paragraph(text: string): Markup {
  return html`<p>${text.split('\n').map((line, i) => html`${i > 0 ? html`<br>` : ''}${inlineText(line)}`)}</p>`;
}

export interface CardLabels {
  more: string;
  less: string;
}

export function cardLabels(ui: UiStrings, readMore?: string): CardLabels {
  return { more: readMore?.trim() || ui.readMore, less: ui.readLess };
}

interface CardOptions {
  level: 2 | 3;
  /** a unique prefix for the ids on this page */
  id: string;
  /** position in its batch, for the entrance stagger */
  index?: number;
  eager?: boolean;
  /** how many cards share a row on a wide screen (1, 2 or 3), for the picture's sizes */
  across?: number;
}

/** The width a card's picture is shown at, so the browser picks the smallest file that is sharp. */
function cardSizes(across = 3): string {
  const wide = across === 1 ? '54vw' : across === 2 ? '48vw' : '31vw';
  return `(max-width: 639px) calc(100vw - 32px), (max-width: 1023px) ${across === 1 ? '50vw' : '46vw'}, ${wide}`;
}

/** One story as a card. The fold button stays hidden until the text is measured as too long. */
export function newsCard(card: NewsCard, lang: Lang, labels: CardLabels, o: CardOptions): Markup {
  const titleId = `${o.id}-t`;
  const textId = `${o.id}-x`;
  const title = o.level === 2 ? html`<h2 class="nc-title" id="${titleId}" dir="auto">${card.title}</h2>` : html`<h3 class="nc-title" id="${titleId}" dir="auto">${card.title}</h3>`;
  const words = cardParagraphs(card);
  return html`<article class="nc" data-tone="${storyTone(card)}" aria-labelledby="${titleId}" style="--i:${Math.min(o.index ?? 0, 8)}">
<div class="nc-photo${card.image ? '' : ' is-drawn'}">${storyPicture(card, cardSizes(o.across), o.eager)}</div>
<div class="nc-body">${title}${newsMeta(card, lang)}${
    words.length
      ? html`<div class="nc-text" id="${textId}" dir="auto">${words.map(paragraph)}</div><button class="nc-more" type="button" aria-expanded="false" aria-controls="${textId}" data-more="${labels.more}" data-less="${labels.less}" hidden><span>${labels.more}</span>${CHEVRON}</button>`
      : ''
  }</div>
</article>`;
}

/** Cards in a grid; the grid knows how many it holds (one story is laid out wide). */
export function newsGrid(cards: NewsCard[], lang: Lang, labels: CardLabels, o: { level: 2 | 3; id: string; eager?: number }): Markup {
  const across = Math.min(cards.length, 3);
  return html`<div class="nc-grid" data-n="${across}">${cards.map((c, i) =>
    newsCard(c, lang, labels, { level: o.level, id: `${o.id}-${i}`, index: i, eager: i < (o.eager ?? 0), across }),
  )}</div>`;
}

/** The home page shows the featured story first, then the newest. */
export function homeOrder(items: NewsCard[], count: number): NewsCard[] {
  const featured = items.find((i) => i.featured);
  return (featured ? [featured, ...items.filter((i) => i !== featured)] : items).slice(0, count);
}

/** Nothing published yet: a quiet field with one of the drawings, not an empty band. */
export function emptyState(message: string): Markup {
  return html`<div class="news-state is-empty"><img src="/assets/logo/qoot-draw-croissant.svg" alt="" width="120" height="120"><p class="state state-empty">${message}</p></div>`;
}

export function errorState(message: string, retry: string): Markup {
  return html`<div class="news-state is-error" role="alert"><img src="/assets/logo/qoot-draw-sack.svg" alt="" width="120" height="120"><div class="news-state-text"><p class="state">${message}</p><button class="btn" type="button" data-retry><span>${retry}</span></button></div></div>`;
}

// ------------------------------------------------------------------ folding

const reduceMotion = () => typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

/** Shows "Read more" only on cards whose text is longer than the folded height. */
export function measureCards(root: ParentNode): void {
  root.querySelectorAll<HTMLButtonElement>('.nc:not(.is-open) .nc-more').forEach((button) => {
    const text = document.getElementById(button.getAttribute('aria-controls') ?? '');
    if (text) button.hidden = text.scrollHeight <= text.clientHeight + 2;
  });
}

function setOpen(button: HTMLButtonElement, open: boolean): void {
  const card = button.closest<HTMLElement>('.nc');
  const text = document.getElementById(button.getAttribute('aria-controls') ?? '');
  if (!card || !text) return;
  const label = button.querySelector('span');
  button.setAttribute('aria-expanded', String(open));
  if (label) label.textContent = (open ? button.dataset.less : button.dataset.more) ?? '';

  // Opening: unfold, then grow from the folded height. Folding: measure the folded height, shrink to
  // it with the whole text still in place, and fold at the end.
  const from = text.offsetHeight;
  card.classList.toggle('is-open', open);
  const to = text.offsetHeight;
  const animate = !reduceMotion() && typeof text.animate === 'function' && Math.abs(to - from) > 1;
  if (animate) {
    if (!open) card.classList.add('is-open');
    card.classList.add('is-moving');
    const run = text.animate([{ height: `${from}px` }, { height: `${to}px` }], { duration: Math.min(460, 200 + Math.abs(to - from) * 0.35), easing: 'cubic-bezier(.22,1,.36,1)' });
    const done = () => {
      card.classList.remove('is-moving');
      if (button.getAttribute('aria-expanded') !== 'true') card.classList.remove('is-open');
    };
    run.onfinish = done;
    run.oncancel = done;
  }

  // Folding a long story can leave the visitor below it: bring its card back into view.
  if (!open) {
    const top = card.getBoundingClientRect().top;
    if (top < 0) card.scrollIntoView({ block: 'start', behavior: reduceMotion() ? 'auto' : 'smooth' });
  }
}

const bound = new WeakSet<Node>();

/**
 * Makes the cards under root foldable: measures them now, when the fonts arrive and when the
 * width changes, and opens or folds a story when its button is pressed.
 */
export function initCards(root: HTMLElement): void {
  measureCards(root);
  if (bound.has(root)) return;
  bound.add(root);
  root.addEventListener('click', (event) => {
    const button = (event.target as Element | null)?.closest<HTMLButtonElement>('.nc-more');
    if (button && root.contains(button)) setOpen(button, button.getAttribute('aria-expanded') !== 'true');
  });
  void document.fonts?.ready.then(() => measureCards(root));
  let width = window.innerWidth;
  let timer = 0;
  window.addEventListener('resize', () => {
    if (window.innerWidth === width) return;
    width = window.innerWidth;
    window.clearTimeout(timer);
    timer = window.setTimeout(() => measureCards(root), 120);
  });
}

/**
 * The article page's head and body arrive as they come into view. Without the observer, or with
 * reduced motion, they are simply there; nothing is hidden before the script runs.
 */
export function arrive(root: ParentNode): void {
  const items = Array.from(root.querySelectorAll<HTMLElement>('[data-arrive]:not([data-arrive="go"])'));
  if (!items.length || !('IntersectionObserver' in window) || reduceMotion()) return;
  const io = new IntersectionObserver(
    (entries) => {
      for (const e of entries) {
        if (!e.isIntersecting) continue;
        io.unobserve(e.target);
        (e.target as HTMLElement).dataset.arrive = 'go';
      }
    },
    { rootMargin: '0px 0px 10% 0px' },
  );
  for (const el of items) {
    el.dataset.arrive = 'wait';
    io.observe(el);
  }
}

// ------------------------------------------------------------------ home

/** Latest news on the home page: loading, then the cards, or the empty / error state. */
export async function loadHomeNews(lang: Lang, ui: UiStrings): Promise<void> {
  const body = document.querySelector<HTMLElement>('[data-news-home]');
  const section = body?.closest<HTMLElement>('section');
  if (!body || !section) return;
  const count = Number(body.dataset.count) || 3;
  const labels = cardLabels(ui, body.dataset.readMore);
  const empty = body.dataset.empty ?? '';

  const run = async (retrying: boolean) => {
    body.setAttribute('aria-busy', 'true');
    if (retrying) body.innerHTML = newsSkeleton(ui, count).value;
    try {
      const page = await early<NewsPage>(homeNewsPath(lang), lang);
      const items = homeOrder(page.items, count);
      if (!items.length) {
        if (section.dataset.hideEmpty === 'true') {
          section.remove();
          document.querySelectorAll(`.site-nav a[href$="#${section.id}"]`).forEach((a) => a.closest('li')?.remove());
          return;
        }
        body.innerHTML = emptyState(empty).value;
        return;
      }
      body.innerHTML = newsGrid(items, lang, labels, { level: 3, id: 'home-news' }).value;
      initCards(body);
    } catch (error) {
      const message = error instanceof ApiError && !error.isNetwork ? ui.newsError : ui.newsError + ' ' + ui.pageErrorHint;
      body.innerHTML = errorState(message, ui.retry).value;
      body.querySelector('[data-retry]')?.addEventListener('click', () => void run(true), { once: true });
    } finally {
      body.removeAttribute('aria-busy');
    }
  };
  await run(false);
}
