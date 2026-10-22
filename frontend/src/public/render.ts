import type {
  ContactData, FaqData, FooterData, HeaderData, HeroData, HowData, Lang, Language, NewsSectionData, Picture,
  SectionPayload, Settings, SitePayload, SuppliesData, VisitData, WhoData,
} from '../shared/types';
import { BENGHAZI_SEA, CITY_GEOMETRY, LIBYA_LAND, MAP_HQ, Q_BODY, Q_LEAF, SECTOR_ICONS } from './drawings';
import { escapeHtml, fill, html, type Markup, paragraphs, picture, safeHref, telHref, trusted } from './html';
import { localizePath, type UiStrings } from './i18n';

/**
 * The page's markup, section by section. Each function reproduces the structure and class names
 * of the original static page, so the original stylesheet and motion apply unchanged; only the
 * words, pictures, links and repeated items now come from the API.
 */

export interface RenderContext {
  lang: Lang;
  site: SitePayload;
  settings: Settings;
  ui: UiStrings;
  page: 'home' | 'news';
  /** Element ids present on this page (#supplies ...), used to drop links to hidden sections. */
  anchors: Set<string>;
  homePath: string;
  /** The same page in another language. */
  alternate: (lang: Lang) => string;
}

export const ARROW = trusted('<svg class="arr" viewBox="0 0 16 16" aria-hidden="true"><path d="M2 8h11M9 4l4 4-4 4" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>');
const ARROW_DOWN = trusted('<svg class="arr arr-down" viewBox="0 0 18 18" aria-hidden="true"><path d="M9 2v13M4 10l5 5 5-5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>');
export const CHEVRON = trusted('<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3 6l5 5 5-5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>');
const EXTERNAL = trusted('<svg class="ext" viewBox="0 0 16 16" aria-hidden="true"><path d="M4.5 11.5l7-7M6 4.5h5.5V10" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>');
export const Q_LEAF_IMG = html`<img class="q-leaf" src="/assets/logo/qoot-q-leaf.svg" width="290" height="355" alt="">`;

/** Resolves an editable link target for this page: anchors to hidden sections disappear (null). */
export function resolveLink(target: string, ctx: RenderContext): string | null {
  const value = (target ?? '').trim();
  if (!value) return null;
  if (value.startsWith('#')) {
    const id = value.slice(1);
    if (id !== 'main' && !ctx.anchors.has(id)) return null;
    return ctx.page === 'home' || id === 'main' ? safeHref(value) : ctx.homePath + safeHref(value);
  }
  return safeHref(localizePath(value, ctx.lang));
}

function otherLanguage(ctx: RenderContext): Language | undefined {
  return ctx.site.languages.find((l) => l.code !== ctx.lang);
}

function heading(id: string, text: string): Markup {
  return html`<h2 id="${id}"><span class="m"><span>${text}</span></span></h2>`;
}

function textOr(value: string, markup: (v: string) => Markup): Markup {
  return value ? markup(value) : html``;
}

// ------------------------------------------------------------------ header

export function renderHeader(data: HeaderData, ctx: RenderContext): Markup {
  const logo = ctx.lang === 'ar' ? ctx.settings.logoAr : ctx.settings.logoEn;
  const other = otherLanguage(ctx);
  const cta = resolveLink(data.ctaTarget, ctx);
  const links = data.links
    .map((l) => ({ label: l.label, href: resolveLink(l.target, ctx) }))
    .filter((l): l is { label: string; href: string } => l.href !== null);
  const open = data.menuOpenLabel || ctx.ui.mainNav;
  return html`<a class="skip" href="#main">${data.skipLabel || 'Skip'}</a>
<header class="site-header">
<a class="brand" href="${ctx.homePath}">${
    logo
      ? html`<img src="${logo.url}" width="${logo.width}" height="${logo.height}" alt="${data.logoAlt || logo.alt}">`
      : html`<span class="brand-name">${ctx.settings.siteName}</span>`
  }</a>
<nav class="site-nav" id="site-nav" aria-label="${ctx.ui.mainNav}"><ul>${links.map((l) => html`<li><a class="u" href="${l.href}">${l.label}</a></li>`)}</ul>${
    cta ? html`<a class="btn btn-leaf nav-cta" href="${cta}"><span>${data.ctaLabel}</span>${ARROW}</a>` : ''
  }</nav>
${other ? html`<a class="lang u" href="${ctx.alternate(other.code)}" hreflang="${other.code}" lang="${other.code}" data-lang-switch="${other.code}">${other.label}</a>` : ''}
${cta ? html`<a class="btn head-cta" href="${cta}"><span>${data.ctaLabel}</span></a>` : ''}
<button class="menu-btn" type="button" aria-expanded="false" aria-controls="site-nav" data-open="${open}" data-close="${data.menuCloseLabel || open}"><span class="menu-ico" aria-hidden="true"><i></i><i></i></span><span class="menu-txt">${open}</span></button>
</header>`;
}

// ------------------------------------------------------------------ hero

/** Words of a title; a lone symbol such as "&" travels with the next word. */
export function titleWords(text: string): string[] {
  const tokens = text.split(/\s+/).filter(Boolean);
  const words: string[] = [];
  let carry = '';
  for (const token of tokens) {
    if (/^[^\p{L}\p{N}]+$/u.test(token)) {
      carry = carry ? `${carry} ${token}` : token;
      continue;
    }
    words.push(carry ? `${carry} ${token}` : token);
    carry = '';
  }
  if (carry) words.push(carry);
  return words;
}

/** The reel fills a portrait Q window, so wide photographs are drawn wider than the window. */
function reelSizes(image: Picture): string {
  const window = 290 / 355;
  const ratio = image.width && image.height ? image.width / image.height : 1;
  const factor = Math.max(1, ratio / window) * 1.08;
  return `(max-width: 720px) ${Math.round(62 * factor)}vw, ${Math.round(26 * factor)}vw`;
}

export function renderHero(data: HeroData, ctx: RenderContext): Markup {
  let index = 0;
  const half = (text: string, cls: string) =>
    html`<span class="half ${cls}">${titleWords(text).map((w, i) => html`${i > 0 ? ' ' : ''}<span class="w"><span style="--i:${index++}">${w}</span></span>`)}</span>`;
  const slides = data.slides.filter((s) => s.image);
  const link = resolveLink(data.linkTarget, ctx);
  return html`<section class="hero on-cream" aria-labelledby="hero-title">
<div class="hero-stage">
<h1 id="hero-title" class="hero-title">${half(data.titleStart, 'half-a')} ${half(data.titleEnd, 'half-b')}</h1>
<figure class="qwin">
<div class="q-frame"><div class="q-mask"><div class="reel">${slides.map((s, i) =>
    picture(s.image, { sizes: reelSizes(s.image!), priority: i === 0, extra: html` data-caption="${s.caption}"` }),
  )}</div></div>${Q_LEAF_IMG}</div>
<figcaption class="qwin-cap" data-cap style="--d:700ms">${slides[0]?.caption ?? ''}</figcaption>
</figure>
<div class="hero-foot"><p class="lede" style="--d:560ms">${data.lede}</p>${
    link && data.linkLabel ? html`<a class="go u" href="${link}" style="--d:680ms">${data.linkLabel} ${ARROW_DOWN}</a>` : ''
  }</div>
</div>
</section>`;
}

// ------------------------------------------------------------------ what we supply

const TILE_SPANS = new Set(['3', '4', '5', '7', '12']);

export function renderSupplies(data: SuppliesData, ctx: RenderContext, anchor: string): Markup {
  const noteLink = resolveLink(data.noteLinkTarget, ctx);
  return html`<section class="supplies" id="${anchor}" aria-labelledby="supplies-title" data-reveal>
<div class="sup-head">${heading('supplies-title', data.title)}${textOr(data.intro, (v) => html`<p class="sup-intro" data-r style="--i:1">${v}</p>`)}</div>
<ul class="shelf">${data.tiles.map(
    (t, i) => html`<li class="tile" data-tone="${t.tone}" style="--i:${i}${TILE_SPANS.has(t.size) ? `;--span:${t.size}` : ''}"><h3 class="tile-name">${t.name}</h3>${textOr(t.when, (v) => html`<p class="tile-when">${v}</p>`)}${
      t.items.length ? html`<ul class="tile-items">${t.items.map((it) => html`<li>${it.text}${it.detail ? html` <span>${it.detail}</span>` : ''}</li>`)}</ul>` : ''
    }${textOr(t.short, (v) => html`<p class="tile-short">${v}</p>`)}${
      t.art ? html`<img class="tile-art" src="${t.art.url}" width="${t.art.width}" height="${t.art.height}" alt="" loading="lazy">` : ''
    }</li>`,
  )}</ul>
${
  data.notePrefix || data.noteLinkLabel
    ? html`<p class="sup-note" data-r style="--i:3">${data.notePrefix} ${noteLink && data.noteLinkLabel ? html`<a class="u" href="${noteLink}">${data.noteLinkLabel}</a>` : data.noteLinkLabel} ${data.noteSuffix}</p>`
    : ''
}
</section>`;
}

// ------------------------------------------------------------------ how we work

export function renderHow(data: HowData, _ctx: RenderContext, anchor: string): Markup {
  return html`<section class="how on-cream" id="${anchor}" aria-labelledby="how-title" data-reveal>
<div class="how-head">${heading('how-title', data.title)}${textOr(data.intro, (v) => html`<p data-r style="--i:1">${v}</p>`)}</div>
<ol class="stack">${data.steps.map((s, i) => {
    const x = clampPercent(s.focusX, 50);
    const y = clampPercent(s.focusY, 50);
    return html`<li class="card" data-tone="${s.tone}" style="--i:${i}"><div class="card-text"><span class="card-route" aria-hidden="true">${data.steps.map((_, j) => (j <= i ? html`<i class="on"></i>` : html`<i></i>`))}</span><h3>${s.title}</h3>${textOr(s.text, (v) => html`<p>${v}</p>`)}</div><figure class="card-photo">${picture(s.image, {
      sizes: '(max-width: 720px) 100vw, 58vw',
      style: `object-position:${x}% ${y}%`,
    })}</figure></li>`;
  })}</ol>
</section>`;
}

function clampPercent(value: number | null | undefined, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? Math.min(100, Math.max(0, Math.round(value))) : fallback;
}

// ------------------------------------------------------------------ who we feed

export function renderWho(data: WhoData, ctx: RenderContext, anchor: string): Markup {
  const seen = new Set<string>();
  const cities = data.cities.filter((c) => CITY_GEOMETRY[c.city] && !seen.has(c.city) && seen.add(c.city));
  const separator = ctx.lang === 'ar' ? '، ' : ', ';
  const mapTitle = `${data.mapIntro} ${cities.map((c) => c.label).join(separator)}.`.trim();
  const cta = resolveLink(data.ctaTarget, ctx);
  return html`<section class="who on-cream" id="${anchor}" aria-labelledby="who-title" data-reveal>
<div class="who-panel">
<div class="who-start">${heading('who-title', data.title)}${textOr(data.lede, (v) => html`<p class="who-lede" data-r style="--i:1">${v}</p>`)}<ul class="sectors" data-r style="--i:2">${data.sectors.map(
    (s) => html`<li>${trusted(`<svg viewBox="0 0 48 48" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${SECTOR_ICONS[s.icon] ?? SECTOR_ICONS.canteen ?? ''}</svg>`)}<span>${s.label}</span></li>`,
  )}</ul>${
    data.meaningTitle || data.meaningText
      ? html`<div class="name-meaning" data-r style="--i:3">${textOr(data.meaningTitle, (v) => html`<h3>${v}</h3>`)}${textOr(data.meaningText, (v) => html`<p>${v}</p>`)}</div>`
      : ''
  }</div>
<div class="who-end"><figure class="map" data-r style="--i:2"><svg viewBox="0 -44 1000 984" direction="ltr" role="img" aria-labelledby="map-title"><title id="map-title">${mapTitle}</title><path class="land" d="${LIBYA_LAND}"/><g class="routes">${cities.map((c) => {
    const g = CITY_GEOMETRY[c.city]!;
    return html`<path class="route" data-c="${c.city}" d="${g.route}" pathLength="1" style="--i:${g.i}"/>`;
  })}</g>${cities.map((c) => {
    const g = CITY_GEOMETRY[c.city]!;
    return html`<g class="city${g.minor ? ' minor' : ''}" data-c="${c.city}" style="--i:${g.i}"><circle class="hit" cx="${g.cx}" cy="${g.cy}" r="30"/><circle class="dot" cx="${g.cx}" cy="${g.cy}" r="8"/>${
      c.showLabel ? html`<text class="lbl${g.out ? ' out' : ''}" x="${g.lx}" y="${g.ly}" text-anchor="${g.anchor}">${c.label}</text>` : ''
    }</g>`;
  })}<g class="hq"><circle class="pulse" cx="${MAP_HQ.x}" cy="${MAP_HQ.y}" r="40"/><g transform="translate(627 40) scale(0.2759)"><g transform="translate(0.000000,355.000000) scale(0.100000,-0.100000)"><path class="hq-q" d="${Q_BODY}"/><path class="hq-leaf" d="${Q_LEAF}"/></g></g><text class="hq-lbl" x="617" y="108" text-anchor="end">${data.hqLabel}</text></g></svg></figure>${
    data.facts.length ? html`<dl class="facts" data-r style="--i:3">${data.facts.map((f) => html`<div><dt>${f.label}</dt><dd>${f.value}</dd></div>`)}</dl>` : ''
  }${cta && data.ctaLabel ? html`<a class="btn" href="${cta}" data-r style="--i:4"><span>${data.ctaLabel}</span>${ARROW}</a>` : ''}</div>
</div>
</section>`;
}

// ------------------------------------------------------------------ latest news (home)

export function renderNewsSection(data: NewsSectionData, ctx: RenderContext, anchor: string): Markup {
  const count = Math.min(6, Math.max(1, data.homeCount ?? 3));
  const newsPath = ctx.site.languages.find((l) => l.code === ctx.lang)?.news ?? (ctx.lang === 'ar' ? '/news/' : '/en/news/');
  return html`<section class="news-home on-cream" id="${anchor}" aria-labelledby="news-title" data-reveal data-hide-empty="${data.hideWhenEmpty ? 'true' : 'false'}">
<div class="news-head"><div class="news-head-text">${heading('news-title', data.title)}${textOr(data.intro, (v) => html`<p class="news-intro" data-r style="--i:1">${v}</p>`)}</div>${
    data.viewAllLabel ? html`<a class="btn btn-quiet news-all" href="${newsPath}" data-r style="--i:2"><span>${data.viewAllLabel}</span>${ARROW}</a>` : ''
  }</div>
<div class="news-body" data-news-home data-count="${count}" data-read-more="${data.readMoreLabel}" data-empty="${data.emptyText}" aria-busy="true">${newsSkeleton(ctx.ui, count)}</div>
</section>`;
}

/** The shape of what is coming, in the cream one step down: one card per story (picture, headline, lines). */
export function newsSkeleton(ui: UiStrings, cards = 3): Markup {
  const n = Math.max(1, Math.min(cards, 6));
  const card = html`<span class="sk-card"><span class="sk sk-photo"></span><span class="sk-lines"><span class="sk sk-line big"></span><span class="sk sk-line short"></span><span class="sk sk-line"></span><span class="sk sk-line half"></span></span></span>`;
  return html`<div class="news-loading nc-grid" data-n="${Math.min(n, 3)}" role="status"><span class="sr-only">${ui.newsLoading}</span>${Array.from({ length: n }, () => card)}</div>`;
}

// ------------------------------------------------------------------ questions

export function renderFaq(data: FaqData, ctx: RenderContext, anchor: string): Markup {
  const s = ctx.settings;
  return html`<section class="faq" id="${anchor}" aria-labelledby="faq-title" data-reveal>
<div class="faq-grid">
${heading('faq-title', data.title)}
<div class="faq-side" data-r style="--i:1">${textOr(data.sideTitle, (v) => html`<p>${v}</p>`)}${contactLinks(s)}${textOr(s.address, (v) => html`<p>${v}</p>`)}</div>
<div class="faq-list">${data.items.map(
    (q, i) => html`<details class="qa" data-r="side" style="--i:${i + 1}"><summary><span>${q.question}</span><span class="chev">${CHEVRON}</span></summary><div class="qa-a">${paragraphs(q.answer).map((p) => html`<p>${p}</p>`)}</div></details>`,
  )}</div>
${
  data.morePrefix || data.moreLinkLabel
    ? html`<p class="faq-more" data-r style="--i:8">${data.morePrefix} ${s.email && data.moreLinkLabel ? html`<a class="u" href="${safeHref(`mailto:${s.email}`)}">${fill(data.moreLinkLabel, { email: s.email, phone: s.phoneDisplay || s.phone })}</a>` : ''}</p>`
    : ''
}
</div>
</section>`;
}

function contactLinks(s: Settings): Markup {
  return html`${s.email ? html`<a class="u" href="${safeHref(`mailto:${s.email}`)}">${s.email}</a>` : ''}${
    s.phone ? html`<a class="u" href="${telHref(s.phone)}" dir="ltr">${s.phoneDisplay || s.phone}</a>` : ''
  }`;
}

// ------------------------------------------------------------------ contact us

/** A wa.me link for the phone number in Settings (digits only, with the country code). */
export function whatsappHref(phone: string): string | null {
  const digits = phone.replace(/\D/g, '').replace(/^00/, '');
  return digits.length >= 8 ? `https://wa.me/${digits}` : null;
}

export function renderContact(data: ContactData, ctx: RenderContext, anchor: string): Markup {
  const s = ctx.settings;
  const wa = data.whatsappLabel && s.phone ? whatsappHref(s.phone) : null;
  const reach: Markup[] = [];
  if (s.phone) reach.push(html`<li><a class="reach" href="${telHref(s.phone)}"><span class="reach-k">${ctx.ui.phone}</span><span class="reach-v" dir="ltr">${s.phoneDisplay || s.phone}</span></a></li>`);
  if (wa) reach.push(html`<li><a class="reach" href="${wa}" target="_blank" rel="noopener"><span class="reach-k">${ctx.ui.whatsapp}</span><span class="reach-v">${data.whatsappLabel}</span></a></li>`);
  if (s.email) reach.push(html`<li><a class="reach" href="${safeHref(`mailto:${s.email}`)}"><span class="reach-k">${ctx.ui.email}</span><span class="reach-v" dir="ltr">${s.email}</span></a></li>`);
  const optional = data.optionalLabel ? html`<span class="opt">(${data.optionalLabel})</span>` : '';
  return html`<section class="contact on-cream" id="${anchor}" aria-labelledby="contact-title" data-reveal>
<div class="coupon"><span class="perf" aria-hidden="true"></span>
<div class="coupon-stub"><h2 id="contact-title">${data.title}</h2>${textOr(data.sub, (v) => html`<p class="sub">${v}</p>`)}${
    reach.length ? html`<div class="contact-reach">${textOr(data.directLabel, (v) => html`<p class="reach-label">${v}</p>`)}<ul class="reach-list">${reach}</ul></div>` : ''
  }${
    data.image
      ? html`<div class="stub-foot"><figure class="stamp" aria-hidden="true"><span class="q-frame"><span class="q-mask">${picture(data.image, { sizes: '120px', decorative: true })}</span>${Q_LEAF_IMG}</span></figure></div>`
      : ''
  }</div>
<form id="contact-form" class="coupon-form" novalidate data-msg-name="${data.msgNameMissing}" data-msg-phone="${data.msgPhoneInvalid}" data-msg-email="${data.msgEmailInvalid}" data-msg-message="${data.msgMessageMissing}" data-msg-sending="${data.msgSending}" data-msg-success="${data.msgSuccess}" data-msg-error="${data.msgError}">
<div class="fields"><label><span class="lbl">${data.nameLabel}</span><input type="text" name="name" autocomplete="name" required aria-required="true" maxlength="120" placeholder="${data.namePlaceholder}"></label><label><span class="lbl">${data.phoneLabel}</span><input type="tel" name="phone" autocomplete="tel" inputmode="tel" required aria-required="true" maxlength="40" placeholder="${data.phonePlaceholder}" dir="ltr"></label><label class="wide"><span class="lbl">${data.emailLabel}${optional}</span><input type="email" name="email" autocomplete="email" maxlength="254" placeholder="${data.emailPlaceholder}" dir="ltr"></label><label class="wide"><span class="lbl">${data.messageLabel}</span><textarea name="message" rows="5" required aria-required="true" maxlength="2000" placeholder="${data.messagePlaceholder}"></textarea></label><label class="hp" aria-hidden="true">Website<input type="text" name="website" tabindex="-1" autocomplete="off"></label></div>
<button class="btn btn-leaf" type="submit"><span>${data.submitLabel}</span>${ARROW}</button>
<p class="contact-status" role="status" aria-live="polite"></p>
</form>
</div>
</section>`;
}

// ------------------------------------------------------------------ visit us

export function renderVisit(data: VisitData, ctx: RenderContext, anchor: string): Markup {
  const s = ctx.settings;
  const seaWords = data.seaLabel.split(/\s+/).filter(Boolean);
  const sea =
    ctx.lang === 'en' && seaWords.length > 1
      ? html`<tspan x="150">${seaWords.slice(0, -1).join(' ')}</tspan><tspan x="150" dy="54">${seaWords.at(-1)}</tspan>`
      : html`${data.seaLabel}`;
  const maps = s.mapsUrl ? safeHref(s.mapsUrl) : null;
  return html`<section class="visit on-cream" id="${anchor}" aria-labelledby="location-title" data-reveal>
<div class="visit-map"><svg viewBox="0 0 1900 1000" preserveAspectRatio="xMinYMid meet" direction="ltr" role="img" aria-labelledby="visit-map-title"><title id="visit-map-title">${data.mapTitle}</title><path class="sea" d="${BENGHAZI_SEA}"/><text class="sea-lbl" x="150" y="230">${sea}</text><g class="city"><circle class="city-ping" cx="648" cy="466" r="42"/><g class="city-q"><g class="city-lift"><g transform="translate(597.4 404.0) scale(0.3493)"><g transform="translate(0.000000,355.000000) scale(0.100000,-0.100000)"><path class="q-body" d="${Q_BODY}"/><path class="q-leaf" d="${Q_LEAF}"/></g></g></g></g><text class="city-lbl" x="721" y="482">${data.cityLabel}</text></g>${
    data.scaleLabel ? html`<g class="scale"><rect x="330" y="900" width="224.5" height="10"/><text x="572.5" y="911">${data.scaleLabel}</text></g>` : ''
  }</svg></div>
<div class="visit-inner"><div class="visit-text">${heading('location-title', data.title)}${textOr(data.lede, (v) => html`<p class="visit-lede" data-r style="--i:1">${v}</p>`)}<div class="visit-status" data-r style="--i:2" data-hours hidden><p class="visit-now" aria-live="polite"></p><p class="visit-clock"></p></div>${
    data.hoursRows.length
      ? html`<dl class="visit-hours" data-r style="--i:3">${data.hoursRows.map(
          (r) => html`<div data-days="${r.days.join(',')}"><dt>${r.label}</dt><dd><span class="nw">${r.value}</span></dd></div>`,
        )}</dl>`
      : ''
  }<div class="visit-actions" data-r style="--i:4">${
    maps && data.mapsLabel
      ? html`<a class="btn" href="${maps}" target="_blank" rel="noopener"><span>${data.mapsLabel}</span>${EXTERNAL}<span class="sr-only">${data.newTabLabel || ctx.ui.opensNewTab}</span></a>`
      : ''
  }${s.phone && data.callLabel ? html`<a class="u visit-call" href="${telHref(s.phone)}">${data.callLabel}</a>` : ''}</div></div></div>
</section>`;
}

// ------------------------------------------------------------------ footer

export function renderFooter(data: FooterData, ctx: RenderContext): Markup {
  const s = ctx.settings;
  const other = otherLanguage(ctx);
  const year = String(new Date().getFullYear());
  const marks = [s.footerMarkAr, s.footerMarkEn].filter((m): m is Picture => m !== null);
  return html`<footer class="site-footer" aria-label="${data.contactLabel}">
<div class="footer-band" aria-hidden="true"><div class="band-track"></div></div>
<div class="footer-body">
<div class="footer-top"><a class="footer-marks" href="#main" aria-label="${data.backToTopLabel}">${marks.map(
    (m) => html`<img class="footer-mark" src="${m.url}" width="${m.width}" height="${m.height}" alt="${m.alt}" loading="lazy">`,
  )}</a><address class="footer-reach">${contactLinks(s)}${textOr(s.address, (v) => html`<span>${v}</span>`)}</address></div>
<div class="footer-legal">${data.lines.map((l) => html`<p>${fill(l.text, { year })}</p>`)}<div class="legal-links">${
    other ? html`<a class="u" href="${ctx.alternate(other.code)}" hreflang="${other.code}" lang="${other.code}" data-lang-switch="${other.code}">${other.label}</a>` : ''
  }${data.backToTopLabel ? html`<a class="u" href="#main">${data.backToTopLabel}</a>` : ''}</div></div>
</div>
</footer>`;
}

// ------------------------------------------------------------------ page assembly

export function sectionData<K extends SectionPayload['key']>(site: SitePayload, key: K): SectionPayload<K> | undefined {
  return site.sections.find((s) => s.key === key) as SectionPayload<K> | undefined;
}

/** All sections between the header and the footer, in the order set in the dashboard. */
export function renderMain(site: SitePayload, ctx: RenderContext): Markup {
  const parts = site.sections.map((section) => {
    const anchor = section.anchor ?? section.key;
    switch (section.key) {
      case 'hero':
        return renderHero(section.data as HeroData, ctx);
      case 'supplies':
        return renderSupplies(section.data as SuppliesData, ctx, anchor);
      case 'how':
        return renderHow(section.data as HowData, ctx, anchor);
      case 'who':
        return renderWho(section.data as WhoData, ctx, anchor);
      case 'news':
        return renderNewsSection(section.data as NewsSectionData, ctx, anchor);
      case 'faq':
        return renderFaq(section.data as FaqData, ctx, anchor);
      case 'contact':
        return renderContact(section.data as ContactData, ctx, anchor);
      case 'visit':
        return renderVisit(section.data as VisitData, ctx, anchor);
      default:
        return html``;
    }
  });
  return html`${parts}`;
}

export function anchorsOf(site: SitePayload): Set<string> {
  return new Set(site.sections.filter((s) => s.anchor).map((s) => s.anchor as string));
}

/** For text placed in attributes outside html`` (rare); kept here so escaping has one home. */
export const attr = escapeHtml;
