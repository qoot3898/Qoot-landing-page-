# Testing

## Update of 22 October 2026, later (simple news cards, faster loading)

The website news became simple cards (picture, headline, category and date, the story's words); a
long story folds to a few lines with "Read more" and opens in place. The public news list now sends
each card's `text` (summary, then the article's words). The page asks for the news at the same time
as the page content (boot.js), and the scroll entrances start sooner and run shorter.

| Check | Result |
| --- | --- |
| Frontend typecheck, lint | passed (0 problems) |
| Frontend unit + component tests (Vitest) | **78 / 78 passed** (new: cards in page order with the featured story first, the whole story on the card with its marks, "Read more" only on long text, open and fold) |
| Frontend production build | passed |
| Screens by eye (1440 / 768 / 390 px, both languages) | home news with 1 and 3 stories, /news/ with 3 and 6, a story opened and folded, loading state, article "More news" |
| Requests | home, /news/ (with and without a category) and an article: every API request starts with the page content, once each |
| Backend C# | **not compiled here** (nuget.org unreachable); one new test `A_card_carries_the_whole_story_so_the_website_can_open_it_in_place` |

Run on your machine: `dotnet build` and `dotnet test --project tests/Qoot.Api.Tests` in `backend/`,
then `npm run check` in `frontend/`. The website also works with the older API (cards then show the
summary).

## Update of 22 October 2026 (Contact us, dashboard identity, news redesign)

This round replaced the quote-request form with **Contact us** (website, dashboard, API, database,
e-mails), restyled the dashboard and sign-in page in Qoot's identity, simplified the news editor and
redesigned the news on the website. What was run for it:

| Check | Result |
| --- | --- |
| Frontend typecheck, lint (`npm run check`) | passed (0 problems) |
| Frontend unit + component tests (Vitest) | **76 / 76 passed** (new: article text ⇄ blocks, Contact us form helpers, the news panel and rows, category colours, "Yesterday / 3 days ago" dates) |
| Frontend production build | passed |
| Screens by eye (1440 / 768 / 390 px, both languages) | sign-in, overview, inbox, news list, news editor, website news with 1, 2, 3 and 6 stories, news page, articles with and without a picture, loading and error states, Contact us |
| Backend C# | syntax-checked only; **not compiled** (the build environment could not reach nuget.org) |

**Run these on your machine before deploying this round** (they were not run here):

```powershell
cd backend
dotnet build
dotnet test --project tests/Qoot.Api.Tests      # includes the new Contact us, upgrade and news-summary tests
dotnet run --project tools/Qoot.MigrationsTool -- check
cd src/Qoot.Api; dotnet run -- migrate         # applies 20261022080000_ContactUs and turns the quote section into Contact us
cd ../../../frontend; npm run e2e
```

The migration keeps every existing message: the old contact field becomes the phone (or the e-mail,
when it was an address), and the old "people / frequency / location / organisation" answers are
appended to the message text.

The tables below are from the previous delivery (21 October 2026).

## Previous delivery (21 October 2026)

Every number below comes from a run made for that delivery. Nothing is listed as passing that was
not executed.

### Results

| Check | Command | Result |
| --- | --- | --- |
| Backend build | `dotnet build` (backend) | succeeded (analyzer warnings only: generated migration code, naming) |
| Backend tests | `dotnet test --project tests/Qoot.Api.Tests` | **102 / 102 passed** (in-memory database) |
| Migrations match the model | `dotnet run --project tools/Qoot.MigrationsTool -- check` | passed |
| SQL generation for SQL Server | `SqlServerTranslationTests` (part of the 102) | passed: the main queries translate to SQL Server SQL |
| Frontend typecheck | `npm run typecheck` | passed |
| Frontend lint | `npm run lint` | passed (0 problems) |
| Frontend unit + component tests | `npm run test` (Vitest) | **67 / 67 passed** |
| Frontend production build | `npm run build` | passed |
| End-to-end in Chromium | `npm run e2e` (Playwright, API serving the built site) | **8 / 8 passed** |
| Release build | `dotnet publish -c Release`, then run the published folder | serves `/`, `/admin/`, compressed immutable assets, persists cookie keys, OpenAPI off outside Development |
| Production dependencies | `npm audit --omit=dev` / `npm audit` | 0 vulnerabilities (after moving Vite to 8.3.4) |
| `create-admin` / `reset-password` commands | run against the in-memory database | creates the account; refuses weak passwords and passwords typed with a non-English keyboard layout; reset reports unknown accounts and lists the existing ones |

### What the suites cover

**Backend (102 tests, the real ASP.NET Core pipeline via `WebApplicationFactory`)**

* Public site: every seeded section is returned in both languages; hidden sections and unpublished
  drafts never appear; ETag/304 caching; sitemap and robots; problem-details errors; security
  headers; unknown routes 404.
* Section workflow: draft → publish → discard; draft preview for signed-in users only; validation
  errors keyed by field path (required, lengths, link allow-list, media references); HTML typed into
  content stored as plain text; list items added, hidden and removed; missing translations reported
  and falling back; concurrency conflict (409) on stale saves; hiding (header and hero refused) and
  reordering.
* Authentication and access: no session → 401 on every admin endpoint; editors get 403 on settings,
  users and the audit log; antiforgery required on writes; lockout after repeated failures and admin
  unlock; HttpOnly SameSite session cookie and logout; the absolute session limit (with a fake
  clock); changing the password keeps the current session; deactivation ends the session and blocks
  sign-in; the last administrator cannot be removed or demoted; the `reset-password` console command
  sets a new password, lifts a lockout and is audited; weak passwords refused with
  localised messages; login rate limiting; sensitive actions written to the audit log.
* News: drafts invisible until published; unpublishing hides again; scheduled articles appear only at
  their time; featured and priority ordering; categories; slugs (generated, unique, validated);
  body blocks with inline marks and link checks; language fallback with `contentLang`; article SEO.
* Media, contact and settings: uploads identified from their bytes (a file named .png that is not an
  image, or an SVG, is refused), size limits, replace keeps every reference working, usage blocks
  deletion, brand files protected, drawings refused where a photograph is expected; the Contact us
  form validates (name, phone and message required, email optional; messages in the visitor's
  language), stores, ignores the honeypot, rate-limits, and is managed in the dashboard; a database
  made before Contact us gets the new section in place of the quote section; contact details changed in settings reach the site; Arabic cannot be
  turned off; turning English off removes it.
* Page hosting: metadata written into each page, `lang`/`dir`, canonical redirects, 404 + `noindex`
  for unknown or unpublished articles, dashboard routes, cache headers, framing policy.
* Unit: text cleaning, link, slug and phone rules, file-type sniffing, schema validation (unknown keys
  dropped, stable list ids, every problem with its path), the seed content against every schema,
  article bodies.

**Frontend (67 tests)**

* Rendering the home page from real API payloads (captured from the running API): all sections in
  order with the original classes, the language switch, content never turned into markup (XSS),
  navigation links dropped for hidden sections, the news section's loading state.
* The escaping template, link allow-list, inline marks, responsive pictures.
* Dates in Benghazi time, path localisation, open-now logic across days, Contact us form helpers (phone, email, WhatsApp link).
* The API client: antiforgery header and renewal, problem-details mapping, network errors, 304.
* Latest news: featured first, empty state, hide-when-empty (section and its menu link), error and retry.
* Dashboard: both languages have every string, Benghazi date conversion, list item defaults, the
  schema-driven form (separate Arabic/English values, API errors on the right field, add/hide/reorder).

**End-to-end (8 scenarios, Chromium, against the running API and the built site)**

1. Arabic home page right-to-left with every section; switch to English left-to-right.
2. A Contact us message sent from the website arrives in the dashboard; status and notes saved; deleted.
3. The dashboard redirects to sign-in without a session; a wrong password is refused; the API answers 401.
4. A section edit stays a draft (visitors see the old text), appears after publishing, and is reverted.
5. Hiding a section removes it and its menu link from the website; showing it brings both back.
6. An article: draft (404 for visitors) → published (visible in Arabic and in the English list) →
   unpublished (404 again) → deleted.
7. A picture is uploaded, described in both languages, deleted; a disguised file is refused.
8. An Editor can work on content but gets "no permission" on settings and people, and 403 from the API.

Screens were also checked by eye at 1440, 768 and 390 px in both languages (website, news, article,
dashboard overview, section editor with live draft preview, news editor, media, inbox, settings).

## Not executed here, and how to run it

* **SQL Server itself.** The build environment had no SQL Server. The migration was generated and
  checked against the model, and the SQL the app sends was verified offline, but neither was run on
  a live server. On a machine with SQL Server:

  ```powershell
  $env:QOOT_TEST_SQLSERVER = "Server=(localdb)\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=True"
  dotnet test --project backend/tests/Qoot.Api.Tests      # the same 102 tests against SQL Server
  ```

  and `dotnet run -- migrate` in `backend/src/Qoot.Api` for the real database.
* **SMTP delivery** of the notification e-mail (needs your mail server; the sending code runs in the
  background and failures are logged, never shown to visitors).
* **NuGet vulnerability scan** (`dotnet list package --vulnerable --include-transitive`) needs
  nuget.org, which the build environment could not reach. The first restore on a machine with
  nuget.org reported `System.Security.Cryptography.Pkcs` 10.0.0 (pulled in by
  `Microsoft.Data.SqlClient`; GHSA-846f-f764-76mx high, GHSA-2fmv-rv2c-59x7 moderate, fixed in
  10.0.13). It is now pinned to 10.0.13 in `Directory.Packages.props`; build and the tests were
  re-run with the pin. Run the scan again after each package update.
* **Browsers other than Chromium** (Safari, Firefox) were not run; the code uses standard APIs only.
