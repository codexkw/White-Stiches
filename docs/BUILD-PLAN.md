# White Stitches — Build Plan

Phases map to PRD v1.0 priorities (P0 = Phase 1 MVP). Requirement IDs reference `docs/PRD.md`.

## ✅ Phase 0 — Platform foundation (DONE · 2026-06-10)

- [x] .NET 9 solution: Core / Infrastructure / Web / Admin (`docs/ARCHITECTURE.md`)
- [x] Domain model: 27 entities across catalog, cart, orders, returns, marketing, content, settings, audit (`docs/DATABASE.md`)
- [x] EF Core migrations applied to `White-Stiches` DB on 83.229.86.221; idempotent seeding (roles, super admin, categories, settings)
- [x] ASP.NET Identity with PRD §9 roles; Admin default-deny staff policy + audit-logged login
- [x] Service layer (8 services) shared by Web + Admin — no API layer
- [x] All 31 storefront pages converted to Razor (pixel-faithful) with layouts + route map (`docs/HTML-CONVERSION.md`)
- [x] Admin shell: brand login + dashboard with live counts
- [x] Repo: github.com/codexkw/White-Stiches

## ✅ Phase 1A — Storefront goes dynamic (DONE · 2026-06-10)

Verified end-to-end by `scripts/smoke-e2e.ps1` (20-step scripted customer journey — all pass).

- [x] Customer auth: register/login/forgot bound to Identity; guest cart merged at login; logout (SF-ACC-01, SF-CRT-06)
- [x] Catalog: home featured grid (ViewComponent), `/collection` with category/size/color/price/in-stock filters + sort + paging via URL params, `/products/{slug}` PDP with variant selection/stock states/related products, `/search` (SF-HOM-03/04, SF-COL-01..05, SF-PDP-01/02/03/05/07/08)
- [x] Cart: server cart (guest cookie token `ws_cart` + user carts), add/update/remove/discount/options endpoints, mini-cart drawer + header badge as ViewComponents, free-shipping progress (SF-CRT-01..06)
- [x] Checkout: address capture (Kuwait structure), shipping methods from settings, order creation with snapshots + stock decrement + cart clear, confirmation page at `/checkout/confirmation/{orderNumber}` (SF-CHK-01/02/05/06/08 — payment itself is a Manual/COD placeholder until Tap, Phase 1C)
- [x] Account suite: dashboard, orders list + `/account/orders/{number}` detail with status stepper, addresses CRUD + default, profile + password + marketing prefs, wishlist (add/remove/move-to-bag), returns wizard for delivered orders (SF-ACC-02..07, SF-ACC-09)
- [x] Content: journal index/post from DB, contact form persists, newsletter endpoint, public order tracking by number + email/phone (SF-JRN-01/02, SF-STA-02/06)
- [x] Demo catalog seeder (`SampleDataSeeder`, opt-in via `SeedSampleData` config): 16 products with options/variants/images, Spring Edit collection, 4 journal categories + posts, demo discount codes (WELCOME10/SS26/EID2026)
- [x] Entry flow: first visit to `/` shows the intro splash once per browsing session (`ws_intro` session cookie set when `/intro` is served), then auto-advances/taps into home; deep links are never gated; no-JS fallback via real enter link + `<noscript>` refresh

**Phase 1A leftovers (carried forward):**
- Cart page "Complete the bag" row and the layout's search-overlay suggestions are still static demo markup (live data follow-up)
- Policy/static pages still render their converted static content (not yet `StaticPage`-driven) — pairs with the Admin pages editor in 1B
- PDP reviews/Q&A and product video are P1 (no models yet); collection fabric/occasion filters need product fields
- `DiscountCode.TimesUsed` is not incremented at order placement yet
- Forgot-password generates a token but cannot email it until SMTP (1C)

## ✅ Phase 1B — Admin modules (DONE · 2026-06-10)

Built as 8 parallel modules with disjoint file ownership (`docs/PHASE-1B-PLAN.md`); shared
scaffolding (file storage at `/media`, admin service stubs, sidebar, pager, CSS kit) pre-built.
Verified end-to-end by `scripts/smoke-admin.ps1` (25-step back-office journey — all pass) and
the storefront `scripts/smoke-e2e.ps1` (21-step — all pass, no service-layer regression).

| # | Work | Requirements | Routes |
| --- | --- | --- | --- |
| 1 | ✅ Products: CRUD + bilingual fields, images, options/variants editor, inventory adjustments | AD-PRD-01..05 | `/products*`, `/categories*` |
| 2 | ✅ Collections: manual curation + smart rules (RulesJson, evaluate/apply) | AD-PRD-07 | `/collections*` |
| 3 | ✅ Orders: list/filters/search, detail timeline, mark-paid, fulfil (manual AWB), cancel/restock, refunds, notes, draft orders → convert | AD-ORD-01..05, 08 | `/orders*` |
| 4 | ✅ Returns queue: approve/reject/receive/refund state machine with restock | AD-ORD-10 | `/returns*` |
| 5 | ✅ Customers: list, profile, consent status, lock/unlock | AD-CUS-01/02 | `/customers*` |
| 6 | ✅ Discounts: CRUD with limits/schedule; newsletter list + CSV export | AD-MKT-01 | `/discounts*`, `/newsletter*` |
| 7 | ✅ Content: pages + journal editors (hero upload), contact inbox | AD-CNT-01/02 | `/pages*`, `/journal*`, `/inbox*` |
| 8 | ✅ Settings (grouped panels); staff & roles management; audit log viewer; TOTP 2FA (enable/login/recovery) | AD-SET-01/02/04/06 | `/settings*`, `/staff*`, `/audit`, `/profile/2fa*` |

**Phase 1B leftovers (carried forward):**
- Product/collection image upload is wired (`IFileStorage` → `/media`) but not exercised by the
  HTTP smoke (PowerShell 5.1 can't post multipart) — verify manually or in 1D QA.
- Storefront static/policy pages still render converted markup; binding them to the new
  `StaticPage` editor (Content module) is a small Web-side follow-up.
- Admin 2FA login step exists but the TOTP enrolment flow needs a manual device pass.

## Phase 1C — Integrations (launch blockers)

| # | Work | Requirements |
| --- | --- | --- |
| 1 | ✅ **Tap Payments**: hosted checkout, webhooks (signature + idempotency), refunds from Admin | INT-PAY-01..05, SF-CHK-03 |
| 2 | Delivery partner behind provider-agnostic interface (partner TBD — open question in PRD §12) | INT-DLV-01/02 |
| 3 | SMTP transactional email (bilingual templates) | INT-EML-01..03 |
| 4 | WhatsApp transactional templates (order confirmed/shipped/delivered) | INT-WAP-01..03 |
| 5 | GA4 + Consent Mode v2 wired to the cookie-consent state already in the front end | INT-GA4-01/02 |

### ✅ 1C-1 Tap Payments (DONE · 2026-06-11)

Provider-agnostic gateway (`IPaymentGateway` → `TapPaymentService`, typed `HttpClient`) + order-aware
orchestration (`IPaymentService` → `PaymentService`). Tap v2 Charges API: hosted redirect via
`source.id="src_all"` + `redirect.url`; confirmed by GET retrieve **and** the `post.url` webhook
(HMAC-SHA256 `hashstring`). KWD sent as 3-decimal major units. All three API contracts validated
against the live sandbox; a full card+3DS purchase was captured and finalized end-to-end, and an
Admin → Tap partial refund was issued.

- **Online payment only** — knet/card/applepay → Tap hosted page. **Cash on delivery was removed**
  (2026-06-11): no `cod` option in the UI, the form model, or the controller. Because there is no
  longer a non-Tap method, the no-key **manual fallback is now gated to the Development environment**
  (so local dev + smoke tests still complete); in any other environment an unconfigured gateway
  returns a "payment temporarily unavailable" error instead of silently booking an **unpaid** order.
- Order is created `Pending`; **stock is decremented + cart cleared only once payment is confirmed**
  (return or webhook). Finalization is idempotent + race-safe: an atomic `ExecuteUpdate` claim on the
  payment row (return vs webhook) and an atomic `Stock = Stock - qty` (cross-order). Captured amount is
  reconciled against the order total before marking paid (mismatch → held for review).
- Re-submitting checkout **resumes** the in-flight order via a `ws_pay` cookie instead of duplicating.
  The resume now also requires the **same bag** (line items + amount), not just a matching total — this
  closes a shared-device case where one guest (null UserId) could otherwise resume another's order.
- `Program.cs` logs the Tap config state at startup ("Tap payments: CONFIGURED / NOT configured" +
  PublicBaseUrl) so a deployment can be verified from the logs.
- Admin refunds call Tap for `Provider="Tap"` payments (store `GatewayRefundId`); `"Manual"` stay local.
- Keys: the `Tap` section lives in `appsettings.json` (Web + Admin). `Tap:SecretKey` now holds the
  **sandbox** key locally so a Web-Deploy/publish carries it to the server. ⚠️ **Do not `git push` this** —
  GitHub push-protection blocks the `sk_test_`/`sk_live_` value (flags it as a Stripe key); keep the key in
  local appsettings for the publish only, and **never** commit a live `sk_live_` key. `Tap:MerchantId` = 599424,
  `Tap:PublicBaseUrl` = `https://white-stiches-testing.codexkw.co`.

**1C-1 leftovers / before go-live:**
- **The deployed server must have `Tap:SecretKey` set out-of-band** (env var `Tap__SecretKey`, or the
  server's `appsettings`). The committed value is empty, so a fresh deploy has `IsConfigured=false` and
  checkout never redirects to Tap — verified 2026-06-11 on the testing site: a guest KNET checkout went
  straight to a (manual, unpaid) confirmation, no Tap redirect. With the key set locally, the same guest
  flow returns `302 → checkout.tap.company` — so this is purely a server-config gap, not a code bug.
- Set `Tap:PublicBaseUrl` to the public https origin in production so the webhook/return URLs are
  always https (there is no `ForwardedHeaders` middleware, so behind a TLS proxy `Request.Scheme` may be
  http). The webhook needs a public HTTPS endpoint Tap can reach — configure the post URL / dashboard
  webhook. Rotate `Tap:SecretKey` to the `sk_live_` key for production.
- **Webhook-first finalize doesn't clear the guest cart** (PaymentService has no HTTP context): if the
  webhook captures the charge and the browser never returns to `/checkout/tap-return`, the guest's
  `ws_cart` still has the items. Low-probability, but to fix it cleanly, link the order to its cart
  (e.g. a nullable `Order.CartToken`) and clear that cart inside `FinalizeCapturedChargeAsync`.
- No background sweep yet for abandoned `Pending` Tap orders (hosted-page TTL ~30 min) — they linger in
  the admin order list. Add an `IHostedService` to expire/cancel them (and/or filter Pending+Initiated
  Tap orders out of the default admin list).
- Webhook signature verified by code review against the documented algorithm; exercise it against a real
  Tap delivery once a public HTTPS endpoint exists (can't reach localhost).

## Phase 1D — Localization & launch QA

- Arabic content rendering (`…Ar` columns), full RTL pass (`dir="rtl"` toggle already in shell), KWD 3-decimal formatting everywhere (LOC-01..05) — **now planned in full as Phase 1E‑3** (real i18n infra + Arabic admin), this 1D line is the launch-QA sign-off on it
- Cross-page QA polish pass per PRD §11 Definition of Done
- Lighthouse / Core Web Vitals pass (NFR-PRF-01)
- Security pass: rate limiting on auth/checkout/search, headers, pen test (NFR-SEC-01..03)

## ✅ Phase 1E — Admin power-ups & full bilingual platform (DONE · 2026-06-11)

Four work-items requested 2026-06-11, built in the order **1E‑3 foundation → 1E‑2 → 1E‑1 →
1E‑4 → 1E‑3 string-sweep**. **1E‑1 and 1E‑4 share one `IAnalyticsService`/aggregation layer.**
The ~71-view string sweep was run as two parallel agent workflows (one agent per view, disjoint
file ownership), the returned translations merged into the resx by `scripts/merge-i18n.ps1`, and
coverage proven by `scripts/i18n-coverage.ps1`.

**Verified:** solution builds 0/0 · storefront `smoke-e2e.ps1` 21/21 · admin `smoke-admin.ps1`
25/25 · all 8 reports + CSV export + filtered report render 200 · dashboard ranges 7/90/365 render ·
storefront **and** admin render Arabic with `dir="rtl"` (`?culture=ar`) · i18n coverage gate PASS
(every `@L` key used in the 699 storefront + 569 admin view references has an Arabic translation).

| # | Work | Reqs | Status |
| --- | --- | --- | --- |
| 1E‑1 | ✅ Advanced analytics dashboard (`IAnalyticsService` + vanilla SVG charts, KPIs w/ period deltas, breakdowns, leaderboards, ops snapshot, 60s cache) | AD‑DSH‑01..06 | done |
| 1E‑2 | ✅ Rich-text WYSIWYG (dependency-free `editor.js`) on the 12 HTML fields + **server-side `HtmlSanitizer`** (`IRichTextSanitizer`) on save | AD‑RTE‑01..03 | done |
| 1E‑3 | ✅ Full bilingual: `RequestLocalization` (en/ar-KW) + cookie/login switcher + RTL CSS + culture-aware DB-content accessors + resx sweep of ~71 views | LOC‑01..09 | done |
| 1E‑4 | ✅ Reports module: 8 reports (`IReportService`), rich filters, CSV export (UTF‑8 BOM), audit-logged | AD‑RPT‑01..09 | done |

**1E leftovers / notes:**
- The string sweep covers all interactive UI chrome; **long-form legal/marketing prose** (Terms,
  Privacy, About, FAQ, Cookies, Shipping, Size Guide, Returns Policy body copy) and the static
  mega-menu placeholder links were deliberately left in English — bind them to the `StaticPage`
  editor + translate when those pages go DB-driven (pairs with the 1A/1B static-page follow-up).
- A few storefront labels with literal `&amp;` double-encode to `&amp;` on screen (resx key vs
  HTML-entity) — cosmetic, low priority.
- RTL is a solid baseline (`rtl.css` per app, scoped to `[dir=rtl]`); a fine-grained per-component
  RTL polish pass belongs in Phase 1D launch QA.
- Reports XLSX/PDF export (beyond CSV) and saved/scheduled exports remain a stretch (XLSX needs
  ClosedXML; scheduled email needs 1C‑3 SMTP).
- New requirement IDs (AD‑DSH/AD‑RTE/AD‑RPT/LOC‑06..09) extend the PRD — fold into PRD on next revision.

### 1E‑1 — Advanced analytics dashboard

Replaces the current `DashboardController` (which queries `DbContext` directly for 6 count
tiles) with a service-backed, date-range-driven analytics home.

- **New `IAnalyticsService` → `AnalyticsService`** (`Core/Interfaces/Admin` + `Infrastructure/Services/Admin`,
  registered in `AddWhiteStichesAdminServices`); DTOs in `Core/Models/Admin/Analytics`. Dashboard
  controller stops touching `DbContext` directly. **Shared with 1E‑4 reports.**
- **KPI tiles with period-over-period delta** (selected range vs the preceding equal range):
  net revenue (Σ `Order.Total` for paid/non-cancelled, minus refunds), orders, AOV, units sold
  (Σ `OrderItem.Quantity`), new customers, refund amount + return rate, discount spend.
- **Time-series chart**: revenue + order count by day/week/month for the chosen range
  (`PlacedAtUtc ?? CreatedAtUtc`). **Charting lib decision (none exists today)** — recommend
  **ApexCharts** (MIT, native RTL, self-hostable to `wwwroot/lib`, no CDN per house convention).
- **Breakdowns**: revenue by payment method (`Payment.Method`) and by channel (`OrderChannel`);
  orders by `Status`; top products by units **and** revenue (aggregate `OrderItem` by `ProductId`);
  top customers by spend; sales by category/collection; new-vs-returning customers.
- **Operational widgets**: pending fulfilment, pending returns, **abandoned `Pending` Tap orders**
  (ties to the 1C‑1 sweep TODO), unread contact messages, low-stock variants
  (`StockQuantity <= LowStockThreshold`).
- **Rules**: all money KWD 3-decimal; exclude `Cancelled` from revenue; net refunds out; `AsNoTracking`;
  cache/throttle heavy aggregates (consider a short MemoryCache TTL). Localized + RTL once 1E‑3 lands.

### 1E‑2 — Rich-text (WYSIWYG) editor

Upgrade the **12 genuine-HTML fields** (the rest stay plain textareas) to a WYSIWYG editor.

- **Fields (each its own editor instance, EN + the `dir="rtl"` AR twin):**
  Products/Edit → `Description`, `MaterialCare`, `SizeFit`; JournalAdmin/Edit → `Body`;
  PagesAdmin/Edit → `Body`; Collections/Edit → `Description`.
  **Leave plain:** all SEO meta descriptions, `EligibilityJson` (Discounts), announcement messages,
  order/return internal & customer notes, journal excerpts.
- **Library decision (none exists today)** — recommend **Quill 2** (MIT, lightweight, self-hosted,
  RTL via the direction format), or TinyMCE self-host if richer tables/media are needed. Decide once.
- **Reusable wiring**: a single init script + partial/tag-helper that upgrades any
  `<textarea data-editor="rich" data-dir="rtl|ltr">` and syncs HTML back to the textarea on submit
  (model binding unchanged). Mirror the existing image-upload to `IFileStorage`→`/media` for inline images (stretch).
- **⚠️ Security — server-side HTML sanitization is mandatory.** These bodies are rendered raw on the
  storefront (`@Html.Raw`), so a WYSIWYG that emits arbitrary HTML is a stored-XSS vector. Add
  **`Ganss.Xss` (HtmlSanitizer)** and sanitize on save in the owning admin service/controller before persist.

### 1E‑3 — Full bilingual (Arabic admin + storefront content/UI + missing-string sweep)

Covers the requests "admin should support Arabic" and "check all missing localizations in both
languages and use all in all screens." **Today: zero i18n infrastructure** — no `.resx`, no
`IStringLocalizer`, no `RequestLocalization`; the data layer is fully bilingual (9 entities with
`…Ar`) but the storefront renders English-only, the header switcher only flips the CSS `dir`
attribute (no text changes), and the **admin is 100% hardcoded English**. `ApplicationUser.PreferredLanguage`
and `Order.LanguageCode` columns exist but are never read. ~80 views (40 web + 40 admin) are affected.

1. **i18n infrastructure (both apps)**: `AddLocalization` + `UseRequestLocalization` with supported
   cultures `en` + `ar-KW`; culture providers = cookie (`.AspNetCore.Culture`) → user
   `PreferredLanguage` → `Accept-Language`. Drive `<html lang dir>` in all three layouts from the
   current culture (currently hardcoded `lang="en" dir="ltr"`).
2. **Real language switcher**: replace the JS `dir`-flip with a `POST /set-culture` that writes the
   culture cookie and persists `ApplicationUser.PreferredLanguage`; storefront **and** admin both get one.
3. **DB content localization**: a culture-aware accessor (e.g. `product.Title()` returns `TitleAr`
   when culture is Arabic, else `TitleEn`, with EN fallback). Sweep storefront views to replace every
   `.TitleEn/.NameEn/.AltEn/.DescriptionEn/.BodyEn/...` with the accessor; render `OrderItem.TitleAr`
   on confirmations/account. **This is what finally uses the dormant `…Ar` columns.**
4. **Static UI-string localization**: introduce a `SharedResource` + `.resx` (en + ar) and
   `IStringLocalizer`/`IViewLocalizer`; sweep all ~40 storefront + ~40 admin views replacing
   hardcoded literals (nav, buttons, labels, headings, placeholders, validation/DataAnnotation
   messages) with keys. This is the bulk of the "missing localizations" work.
5. **Arabic admin specifically**: wire the same infra into the Admin app, localize nav/labels/
   buttons/validation, and add an **admin RTL CSS pass** (logical properties / `[dir=rtl]` rules,
   not just the attribute) + admin language switcher.
6. **Storefront RTL CSS pass**: ensure real RTL styling beyond the attribute flip.
7. **Coverage gate**: a grep/checklist script to flag remaining hardcoded literals and assert every
   resx key has both `en` + `ar` (a missing-translation report) — this is the "all screens" guarantee.
   Keep KWD at 3-decimal/invariant; localize date display.

### 1E‑4 — Detailed reports module (filter + export)

A dedicated `/reports` area. Today only the newsletter CSV export exists — but the conventions are
proven: `PagedResult<T>`, the `_Pager` partial (query-string-preserving), the RFC-4180 `Csv()`
helper, and `audit.LogAsync` on export. Reuse all of them.

- **New `IReportService` → `ReportService`** (`Core/Interfaces/Admin` + `Infrastructure/Services/Admin/Reporting`),
  registered in `AddWhiteStichesAdminServices`; report DTOs in `Core/Models/Admin/Reports`. Reuses the
  1E‑1 `IAnalyticsService` aggregations where they overlap.
- **Report types**: Sales (by day/month — revenue, orders, AOV, tax, shipping, discount, **net** of
  refunds), Orders, Best-sellers (units + revenue per product/variant), Inventory & stock valuation
  (`Σ StockQuantity × Cost`, low-stock list), Customers/LTV (new vs returning, top spenders),
  Discounts (usage + revenue impact), Returns (rate + reasons), Payments (by provider/method, captured
  vs refunded), Newsletter/acquisition source, Tax/finance summary.
- **Rich filtering** via the established conditional-`.Where()` + query-string + `_Pager` pattern:
  date range (presets + custom), status enums, channel, category/collection, payment method, customer,
  product. Sortable columns.
- **Export**: CSV for every report (reuse `Csv()`); add **XLSX via ClosedXML**; optional PDF for the
  finance summary. Stream as `File(...)` downloads with an `audit.LogAsync` entry each (newsletter-export pattern).
- **Permissions**: gate `/reports` behind an appropriate staff role (admin default-deny already applies).
- **Stretch** (after 1C‑3 SMTP): saved filters + scheduled emailed exports.

## Phase 2+ (PRD §10.2/10.3)

GCC markets, mada/BNPL/COD, marketing automations, reviews, loyalty, gift cards,
multi-location inventory, Instagram inbox — out of current scope.
(Advanced analytics & detailed reports were pulled forward into Phase 1E.)

## Known technical debt / notes

- Storefront views are static ports; demo logic in `site.js` (hardcoded discount codes, fake auth redirects, client cart math) must be replaced during 1A — inventory in `HTML-CONVERSION.md`.
- `SeedAdmin` default password must be rotated; move secrets out of appsettings before any shared deployment.
- `ProductSort.BestSelling` approximates (featured→newest) until order-aggregation reporting exists.
- Order numbers derive from max(Id)+base — safe for single-instance; revisit with a sequence when scaling out.
