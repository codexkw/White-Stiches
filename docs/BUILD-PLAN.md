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
| 1 | **Tap Payments**: hosted checkout, webhooks (signature + idempotency), refunds from Admin | INT-PAY-01..05, SF-CHK-03 |
| 2 | Delivery partner behind provider-agnostic interface (partner TBD — open question in PRD §12) | INT-DLV-01/02 |
| 3 | SMTP transactional email (bilingual templates) | INT-EML-01..03 |
| 4 | WhatsApp transactional templates (order confirmed/shipped/delivered) | INT-WAP-01..03 |
| 5 | GA4 + Consent Mode v2 wired to the cookie-consent state already in the front end | INT-GA4-01/02 |

## Phase 1D — Localization & launch QA

- Arabic content rendering (`…Ar` columns), full RTL pass (`dir="rtl"` toggle already in shell), KWD 3-decimal formatting everywhere (LOC-01..05)
- Cross-page QA polish pass per PRD §11 Definition of Done
- Lighthouse / Core Web Vitals pass (NFR-PRF-01)
- Security pass: rate limiting on auth/checkout/search, headers, pen test (NFR-SEC-01..03)

## Phase 2+ (PRD §10.2/10.3)

GCC markets, mada/BNPL/COD, marketing automations, reviews, loyalty, gift cards,
multi-location inventory, Instagram inbox, advanced analytics — out of current scope.

## Known technical debt / notes

- Storefront views are static ports; demo logic in `site.js` (hardcoded discount codes, fake auth redirects, client cart math) must be replaced during 1A — inventory in `HTML-CONVERSION.md`.
- `SeedAdmin` default password must be rotated; move secrets out of appsettings before any shared deployment.
- `ProductSort.BestSelling` approximates (featured→newest) until order-aggregation reporting exists.
- Order numbers derive from max(Id)+base — safe for single-instance; revisit with a sequence when scaling out.
