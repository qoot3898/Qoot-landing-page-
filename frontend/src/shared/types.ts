/** Shapes returned by the public API (/api/public/...). Texts are already in the page's language. */

export type Lang = 'ar' | 'en';

export interface PictureSource {
  type: string;
  srcset: string;
}

export interface Picture {
  id: number;
  url: string;
  type: string;
  width: number;
  height: number;
  alt: string;
  sources: PictureSource[];
}

export interface ListItem {
  id: string;
}

export interface Seo {
  title: string;
  description: string;
  canonical: string;
  robots: string;
  lang: Lang;
  alternates: { hreflang: string; href: string }[];
  og: Record<string, string | null>;
  twitter: Record<string, string | null>;
  jsonLd: unknown;
}

export interface Language {
  code: Lang;
  label: string;
  dir: 'rtl' | 'ltr';
  home: string;
  news: string;
}

export interface Settings {
  siteName: string;
  logoAr: Picture | null;
  logoEn: Picture | null;
  footerMarkAr: Picture | null;
  footerMarkEn: Picture | null;
  siteUrl: string;
  seoTitle: string;
  seoDescription: string;
  ogImage: Picture | null;
  ogImageAlt: string;
  email: string;
  phone: string;
  phoneDisplay: string;
  address: string;
  mapsUrl: string;
  socialLinks: (ListItem & { network: string; url: string })[];
  openDays: number[];
  opensAt: string;
  closesAt: string;
  timeZone: string;
  defaultLanguage: Lang;
  enabledLanguages: Lang[];
}

export interface HeaderData {
  links: (ListItem & { label: string; target: string })[];
  ctaLabel: string;
  ctaTarget: string;
  menuOpenLabel: string;
  menuCloseLabel: string;
  skipLabel: string;
  logoAlt: string;
}

export interface HeroData {
  titleStart: string;
  titleEnd: string;
  lede: string;
  linkLabel: string;
  linkTarget: string;
  slides: (ListItem & { image: Picture | null; caption: string })[];
}

export interface SuppliesData {
  title: string;
  intro: string;
  tiles: (ListItem & {
    name: string;
    tone: string;
    size: string;
    when: string;
    items: (ListItem & { text: string; detail: string })[];
    short: string;
    art: Picture | null;
  })[];
  notePrefix: string;
  noteLinkLabel: string;
  noteLinkTarget: string;
  noteSuffix: string;
}

export interface HowData {
  title: string;
  intro: string;
  steps: (ListItem & { title: string; text: string; tone: string; image: Picture | null; focusX: number | null; focusY: number | null })[];
}

export interface WhoData {
  title: string;
  lede: string;
  sectors: (ListItem & { label: string; icon: string })[];
  meaningTitle: string;
  meaningText: string;
  mapIntro: string;
  hqLabel: string;
  cities: (ListItem & { city: string; label: string; showLabel: boolean })[];
  facts: (ListItem & { label: string; value: string })[];
  ctaLabel: string;
  ctaTarget: string;
}

export interface NewsSectionData {
  title: string;
  intro: string;
  homeCount: number | null;
  viewAllLabel: string;
  readMoreLabel: string;
  emptyText: string;
  hideWhenEmpty: boolean;
}

export interface FaqData {
  title: string;
  sideTitle: string;
  items: (ListItem & { question: string; answer: string })[];
  morePrefix: string;
  moreLinkLabel: string;
}

/** The Contact us section: the stub (who to reach, how) and the form (name, phone, optional email, message). */
export interface ContactData {
  title: string;
  sub: string;
  directLabel: string;
  whatsappLabel: string;
  image: Picture | null;
  nameLabel: string;
  namePlaceholder: string;
  phoneLabel: string;
  phonePlaceholder: string;
  emailLabel: string;
  emailPlaceholder: string;
  optionalLabel: string;
  messageLabel: string;
  messagePlaceholder: string;
  submitLabel: string;
  msgNameMissing: string;
  msgPhoneInvalid: string;
  msgEmailInvalid: string;
  msgMessageMissing: string;
  msgSending: string;
  msgSuccess: string;
  msgError: string;
}

export interface VisitData {
  title: string;
  lede: string;
  mapsLabel: string;
  callLabel: string;
  newTabLabel: string;
  hoursRows: (ListItem & { label: string; value: string; days: number[] })[];
  statusOpen: string;
  statusToday: string;
  statusTomorrow: string;
  statusDay: string;
  clock: string;
  mapTitle: string;
  seaLabel: string;
  cityLabel: string;
  scaleLabel: string;
}

export interface FooterData {
  lines: (ListItem & { text: string })[];
  backToTopLabel: string;
  contactLabel: string;
}

export interface SectionDataMap {
  header: HeaderData;
  hero: HeroData;
  supplies: SuppliesData;
  how: HowData;
  who: WhoData;
  news: NewsSectionData;
  faq: FaqData;
  contact: ContactData;
  visit: VisitData;
  footer: FooterData;
}

export type SectionKey = keyof SectionDataMap;

export interface SectionPayload<K extends SectionKey = SectionKey> {
  key: K;
  anchor: string | null;
  data: SectionDataMap[K];
}

export interface SitePayload {
  version: number;
  preview: boolean;
  lang: Lang;
  dir: 'rtl' | 'ltr';
  defaultLanguage: Lang;
  languages: Language[];
  settings: Settings;
  sections: SectionPayload[];
  seo: Seo;
}

export interface NewsCard {
  slug: string;
  title: string;
  summary: string;
  /** The story's words for its card: the summary, then the article's text (blank lines between paragraphs). */
  text?: string;
  publishedAt: string | null;
  featured: boolean;
  image: Picture | null;
  category: { slug: string; name: string } | null;
}

export type BodyBlock =
  | { type: 'paragraph'; text: string }
  | { type: 'heading'; text: string; level: 2 | 3 }
  | { type: 'list'; style: 'bullet' | 'number'; items: string[] }
  | { type: 'quote'; text: string; cite: string }
  | { type: 'image'; image: Picture; caption: string };

export interface Article extends NewsCard {
  contentLang: Lang;
  body: BodyBlock[];
  updatedAt: string;
  metaTitle: string;
  metaDescription: string;
  related?: NewsCard[];
  seo?: Seo;
  preview?: boolean;
  status?: string;
}

export interface NewsPage {
  items: NewsCard[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  seo: Seo;
}
