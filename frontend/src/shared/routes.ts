/**
 * Page routes the static files don't cover: /en/ (English home), /news/... (news list and
 * articles in both languages) and /admin/... (the dashboard's client-side routes).
 * Used by the Vite dev/preview servers; the .NET API does the same rewriting in production
 * (FrontendHostMiddleware), and public/_redirects does it on static hosts.
 */
export function pageFor(path: string): string | null {
  const clean = path.split('?')[0] ?? '/';
  if (/\.[a-z0-9]+$/i.test(clean) && !clean.endsWith('.html')) return null;
  if (clean === '/admin' || clean.startsWith('/admin/')) return '/admin/index.html';
  if (/^\/(en\/)?news(\/|$)/.test(clean)) return '/news.html';
  if (clean === '/en' || clean === '/en/') return '/index.html';
  return null;
}
