import './styles/site.css';
import './styles/additions.css';
import type { VisitData } from '../shared/types';
import { html } from './html';
import { initFaq, initHeader, initHero, initHours, initLanguageSwitch, initMap, initContactForm, initReveal, initStack } from './interactions';
import { loadHomeNews } from './news-ui';
import { context, chrome, loadSite, previewBanner, redirectIfLanguageOff, setup, showLoadError, showLoading } from './shell';
import { renderMain, sectionData } from './render';
import { applySeo } from './seo';

/** The home page (/ in Arabic, /en/ in English), built from /api/public/site. */
async function start(): Promise<void> {
  const { lang, ui, preview, app } = setup();
  try {
    const { site, previewDenied } = await loadSite(lang, preview);
    if (redirectIfLanguageOff(site, lang)) return;
    const query = preview && !previewDenied ? '?preview' : '';
    const ctx = context(site, lang, 'home', (other) => (other === 'ar' ? '/' : '/en/') + query);
    const { header, footer } = chrome(site, ctx);
    const banner = preview ? previewBanner(ui, previewDenied, location.pathname) : html``;

    app.innerHTML = html`${banner}${header}<main id="main">${renderMain(site, ctx)}</main>${footer}`.value;
    app.removeAttribute('aria-busy');
    document.body.classList.toggle('is-preview', preview);
    applySeo(site.seo, { noindex: preview });

    initHeader();
    initHero();
    initReveal();
    initStack();
    initMap();
    initFaq();
    initContactForm(lang);
    initLanguageSwitch();
    const visit = sectionData(site, 'visit');
    if (visit) {
      const v = visit.data as VisitData;
      initHours(site.settings, { open: v.statusOpen, today: v.statusToday, tomorrow: v.statusTomorrow, day: v.statusDay, clock: v.clock }, lang);
    }

    // Old links to the quote form (#quote) lead to Contact us, which took its place.
    if (location.hash === '#quote') history.replaceState(null, '', '#contact');
    // A link to a section (#faq) must still land there now that the page is built, and again once the
    // news above it has loaded (unless the visitor has scrolled away in the meantime).
    const target = location.hash.length > 1 ? document.getElementById(decodeURIComponent(location.hash.slice(1))) : null;
    target?.scrollIntoView();
    const landedAt = window.scrollY;

    await loadHomeNews(lang, ui);
    if (target && Math.abs(window.scrollY - landedAt) < 4) target.scrollIntoView();
  } catch {
    showLoadError(app, ui, () => {
      showLoading(app, ui);
      void start();
    });
  }
}

void start();
