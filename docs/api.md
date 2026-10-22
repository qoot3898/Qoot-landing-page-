# Qoot API reference

Base path: the site's own origin (e.g. `https://www.qoot.com`). The machine-readable description is
[`openapi.json`](openapi.json) (OpenAPI 3); in Development the API also serves it at
`/openapi/v1.json` with an interactive reference at `/scalar` (`OpenApi__Enabled=true` turns them on
elsewhere).

## Conventions

* **JSON** in camelCase. Dates are ISO 8601 in UTC (`2026-10-18T09:00:00Z`).
* **Language**: `?lang=ar|en` on public endpoints; otherwise `Accept-Language`. Error messages come
  back in that language.
* **Errors**: [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807) problem details with a stable `code`
  and, for validation, `errors` keyed by field path:

  ```json
  { "status": 400, "code": "validation_failed", "title": "Some fields need attention.",
    "errors": { "titleStart.ar": ["This field is required."], "slides[0].caption.ar": ["Keep it under 80 characters."] } }
  ```

  Common codes: `validation_failed`, `not_found`, `unauthorized` (401), `forbidden` (403),
  `antiforgery` (400, token missing or expired), `concurrency_conflict` (409, someone saved first:
  reload), `rate_limited` (429), `login_failed`, `locked_out`, `account_disabled`, `media_in_use`, `media_bundled`, `slug_taken`, `last_admin`, `cannot_change_self`.
* **Paging**: `page` (from 1) and `pageSize`; responses are `{ items, page, pageSize, total, totalPages }`.
* **Caching**: public JSON carries a content `ETag` and `Cache-Control: no-cache` (revalidated every
  time, `304` while unchanged), so published changes show immediately. Dashboard and auth responses
  are `no-store`.
* **Dashboard calls** need the session cookie and, on `POST/PUT/PATCH/DELETE`, the header
  `X-XSRF-TOKEN` with the token from `GET /api/auth/antiforgery` (fetch it again after signing in or out).

## Public (no session)

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/public/site?lang=` | Settings, the visible published sections in page order (texts resolved to the language, pictures as `{url, width, height, alt, sources}`), languages, and the page's search metadata (`seo`). |
| GET | `/api/public/news?lang=&page=&pageSize=&category=` | Published articles (not drafts, not scheduled ones before their time), newest first, pinned priority first. Each card has `summary` (one line) and `text`: the written summary, then the article's words (paragraphs separated by a blank line, list items one per line, `**bold**` and `[label](link)` kept, pictures left out) so the website can show the whole story on its card. Includes `seo` for the list page. |
| GET | `/api/public/news/categories?lang=` | Categories that have published articles. |
| GET | `/api/public/news/{slug}?lang=` | One published article with its body blocks, `contentLang` (the language actually served), related articles and `seo`. 404 for drafts, scheduled or unknown slugs. |
| POST | `/api/public/contact` | Contact us message: `{ name, phone, email, message, language, website }`. `name`, `phone` and `message` are required, `email` is optional (checked when given); `website` is a honeypot and must stay empty. 202. Rate limited per address. |
| GET | `/sitemap.xml`, `/robots.txt` | Generated from published content and the site address. |
| GET | `/health` | Liveness and database check. |

Pages (`/`, `/en/`, `/news/…`, `/en/news/…`, `/admin/…`) are served by the same process when the
built frontend is present, with each page's title, description, canonical, `hreflang`, Open Graph,
Twitter and JSON-LD written into the HTML.

## Authentication (`/api/auth`)

| Method | Path | Body / notes |
| --- | --- | --- |
| GET | `/api/auth/antiforgery` | → `{ token, headerName }` and an HttpOnly cookie. |
| POST | `/api/auth/login` | `{ email, password, rememberMe }` → the user. Rate limited; locks the account for 15 minutes after 5 failures. |
| POST | `/api/auth/logout` | Ends the session. |
| GET | `/api/auth/me` | → `{ id, email, displayName, role, preferredLanguage }` or 401. |
| POST | `/api/auth/change-password` | `{ currentPassword, newPassword }`. Ends other sessions. |
| PUT | `/api/auth/profile` | `{ displayName, preferredLanguage }`. |

There is no registration endpoint.

## Dashboard (`/api/admin`, signed in; role Editor or Administrator unless marked **Admin**)

**Overview and schemas**

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/admin/dashboard` | Counts (sections, news, requests, media), requests per day for 14 days, open requests, recent activity (Admin only). |
| GET | `/api/admin/schemas` | The editable fields of every section and of the settings (types, labels in both languages, limits, options). The dashboard builds its forms from this; the API validates against the same schemas. |

**Sections** (`{key}` = `header`, `hero`, `supplies`, `how`, `who`, `news`, `faq`, `contact`, `visit`, `footer`)

| Method | Path | Body / notes |
| --- | --- | --- |
| GET | `/api/admin/sections` | All sections in page order with state (visible, published, unpublished changes, missing translations). |
| GET | `/api/admin/sections/{key}` | `{ section, draft, published, concurrencyStamp, missingTranslations[] }`. |
| PUT | `/api/admin/sections/{key}/draft` | `{ data, concurrencyStamp }`. Validates and saves the draft; the website is unchanged. |
| POST | `/api/admin/sections/{key}/publish` | Publishes the draft. |
| POST | `/api/admin/sections/{key}/discard` | Throws the draft away (back to the published version). |
| PUT | `/api/admin/sections/{key}/visibility` | `{ visible }`. Immediate. Header, hero and footer cannot be hidden. |
| PUT | `/api/admin/sections/order` | `{ keys: [...] }` the movable sections in their new order. Immediate. |
| POST | `/api/admin/sections/publish-all` | Publishes every section with unpublished changes. |
| GET | `/api/admin/preview/site?lang=` | Like `/api/public/site` but with drafts (used by `?preview` pages). |
| GET | `/api/admin/preview/news/{id}?lang=` | An article in any state, for preview. |

**News**

| Method | Path | Body / notes |
| --- | --- | --- |
| GET | `/api/admin/news?search=&status=&categoryId=&sort=&page=&pageSize=` | `status`: `draft`, `published`, `scheduled`, `featured`; `sort`: (newest), `oldest`, `updated`, `title`, `priority`. |
| POST | `/api/admin/news` | `{ slug?, titleAr, titleEn, summaryAr, summaryEn, bodyAr: [blocks], bodyEn: [blocks], metaTitleAr, metaTitleEn, metaDescriptionAr, metaDescriptionEn, featuredImageId?, categoryId?, isFeatured, priority, publishedAt? }` → 201, a draft. |
| GET / PUT / DELETE | `/api/admin/news/{id}` | PUT takes the same body plus `concurrencyStamp`. |
| POST | `/api/admin/news/{id}/publish` | `{ publishAt? }`: now, or a future date (scheduled). |
| POST | `/api/admin/news/{id}/unpublish` | Back to draft. |
| GET / POST | `/api/admin/news-categories` | `{ slug?, nameAr, nameEn, sortOrder }`. |
| PUT / DELETE | `/api/admin/news-categories/{id}` | Deleting keeps the articles (without category). |

Body blocks: `{type:"paragraph",text}`, `{type:"heading",text,level:2|3}`,
`{type:"list",style:"bullet"|"number",items:[...]}`, `{type:"quote",text,cite}`,
`{type:"image",mediaId,caption}`. Texts accept `**bold**` and `[label](link)` only.

**Media**

| Method | Path | Body / notes |
| --- | --- | --- |
| GET | `/api/admin/media?search=&kind=upload|bundled&type=photo|drawing&page=&pageSize=` | |
| POST | `/api/admin/media` | multipart: `files` (1–10 images), `altAr`, `altEn`. JPEG, PNG, WebP, AVIF, GIF, checked from the bytes; size and dimension limits. |
| GET / PUT / DELETE | `/api/admin/media/{id}` | PUT `{ altAr, altEn, fileName }`. DELETE refuses brand files and pictures still in use (409). |
| POST | `/api/admin/media/{id}/replace` | multipart `file`: new file, same id (everything using it updates). |
| GET | `/api/admin/media/{id}/usage` | Where the picture is used. |

**Contact us messages**

| Method | Path | Body / notes |
| --- | --- | --- |
| GET | `/api/admin/messages?status=&search=&page=&pageSize=` | `status`: `open` (default in the dashboard), `new`, `in_progress`, `done`, `archived`, `unread`. |
| GET | `/api/admin/messages/{id}` | Marks it read. Shape: `{ id, name, phone, email, message, language, status, isRead, adminNotes, createdAt, updatedAt }`. |
| PATCH | `/api/admin/messages/{id}` | `{ status?, adminNotes?, isRead? }`. |
| DELETE | `/api/admin/messages/{id}` | |

**Administration (Admin)**

| Method | Path | Body / notes |
| --- | --- | --- |
| GET | `/api/admin/settings` | Editors may read. |
| PUT | `/api/admin/settings` | **Admin**. `{ data, concurrencyStamp }`. Applies immediately. |
| GET / POST | `/api/admin/users` | **Admin**. POST `{ email, displayName, role: "Admin"|"Editor", password }`. |
| PUT / DELETE | `/api/admin/users/{id}` | **Admin**. PUT `{ displayName, role, isActive }`. You cannot remove or demote yourself or the last administrator. |
| POST | `/api/admin/users/{id}/reset-password` | **Admin**. `{ newPassword }`. |
| POST | `/api/admin/users/{id}/unlock` | **Admin**. |
| GET | `/api/admin/audit?search=&action=&page=&pageSize=` | **Admin**. `action` filters by prefix (`auth`, `section`, `news`, `media`, `settings`, `user`, `message`). |
