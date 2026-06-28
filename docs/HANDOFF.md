# White Stitches — Handoff

_Last updated: 2026-06-14_

A working snapshot for whoever deploys or continues this project. It captures **what's
done, what's pushed-but-not-deployed, and the exact server-side steps still required to go
live.** For deeper design context see `PRD.md`, `ARCHITECTURE.md`, `DATABASE.md`, and
`BUILD-PLAN.md` in this folder.

---

## 1. TL;DR (read this first)

- The **entire post-launch backlog (9 items, 4 waves) is complete, committed, and pushed** to
  `origin/main`. Plus two production hotfixes from 2026-06-14 (see §4).
- **All database migrations are applied to production** (InitialCreate, AddProductMediaKind,
  AddBanners). Nothing pending DB-side.
- **Code is ahead of the running servers.** The latest commits are NOT deployed yet — both apps
  must be redeployed (§6). This is the safe order: schema is already ahead of code, so new
  columns/tables sit unused until the new build ships.
- **Two operational gaps will keep features broken until fixed on the server**, even after deploy:
  1. **`Storage:Root`** is still the dev default → image/video uploads fail. Must point at an
     absolute, writable, **shared** folder (§5.A).
  2. **Tap live key** — production should run `sk_live_…`, set out-of-band (§5.C).
- Everything in §5 is **your action on the server** — not code. Work through that checklist.

---

## 2. The system

| | |
|---|---|
| **What** | .NET 9 MVC e-commerce platform (women's fashion, Kuwait/GCC). No public APIs; server-rendered Razor. |
| **Repo** | `codexkw/White-Stiches` (**public**) |
| **Storefront** | `https://white-stiches-testing.codexkw.co` (`WhiteStiches.Web`) |
| **Back office** | `https://white-stiches-testing-admin.codexkw.co` (`WhiteStiches.Admin`) |
| **Database** | SQL Server at `83.229.86.221,1433`, DB `White-Stiches` (conn string in each app's `appsettings.json`) |
| **Hosting** | IIS + ASP.NET Core Module (in-process). Behind a TLS proxy / Cloudflare. |
| **Payments** | Tap Payments v2, hosted-redirect flow (`source.id=src_all`) |
| **Email** | Mailgun SMTP (`mg.codexkw.co`) |

**Two apps, one core.** `WhiteStiches.Web` (storefront) and `WhiteStiches.Admin` (back office)
are separate ASP.NET Core apps that share `WhiteStiches.Infrastructure` (services, EF
`DbContext`, payments, storage) and `WhiteStiches.Core` (entities, interfaces, enums). They talk
to the **same database**. There is no API tier — controllers call the service layer directly.

**Projects**
- `WhiteStiches.Core` — entities, interfaces, enums, admin DTOs/models. No infrastructure deps.
- `WhiteStiches.Infrastructure` — EF Core `WhiteStichesDbContext`, services, payments, file
  storage, localization, security headers, DI registration.
- `WhiteStiches.Web` — storefront MVC.
- `WhiteStiches.Admin` — back office MVC (auth-gated).

**Localization.** Bilingual EN/AR. UI strings via `IStringLocalizer<SharedResource>` (`.resx`;
a missing key falls back to the key text = English). Content columns are bilingual (`…En`/`…Ar`)
and read culture-aware through `LocalizedContent` extension methods
(`Infrastructure/Localization/LocalizedContent.cs`).

---

## 3. Build / migrate / run

**Build gate**
```powershell
dotnet build WhiteStiches.sln -v q -nologo
```
A clean build prints `0 Error(s)`. There are **40 pre-existing `MSB3568` "duplicate resource
name" warnings** (duplicate keys in the `.resx` files) — harmless and unrelated to any recent
work; don't be alarmed by them.

**Run locally** — start `WhiteStiches.Web` and/or `WhiteStiches.Admin`. In `Development` the
default `Storage:Root` (`../../storage`, solution-level) works and a dev-only checkout fallback
lets you complete an order without a real Tap charge.

**Migrations** — see §5.B. The apps do **not** auto-migrate.

---

## 4. What's done

### Launch backlog (4 waves — all pushed)

| Wave | Commit(s) | Summary |
|---|---|---|
| 1 | `9d9882d` | Dark-theme contrast fix; data-driven catalog filter (was hardcoded swatches); removed in-app payment-method picker (all flows → Tap). |
| 2 | `658e168` | Email for every order/return/refund lifecycle event; EN/AR culture fix; deliverability (Reply-To, List-Unsubscribe). |
| 3 | `efb4af1`, `a254c31` | Invoice PDF (QuestPDF); admin-managed header/hero **tickers**; localized search overlay + real recent-searches. |
| 4 · #9 | `765c158` | **Product video upload** — `MediaKind {Image,Video}` enum, `.mp4/.webm` allow-list, PDP gallery img/video branch, photo-only thumbnails everywhere else. Migration `AddProductMediaKind`. |
| 4 · #1 | `8e83cc9` | **Homepage hero banner CMS** — `Banner`/`BannerImage`/`BannerStat` entities, admin `/banners`, storefront renders active hero (falls back to hardcoded hero). Migration `AddBanners` (also seeds one default hero). |

### Production hotfixes — 2026-06-14 (pushed, **not deployed**)

- **`7f9a1e5` — Tap checkout CSP fix (storefront).** Placing an order POSTs to `/checkout/place`,
  which 302-redirects to `https://checkout.tap.company/…`. Chromium enforces `form-action`
  against the **redirect target** of a form submission, so the old `form-action 'self'` blocked
  the hand-off to Tap. Fixed in `WhiteStiches.Web/Program.cs`:
  `form-action 'self' https://*.tap.company`. (Surfaced only once prod had a real Tap key —
  before that, checkout fell back to a no-redirect manual confirmation.)
- **`2556ebe` — graceful upload error handling (back office).** Image/video upload returned an
  opaque **500** when the storage root wasn't writable. Added `StorageWriteException` (Core);
  `FileStorageService.SaveAsync` now rethrows write failures as that type with the resolved path;
  `ProductsController`/`BannersController` `UploadImages` catch it → clear admin message + logged
  cause. **This does not make uploads work** — it makes the failure self-explaining. The real fix
  is `Storage:Root` (§5.A).

---

## 5. ⚠️ Outstanding server-side actions (go-live checklist)

These are configuration/ops tasks. None are code changes.

### A. Storage root for uploads — REQUIRED, or all media upload fails

Uploads are written by **Admin** and served by **Web** at `/media`, from a folder deliberately
**outside** both apps' `wwwroot`. The dev default is `"Storage:Root": "../../storage"`; on the
testing server both apps now ship `appsettings.Production.json` overriding it with the absolute
folder **`C:\inetpub\media\White-Stiches-Testing`** (committed, so it survives every redeploy).
Because the server runs as Production (neither `IISProfile.pubxml` sets `EnvironmentName`), that
override loads automatically — **no machine env var is needed**. The only remaining server step is
granting the two app pools write access.

Testing server (`83.229.86.221`) — app pools: **`white-stiches-testing`** (Web) and
**`white-stiches-testing-admin`** (Admin).

1. **Grant Modify** to **both** app-pool identities on the media folder (run elevated on the
   server):
   ```powershell
   $dir = 'C:\inetpub\media\White-Stiches-Testing'
   New-Item -ItemType Directory -Force -Path $dir | Out-Null
   icacls $dir /grant "IIS AppPool\white-stiches-testing:(OI)(CI)M"
   icacls $dir /grant "IIS AppPool\white-stiches-testing-admin:(OI)(CI)M"
   ```
   (`(OI)(CI)M` = Modify, inherited by new sub-folders/files. If a pool runs as NetworkService or
   a custom user instead of `ApplicationPoolIdentity`, grant that account instead.)
2. **Redeploy both apps** (Web + Admin) so the new `appsettings.Production.json` lands on the
   server, then **recycle both app pools**.
3. **Verify:** upload an image in Admin, then confirm it renders on the storefront. If it still
   fails, the Admin error now names the path it tried — it should be
   `C:\inetpub\media\White-Stiches-Testing`. If it instead shows a `../../storage`-derived path,
   the site isn't running as Production; set a machine env var as a fallback:
   `[Environment]::SetEnvironmentVariable('Storage__Root', $dir, 'Machine')` then `iisreset`.

### B. Apply migrations — already done, but the procedure for the future

Apps do **not** call `Database.Migrate()` — migrations are applied **manually**. As of
2026-06-14 the live DB has all three migrations and `dotnet ef database update` reports *"already
up to date."* When a future change adds a migration:

```powershell
dotnet ef migrations add <Name> --project src\WhiteStiches.Infrastructure --startup-project src\WhiteStiches.Web
dotnet ef database update      --project src\WhiteStiches.Infrastructure --startup-project src\WhiteStiches.Web
```

EF reads `DefaultConnection` from the startup project's `appsettings.json` **in-process** (= prod
DB). Migrations run in a transaction (a bad statement rolls back). **Never** pass `--verbose` (it
echoes the connection string). Migrate **before** deploying code that needs the new schema.

### C. Tap Payments — live key for real go-live

- The server must have `Tap:SecretKey` set out-of-band (env var `Tap__SecretKey` or the server's
  `appsettings`). The committed value is the **sandbox** `sk_test_…`.
- For production, rotate to the **`sk_live_…`** key. ⚠️ **Never commit a live key** — GitHub
  push-protection blocks `sk_test_`/`sk_live_` values, which is why `appsettings.json` must never
  be `git add`-ed (see §8).
- Set `Tap:PublicBaseUrl` to the public HTTPS origin (so webhook/return URLs are https behind the
  TLS proxy), and configure the Tap dashboard webhook to the public `/checkout/tap-webhook` URL.

### D. Email deliverability

- Authenticate the sending domain in Mailgun: **SPF** (`include:mailgun.org`), **DKIM** selector,
  **DMARC** (`_dmarc`). Then move `Smtp:FromEmail` off `postmaster@mg.codexkw.co` to the branded
  domain.
- Set `Smtp:AdminNotifyEmail` (staff alerts: new order / new return / charge mismatch) and
  `Smtp:ReplyToEmail`. Both are no-ops while unset.

### E. Deploy both apps (see §6) and confirm the Admin `web.config` ships

`WhiteStiches.Admin/web.config` raises IIS `maxAllowedContentLength` to 64 MB so large video
uploads aren't rejected (IIS default ~28.6 MB → 404.13 **before** ASP.NET Core sees the request;
`[RequestSizeLimit]` alone is insufficient on IIS). Make sure this file lands in the deployed
folder.

---

## 6. Deploy

Both apps are deployed independently (IIS). The latest `main` is **not deployed** — ship both:

- **Web** redeploy carries the **Tap CSP fix** (`7f9a1e5`).
- **Admin** redeploy carries the **upload error-handling** (`2556ebe`).

After deploying, work the §5 checklist (Storage:Root + permissions, Tap key, DNS/SMTP). A redeploy
without those leaves uploads and/or live payments non-functional.

**Immediate Tap unblock without a redeploy** (optional): the `form-action` CSP can be overridden at
the Cloudflare edge (Transform Rule → Modify Response Header) to add `https://*.tap.company` — but
the pushed code change is the durable fix.

---

## 7. Operational gotchas & known limitations

- **No auto-migration.** New migrations must be applied by hand (§5.B).
- **CSP is compiled into `Program.cs`** (one per app). Changing it requires a rebuild + redeploy,
  not just a server config tweak.
- **`form-action` + payment redirects.** Any new off-origin form-POST redirect must be added to
  `form-action` in `Web/Program.cs`, or Chromium blocks it.
- **Storage is shared + external.** Web and Admin must point `Storage:Root` at the *same* absolute
  folder, or Admin uploads succeed but storefront images 404 (and vice-versa).
- **`appsettings.json` is never committed-with-secrets-changed / never `git add`-ed** — it holds
  the Tap key that push-protection blocks. Stage files explicitly (§8).
- **BaseEntity uses INT identity keys** (not Guid) — the EF "tracked-parent `.Add` → UPDATE-0-rows"
  trap does **not** apply here.
- **Webhook-first finalize doesn't clear the guest cart** (no HTTP context in `PaymentService`); a
  guest who never returns to `/checkout/tap-return` keeps their bag. Low-probability; documented in
  `BUILD-PLAN.md`.
- **No background sweep** for abandoned `Pending` Tap orders (hosted-page TTL ~30 min) — they
  linger in the admin order list.
- **Admin-created draft/manual orders** default `LanguageCode="en"` for emails.

---

## 8. Git & secrets discipline

- **Public repo.** Per project decision, config + secrets currently live in `appsettings.json` and
  the exposure is accepted (to be hardened later). **Do not re-block on this.**
- **`sk_test_` is sandbox; `sk_live_` must NEVER be committed.** GitHub push-protection blocks the
  Tap key, so **never `git add appsettings.json`** — stage files **explicitly** (never `git add -A`).
- Commit flow used here: branch from `main` → stage an explicit file list → commit → `git checkout
  main && git merge --ff-only <branch> && git push origin main && git branch -d <branch>`.
- Commit trailer: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

## 9. Key file map

| Concern | Path |
|---|---|
| Storefront CSP / security headers | `src/WhiteStiches.Web/Program.cs` (+ `Admin/Program.cs`) |
| Security-header middleware | `src/WhiteStiches.Infrastructure/Security/SecurityHeadersSetup.cs` |
| File storage (save/delete, allow-list) | `src/WhiteStiches.Infrastructure/Services/FileStorageService.cs` |
| Storage root resolution + `/media` serving | `src/WhiteStiches.Infrastructure/StorageSetup.cs` |
| Storage failure type | `src/WhiteStiches.Core/Interfaces/StorageWriteException.cs` |
| Media kind enum + classifier | `src/WhiteStiches.Core/Enums/CatalogEnums.cs` |
| Tap client (hosted redirect) | `src/WhiteStiches.Infrastructure/Payments/TapPaymentService.cs` |
| Checkout flow | `src/WhiteStiches.Web/Controllers/CheckoutController.cs` |
| Tap return/webhook | `src/WhiteStiches.Web/Controllers/PaymentsController.cs` |
| Product admin (images/variants) | `src/WhiteStiches.Admin/Controllers/ProductsController.cs` |
| Banner CMS | `src/WhiteStiches.Admin/Controllers/BannersController.cs`, `Infrastructure/Services/(Admin/)BannerService(s)` |
| Bilingual content accessors | `src/WhiteStiches.Infrastructure/Localization/LocalizedContent.cs` |
| EF model + migrations | `src/WhiteStiches.Infrastructure/Data/` |
| Admin IIS upload limit | `src/WhiteStiches.Admin/web.config` |

---

## 10. Suggested first move for the next session

1. Deploy **Web** + **Admin** from `main` (`2556ebe`).
2. Do §5.A (Storage:Root + folder permissions) — unblocks all media uploads.
3. Smoke-test on the live site: a guest checkout redirects to Tap (`302 → checkout.tap.company`);
   an admin product image upload succeeds and renders on the storefront.
4. Then §5.C/D (Tap live key, DNS/SMTP) as part of the real go-live.
