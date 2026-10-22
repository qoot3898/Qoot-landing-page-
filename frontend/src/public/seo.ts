import type { Seo } from '../shared/types';

/**
 * Applies the API's search metadata to <head>. When the API served the page it already wrote the
 * same tags (for crawlers without JavaScript); this keeps them right for static hosting, language
 * switches and in-page navigation, without duplicating anything.
 */
export function applySeo(seo: Seo | undefined, options: { noindex?: boolean } = {}): void {
  if (!seo) return;
  document.title = seo.title;
  setMeta('name', 'description', seo.description);
  setMeta('name', 'robots', options.noindex ? 'noindex, nofollow' : seo.robots);
  setLink('canonical', seo.canonical);

  document.head.querySelectorAll('link[rel="alternate"][hreflang]').forEach((el) => el.remove());
  for (const alt of seo.alternates) {
    const link = document.createElement('link');
    link.rel = 'alternate';
    link.hreflang = alt.hreflang;
    link.href = alt.href;
    document.head.appendChild(link);
  }

  for (const [key, value] of Object.entries(seo.og)) {
    if (value != null) setMeta('property', `og:${key}`, String(value));
  }
  for (const [key, value] of Object.entries(seo.twitter)) {
    if (value != null) setMeta('name', `twitter:${key}`, String(value));
  }

  let ld = document.head.querySelector<HTMLScriptElement>('script[type="application/ld+json"][data-qoot-ld]');
  if (!ld) {
    ld = document.createElement('script');
    ld.type = 'application/ld+json';
    ld.dataset.qootLd = '';
    document.head.appendChild(ld);
  }
  // textContent: JSON is data here, and "</script>" inside a string cannot end the element.
  ld.textContent = JSON.stringify(seo.jsonLd).replace(/</g, '\\u003c');
}

function setMeta(attr: 'name' | 'property', key: string, content: string): void {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${CSS.escape(key)}"]`);
  if (!el) {
    el = document.createElement('meta');
    el.setAttribute(attr, key);
    document.head.appendChild(el);
  }
  el.content = content;
}

function setLink(rel: string, href: string): void {
  let el = document.head.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`);
  if (!el) {
    el = document.createElement('link');
    el.rel = rel;
    document.head.appendChild(el);
  }
  el.href = href;
}
