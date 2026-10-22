import { describe, expect, it } from 'vitest';
import type { HeroData, SitePayload } from '../shared/types';
import siteAr from './__fixtures__/site-ar.json';
import siteEn from './__fixtures__/site-en.json';
import { renderMain, sectionData, titleWords } from './render';
import { chrome, context } from './shell';

/** Payloads captured from the running API (seeded content), so the renderer is tested against the real contract. */
const load = (raw: unknown): SitePayload => structuredClone(raw) as SitePayload;

function build(site: SitePayload, lang: 'ar' | 'en') {
  const ctx = context(site, lang, 'home', (other) => (other === 'ar' ? '/' : '/en/'));
  const { header, footer } = chrome(site, ctx);
  document.body.innerHTML = `${header.value}<main id="main">${renderMain(site, ctx).value}</main>${footer.value}`;
  return document.body;
}

describe('home page renderer', () => {
  it('renders every published section in the API order, with the original classes', () => {
    const body = build(load(siteEn), 'en');
    const ids = Array.from(body.querySelectorAll('main > section')).map((s) => s.className.split(' ')[0]);
    expect(ids).toEqual(['hero', 'supplies', 'how', 'who', 'news-home', 'faq', 'contact', 'visit']);
    expect(body.querySelectorAll('.tile')).toHaveLength(8);
    expect(body.querySelectorAll('.stack .card')).toHaveLength(4);
    expect(body.querySelectorAll('.sectors li')).toHaveLength(6);
    expect(body.querySelectorAll('.qa')).toHaveLength(6);
    expect(body.querySelectorAll('.reel picture')).toHaveLength(4);
    const form = body.querySelector('#contact-form');
    expect(Array.from(form!.querySelectorAll('input:not([name="website"]), textarea')).map((f) => f.getAttribute('name'))).toEqual(['name', 'phone', 'email', 'message']);
    expect(body.querySelector('.reach[href^="tel:"]')).not.toBeNull();
    expect(body.querySelector('.reach[href^="https://wa.me/"]')).not.toBeNull();
    expect(body.querySelector('.site-header .nav-cta')?.getAttribute('href')).toBe('#contact');
    expect(body.querySelector('.site-footer')).not.toBeNull();
  });

  it('links the language switch to the other language', () => {
    const body = build(load(siteEn), 'en');
    const sw = body.querySelector<HTMLAnchorElement>('[data-lang-switch]');
    expect(sw?.getAttribute('href')).toBe('/');
    expect(sw?.getAttribute('data-lang-switch')).toBe('ar');
    const ar = build(load(siteAr), 'ar');
    expect(ar.querySelector('[data-lang-switch]')?.getAttribute('href')).toBe('/en/');
  });

  it('never turns content into markup', () => {
    const site = load(siteEn);
    const hero = sectionData(site, 'hero')!.data as HeroData;
    hero.titleStart = '<img src=x onerror=alert(1)>';
    hero.lede = '"><script>alert(1)</script>';
    const body = build(site, 'en');
    expect(body.querySelector('img[src="x"]')).toBeNull();
    expect(body.querySelector('main script')).toBeNull();
    expect(body.querySelector('.hero-foot .lede')?.textContent).toBe('"><script>alert(1)</script>');
  });

  it('drops navigation links that point at a section that is not shown', () => {
    const site = load(siteEn);
    site.sections = site.sections.filter((s) => s.key !== 'faq');
    const body = build(site, 'en');
    const hrefs = Array.from(body.querySelectorAll('.site-nav a')).map((a) => a.getAttribute('href'));
    expect(hrefs).not.toContain('#faq');
    expect(body.querySelector('#faq')).toBeNull();
  });

  it('prepares the news section for loading', () => {
    const body = build(load(siteAr), 'ar');
    const news = body.querySelector<HTMLElement>('[data-news-home]');
    expect(news?.dataset.count).toBe('3');
    // one card-shaped placeholder for each story the section will show
    expect(news?.querySelectorAll('.news-loading .sk-card')).toHaveLength(3);
    expect(body.querySelector('.news-all')?.getAttribute('href')).toBe('/news/');
  });
});

describe('titleWords', () => {
  it('keeps punctuation with the next word', () => {
    expect(titleWords('Food , supplies  and — care')).toEqual(['Food', ', supplies', 'and', '— care']);
    expect(titleWords('  ')).toEqual([]);
  });
});
