# HTML → Razor Conversion Spec

The static site in `HTML/` (kept verbatim as the design reference) was converted to Razor
views by `scripts/convert-html.ps1` — a deterministic, re-runnable transform. Every generated
view was then verified page-by-page against its source by an automated review pass.

## Shell analysis (what made this mechanical)

- **25 pages share a byte-identical full shell**: announcement bar, header + 3 mega menus,
  mobile drawer, footer, mini-cart drawer, search overlay, cookie banner/modal, WhatsApp float,
  SVG defs, and a single `js/site.js?v=2.6` script. No per-page variations, no inline scripts,
  no active-nav states. → extracted once into `Views/Shared/_Layout.cshtml`.
- **checkout + order-confirmation** share the minimal document shell and `.co-footer`, but their
  `.co-hdr` header and `.co-steps` states differ per page → `_CheckoutLayout.cshtml` holds only
  the document head + body open + footer + script; header/steps stay in each view.
- **intro / maintenance / design-system** are self-contained → `Layout = null` standalone views.

## Transform rules (applied per page)

1. **Razor escaping** — every literal `@` becomes `@@` (emails, CSS `@media`, JS).
2. **Assets** — `href|src="css/..."` → `~/css/...`, same for `js/`, `assets/`; `url(assets/…)` → `url(/assets/…)`.
3. **Links** — every quoted `*.html` reference (including `#anchors` and `?query`) rewritten via the route map below; also applied to `wwwroot/js/site.js` (its 5 hardcoded `.html` links).
4. **Metadata** — source `<title>` and meta description become `ViewData["Title"]` / `ViewData["MetaDescription"]`, rendered by the layouts.
5. **Encoding** — read/written as UTF-8 (BOM on .cshtml); em-dashes, `·`, `−` and Arabic glyphs preserved.

## Route map

| Static page | MVC route | Controller.Action | View |
| --- | --- | --- | --- |
| index.html | `/` | Home.Index | Home/Index |
| intro.html | `/intro` | Home.Intro | Home/Intro *(standalone)* |
| not-found.html | `/not-found` (+ every 404) | Home.PageNotFound | Home/NotFound |
| maintenance.html | `/maintenance` | Home.Maintenance | Home/Maintenance *(standalone)* |
| design-system.html | `/design-system` | Home.DesignSystem | Home/DesignSystem *(standalone)* |
| collection.html | `/collection` | Shop.Collection | Shop/Collection |
| product.html | `/product` | Shop.Product | Shop/Product |
| search.html | `/search` | Shop.Search | Shop/Search |
| cart.html | `/cart` | Cart.Index | Cart/Index |
| checkout.html | `/checkout` | Checkout.Index | Checkout/Index *(checkout layout)* |
| order-confirmation.html | `/checkout/confirmation` | Checkout.Confirmation | Checkout/Confirmation *(checkout layout)* |
| account.html | `/account` | Account.Index | Account/Index |
| account-login.html | `/account/login` | Account.Login | Account/Login |
| account-orders.html | `/account/orders` | Account.Orders | Account/Orders |
| account-order-detail.html | `/account/orders/detail` | Account.OrderDetail | Account/OrderDetail |
| account-addresses.html | `/account/addresses` | Account.Addresses | Account/Addresses |
| account-profile.html | `/account/profile` | Account.Profile | Account/Profile |
| account-wishlist.html | `/account/wishlist` | Account.Wishlist | Account/Wishlist |
| account-returns.html | `/account/returns` | Account.Returns | Account/Returns |
| about.html | `/about` | Pages.About | Pages/About |
| contact.html | `/contact` | Pages.Contact | Pages/Contact |
| faq.html | `/faq` | Pages.Faq | Pages/Faq |
| size-guide.html | `/size-guide` | Pages.SizeGuide | Pages/SizeGuide |
| shipping.html | `/shipping` | Pages.Shipping | Pages/Shipping |
| returns-policy.html | `/returns-policy` | Pages.ReturnsPolicy | Pages/ReturnsPolicy |
| privacy.html | `/privacy` | Pages.Privacy | Pages/Privacy |
| terms.html | `/terms` | Pages.Terms | Pages/Terms |
| cookies.html | `/cookies` | Pages.Cookies | Pages/Cookies |
| track.html | `/track` | Pages.Track | Pages/Track |
| journal.html | `/journal` | Journal.Index | Journal/Index |
| journal-post.html | `/journal/post` | Journal.Post | Journal/Post |

Slug-based routes (`/products/{handle}`, `/collections/{handle}`, `/journal/{slug}`) arrive
when views bind to the database — the current flat routes match the static pages 1:1.

## Front-end behavior inventory (site.js)

All modules initialize on `DOMContentLoaded` and guard against missing elements, so one
script serves every page. Demo behaviors that must move server-side during integration:

- **Discount codes** hardcoded client-side: `WELCOME10`, `SS26`, `EID2026` → replace with `IMarketingService.ValidateDiscountCodeAsync`.
- **Cart/checkout math** (50 KWD free-shipping threshold, 3.5 gift wrap, shipping rates) → replace with `ICartService.GetSummaryAsync` (already settings-driven).
- **Auth flows** redirect client-side after fake validation → replace with Identity (`/account/login`).
- **Track lookup** treats any order containing "WS" as found → replace with `IOrderService.TrackAsync`.
- Browser storage keys: `ws_cookie_consent` (localStorage, consent JSON), `ws_splash_seen` (sessionStorage, intro splash). Keep names — returning visitors' consent carries over.

## Re-running the conversion

`scripts/convert-html.ps1` is idempotent — edit the HTML reference, re-run, and the views are
regenerated. Don't hand-edit generated sections you intend to regenerate. Once views start
binding to services/models, retire the script for those pages.
