import { describe, expect, it } from 'vitest';
import { formatDate, localizePath, pageLang, ui } from './i18n';

describe('i18n', () => {
  it('reads the language from the path', () => {
    expect(pageLang('/')).toBe('ar');
    expect(pageLang('/news/abc')).toBe('ar');
    expect(pageLang('/en/')).toBe('en');
    expect(pageLang('/en')).toBe('en');
    expect(pageLang('/en/news/abc')).toBe('en');
    expect(pageLang('/english')).toBe('ar');
  });

  it('localizes site paths for English only', () => {
    expect(localizePath('/news/', 'ar')).toBe('/news/');
    expect(localizePath('/news/', 'en')).toBe('/en/news/');
    expect(localizePath('/en/news/', 'en')).toBe('/en/news/');
    expect(localizePath('/media/x.jpg', 'en')).toBe('/media/x.jpg');
    expect(localizePath('https://x.y/', 'en')).toBe('https://x.y/');
    expect(localizePath('//x.y/', 'en')).toBe('//x.y/');
  });

  it('formats dates in Benghazi time with Latin digits', () => {
    // 23:30 UTC on 31 Jan is 1 Feb in Libya (UTC+2)
    expect(formatDate('2026-01-31T23:30:00Z', 'en')).toBe('1 February 2026');
    expect(formatDate('2026-01-31T23:30:00Z', 'ar')).toMatch(/^1 .+ 2026$/);
    expect(formatDate(null, 'en')).toBe('');
    expect(formatDate('not a date', 'en')).toBe('');
  });

  it('has the same interface strings in both languages', () => {
    expect(Object.keys(ui('ar')).sort()).toEqual(Object.keys(ui('en')).sort());
    expect(ui('en').showing(3, 9)).toBe('3 of 9');
  });
});
