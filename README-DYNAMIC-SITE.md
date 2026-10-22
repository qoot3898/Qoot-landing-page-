# Qoot website and dashboard

The Qoot Catering & Supplies website (Arabic at `/`, English at `/en/`), now fully dynamic: every
section, the **Latest news** and the site settings are edited in a bilingual dashboard at `/admin/`
(in Qoot's own colours and type), stored in SQL Server, and published without touching code.
Visitors reach Qoot through the **Contact us** form (name, phone, optional e-mail, message), whose
messages arrive in the dashboard inbox and, optionally, by e-mail.

| Part | Stack | Folder |
| --- | --- | --- |
| API, database, authentication, page hosting | ASP.NET Core 10 Web API (C#), EF Core 10, SQL Server, ASP.NET Core Identity | [`backend/`](backend/README.md) |
| Public website | TypeScript (no framework), the original `styles.css` and behaviours, Vite | [`frontend/src/public`](frontend/README.md) |
| Dashboard | React 19 + TypeScript, TanStack Query, React Router | [`frontend/src/admin`](frontend/README.md) |
| Documentation | API reference, database, deployment, test results | [`docs/`](docs/) |

The original static files (`index.html`, `en/index.html`, `styles.css`, `script.js`, `assets/`) are
superseded by this project: their design, copy, pictures and behaviour live on in `frontend/`
(styles and assets unchanged) and in the database seed (`backend/src/Qoot.Api/Data/Seed`).

## How it fits together

```
browser ──► Qoot.Api (one origin, recommended)
              ├─ /, /en/, /news/…, /en/news/…  built pages + search metadata written into the HTML
              ├─ /admin/…                      the dashboard (React)
              ├─ /api/public/…                 published content only (no session)
              ├─ /api/auth/…, /api/admin/…     signed-in users only, role-checked on the server
              ├─ /media/…                      uploaded pictures (App_Data/media)
              └─ /sitemap.xml, /robots.txt, /health
            SQL Server ◄── EF Core migrations
```

* Content is stored per section as validated JSON (draft and published copies), every text in both
  languages. Visitors only ever receive published content; drafts are visible to dashboard users in
  the preview.
* No HTML is accepted anywhere in content: texts are plain, article bodies are structured blocks
  (paragraph, heading, list, quote, picture) with two inline marks (`**bold**`, `[link](url)`),
  links go through an allow-list, and the website builds its markup with escaping.
* The API sets a strict Content-Security-Policy (`script-src 'self'`), HSTS, nosniff, frame and
  referrer policies, and checks antiforgery tokens on every state-changing dashboard request.

## Quick start (development)

Requirements: .NET SDK 10, Node.js 22+, SQL Server (LocalDB, Express, Developer or a container).

```powershell
# 1. API. Development uses SQL Server LocalDB (appsettings.Development.json); for another server:
#    dotnet user-secrets set "ConnectionStrings:Qoot" "Server=...;Database=Qoot;..."
cd backend/src/Qoot.Api
dotnet run -- migrate                                          # creates the database and the seed content
dotnet run -- create-admin --email you@qoot.com --name "Your Name"   # asks for a password (twice)
dotnet run -- reset-password --email you@qoot.com                     # forgot it / locked out (stop `dotnet run` first)
dotnet run                                                     # http://localhost:5080

# 2. Website + dashboard with hot reload (proxies /api and /media to the API)
cd ../../../frontend
npm ci
npm run dev                                                                         # http://localhost:5173
```

Open `http://localhost:5173/` (Arabic), `/en/` (English) and `/admin/` (dashboard).
Details: [backend/README.md](backend/README.md) · [frontend/README.md](frontend/README.md).

## Production in one paragraph

Build the frontend into the API (`npm run build:api`), publish the API
(`dotnet publish backend/src/Qoot.Api -c Release -o out`), set the configuration through environment
variables (connection string, site URL, SMTP), run `Qoot.Api migrate` once, create the first
administrator with `Qoot.Api create-admin`, and run it behind IIS or a reverse proxy with HTTPS.
Step by step: [docs/deployment.md](docs/deployment.md).

## Tests

| Suite | Command | Latest result |
| --- | --- | --- |
| Backend (xUnit, real middleware, in-memory DB) | `dotnet test --project backend/tests/Qoot.Api.Tests` | 102 passed on 21 Oct; **re-run after the 22 Oct Contact us update** (not compiled in the build environment) |
| Frontend unit + component (Vitest) | `npm run test` in `frontend/` | 76 passed (22 Oct) |
| Typecheck / lint / build | `npm run check` in `frontend/` | passed (22 Oct) |
| End-to-end (Playwright, real browser + running API) | `npm run e2e` in `frontend/` | 8 passed on 21 Oct; re-run after the update |

What was and was not run, and how: [docs/testing.md](docs/testing.md).

## Still to configure (outside this code)

These need values only the owner has; nothing here simulates them.

1. **SQL Server**: the production connection string (`ConnectionStrings__Qoot`). The migrations were
   generated and checked against the model, but not executed against a live SQL Server in the build
   environment; run `Qoot.Api migrate` (or `docs/database/qoot-schema.sql`) on yours.
2. **First administrator**: `Qoot.Api create-admin --email … ` on the server (there is no public sign-up).
3. **Real contact details**: the phone number (`+218 123 456 789`) and e-mail (`info@qoot.com`) come
   from the original page and look like placeholders; correct them in *Site settings → Contact*.
   Check the Google Maps link and social profiles there too.
4. **Site address**: *Site settings → Search and sharing → Website address* (used for canonical links,
   the sitemap and sharing) is `https://www.qoot.com`; change it if the domain differs.
5. **E-mail notifications** (optional): SMTP host, port, user, password and sender (`Smtp__*`), and the
   recipient in *Site settings → Notifications*. Without them Contact us messages are only kept in the dashboard.
6. **HTTPS, domain and reverse proxy**: certificate and host binding (IIS, nginx, a load balancer).
7. **Persistent storage**: `App_Data/media` (uploads) and `App_Data/keys` (sign-in cookie keys) must
   survive deployments and be backed up with the database.
