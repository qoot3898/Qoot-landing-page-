import { api, ApiError } from '../shared/api';
import type { Lang, Settings } from '../shared/types';
import { rememberLanguage } from './i18n';

/*
 * The original page's behaviour (script.js), unchanged in what visitors see, now started after the
 * page has been built from the API:
 *   header that tucks away and returns, phone menu, the Q-window photo reel, section entrances,
 *   stacking "how we work" cards, map roads that light up, smooth FAQ answers, the Contact us form,
 *   and the open-now line in "Visit us".
 * Hidden starting states only apply under html.js and never under reduced motion.
 */

const $ = <T extends Element = HTMLElement>(s: string, r: ParentNode = document) => r.querySelector<T>(s);
const $$ = <T extends Element = HTMLElement>(s: string, r: ParentNode = document) => Array.from(r.querySelectorAll<T>(s));

/** Media queries, created on first use (and harmless where matchMedia is missing). */
const query = (q: string): Pick<MediaQueryList, 'matches' | 'addEventListener'> =>
  typeof window.matchMedia === 'function' ? window.matchMedia(q) : { matches: false, addEventListener: () => undefined };
let queries: { reduce: ReturnType<typeof query>; phone: ReturnType<typeof query>; fine: ReturnType<typeof query> } | null = null;
const mq = () => (queries ??= {
  reduce: query('(prefers-reduced-motion: reduce)'),
  phone: query('(max-width: 720px)'),
  fine: query('(hover: hover) and (pointer: fine)'),
});

/** Remembers the language a visitor picks so the Arabic home can send returning English readers to /en/. */
export function initLanguageSwitch(): void {
  $$('[data-lang-switch]').forEach((a) =>
    a.addEventListener('click', () => rememberLanguage(a.getAttribute('data-lang-switch') === 'en' ? 'en' : 'ar')),
  );
}

// 0. Header ------------------------------------------------------------------
export function initHeader(): void {
  const header = $('.site-header');
  if (!header) return;
  const menuBtn = $<HTMLButtonElement>('.menu-btn', header);
  const nav = $('.site-nav', header);
  const wide = window.matchMedia('(min-width: 1025px)');
  let lastY = window.scrollY || 0;
  let openY = 0;
  let tick = false;
  const isOpen = () => header.classList.contains('menu-open');
  const setMenu = (open: boolean, returnFocus = false) => {
    if (!menuBtn || open === isOpen()) return;
    header.classList.toggle('menu-open', open);
    menuBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
    const label = $('.menu-txt', menuBtn);
    if (label) label.textContent = (open ? menuBtn.dataset.close : menuBtn.dataset.open) ?? '';
    if (open) {
      openY = window.scrollY || 0;
      nav?.querySelector<HTMLElement>('a')?.focus({ preventScroll: true });
    } else if (returnFocus) {
      menuBtn.focus({ preventScroll: true });
    }
  };
  const update = () => {
    tick = false;
    const y = window.scrollY || 0;
    if (isOpen() && Math.abs(y - openY) > 80) setMenu(false);
    if (y <= 2) {
      header.classList.remove('is-stuck', 'is-shown', 'is-ready');
      lastY = y;
      return;
    }
    if (!header.classList.contains('is-stuck')) {
      if (y > header.offsetHeight + 40) {
        header.classList.add('is-stuck');
        void header.offsetWidth;
        header.classList.add('is-ready');
      }
      lastY = y;
      return;
    }
    const d = y - lastY;
    if (Math.abs(d) < 8) return;
    if (d < 0) header.classList.add('is-shown');
    else if (!isOpen()) header.classList.remove('is-shown');
    lastY = y;
  };
  window.addEventListener('scroll', () => {
    if (!tick) {
      tick = true;
      requestAnimationFrame(update);
    }
  }, { passive: true });
  update();

  if (menuBtn && nav) {
    menuBtn.addEventListener('click', () => setMenu(!isOpen()));
    nav.addEventListener('click', (e) => {
      if ((e.target as Element).closest('a')) setMenu(false);
    });
    document.addEventListener('click', (e) => {
      if (isOpen() && !header.contains(e.target as Node)) setMenu(false);
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && isOpen()) setMenu(false, true);
    });
    wide.addEventListener('change', () => {
      if (wide.matches) setMenu(false);
    });
  }
}

// 1. Hero reel ---------------------------------------------------------------
export function initHero(): void {
  const hero = $('.hero');
  const reel = hero && $('.reel', hero);
  const qwin = hero && $('.qwin', hero);
  const cap = hero && $('[data-cap]', hero);
  if (!hero || !reel || !qwin) return;
  const frames = $$<HTMLImageElement>('img', reel);
  if (frames.length === 0) return;
  // Stack the pictures one under another, as the original markup did with nth-child rules (any count works).
  Array.from(reel.children).forEach((p, i) => ((p as HTMLElement).style.top = `${i * 100}%`));
  const steps = Math.max(frames.length - 1, 1);
  let current = 0;
  let ticking = false;
  const ease = (t: number) => (t < 0 ? 0 : t > 1 ? 1 : t * t * (3 - 2 * t));

  const setCaption = (idx: number) => {
    if (idx === current || !cap) return;
    current = idx;
    const text = frames[idx]?.getAttribute('data-caption') ?? '';
    if (mq().reduce.matches || !cap.animate) {
      cap.textContent = text;
      return;
    }
    const x = mq().phone.matches ? '' : 'translateX(-50%) ';
    cap.animate([{ opacity: 1, transform: `${x}translateY(0)` }, { opacity: 0, transform: `${x}translateY(-8px)` }], { duration: 160, easing: 'cubic-bezier(.22,1,.36,1)', fill: 'forwards' }).onfinish = () => {
      cap.textContent = text;
      cap.animate([{ opacity: 0, transform: `${x}translateY(8px)` }, { opacity: 1, transform: `${x}translateY(0)` }], { duration: 380, easing: 'cubic-bezier(.22,1,.36,1)', fill: 'forwards' });
    };
  };

  const place = (y: number) => {
    reel.style.transform = `translate3d(0,${-y * 100}%,0)`;
    frames.forEach((f, k) => (f.style.visibility = Math.abs(k - y) < 1 ? 'visible' : 'hidden'));
  };

  const update = () => {
    ticking = false;
    if (mq().reduce.matches || mq().phone.matches) return;
    const r = hero.getBoundingClientRect();
    const travel = hero.offsetHeight - window.innerHeight;
    const p = travel > 0 ? Math.min(Math.max(-r.top / travel, 0), 1) : 0;
    const pos = frames.length > 1 ? p * steps : 0;
    const base = Math.min(Math.floor(pos), steps - 1);
    const t = frames.length > 1 ? ease((pos - base - 0.25) / 0.5) : 0;
    const y = base + t;
    place(y);
    qwin.style.setProperty('--lr', `${(Math.sin(t * Math.PI) * -9).toFixed(2)}deg`);
    qwin.style.setProperty('--qs', (1 + p * 0.06).toFixed(4));
    hero.style.setProperty('--hx', `${(p * 28).toFixed(1)}px`);
    setCaption(Math.round(y));
  };
  const onScroll = () => {
    if (!ticking) {
      ticking = true;
      requestAnimationFrame(update);
    }
  };

  const stage = $('.hero-stage', hero);
  stage?.addEventListener('pointermove', (e) => {
    if (!mq().fine.matches || mq().reduce.matches) return;
    const r = stage.getBoundingClientRect();
    const dx = (e.clientX - r.left) / r.width - 0.5;
    const dy = (e.clientY - r.top) / r.height - 0.5;
    reel.style.setProperty('--px', `${(-dx * 4).toFixed(2)}%`);
    reel.style.setProperty('--py', `${(-dy * 4).toFixed(2)}%`);
  });
  stage?.addEventListener('pointerleave', () => {
    reel.style.setProperty('--px', '0%');
    reel.style.setProperty('--py', '0%');
  });
  window.addEventListener('scroll', onScroll, { passive: true });
  window.addEventListener('resize', onScroll, { passive: true });
  update();

  if (frames.length < 2) return;
  // Phones: the hero is not held, so the reel advances on its own while it is on screen.
  const first = reel.children[0]!.cloneNode(true) as HTMLElement;
  first.setAttribute('aria-hidden', 'true');
  first.style.top = `${frames.length * 100}%`;
  const firstImg = first.querySelector('img')!;
  firstImg.alt = '';
  firstImg.removeAttribute('fetchpriority');
  firstImg.style.visibility = 'hidden';
  reel.appendChild(first);
  let timer: number | undefined;
  let auto = 0;
  let visible = true;
  const advance = () => {
    auto += 1;
    frames.forEach((f) => (f.style.visibility = 'visible'));
    firstImg.style.visibility = 'visible';
    reel.style.transition = 'transform 1s cubic-bezier(.76,0,.24,1)';
    reel.style.transform = `translate3d(0,${-auto * 100}%,0)`;
    qwin.style.setProperty('--lr', '-9deg');
    window.setTimeout(() => qwin.style.setProperty('--lr', '0deg'), 520);
    setCaption(auto % frames.length);
    if (auto === frames.length) {
      window.setTimeout(() => {
        reel.style.transition = 'none';
        auto = 0;
        reel.style.transform = 'translate3d(0,0,0)';
      }, 1050);
    }
  };
  const runAuto = () => {
    window.clearInterval(timer);
    timer = undefined;
    if (mq().phone.matches && !mq().reduce.matches) {
      frames.forEach((f) => (f.style.visibility = 'visible'));
      if (visible && !document.hidden) timer = window.setInterval(advance, 3400);
    } else {
      reel.style.transition = '';
      firstImg.style.visibility = 'hidden';
      update();
    }
  };
  new IntersectionObserver((entries) => {
    visible = entries[0]?.isIntersecting ?? true;
    runAuto();
  }).observe(qwin);
  document.addEventListener('visibilitychange', runAuto);
  mq().phone.addEventListener('change', runAuto);
  runAuto();
}

// 2. Entrances ---------------------------------------------------------------
export function initReveal(root: ParentNode = document): void {
  const groups = $$('[data-reveal]', root);
  if ('IntersectionObserver' in window && !mq().reduce.matches) {
    const io = new IntersectionObserver(
      (entries) => {
        entries.forEach((e) => {
          if (!e.isIntersecting) return;
          const el = e.target as HTMLElement;
          io.unobserve(el);
          el.classList.add('is-in');
          window.setTimeout(() => el.classList.add('is-done'), 1500);
        });
      },
      // Starts a little before a section scrolls in, so nothing is still invisible when it arrives.
      { rootMargin: '0px 0px 12% 0px', threshold: 0 },
    );
    groups.forEach((g) => io.observe(g));
  } else {
    groups.forEach((g) => g.classList.add('is-in', 'is-done'));
  }
}

// 3. How we work: the cards stack as you scroll ------------------------------
export function initStack(): void {
  const cards = $$('.stack .card');
  if (!cards.length) return;
  let tops: number[] = [];
  const measure = () => {
    tops = cards.map((c) => parseFloat(getComputedStyle(c).top) || 0);
  };
  let tick = false;
  const stack = () => {
    tick = false;
    if (mq().reduce.matches) return;
    const vh = window.innerHeight;
    const r = cards.map((c) => c.getBoundingClientRect().top);
    cards.forEach((c, i) => {
      const bottom = (r[i] ?? 0) + c.offsetHeight;
      let cover = 0;
      for (let j = i + 1; j < cards.length; j++) {
        const p = (bottom - (r[j] ?? 0)) / c.offsetHeight;
        cover += p < 0 ? 0 : p > 1 ? 1 : p;
      }
      c.style.setProperty('--s', (1 - Math.min(cover, 3) * 0.05).toFixed(4));
      c.style.setProperty('--dim', Math.min(cover * 0.28, 0.6).toFixed(3));
      let e = 1 - ((r[i] ?? 0) - (tops[i] ?? 0)) / vh;
      e = e < 0 ? 0 : e > 1 ? 1 : e;
      c.style.setProperty('--z', (1.2 - e * 0.2).toFixed(4));
    });
  };
  const onStack = () => {
    if (!tick) {
      tick = true;
      requestAnimationFrame(stack);
    }
  };
  measure();
  window.addEventListener('scroll', onStack, { passive: true });
  window.addEventListener('resize', () => {
    measure();
    onStack();
  }, { passive: true });
  stack();
}

// Who we feed: pointing at a city lights up its road from Benghazi ------------
export function initMap(): void {
  $$('.map .city').forEach((city) => {
    const road = $(`.map .route[data-c="${CSS.escape(city.getAttribute('data-c') ?? '')}"]`);
    if (!road) return;
    city.addEventListener('pointerenter', () => {
      city.classList.add('is-hot');
      road.classList.add('is-hot');
    });
    city.addEventListener('pointerleave', () => {
      city.classList.remove('is-hot');
      road.classList.remove('is-hot');
    });
  });
}

// 4. FAQ: smooth open and close ----------------------------------------------
export function initFaq(): void {
  $$<HTMLDetailsElement>('.qa').forEach((d) => {
    const summary = $('summary', d);
    const answer = $('.qa-a', d);
    if (!summary || !answer || !answer.animate) return;
    let anim: Animation | null = null;
    summary.addEventListener('click', (e) => {
      if (mq().reduce.matches) return;
      e.preventDefault();
      anim?.cancel();
      if (d.open) {
        const h = answer.offsetHeight;
        d.classList.add('closing');
        anim = answer.animate([{ height: `${h}px`, opacity: 1 }, { height: '0px', opacity: 0 }], { duration: 320, easing: 'cubic-bezier(.76,0,.24,1)' });
        anim.onfinish = () => {
          d.open = false;
          d.classList.remove('closing');
          anim = null;
        };
      } else {
        d.open = true;
        const full = answer.offsetHeight;
        anim = answer.animate([{ height: '0px', opacity: 0 }, { height: `${full}px`, opacity: 1 }], { duration: 460, easing: 'cubic-bezier(.22,1,.36,1)' });
        anim.onfinish = () => {
          anim = null;
        };
      }
    });
  });
}

// 5. Contact us ----------------------------------------------------------------
/** Accepts what people type as a phone number: +218 91 234 5678, 091-2345678, (091) 234 5678 ... */
export function looksLikePhone(value: string): boolean {
  const v = value.trim();
  const digits = v.replace(/\D/g, '').length;
  return digits >= 6 && digits <= 15 && /^\+?[0-9 ()\-.]{6,24}$/.test(v);
}

export function looksLikeEmail(value: string): boolean {
  return /^[^\s@<>()",;:\\]+@[^\s@<>()",;:\\]+\.[^\s@<>()",;:\\]{2,}$/.test(value.trim());
}

/** The Contact us form: name, phone and message are needed, the email is optional. */
export function initContactForm(lang: Lang): void {
  const form = document.getElementById('contact-form') as HTMLFormElement | null;
  if (!form) return;
  const status = $('.contact-status', form);
  const button = $<HTMLButtonElement>('button[type="submit"]', form);
  const msg = form.dataset;

  const say = (text: string, kind: 'info' | 'error' | 'ok' = 'info') => {
    if (!status) return;
    status.textContent = text;
    status.dataset.kind = kind;
  };
  const field = (name: string) => (form.elements.namedItem(name) as HTMLInputElement | null)?.value.trim() ?? '';

  // Typing in a field that was marked wrong clears the mark and the message.
  form.addEventListener('input', (e) => {
    const target = e.target as HTMLElement;
    if (target.getAttribute('aria-invalid') === 'true') {
      target.removeAttribute('aria-invalid');
      if (status?.dataset.kind === 'error') say('');
    }
    if (status?.dataset.kind === 'ok') say('');
  });

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (button?.disabled) return;
    form.querySelectorAll('[aria-invalid]').forEach((el) => el.removeAttribute('aria-invalid'));
    const invalid = (name: string, message: string) => {
      const el = form.elements.namedItem(name) as HTMLElement | null;
      el?.setAttribute('aria-invalid', 'true');
      el?.focus();
      say(message, 'error');
    };
    if (!field('name')) return invalid('name', msg.msgName ?? '');
    if (!looksLikePhone(field('phone'))) return invalid('phone', msg.msgPhone ?? '');
    if (field('email') && !looksLikeEmail(field('email'))) return invalid('email', msg.msgEmail ?? '');
    if (!field('message')) return invalid('message', msg.msgMessage ?? '');

    say(msg.msgSending ?? '');
    if (button) button.disabled = true;
    form.setAttribute('aria-busy', 'true');
    try {
      await api('/api/public/contact', {
        method: 'POST',
        lang,
        body: {
          name: field('name'),
          phone: field('phone'),
          email: field('email'),
          message: (form.elements.namedItem('message') as HTMLTextAreaElement | null)?.value ?? '',
          language: lang,
          website: field('website'),
        },
      });
      form.reset();
      say(msg.msgSuccess ?? '', 'ok');
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && Object.keys(error.errors).length) {
        const [name, messages] = Object.entries(error.errors)[0]!;
        invalid(name, messages[0] ?? error.message);
      } else {
        say(error instanceof ApiError && error.code === 'rate_limited' ? error.message : msg.msgError ?? '', 'error');
      }
    } finally {
      if (button) button.disabled = false;
      form.removeAttribute('aria-busy');
    }
  });
}

// Location: open or closed right now, by the clock in Benghazi ----------------
export function minutes(t: string): number {
  const [h, m] = t.split(':');
  return Number(h) * 60 + Number(m);
}

export interface OpenState {
  message: string;
  isOpen: boolean;
  day: number;
  time: string;
}

/** Works out the "open now" line for a moment in time (exported for tests). */
export function openState(now: Date, settings: Pick<Settings, 'openDays' | 'opensAt' | 'closesAt' | 'timeZone'>, words: { open: string; today: string; tomorrow: string; day: string }, lang: Lang): OpenState | null {
  let parts: Record<string, string>;
  try {
    parts = Object.fromEntries(
      new Intl.DateTimeFormat('en-GB', { timeZone: settings.timeZone || 'Africa/Tripoli', weekday: 'short', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
        .formatToParts(now)
        .map((p) => [p.type, p.value]),
    );
  } catch {
    return null;
  }
  const WD: Record<string, number> = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 };
  const day = WD[parts.weekday ?? 'Sun'] ?? 0;
  const m = Number(parts.hour) * 60 + Number(parts.minute);
  const from = settings.opensAt || '08:00';
  const to = settings.closesAt || '17:00';
  const open = settings.openDays ?? [];
  const isOpenDay = open.includes(day);
  const isOpen = isOpenDay && m >= minutes(from) && m < minutes(to);
  const show = (t: string) => t.replace(/^0(\d):/, '$1:');
  const fillIn = (s: string, v: Record<string, string>) => s.replace(/\{(\w+)\}/g, (x, k: string) => v[k] ?? x);
  let message: string;
  if (open.length === 0) return null;
  if (isOpen) message = fillIn(words.open, { to: show(to) });
  else if (isOpenDay && m < minutes(from)) message = fillIn(words.today, { from: show(from) });
  else {
    let n = 1;
    while (n < 7 && !open.includes((day + n) % 7)) n++;
    const dayName = new Intl.DateTimeFormat(lang === 'ar' ? 'ar-LY' : 'en-GB', { weekday: 'long', timeZone: 'UTC' }).format(new Date(Date.UTC(2023, 0, 1 + ((day + n) % 7))));
    message = n === 1 ? fillIn(words.tomorrow, { from: show(from) }) : fillIn(words.day, { from: show(from), day: dayName });
  }
  return { message, isOpen, day, time: `${parts.hour}:${parts.minute}` };
}

export function initHours(settings: Settings, words: { open: string; today: string; tomorrow: string; day: string; clock: string }, lang: Lang): void {
  const box = $('[data-hours]');
  if (!box) return;
  const now = $('.visit-now', box);
  const clock = $('.visit-clock', box);
  const rows = $$('.visit-hours [data-days]');
  const tick = () => {
    const state = openState(new Date(), settings, words, lang);
    if (!state || !now || !clock) return;
    if (now.textContent !== state.message) now.textContent = state.message;
    now.classList.toggle('is-open', state.isOpen);
    clock.textContent = words.clock.replace('{time}', state.time);
    rows.forEach((r) => r.classList.toggle('is-today', (r.dataset.days ?? '').split(',').filter(Boolean).map(Number).includes(state.day)));
    box.hidden = false;
  };
  tick();
  window.setInterval(tick, 30000);
}
