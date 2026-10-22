import type { Picture } from '../shared/types';

/**
 * Markup is built with a tagged template that escapes every interpolated value. Content from the
 * API is plain text, so escaping it means nothing an editor types can become markup or script.
 * Only fragments produced by html`` itself (or trusted constants wrapped in trusted()) pass through.
 */
export class Markup {
  constructor(readonly value: string) {}
  toString(): string {
    return this.value;
  }
}

const ESCAPES: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;', '`': '&#96;' };

export function escapeHtml(value: string): string {
  return value.replace(/[&<>"'`]/g, (c) => ESCAPES[c] ?? c);
}

function render(value: unknown): string {
  if (value instanceof Markup) return value.value;
  if (Array.isArray(value)) return value.map(render).join('');
  if (value === null || value === undefined || value === false) return '';
  return escapeHtml(String(value));
}

export function html(strings: TemplateStringsArray, ...values: unknown[]): Markup {
  let out = strings[0] ?? '';
  values.forEach((value, i) => {
    out += render(value) + (strings[i + 1] ?? '');
  });
  return new Markup(out);
}

/** For constants written in this codebase (icon and map drawings), never for API data. */
export function trusted(markup: string): Markup {
  return new Markup(markup);
}

/**
 * Link targets accepted on the page; mirrors the API's allow-list. Anything else becomes "#"
 * (defence in depth: the API already refuses unsafe links when content is saved).
 */
export function safeHref(url: string | null | undefined): string {
  const value = (url ?? '').trim();
  if (!value) return '#';
  if (/^#[A-Za-z][A-Za-z0-9_-]{0,60}$/.test(value)) return value;
  if (/^\/(?!\/)[A-Za-z0-9._~!$&'()*+,;=:@%/?#-]*$/.test(value)) return value;
  if (/^mailto:[^\s@<>"']+@[^\s@<>"']+(\?[^\s<>"']*)?$/i.test(value)) return value;
  if (/^tel:\+?[0-9]{3,16}$/i.test(value)) return value;
  try {
    const parsed = new URL(value);
    if (parsed.protocol === 'https:' || parsed.protocol === 'http:') return parsed.href;
  } catch {
    /* not a URL */
  }
  return '#';
}

export function isExternal(href: string): boolean {
  return /^https?:\/\//i.test(href) && !href.startsWith(location.origin);
}

/** Replaces {placeholders} in an editable text. Values are plain strings (escaped by html``). */
export function fill(template: string, values: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (match, key: string) => values[key] ?? match);
}

export function telHref(phone: string): string {
  const digits = phone.replace(/[^\d+]/g, '');
  return digits ? `tel:${digits}` : '#';
}

export interface PictureOptions {
  sizes: string;
  className?: string;
  loading?: 'lazy' | 'eager';
  priority?: boolean;
  alt?: string;
  style?: string;
  decorative?: boolean;
  /** Extra attributes for the <img>, built with html``. */
  extra?: Markup;
}

/** Responsive <picture> with AVIF/WebP sources when the brand file has them. */
export function picture(image: Picture | null | undefined, options: PictureOptions): Markup {
  if (!image) return html``;
  const alt = options.decorative ? '' : options.alt ?? image.alt;
  return html`<picture>${image.sources.map(
    (s) => html`<source type="${s.type}" srcset="${s.srcset}" sizes="${options.sizes}">`,
  )}<img src="${image.url}" width="${image.width || ''}" height="${image.height || ''}" alt="${alt}"${options.className ? html` class="${options.className}"` : ''}${
    options.priority ? html` fetchpriority="high"` : html` loading="${options.loading ?? 'lazy'}"`
  } decoding="async"${options.style ? html` style="${options.style}"` : ''}${options.extra ?? ''}></picture>`;
}

/**
 * Article text: plain text with two marks only, **bold** and [label](link). Everything else is
 * escaped; links go through safeHref.
 */
export function inlineText(text: string): Markup {
  const parts: Markup[] = [];
  const pattern = /\*\*([^*\n]+)\*\*|\[([^\]\n]{1,200})\]\(([^)\s]{1,500})\)/g;
  let last = 0;
  for (const match of text.matchAll(pattern)) {
    const index = match.index ?? 0;
    parts.push(html`${text.slice(last, index)}`);
    if (match[1] !== undefined) {
      parts.push(html`<strong>${match[1]}</strong>`);
    } else {
      const href = safeHref(match[3]);
      const external = isExternal(href);
      parts.push(html`<a class="u" href="${href}"${external ? html` target="_blank" rel="noopener noreferrer"` : ''}>${match[2]}</a>`);
    }
    last = index + match[0].length;
  }
  parts.push(html`${text.slice(last)}`);
  return html`${parts}`;
}

/** Splits a paragraph on blank lines and single line breaks. */
export function paragraphs(text: string): string[] {
  return text.split(/\n{2,}/).map((p) => p.trim()).filter(Boolean);
}
