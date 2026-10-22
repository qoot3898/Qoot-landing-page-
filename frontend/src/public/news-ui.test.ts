import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { NewsCard } from '../shared/types';
import { ui } from './i18n';
import { cardParagraphs, loadHomeNews, measureCards, storyTone, whenLabel } from './news-ui';

const card = (id: number, extra: Partial<NewsCard> = {}): NewsCard => ({
  slug: `story-${id}`,
  title: `Story <${id}>`,
  summary: `Summary ${id}`,
  image: null,
  category: null,
  publishedAt: '2026-10-01T09:00:00Z',
  featured: false,
  ...extra,
});

const page = (items: NewsCard[]) => new Response(JSON.stringify({ items, page: 1, pageSize: 3, total: items.length, totalPages: 1 }), { status: 200 });

function mount(hideEmpty = false) {
  document.body.innerHTML = `<nav class="site-nav"><ul><li><a href="#news">News</a></li><li><a href="#faq">FAQ</a></li></ul></nav>
<section id="news" data-hide-empty="${hideEmpty}"><div data-news-home data-count="3" data-read-more="Read" data-empty="Nothing yet"></div></section>`;
}

describe('Latest news on the home page', () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => vi.unstubAllGlobals());

  it('shows the stories as cards, the featured one first, escaped, in the order of the page', async () => {
    mount();
    fetchMock.mockResolvedValueOnce(page([card(1), card(2, { featured: true }), card(3), card(4)]));
    await loadHomeNews('en', ui('en'));
    // the same request boot.js starts early (the most the dashboard allows), cut to the count
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/public/news?lang=en&page=1&pageSize=6');
    const titles = [...document.querySelectorAll('.nc .nc-title')].map((h) => h.textContent);
    expect(titles).toEqual(['Story <2>', 'Story <1>', 'Story <3>']);
    expect(document.querySelector('.nc-grid')?.getAttribute('data-n')).toBe('3');
    // each card is named by its headline, and opens nowhere: the story is read on the card
    const first = document.querySelector<HTMLElement>('.nc')!;
    expect(document.getElementById(first.getAttribute('aria-labelledby')!)?.textContent).toBe('Story <2>');
    expect(first.querySelector('a')).toBeNull();
    expect(document.querySelector('[data-news-home]')?.hasAttribute('aria-busy')).toBe(false);
  });

  it('puts the whole story on the card, paragraph by paragraph, with its two marks', async () => {
    mount();
    fetchMock.mockResolvedValueOnce(page([card(1, { text: 'First **part**.\n\n• one\n• two\n\nSee [our site](https://www.qoot.com/).' })]));
    await loadHomeNews('en', ui('en'));
    const paras = document.querySelectorAll('.nc-text > p');
    expect(paras).toHaveLength(3);
    expect(paras[0]!.querySelector('strong')?.textContent).toBe('part');
    expect(paras[1]!.querySelectorAll('br')).toHaveLength(1);
    expect(paras[2]!.querySelector('a')?.getAttribute('href')).toBe('https://www.qoot.com/');
    // an older API without the text: the summary
    expect(cardParagraphs({ summary: 'Only this', text: undefined })).toEqual(['Only this']);
    expect(cardParagraphs({ summary: 'Short', text: '' })).toEqual(['Short']);
  });

  it('offers "Read more" only when the text is longer than the fold, and opens and folds it in place', async () => {
    mount();
    fetchMock.mockResolvedValueOnce(page([card(1, { text: 'Long story' }), card(2, { text: 'Short' })]));
    await loadHomeNews('en', ui('en'));
    const [long, short] = [...document.querySelectorAll<HTMLElement>('.nc-text')];
    // jsdom has no layout: give the long text more height than its fold
    Object.defineProperty(long!, 'scrollHeight', { configurable: true, value: 400 });
    Object.defineProperty(long!, 'clientHeight', { configurable: true, value: 100 });
    Object.defineProperty(short!, 'scrollHeight', { configurable: true, value: 60 });
    Object.defineProperty(short!, 'clientHeight', { configurable: true, value: 60 });
    measureCards(document);
    const [more, none] = [...document.querySelectorAll<HTMLButtonElement>('.nc-more')];
    expect(none!.hidden).toBe(true);
    expect(more!.hidden).toBe(false);
    expect(more!.getAttribute('aria-controls')).toBe(long!.id);
    expect(more!.textContent).toBe('Read');

    more!.click();
    expect(more!.getAttribute('aria-expanded')).toBe('true');
    expect(more!.closest('.nc')?.classList.contains('is-open')).toBe(true);
    expect(more!.textContent).toBe('Show less');
    more!.click();
    expect(more!.getAttribute('aria-expanded')).toBe('false');
    expect(more!.closest('.nc')?.classList.contains('is-open')).toBe(false);
    expect(more!.textContent).toBe('Read');
  });

  it('shows one story wide; a story without a picture gets a drawing on its colour', async () => {
    mount();
    fetchMock.mockResolvedValueOnce(page([card(1)]));
    await loadHomeNews('en', ui('en'));
    expect(document.querySelectorAll('.nc')).toHaveLength(1);
    expect(document.querySelector('.nc-grid')?.getAttribute('data-n')).toBe('1');
    expect(document.querySelector('.nc-photo.is-drawn .story-drawing img')?.getAttribute('src')).toMatch(/^\/assets\/logo\/qoot-draw-[a-z]+\.svg$/);
    expect(document.querySelector('.nc')?.getAttribute('data-tone')).toBe(storyTone(card(1)));
  });

  it('gives a category the same colour on every story', () => {
    const contracts = { slug: 'contracts', name: 'Contracts' };
    expect(storyTone({ slug: 'a', category: contracts })).toBe(storyTone({ slug: 'b', category: contracts }));
  });

  it('says when a story happened the way a reader would, then the date', () => {
    const now = Date.parse('2026-10-22T10:00:00Z');
    expect(whenLabel('2026-10-22T07:00:00Z', 'en', now)).toBe('Today');
    expect(whenLabel('2026-10-21T07:00:00Z', 'en', now)).toBe('Yesterday');
    expect(whenLabel('2026-10-19T07:00:00Z', 'en', now)).toBe('3 days ago');
    expect(whenLabel('2026-10-21T07:00:00Z', 'ar', now)).toBe('أمس');
    expect(whenLabel('2026-10-01T07:00:00Z', 'en', now)).toBe('1 October 2026');
    expect(whenLabel(null, 'en', now)).toBe('');
  });

  it('shows the empty text, or removes the section and its menu link when set to hide', async () => {
    mount(false);
    fetchMock.mockResolvedValueOnce(page([]));
    await loadHomeNews('en', ui('en'));
    expect(document.querySelector('.news-state.is-empty .state-empty')?.textContent).toBe('Nothing yet');

    mount(true);
    fetchMock.mockResolvedValueOnce(page([]));
    await loadHomeNews('en', ui('en'));
    expect(document.getElementById('news')).toBeNull();
    expect(document.querySelector('a[href="#news"]')).toBeNull();
    expect(document.querySelector('a[href="#faq"]')).not.toBeNull();
  });

  it('shows an error with a retry that works', async () => {
    mount();
    fetchMock.mockResolvedValueOnce(new Response('{"code":"server_error","title":"Oops"}', { status: 500 }));
    await loadHomeNews('ar', ui('ar'));
    const retry = document.querySelector<HTMLButtonElement>('[data-retry]');
    expect(document.querySelector('.news-state.is-error')).not.toBeNull();
    fetchMock.mockResolvedValueOnce(page([card(7)]));
    retry!.click();
    await vi.waitFor(() => expect(document.querySelector('.nc .nc-title')?.textContent).toBe('Story <7>'));
  });
});
