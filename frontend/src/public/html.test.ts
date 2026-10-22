import { describe, expect, it } from 'vitest';
import { escapeHtml, html, inlineText, paragraphs, picture, safeHref, trusted } from './html';

describe('html``', () => {
  it('escapes every interpolated value', () => {
    const evil = `<img src=x onerror="alert(1)">'\``;
    const out = html`<p title="${evil}">${evil}</p>`.value;
    expect(out).not.toContain('<img');
    expect(out).toContain('&lt;img src=x onerror=&quot;alert(1)&quot;&gt;&#39;&#96;');
  });

  it('passes nested markup and arrays through, drops null/false', () => {
    const items = ['a', '<b>'].map((x) => html`<li>${x}</li>`);
    expect(html`<ul>${items}${null}${false}${undefined}</ul>`.value).toBe('<ul><li>a</li><li>&lt;b&gt;</li></ul>');
    expect(html`${trusted('<svg></svg>')}`.value).toBe('<svg></svg>');
  });

  it('escapeHtml covers the five HTML specials and backtick', () => {
    expect(escapeHtml(`&<>"'\``)).toBe('&amp;&lt;&gt;&quot;&#39;&#96;');
  });
});

describe('safeHref', () => {
  it.each([
    ['#contact', '#contact'],
    ['/news/', '/news/'],
    ['/en/news/a-b?x=1#y', '/en/news/a-b?x=1#y'],
    ['mailto:info@qoot.ly', 'mailto:info@qoot.ly'],
    ['tel:+218910000000', 'tel:+218910000000'],
    ['https://maps.google.com/?q=Benghazi', 'https://maps.google.com/?q=Benghazi'],
  ])('keeps %s', (input, expected) => expect(safeHref(input)).toBe(expected));

  it.each(['javascript:alert(1)', 'JaVaScRiPt:alert(1)', 'data:text/html,<script>', '//evil.example/x', 'vbscript:x', ' ', '', null, undefined, 'ftp://x.y'])(
    'refuses %s',
    (input) => expect(safeHref(input as string)).toBe('#'),
  );
});

describe('inlineText', () => {
  it('supports **bold** and [label](link) only, escaping the rest', () => {
    const out = inlineText('Hello **world** <script>x</script> [site](https://qoot.ly) [bad](javascript:alert(1))').value;
    expect(out).toContain('<strong>world</strong>');
    expect(out).toContain('&lt;script&gt;x&lt;/script&gt;');
    expect(out).toContain('href="https://qoot.ly/"');
    expect(out).toContain('target="_blank" rel="noopener noreferrer"');
    expect(out).toContain('<a class="u" href="#">bad</a>');
    expect(out).not.toContain('javascript:');
  });

  it('escapes markup inside the marks', () => {
    expect(inlineText('**<i>x</i>**').value).toBe('<strong>&lt;i&gt;x&lt;/i&gt;</strong>');
  });
});

describe('picture', () => {
  const image = { id: 1, url: '/media/a.jpg', type: 'image/jpeg', width: 800, height: 600, alt: 'A "quoted" alt', sources: [{ type: 'image/avif', srcset: '/a.avif 800w' }] };

  it('renders sources, dimensions, lazy loading and escaped alt', () => {
    const out = picture(image, { sizes: '50vw' }).value;
    expect(out).toContain('<source type="image/avif" srcset="/a.avif 800w" sizes="50vw">');
    expect(out).toContain('width="800" height="600"');
    expect(out).toContain('alt="A &quot;quoted&quot; alt"');
    expect(out).toContain('loading="lazy"');
  });

  it('marks priority images and decorative ones', () => {
    const out = picture(image, { sizes: '1vw', priority: true, decorative: true }).value;
    expect(out).toContain('fetchpriority="high"');
    expect(out).toContain('alt=""');
  });

  it('renders nothing for a missing image instead of a broken one', () => {
    expect(picture(null, { sizes: '1vw' }).value).toBe('');
  });
});

describe('paragraphs', () => {
  it('splits on blank lines', () => {
    expect(paragraphs('a\n\n b \n\n\nc')).toEqual(['a', 'b', 'c']);
  });
});
