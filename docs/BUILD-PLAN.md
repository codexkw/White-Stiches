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

## Phase 1A — Storefront goes dynamic

| # | Work | Requirements |
| --- | --- | --- |
| 1 | Customer auth: register/login/forgot views → Identity; guest wishlist/cart merge at login | SF-ACC-01, SF-CRT-06 |
| 2 | Catalog binding: home featured products, collection grid + filters/sort from `ProductQuery` URL params, PDP from slug (`/products/{handle}`), search | SF-HOM-03/04, SF-COL-01..07, SF-PDP-01..08/12 |
| 3 | Cart: server cart via cookie token, add/update/remove endpoints, mini-cart partial, discount application | SF-CRT-01..06 |
| 4 | Checkout (pre-payment): address capture, shipping methods from settings, order creation + confirmation page, stock decrement | SF-CHK-01/02/05/06/08 |
| 5 | Account suite: orders, order detail (status stepper), addresses CRUD, wishlist, profile, returns wizard | SF-ACC-02..09 |
| 6 | Content from DB: journal index/post, policy pages via `StaticPage`, contact form → `ContactMessage`, newsletter band → subscribers | SF-JRN-01/02, SF-STA-01..06 |

## Phase 1B — Admin modules

| # | Work | Requirements |
| --- | --- | --- |
| 1 | Products: CRUD + bilingual fields, images, options/variants editor, inventory adjustments | AD-PRD-01..05 |
| 2 | Collections: manual curation + smart rules | AD-PRD-07 |
| 3 | Orders: list/filters/search, detail with timeline, fulfilment (manual AWB), cancel/restock, draft orders | AD-ORD-01..05, 08 |
| 4 | Returns queue: approve/reject/receive/refund flow | AD-ORD-10 |
| 5 | Customers: list, profile, consent status | AD-CUS-01/02 |
| 6 | Discounts: CRUD with limits/schedule | AD-MKT-01 |
| 7 | Content: pages + journal editors, contact inbox | AD-CNT-01/02 |
| 8 | Settings: store/shipping/notifications; staff & roles management; audit log viewer; 2FA | AD-SET-01/02/04/06 |

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
