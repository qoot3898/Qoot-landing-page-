import type { Lang } from '../shared/types';

/**
 * Interface wording that is not editable content (states, controls, dates). Everything a visitor
 * reads about Qoot itself comes from the API.
 */
const strings = {
  ar: {
    mainNav: 'القائمة الرئيسية',
    loading: 'جارٍ التحميل…',
    pageError: 'تعذّر تحميل الصفحة.',
    pageErrorHint: 'تحقق من الاتصال ثم حاول مرة أخرى.',
    retry: 'حاول مرة أخرى',
    newsLoading: 'جارٍ تحميل الأخبار…',
    newsError: 'تعذّر تحميل الأخبار.',
    allNews: 'كل الأخبار',
    allCategories: 'الكل',
    filterBy: 'تصفية حسب التصنيف',
    backToNews: 'كل الأخبار',
    showMore: 'عرض المزيد',
    showing: (shown: number, total: number) => `${shown} من ${total}`,
    copyLink: 'نسخ الرابط',
    copied: 'تم نسخ الرابط',
    moreNews: 'أخبار أخرى',
    notFoundTitle: 'هذا الخبر غير موجود',
    notFoundText: 'ربما حُذف أو لم يُنشر بعد.',
    previewBanner: 'معاينة: تعرض هذه الصفحة المسودات غير المنشورة. الزوار لا يرونها.',
    previewSignIn: 'سجّل الدخول إلى لوحة التحكم لمعاينة المسودات.',
    previewExit: 'الخروج من المعاينة',
    dashboard: 'لوحة التحكم',
    opensNewTab: '(يُفتح في نافذة جديدة)',
    readingIn: 'هذا الخبر متاح بالإنجليزية فقط.',
    homeLink: 'الصفحة الرئيسية',
    phone: 'الهاتف',
    whatsapp: 'واتساب',
    email: 'البريد',
    readStory: 'اقرأ الخبر',
    readMore: 'اقرأ المزيد',
    readLess: 'عرض أقل',
    latest: 'الأحدث',
    noStories: 'لا توجد أخبار في هذا التصنيف بعد.',
    breadcrumb: 'مسار التصفح',
  },
  en: {
    mainNav: 'Main',
    loading: 'Loading…',
    pageError: "We couldn't load this page.",
    pageErrorHint: 'Check your connection and try again.',
    retry: 'Try again',
    newsLoading: 'Loading the news…',
    newsError: "We couldn't load the news.",
    allNews: 'All news',
    allCategories: 'All',
    filterBy: 'Filter by category',
    backToNews: 'All news',
    showMore: 'Show more',
    showing: (shown: number, total: number) => `${shown} of ${total}`,
    copyLink: 'Copy link',
    copied: 'Link copied',
    moreNews: 'More news',
    notFoundTitle: 'This story doesn’t exist',
    notFoundText: 'It may have been removed, or it isn’t published yet.',
    previewBanner: 'Preview: this page shows unpublished drafts. Visitors don’t see them.',
    previewSignIn: 'Sign in to the dashboard to preview drafts.',
    previewExit: 'Leave preview',
    dashboard: 'Dashboard',
    opensNewTab: '(opens in a new tab)',
    readingIn: 'This story is only available in Arabic.',
    homeLink: 'Home',
    phone: 'Phone',
    whatsapp: 'WhatsApp',
    email: 'Email',
    readStory: 'Read the story',
    readMore: 'Read more',
    readLess: 'Show less',
    latest: 'Latest',
    noStories: 'No stories in this category yet.',
    breadcrumb: 'Breadcrumb',
  },
} as const;

export type UiStrings = (typeof strings)['ar'] | (typeof strings)['en'];

export function ui(lang: Lang): UiStrings {
  return strings[lang];
}

/** The language of the page, from the URL: /en/... is English, everything else Arabic. */
export function pageLang(pathname = location.pathname): Lang {
  return /^\/en(\/|$)/.test(pathname) ? 'en' : 'ar';
}

export function formatDate(iso: string | null | undefined, lang: Lang): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return new Intl.DateTimeFormat(lang === 'ar' ? 'ar-LY' : 'en-GB', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    numberingSystem: 'latn',
    timeZone: 'Africa/Tripoli',
  }).format(date);
}

/** Site paths written once by editors (/news/) point to the same page in the visitor's language. */
export function localizePath(href: string, lang: Lang): string {
  if (lang === 'ar' || !href.startsWith('/') || href.startsWith('//')) return href;
  if (/^\/(en|admin|api|media|assets)(\/|$)/.test(href)) return href;
  return '/en' + href;
}

const PREF_KEY = 'qoot.lang';

export function rememberLanguage(lang: Lang): void {
  try {
    localStorage.setItem(PREF_KEY, lang);
  } catch {
    /* storage can be unavailable (private mode); the URL still carries the language */
  }
}
