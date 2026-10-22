# Qoot website and dashboard (frontend)

One Vite project with three pages:

| Page | Entry | Built from |
| --- | --- | --- |
| Home, `/` (Arabic) and `/en/` (English) | `index.html` → `src/public/main.ts` | `/api/public/site` |
| News list and articles, `/news/…`, `/en/news/…` | `news.html` → `src/public/news-page.ts` | `/api/public/news…` |
| Dashboard, `/admin/…` | `admin/index.html` → `src/admin/main.tsx` (React) | `/api/auth/…`, `/api/admin/…` |

The public website keeps the original design exactly: `src/public/styles/site.css` is the original
`styles.css` (only asset paths made absolute), `public/assets/` holds the original fonts, logos,
drawings and photographs, and `src/public/interactions.ts` is the original `script.js` behaviour
(header, Q-window reel, reveals, stacking cards, map, FAQ, Contact us form, open-now line), started once
the page is built from the API. `src/public/styles/additions.css` only adds what the dynamic site
needs (Latest news, news pages, loading / error / preview states).

## Structure

```
frontend/
├─ index.html, news.html, admin/index.html   page shells (the API writes each page's metadata into them)
├─ public/                                   original assets, favicons, boot.js (language + early fetch), _redirects
├─ src/shared/                               api.ts (the one HTTP client), types.ts, routes.ts
├─ src/public/                               the website: render.ts (sections → markup, escaped), shell.ts,
│                                            news-ui.ts, news-page.ts, seo.ts, i18n.ts, interactions.ts, drawings.ts
├─ src/admin/                                the dashboard: Shell, auth, data (TanStack Query), i18n (ar/en),
│  ├─ form/                                  SchemaForm (fields from the API's section schemas), MediaPicker, unsaved-changes guard
│  ├─ pages/                                 Overview, Sections, SectionEditor, News, NewsEditor, Categories,
│  │                                         Media, Messages, Settings, Users, Audit, Account
│  └─ ui/                                    controls, dialogs, toasts, status pills, chart, icons
└─ e2e/                                      Playwright end-to-end tests
```

## Commands

```bash
npm ci                 # install (exact versions from package-lock.json)
npm run dev            # http://localhost:5173, proxies /api and /media to QOOT_DEV_API (default http://localhost:5080)
npm run check          # typecheck + lint + unit tests + build
npm run build          # → dist/ (deploy anywhere, or let the API serve it)
npm run build:api      # → ../backend/src/Qoot.Api/wwwroot (the API serves it: recommended)
npm run test           # Vitest unit and component tests
npm run e2e            # Playwright, against a running API (see below)
```

## Configuration

Only one build-time setting, and it is public by nature (it ends up in the browser):

| Variable | Default | Meaning |
| --- | --- | --- |
| `VITE_API_BASE_URL` | empty | Where the browser finds the API. Leave empty when the API serves the site (same origin). Set to e.g. `https://api.qoot.com` only for split hosting, and then add the site's origin to the API's `Security:AllowedOrigins` and set `Security:CookieSameSite=None`. |
| `QOOT_DEV_API` | `http://localhost:5080` | Development only: where `npm run dev` proxies `/api` and `/media`. |

No secret is, or may be, configured here. Sign-in uses an HttpOnly cookie set by the API; the
browser code never sees a token or a password beyond the sign-in form.

## Language and direction

* The address decides the language: `/…` Arabic (RTL), `/en/…` English (LTR). `public/boot.js` sets
  `lang`/`dir` before anything renders and remembers the visitor's choice (`localStorage`), so a
  visitor who chose English comes back to `/en/`.
* All content comes from the API in the page's language; a text missing in one language falls back
  to the other (the dashboard flags missing translations).
* Interface strings that are not editable content (loading, errors, dates) are in `src/public/i18n.ts`
  and `src/admin/i18n.tsx`. Dates use the Benghazi time zone and Latin digits.

## End-to-end tests

They sign in to the dashboard and create, publish and delete real content, so run them only against
a development database:

```bash
# terminal 1: API serving the built site, with room for the tests' sign-ins
npm run build:api
cd ../backend/src/Qoot.Api && Security__LoginAttemptsPerMinute=100 Security__ContactRequestsPer10Minutes=100 dotnet run

# terminal 2
npx playwright install chromium        # once
QOOT_E2E_URL=http://localhost:5080 QOOT_E2E_EMAIL=you@qoot.com QOOT_E2E_PASSWORD='…' npm run e2e
```

On Windows PowerShell set the variables first:

```powershell
$env:Security__LoginAttemptsPerMinute = '100'; $env:Security__ContactRequestsPer10Minutes = '100'   # before dotnet run
$env:QOOT_E2E_URL = 'http://localhost:5080'; $env:QOOT_E2E_EMAIL = 'you@qoot.com'; $env:QOOT_E2E_PASSWORD = '…'
npm run e2e
```
