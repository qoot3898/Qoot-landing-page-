import { api, ApiError } from '../shared/api';
import type { FooterData, HeaderData, Lang, SitePayload } from '../shared/types';
import { html, type Markup } from './html';
import { pageLang, ui, type UiStrings } from './i18n';
import { anchorsOf, type RenderContext, renderFooter, renderHeader, sectionData } from './render';

declare global {
  interface Window {
    __qoot?: boolean;
    __qootSite?: Promise<Response>;
    /** Requests boot.js started early, by exact API path (see news-ui.ts early()). */
    __qootEarly?: Record<string, Promise<Response>>;
  }
}

export interface PageSetup {
  lang: Lang;
  ui: UiStrings;
  preview: boolean;
  app: HTMLElement;
}

/** Language, preview flag and the #app element; marks the script as running (see boot.js). */
export function setup(): PageSetup {
  window.__qoot = true;
  const lang = pageLang();
  document.documentElement.lang = lang;
  document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  const preview = new URLSearchParams(location.search).has('preview');
  const app = document.getElementById('app') ?? document.body;
  return { lang, ui: ui(lang), preview, app };
}

/**
 * Loads the page content. In preview mode (?preview, dashboard users only) it loads the drafts
 * instead and falls back to the public version when the visitor is not signed in.
 */
export async function loadSite(lang: Lang, preview: boolean): Promise<{ site: SitePayload; previewDenied: boolean }> {
  if (preview) {
    try {
      return { site: await api<SitePayload>(`/api/admin/preview/site?lang=${lang}`, { lang }), previewDenied: false };
    } catch (error) {
      if (!(error instanceof ApiError) || (error.status !== 401 && error.status !== 403)) throw error;
      return { site: await api<SitePayload>(`/api/public/site?lang=${lang}`, { lang }), previewDenied: true };
    }
  }

  // boot.js may already have started this request while the scripts were downloading.
  const early = window.__qootSite;
  window.__qootSite = undefined;
  if (early) {
    try {
      const response = await early;
      if (response.ok) return { site: (await response.json()) as SitePayload, previewDenied: false };
    } catch {
      /* fall through to a normal request */
    }
  }
  return { site: await api<SitePayload>(`/api/public/site?lang=${lang}`, { lang }), previewDenied: false };
}

/** Sends visitors of a language that has been turned off to the main language. */
export function redirectIfLanguageOff(site: SitePayload, lang: Lang): boolean {
  if (site.languages.some((l) => l.code === lang)) return false;
  const fallback = site.languages[0];
  if (fallback) location.replace(fallback.home);
  return true;
}

export function context(site: SitePayload, lang: Lang, page: RenderContext['page'], alternate: RenderContext['alternate']): RenderContext {
  return {
    lang,
    site,
    settings: site.settings,
    ui: ui(lang),
    page,
    anchors: anchorsOf(site),
    homePath: lang === 'ar' ? '/' : '/en/',
    alternate,
  };
}

export function chrome(site: SitePayload, ctx: RenderContext): { header: Markup; footer: Markup } {
  const header = sectionData(site, 'header');
  const footer = sectionData(site, 'footer');
  return {
    header: header ? renderHeader(header.data as HeaderData, ctx) : html``,
    footer: footer ? renderFooter(footer.data as FooterData, ctx) : html``,
  };
}

export function previewBanner(t: UiStrings, denied: boolean, exitHref: string): Markup {
  return html`<div class="preview-bar" role="status"><p>${denied ? t.previewSignIn : t.previewBanner}</p><a class="u" href="/admin/">${t.dashboard}</a><a class="u" href="${exitHref}">${t.previewExit}</a></div>`;
}

export function showLoadError(app: HTMLElement, t: UiStrings, retry: () => void): void {
  app.innerHTML = html`<div class="page-state" role="alert"><span class="page-state-q" aria-hidden="true"></span><p class="page-state-title">${t.pageError}</p><p>${t.pageErrorHint}</p><button class="btn" type="button" data-retry><span>${t.retry}</span></button></div>`.value;
  app.querySelector('[data-retry]')?.addEventListener('click', retry, { once: true });
}

export function showLoading(app: HTMLElement, t: UiStrings): void {
  app.innerHTML = html`<div class="page-state" role="status" aria-live="polite"><span class="page-state-q" aria-hidden="true"></span><p>${t.loading}</p></div>`.value;
}
