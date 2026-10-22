# Deploying Qoot

Recommended: one ASP.NET Core process serves the API, the website and the dashboard from one origin
(no CORS, `SameSite=Strict` cookies, one TLS certificate), behind IIS or a reverse proxy.

## 1. Build

```bash
cd frontend
npm ci
npm run check              # typecheck, lint, unit tests, build
npm run build:api          # writes the site into backend/src/Qoot.Api/wwwroot

cd ../backend
dotnet test --project tests/Qoot.Api.Tests
dotnet publish src/Qoot.Api -c Release -o ../publish
```

`publish/` now holds `Qoot.Api.dll`, `web.config` (for IIS), `appsettings.json` and `wwwroot/`.
It contains no secrets.

## 2. Configure

Set these as environment variables on the server (see [`backend/.env.example`](../backend/.env.example)
for the full list). Required:

| Variable | Example |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Qoot` | `Server=sql01;Database=Qoot;User Id=qoot_app;Password=…;Encrypt=True` |

Usually also: `Smtp__*` (notification e-mail), `Media__RootPath` and `DataProtection__KeysPath`
(persistent folders outside the deployment folder, so a redeploy never deletes them), and
`Security__UseForwardedHeaders` + `Security__KnownProxies__0` when a reverse proxy terminates TLS.

## 3. Database and first administrator

From the publish folder, with the same environment variables:

```powershell
dotnet Qoot.Api.dll migrate                                   # schema + seed content (repeat on every release)
dotnet Qoot.Api.dll create-admin --email owner@qoot.com --name "Owner"   # asks for the password
dotnet Qoot.Api.dll reset-password --email owner@qoot.com                 # later, if it is forgotten
```

Then sign in at `https://your-domain/admin/` and, in *Site settings*, check the website address,
the phone number, e-mail, map link and social profiles, and the notification address.

## 4a. Windows Server + IIS

1. Install the **ASP.NET Core 10 Hosting Bundle** and restart IIS.
2. Create a site pointing at the publish folder, with an application pool set to **No Managed Code**.
3. Give the pool identity (`IIS AppPool\<pool>`) modify rights on the `Media__RootPath` and
   `DataProtection__KeysPath` folders (default `App_Data\media` and `App_Data\keys` inside the site;
   on Windows the keys are encrypted with DPAPI).
4. Put the environment variables in IIS: *Configuration Editor → system.webServer/aspNetCore →
   environmentVariables* (stored in `applicationHost.config`, not in the site folder), or in
   `web.config` if your process keeps that file out of source control.
5. Add an HTTPS binding with your certificate. `Security__EnforceHttps=true` (default) redirects HTTP
   and sends HSTS.
6. Large uploads: IIS accepts 30 MB requests by default; the API limits each picture to
   `Media__MaxUploadBytes` (8 MB) and 10 per request.

## 4b. Linux + systemd + nginx

```ini
# /etc/systemd/system/qoot.service
[Unit]
Description=Qoot website and API
After=network.target

[Service]
WorkingDirectory=/srv/qoot/app
ExecStart=/usr/bin/dotnet /srv/qoot/app/Qoot.Api.dll
User=qoot
Restart=always
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5080
Environment=Security__UseForwardedHeaders=true
Environment=Security__KnownProxies__0=127.0.0.1
Environment=Media__RootPath=/srv/qoot/data/media
Environment=DataProtection__KeysPath=/srv/qoot/data/keys
EnvironmentFile=/etc/qoot/secrets.env      # ConnectionStrings__Qoot=…, Smtp__Password=… (chmod 600)

[Install]
WantedBy=multi-user.target
```

```nginx
server {
    listen 443 ssl http2;
    server_name www.qoot.com;
    # ssl_certificate …; ssl_certificate_key …;
    client_max_body_size 100m;
    location / {
        proxy_pass         http://127.0.0.1:5080;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host $host;
    }
}
server { listen 80; server_name www.qoot.com qoot.com; return 301 https://www.qoot.com$request_uri; }
```

Keys on Linux are stored unencrypted (the app logs a warning saying so): keep the keys folder
readable by the service account only (`chmod 700`).

## 4c. Website on a separate host (optional)

Build with `VITE_API_BASE_URL=https://api.qoot.com npm run build`, upload `frontend/dist/` to the
static host (its `_redirects` file maps the page routes on Netlify / Cloudflare Pages), and on the
API set `Security__AllowedOrigins__0=https://www.qoot.com`, `Security__CookieSameSite=None` and
`Frontend__ConnectSources__0` if needed. In this mode crawlers get the page metadata only after
JavaScript runs, so the single-origin setup above is better for search.

## 5. After each release

* Run `dotnet Qoot.Api.dll migrate` (no-op when nothing changed).
* `GET /health` should answer `Healthy` (it checks the database).
* Logs go to standard output as JSON (IIS: enable `stdoutLogEnabled` in `web.config` or add a
  Serilog file sink in `appsettings.json`).

## 6. Backups

Back up the SQL database and the media folder together (daily), and the keys folder once.
Restoring both brings the site and the dashboard back exactly as they were.
