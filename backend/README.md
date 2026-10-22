# Qoot API (ASP.NET Core 10)

The Web API behind the Qoot website and dashboard. It stores the content in SQL Server, signs
dashboard users in with ASP.NET Core Identity, serves only published content to the public, and (in
the recommended setup) also serves the built website and dashboard from the same origin.

## Project structure

```
backend/
├─ Qoot.slnx                      solution
├─ Directory.Build.props          shared build settings (nullable, analyzers, warnings)
├─ Directory.Packages.props       central package versions
├─ global.json                    SDK + Microsoft.Testing.Platform runner
├─ src/Qoot.Api/
│  ├─ Program.cs                  pipeline: security headers, compression, static files, auth, rate limits, API, page hosting
│  ├─ Controllers/                PublicControllers (site, news, contact, sitemap), AuthController, AdminContentControllers
│  ├─ Services/                   one service per area: sections, settings, news, media, contact, users, dashboard, SEO
│  ├─ Content/                    section schemas (the editable fields), validation engine, localiser, article body blocks
│  ├─ Media/                      storage, file-type sniffing, URLs
│  ├─ Domain/Entities.cs          EF entities and roles
│  ├─ Data/                       DbContext, migrations, seeder, seed content (Data/Seed/*.json from the original page)
│  ├─ Infrastructure/             errors (ProblemDetails), messages (ar/en), antiforgery, frontend hosting, CLI, DI setup
│  └─ appsettings*.json           defaults (no secrets)
├─ tests/Qoot.Api.Tests/          xUnit v3 tests against the real pipeline
└─ tools/Qoot.MigrationsTool/     add / script / check migrations without a database
```

## Configuration

Every setting can come from `appsettings.json`, an environment variable (`Section__Key`, e.g.
`ConnectionStrings__Qoot`), user secrets in development, or any ASP.NET Core configuration source.
**Secrets never go into `appsettings.json` or the frontend**: use environment variables, user
secrets, IIS application settings or a vault. See [`.env.example`](.env.example).

| Key | Default | Meaning |
| --- | --- | --- |
| `ConnectionStrings:Qoot` | — (required) | SQL Server connection string |
| `Database:Provider` | `SqlServer` | `InMemory` only for tests and demos (refused in Production) |
| `Database:ApplyMigrationsOnStartup` | `false` (`true` in Development) | otherwise the app logs pending migrations and you run `migrate` |
| `Database:SeedOnStartup` | `true` | adds the original Qoot content where it is missing; never overwrites edits |
| `Security:AllowedOrigins` | `[]` | CORS origins, only when the website runs on another origin |
| `Security:CookieSameSite` | `Strict` (`Lax` in Development) | `None` only for a cross-site frontend (requires HTTPS) |
| `Security:IdleTimeoutMinutes` | `60` | sign-out after this much inactivity (sliding) |
| `Security:AbsoluteSessionHours` | `12` | longest session without "keep me signed in" |
| `Security:RememberMeDays` | `7` | longest session with it |
| `Security:EnforceHttps` | `true` | HTTPS redirect + HSTS (off in Development) |
| `Security:UseForwardedHeaders` / `Security:KnownProxies` | `false` / `[]` | behind nginx etc.: `true` and the proxy IPs (e.g. `127.0.0.1`) |
| `Security:LoginAttemptsPerMinute` | `5` | per IP; accounts also lock for 15 min after 5 wrong passwords |
| `Security:ContactRequestsPer10Minutes` | `5` | Contact us form submissions per IP |
| `Security:PublicRequestsPerMinute` | `300` | public API per IP |
| `Media:RootPath` | `App_Data/media` | where uploads are stored (persistent volume) |
| `Media:PublicBaseUrl` | empty | set if `/media` is served from a CDN |
| `Media:MaxUploadBytes` | `8388608` (8 MB) | per file |
| `Media:MaxDimension` / `Media:MaxFilesPerUpload` | `12000` / `10` | |
| `DataProtection:KeysPath` | `App_Data/keys` | keys for the sign-in and antiforgery cookies (persistent, private) |
| `Frontend:DistPath` | `wwwroot` | the built frontend; empty turns page hosting off |
| `Frontend:ConnectSources` | `[]` | extra `connect-src` origins for the CSP (separate API host) |
| `Smtp:Host`, `Port`, `User`, `Password`, `From`, `Security` | empty, `587`, …, `StartTls` | e-mail for new Contact us messages (optional) |
| `Smtp:DashboardUrl` | empty | e.g. `https://www.qoot.com/admin`, for the link in that e-mail |
| `Bootstrap:AdminEmail` / `AdminPassword` / `AdminName` | empty | one-time creation of the first admin at start-up (prefer `create-admin`) |
| `OpenApi:Enabled` | `false` (`true` in Development) | `/openapi/v1.json` and the Scalar UI at `/scalar` |
| `Serilog:*` | JSON to the console | add a file sink or a log server here |

## Database and migrations

SQL Server 2019+ or Azure SQL. The schema is created by EF Core migrations in `Data/Migrations`
(`InitialCreate`); [`docs/database.md`](../docs/database.md) describes the tables and
[`docs/database/qoot-schema.sql`](../docs/database/qoot-schema.sql) is the same schema as an
idempotent SQL script for DBAs.

```bash
# apply migrations and seed the content (safe to run on every deployment)
dotnet run --project src/Qoot.Api -- migrate             # from source
dotnet Qoot.Api.dll migrate                               # from a published folder

# re-run only the seed (adds missing content, never overwrites)
dotnet Qoot.Api.dll seed

# after changing the entities: add / script / check migrations (no database needed)
dotnet run --project tools/Qoot.MigrationsTool -- add AddSomething
dotnet run --project tools/Qoot.MigrationsTool -- script ../docs/database/qoot-schema.sql
dotnet run --project tools/Qoot.MigrationsTool -- check   # fails if the model has unmigrated changes
```

`dotnet ef` works as well (`Microsoft.EntityFrameworkCore.Design` is referenced).

## The first administrator

There is no public sign-up. Create the first account on the server, with the same configuration as
the app (so it reaches the same database):

```bash
dotnet Qoot.Api.dll create-admin --email owner@qoot.com --name "Owner"
```

The password is typed twice without echo, so it never lands in shell history. Without a terminal
(CI, containers) the command reads `QOOT_ADMIN_PASSWORD` instead. Passwords need 12+ characters with
upper and lower case letters and a digit. After that, administrators add people in the dashboard
(*People and access*) as **Administrator** (everything) or **Editor** (sections, news, media, Contact us
requests; no settings, users or log). Roles are enforced by the API on every request.

Forgotten password, or a locked account, with no other administrator to reset it:

```bash
dotnet Qoot.Api.dll reset-password --email owner@qoot.com      # from source: dotnet run -- reset-password --email …
```

It asks for the new password twice, lifts any lockout, signs the account out everywhere and writes
the reset to the audit log. Both commands refuse a password typed with a non-English keyboard layout
(it is typed blind and would not match what you type later in the browser). Stop a running
`dotnet run` first on Windows, or the rebuild fails because the running app locks its files.

Alternatively `Bootstrap__AdminEmail` / `Bootstrap__AdminPassword` create the account at start-up
when no administrator exists; remove them afterwards (the app logs a warning while they are set).

**Local development sign-in.** `appsettings.Development.json` sets `Bootstrap` with
`SyncInDevelopment: true`, so every `dotnet run` in Development makes sure you can sign in at
`/admin/` as **admin@qoot.com** / **QootAdmin2026**. It creates the account, or puts that password
back and unlocks it if it was changed or locked. Change the values there if you like. The setting is
ignored outside Development, and `appsettings.json` (production) leaves `Bootstrap` empty.

## Running

```bash
cd src/Qoot.Api
dotnet run                     # Development, http://localhost:5080, API docs at /scalar
```

With the website built into `wwwroot` (`npm run build:api` in `frontend/`), the same process serves
`/`, `/en/`, `/news/…` and `/admin/…`. During frontend work use `npm run dev` instead (Vite on
:5173 proxies `/api` and `/media` here).

## Security summary

* Identity password hashing (PBKDF2), lockout after 5 failures, per-IP login rate limit.
* Cookie session: HttpOnly, Secure (outside Development), SameSite Strict, sliding idle timeout,
  absolute lifetime, security-stamp revalidation (password changes and deactivation end sessions).
* Antiforgery token required on every POST/PUT/PATCH/DELETE from the dashboard (`X-XSRF-TOKEN`).
* Authorization policies on every admin endpoint; Administrator-only endpoints for settings, users and the audit log.
* All input validated on the server against the section schemas; text cleaned of control
  characters; links restricted to `#anchor`, `/path`, `https://`, `mailto:` and `tel:`; no HTML.
* Uploads: type decided from the file's bytes (JPEG, PNG, WebP, AVIF, GIF; SVG refused), size and
  dimension limits, generated file names (no path traversal), served with `nosniff` and a sandbox CSP.
* EF Core parameterised queries only; no raw SQL.
* Audit log of sign-ins and every change, with user and IP.
* Security headers (CSP `script-src 'self'`, HSTS, frame, referrer and permissions policies).
* Errors are RFC 7807 problem details with a stable `code` and messages in Arabic or English; no
  stack traces leave the server.

## Tests

```bash
dotnet test --project tests/Qoot.Api.Tests
# against a real SQL Server (each test run creates and drops its own databases):
QOOT_TEST_SQLSERVER="Server=localhost;Database={db};User Id=sa;Password=...;TrustServerCertificate=True" dotnet test --project tests/Qoot.Api.Tests
```

See [`docs/testing.md`](../docs/testing.md) for what the suites cover and the latest results.
