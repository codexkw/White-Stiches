# White Stitches — Product Requirements Document (PRD)

**Storefront (Website) & Admin Panel**

| Field | Value |
| --- | --- |
| **Product** | White Stitches Online Store (whitestiches.kw) |
| **Document** | Product Requirements Document (PRD) |
| **Version** | 1.0 |
| **Date** | June 2026 |
| **Owner** | Anas — White Stitches |
| **Status** | Draft for review |
| **Source documents** | Features & Requirements v1.0 · Storefront Pages Inventory v1.0 |

---

## Contents

1. [Introduction](#1-introduction)
2. [Product Overview](#2-product-overview)
3. [Scope at a Glance](#3-scope-at-a-glance)
4. [Storefront Requirements (Website)](#4-storefront-requirements-website)
5. [Admin Panel Requirements](#5-admin-panel-requirements)
6. [Integration Requirements](#6-integration-requirements)
7. [Localization & Regional Requirements](#7-localization--regional-requirements)
8. [Non-Functional Requirements](#8-non-functional-requirements)
9. [User Roles & Permissions](#9-user-roles--permissions)
10. [Release Plan](#10-release-plan)
11. [Launch Acceptance Criteria — Definition of Done (Phase 1)](#11-launch-acceptance-criteria--definition-of-done-phase-1)
12. [Risks, Dependencies & Open Questions](#12-risks-dependencies--open-questions)
13. [Appendix](#13-appendix)

---

# 1. Introduction

## 1.1 Purpose

This Product Requirements Document (PRD) defines what the White Stitches e-commerce platform must do, for whom, and to what standard. It translates the Features & Requirements Document (v1.0) and the Storefront Pages Inventory (v1.0) into prioritized, testable product requirements covering two products that share one platform: the customer-facing **Storefront** (website) and the merchant-facing **Admin Panel**.

The PRD is the single source of truth for build planning, sprint scoping, QA test design, and launch acceptance. Where this document and earlier documents conflict, this document governs.

## 1.2 Background

White Stitches is a luxury fashion brand selling jackets, dresses, suits, and tops to customers in Kuwait and the wider GCC. The brand's customers shop primarily on mobile, discover products through Instagram, expect to pay with KNET and Apple Pay, and communicate through WhatsApp. No off-the-shelf platform serves this combination without per-transaction fees and compromised regional UX, so White Stitches is building a Shopify-parity platform tailored to the GCC.

At the time of writing, all 30 storefront pages have been built as static HTML/CSS/JavaScript hosted on IIS, with a black/white/silver visual identity, a cinematic intro entry flow, and global overlay components (mini-cart drawer, search overlay, cookie consent). A cross-page QA pass and Phase 1 backend integration remain before launch. The Admin Panel has not yet been built.

## 1.3 Related Documents

| Document | Version | Role |
| --- | --- | --- |
| White Stitches Features & Requirements Document | 1.0 | Full feature catalogue and Shopify-parity rationale |
| Storefront Pages Inventory | 1.0 | Page-by-page build briefs, components, and states |
| Admin Panel Pages Inventory | TBD | To be produced before Admin build begins |
| Brand Guidelines (palette, typography, voice) | Current | Visual identity reference for all UI |

## 1.4 Priority Definitions

Every requirement in this document carries a priority that maps directly to the release phases in Section 10:

| Priority | Meaning | Release |
| --- | --- | --- |
| **P0** | Must-have. Launch is blocked without it. | Phase 1 — MVP |
| **P1** | High value. Committed for the expansion release. | Phase 2 — GCC & Marketing |
| **P2** | Strategic. Planned, not yet committed to a date. | Phase 3 — Scale & Innovation |

Within each requirements table, IDs are stable and must be referenced in tickets, commits, and test cases (e.g., `SF-PDP-04`).

---

# 2. Product Overview

## 2.1 Vision

Give GCC fashion shoppers a premium, bilingual, mobile-first buying experience that feels native to how the region actually shops — Instagram discovery, KNET/Apple Pay/BNPL payment, WhatsApp service — and give the White Stitches team a Shopify-grade back office with zero per-transaction platform fees and full ownership of data and brand.

## 2.2 Goals

- Launch a sellable, bilingual (AR/EN) store for Kuwait with end-to-end checkout via Tap Payments.
- Achieve storefront conversion of at least 1.5% within 90 days of launch, progressing toward 2.5%.
- Enable the operations team to run catalogue, orders, inventory, and marketing without developer support.
- Expand to all six GCC markets in Phase 2 with per-market currency, tax, payments, and shipping.
- Maintain Core Web Vitals: LCP ≤ 2.5s on 4G mobile, with 99.9% uptime.

## 2.3 Users & Personas

### Storefront personas

- **The Mobile Shopper (primary).** Female, 22–40, Kuwait/KSA/UAE, shops on iPhone, discovers via Instagram, pays with KNET or Apple Pay, expects Arabic UI option, free or cheap delivery, and WhatsApp support. Success: finds, trusts, and buys in under five minutes.
- **The Gift Buyer.** Buys for someone else; needs size guide confidence, gift wrap, clear return policy, and delivery-date certainty around Eid and occasions.
- **The Cross-border GCC Shopper.** Browses from KSA/UAE; needs local currency display, accurate landed delivery times, and BNPL (Tabby/Tamara).

### Admin personas

- **Operations Manager.** Lives in Orders and Inventory daily; fulfils, prints labels, processes returns, watches low-stock alerts.
- **Marketing Manager.** Builds discounts, campaigns (email/WhatsApp), banners, and reads conversion and ROAS reports.
- **Customer Service Agent.** Answers WhatsApp/Instagram/email from a unified inbox; looks up orders; issues limited refunds; creates draft orders for Instagram sales.
- **Content Editor.** Maintains pages, journal posts, navigation, and bilingual copy.
- **Super Admin (owner).** Manages staff, integrations, payment configuration, and policies; needs the audit log.

## 2.4 Success Metrics

| Metric | Target | Measured via |
| --- | --- | --- |
| Storefront conversion rate | ≥ 1.5% → 2.5% | GA4 |
| Mobile share of sessions / revenue | ≥ 70% / ≥ 60% | GA4 |
| Cart abandonment (recovered) | ≤ 70% (≥ 10% recovered) | Admin analytics |
| Payment success rate | ≥ 95% KNET; ≥ 92% cards | Tap reconciliation |
| LCP p75 (4G mobile) | ≤ 2.5s | CrUX / Lighthouse |
| Uptime | ≥ 99.9% monthly | Monitoring |
| WhatsApp first response (business hours) | < 5 minutes | Inbox reports |
| Repeat-customer rate (12 months) | ≥ 25% | Admin customer reports |
| Return rate (apparel) | Investigate above 15% | Returns reporting |

## 2.5 Assumptions & Constraints

- Tap Payments is the sole payment gateway; card data never touches White Stitches servers (hosted fields), keeping the platform out of PCI DSS scope.
- Phase 1 launches Kuwait-only, KWD-only, single inventory location.
- Delivery partner is not yet selected; the delivery integration must sit behind a provider-agnostic interface so the carrier can be swapped without rewriting business logic. Partner selection is a Phase 1 blocker (see Section 12).
- The current storefront is static HTML/CSS/JS on IIS; Phase 1 backend services attach to it via APIs without requiring a framework rewrite. The recommended target architecture (Section 8 of the F&R document) remains the long-term direction.
- WhatsApp messaging uses the official WhatsApp Business Platform with pre-approved templates and strict opt-in.
- Kuwait currently applies 0% VAT; the platform must be VAT-ready for the 5–15% GCC framework.

## 2.6 Out of Scope (this PRD)

- Native mobile apps (Phase 3 product, separate PRD).
- B2B/wholesale portal (Phase 3, separate PRD).
- Physical point-of-sale.
- Marketplace sales channels beyond Instagram Shopping catalogue sync (Phase 3).

---

# 3. Scope at a Glance

The platform consists of two surfaces on one backend:

| Surface | Audience | Phase 1 scope | Later phases |
| --- | --- | --- | --- |
| **Storefront** | Customers | 30 pages: home, collections, search, PDP, cart, checkout, confirmation, full account suite, static/policy pages, tracking, 404, maintenance, journal (light) | Reviews, loyalty, gift cards, compare, OTP login, per-market storefronts |
| **Admin Panel** | Staff | Dashboard, products, variants, single-location inventory, collections, orders, customers, basic discounts, basic reports, notifications, settings, users & roles | Marketing automations, unified inbox, multi-location, segments, advanced analytics, theme builder |
| **Integrations** | System | Tap Payments, single Kuwait carrier, SMTP email, WhatsApp transactional, GA4 + Consent Mode v2 | mada/BenefitPay/STC Pay/BNPL, Instagram DM, Meta & TikTok pixels, reviews platform |

---

# 4. Storefront Requirements (Website)

Requirements are grouped by page or global component, following the Storefront Pages Inventory. Every page must additionally satisfy the cross-cutting requirements of Sections 7 (Localization) and 8 (Non-Functional): bilingual AR/EN with full RTL mirroring, mobile-first from 320px, WCAG 2.1 AA, unique SEO metadata with hreflang pairs, and GA4 events behind Consent Mode v2. These cross-cutting rules are not repeated per page.

## 4.1 Global Shell Components

### User stories

- As a shopper, I can reach search, my account, my wishlist, and my cart from any page in one tap.
- As an Arabic speaker, I can switch the entire site to Arabic and have my choice remembered.
- As a shopper who just added an item, I can review my cart in a drawer without losing my place.

### Requirements

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-SHL-01 | Header on all pages (except checkout, which uses a minimal logo + secure-checkout header): logo, primary nav with mega-menu (categories, featured tile), utility nav (search, AR↔EN toggle, account, wishlist with count, cart with count), dismissible announcement bar, sticky-on-scroll with reduced height. | P0 |
| SF-SHL-02 | Mobile navigation: hamburger opens a full-height drawer with collapsible categories, search, and language/currency switchers. | P0 |
| SF-SHL-03 | Footer: brand block with social links, Shop/Help/About columns, country & language selectors, payment partner badges (KNET, Visa, Mastercard, Apple Pay, Tabby, Tamara), copyright + CR number + legal links. | P0 |
| SF-SHL-04 | Mini-cart drawer triggered from the cart icon: free-shipping progress bar, line items with quantity stepper and remove (with undo toast), subtotal, View Cart / Checkout / express checkout CTAs. States: empty, 1–3 items, scrolling list, loading-after-add. | P0 |
| SF-SHL-05 | Search overlay triggered from the header: input with focus, recent/popular searches when idle, live results (products with thumbnail + price, collections, pages), view-all-results link, no-results state. | P0 |
| SF-SHL-06 | Cookie consent banner on first visit with Accept All / Reject All / Customize; Customize modal exposes granular toggles (Necessary, Functional, Analytics, Marketing). Consent state gates GA4 and marketing tags (Consent Mode v2). | P0 |
| SF-SHL-07 | Persistent WhatsApp click-to-chat button on storefront pages. | P0 |
| SF-SHL-08 | Currency switcher displaying converted SAR/AED/BHD/QAR/OMR prices with geo-IP default and manual override. | P1 |
| SF-SHL-09 | Announcement bar supports scheduling and optional countdown timer managed from Admin. | P1 |

### Acceptance criteria (highlights)

- Language toggle re-renders the current page in the other language with mirrored RTL layout, preserved scroll context, and persisted preference (cookie for guests, profile for logged-in).
- Cart badge count updates within 300ms of any add/remove action anywhere on the site.
- No analytics or marketing tag fires before the corresponding consent category is granted.

## 4.2 Home Page (/)

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-HOM-01 | Hero block with editorial image/video, brand statement, and CTA. Video hero autoplays muted, loops, uses playsinline, and pauses when off-screen (IntersectionObserver) to preserve battery. | P0 |
| SF-HOM-02 | Cinematic entry flow: intro page (logo animation + wordmark reveal) is the site default document and redirects to the home page; it must be skippable and shown at most once per session. | P0 |
| SF-HOM-03 | Category tiles for Jackets, Dresses, Suits, Tops (horizontal scroll on mobile). | P0 |
| SF-HOM-04 | Featured products grid (latest arrivals) using the shared Product Card. | P0 |
| SF-HOM-05 | Newsletter signup band (email capture with success/error states; WhatsApp opt-in checkbox). | P0 |
| SF-HOM-06 | Hero slider of up to 5 slides, each with desktop/mobile image variants, heading, CTA, link, and start/end schedule, managed from Admin. | P1 |
| SF-HOM-07 | All home sections individually toggleable and reorderable from Admin. | P1 |
| SF-HOM-08 | Personalized carousels for logged-in users: Recently Viewed, Recommended for You. | P1 |
| SF-HOM-09 | Instagram feed integration block. | P1 |

## 4.3 Collection / Category & Search Results

Routes: `/women`, `/men`, `/accessories`, `/collections/{handle}`, `/search?q={query}`. The Search Results page reuses the collection grid, filters, and sort.

### User stories

- As a shopper, I can narrow a category by size, color, and price and share the filtered URL with a friend.
- As a shopper, I can add a product to my cart directly from the grid by picking a size, without opening the PDP.

### Requirements

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-COL-01 | Collection banner (image, bilingual title and description) and breadcrumb trail. | P0 |
| SF-COL-02 | Filtering: size, color, price range, fabric, occasion, fit, availability (in-stock only). Sidebar on desktop, bottom sheet on mobile. Filter and sort state preserved in URL parameters for shareable links and back-button stability. | P0 |
| SF-COL-03 | Sorting: featured, newest, best-selling, price low→high / high→low, alphabetical. | P0 |
| SF-COL-04 | Product grid with badges (New, Sale, Sold Out, Limited Stock, Best Seller), color/size swatches on tiles (out-of-stock struck through), hover image swap, wishlist heart, and pagination or infinite scroll (configurable). Loading skeletons and end-of-results state. | P0 |
| SF-COL-05 | Quick add: choose a size on the listing card and add to cart; quick view modal with product preview. | P0 |
| SF-COL-06 | Search results page: header with query and result count, products first then matching collections and pages, "did you mean" suggestions, zero-results state with suggested categories and popular products. | P0 |
| SF-COL-07 | Bilingual search: Arabic stemming and diacritic normalization, English typo tolerance and synonyms; cross-language mapping (Arabic query matches English-tagged products). | P0 |
| SF-COL-08 | Smart collections auto-populated by rules (tag, price, type, vendor) alongside manual collections (authored in Admin, rendered here). | P1 |
| SF-COL-09 | Search analytics events captured for Admin reporting (top searches, zero-result searches). | P1 |

## 4.4 Product Detail Page (/products/{handle})

The conversion-critical page. It must sell a single product through imagery, variant clarity, and trust signals.

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-PDP-01 | Gallery with minimum 6 high-resolution images, thumbnail strip, zoom on hover (desktop) and pinch-zoom/swipe (mobile). Selecting a color variant switches the gallery to that color's images. | P0 |
| SF-PDP-02 | Product info column: bilingual title, price with compare-at strike-through for sales, color swatches (named on hover), size selector with per-variant availability (out-of-stock sizes disabled), quantity stepper, Add to Cart, Buy Now (express checkout), Add to Wishlist. | P0 |
| SF-PDP-03 | Stock indicator (In Stock / Only X left / Out of Stock) and estimated delivery date based on customer location and handling time. | P0 |
| SF-PDP-04 | Size guide modal: bilingual body-measurement chart per category with how-to-measure guidance and cm/inch toggle. | P0 |
| SF-PDP-05 | Tabs/accordion: Description (rich text), Material & Care (composition, origin, care symbols, model height/wearing size), Size & Fit, Shipping & Returns. | P0 |
| SF-PDP-06 | Share via WhatsApp, Instagram, and copy-link. | P0 |
| SF-PDP-07 | Cross-sell carousels: Complete the Look, You May Also Like, Recently Viewed (persistent across sessions). | P0 |
| SF-PDP-08 | All variant states handled: in stock, low stock, out of stock, sale price, single color/multiple sizes and the inverse, variant-level out of stock. | P0 |
| SF-PDP-09 | Back-in-stock notification: customer leaves email/WhatsApp on an out-of-stock variant and is automatically notified on restock. | P1 |
| SF-PDP-10 | Customer reviews with photos, verified-purchase badge, sorting and filtering; star rating with count near the title. Q&A section. | P1 |
| SF-PDP-11 | Product video and 360° spin support in the gallery. | P1 |
| SF-PDP-12 | Schema.org Product/Offer structured data emitted for rich results. | P0 |

## 4.5 Cart (/cart)

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-CRT-01 | Line items with image, details, editable quantity, unit price, line total, remove, and move-to-wishlist. Out-of-stock line items flagged with a warning. | P0 |
| SF-CRT-02 | Discount code field with inline validation feedback (applied / invalid / expired). | P0 |
| SF-CRT-03 | Summary panel: subtotal, estimated shipping, estimated tax, total; free-shipping threshold progress bar ("You're 5 KWD away from free shipping"). | P0 |
| SF-CRT-04 | Order notes field and gift wrap option (optional fee). | P0 |
| SF-CRT-05 | CTAs: Continue Shopping, Checkout, and express checkout (Apple Pay / Google Pay). Trust badges below the summary. Designed empty state with category CTAs. | P0 |
| SF-CRT-06 | Cart persistence: logged-in carts persist across devices; guest carts persist via cookie. | P0 |
| SF-CRT-07 | Recommended add-ons / frequently-bought-together row. | P1 |
| SF-CRT-08 | Saved-for-later list for logged-in customers. | P1 |

## 4.6 Checkout & Order Confirmation

Routes: `/checkout` and `/checkout/orders/{order_number}`. Checkout uses a minimal shell — logo and secure-checkout indicator only, no menu or footer — to remove every distraction between intent and payment.

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-CHK-01 | Guest checkout by default with one-click account creation offered after purchase. Logged-in customers get pre-filled contact and saved addresses. | P0 |
| SF-CHK-02 | Steps (single-page or stepper, configurable): Contact (email + phone defaulting to +965) → Shipping address (country, governorate/emirate, area/block, street, building, floor, apartment, directions; AR and EN entry supported) → Shipping method with prices and ETA → Payment. | P0 |
| SF-CHK-03 | Payment rendered by Tap Payments hosted fields: KNET, Visa, Mastercard, Apple Pay at launch; mada, BenefitPay, STC Pay, Tabby/Tamara/deema, and per-zone Cash on Delivery in Phase 2. 3-D Secure for cards. Decline messages mapped to friendly AR/EN copy. | P0 |
| SF-CHK-04 | Express checkout buttons (Apple Pay, Google Pay) at the top of checkout, in cart, and in the mini-cart drawer. | P0 |
| SF-CHK-05 | Sticky order summary (desktop) / collapsible summary (mobile) with line items, discount/gift-card field, shipping, tax, and total. | P0 |
| SF-CHK-06 | Terms acceptance checkbox (lawful electronic contract) and marketing opt-in checkboxes (email/WhatsApp) — unchecked by default. | P0 |
| SF-CHK-07 | States handled: validation errors per field, payment processing, payment failed (retry without data loss), payment success redirect. Checkout never blocks on a non-payment integration failure. | P0 |
| SF-CHK-08 | Order confirmation page: success message + order number, order summary, shipping address + ETA, payment summary, tracking note, create-account prompt for guests, cross-sell row, WhatsApp support link. Handles pending-payment state for BNPL flows. | P0 |
| SF-CHK-09 | GA4 `begin_checkout`, `add_shipping_info`, `add_payment_info`, and server-confirmed `purchase` events with correct currency and value. | P0 |

## 4.7 Customer Account Suite (/account/*)

### User stories

- As a returning customer, I can see where my order is without contacting support.
- As a customer, I can start a return from my order in under a minute.
- As a privacy-conscious customer, I can download my data or delete my account myself.

### Requirements

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-ACC-01 | Login / sign-up: email + password with validation, forgot-password flow (request → email sent → reset form), sign-up with name, email, phone, password, and per-channel marketing consents. | P0 |
| SF-ACC-02 | Dashboard: greeting + member-since, last 3 orders with status, addresses preview, wishlist preview, quick links, logout. Sidebar navigation across all account pages. | P0 |
| SF-ACC-03 | Order history with status/date filters and pagination; order rows show number, date, total, status badge, item thumbnails, View and Re-order actions. | P0 |
| SF-ACC-04 | Order detail: visual status stepper (Placed → Paid → Fulfilled → Shipped → Delivered), tracking (AWB + carrier link), line items, addresses, payment summary with invoice download, refund history, Re-order / Request Return / WhatsApp support CTAs. Cancelled and refunded states rendered. | P0 |
| SF-ACC-05 | Addresses: saved address cards with edit, delete (with confirmation), and set-as-default; add-address form supporting Kuwait address structure (governorate, area, block, street, building, floor, apartment). | P0 |
| SF-ACC-06 | Wishlist: shared product card grid, move-to-cart per item, shareable link (read-only public view), guest wishlist via cookie merged into the account at login. | P0 |
| SF-ACC-07 | Profile & preferences: personal info, contact change with verification, password change, per-channel × per-category communication preferences (Email/SMS/WhatsApp × Transactional/Marketing), language and currency defaults. | P0 |
| SF-ACC-08 | Privacy self-service: download-my-data export and delete-my-account request with confirmation flow. | P0 |
| SF-ACC-09 | Returns: wizard (select order → items → reason → method) producing a return request; return list and detail with status (Pending, Approved + label, Received, Refunded, Rejected). Phase 1 approval is manual in Admin. | P0 |
| SF-ACC-10 | Social login (Google, Apple, Facebook) and OTP sign-in via SMS or WhatsApp. | P1 |
| SF-ACC-11 | Saved payment methods, tokenized at Tap with explicit consent (never stored locally); loyalty balance and history. | P1 |

## 4.8 Static, Policy & Support Pages

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-STA-01 | About / Our Story: hero, alternating editorial story blocks, values, founder quote. | P0 |
| SF-STA-02 | Contact: channel cards (WhatsApp, email, phone, hours) and a contact form with success/error states. | P0 |
| SF-STA-03 | Size Guide: category tabs, measurement tables with cm/inch toggle, how-to-measure illustrations, fit notes. | P0 |
| SF-STA-04 | FAQ: category tabs (Orders, Shipping, Returns, Payments, Account, Products), accordion Q&A, "still need help" card linking to Contact and WhatsApp. | P0 |
| SF-STA-05 | Policy pages (Shipping & Delivery, Returns & Exchanges, Privacy, Terms, Cookies): bilingual long-form content with anchored tables of contents and last-updated dates; the Cookie Policy page re-opens the consent preferences modal; the Returns policy links into the start-a-return flow. Auto-linked from checkout, footer, and account. | P0 |
| SF-STA-06 | Public order tracking (/track): lookup by order number + email or phone, rendering the same status stepper as Order Detail; not-found state. | P0 |
| SF-STA-07 | 404 page with suggested links and search; admin-toggleable Maintenance/holding page with launch-notification email capture. | P0 |

## 4.9 Journal (Editorial Content)

| ID | Requirement | Priority |
| --- | --- | --- |
| SF-JRN-01 | Journal index (/journal): featured article hero, category/tag filters, article grid (image, tag, title, date, author), pagination, empty state. | P0 |
| SF-JRN-02 | Post detail (/journal/{slug}): hero with title overlay, metadata (category, date, author, reading time), long-form body with images and pull quotes, share buttons (WhatsApp, Instagram, Pinterest, copy link), related articles. | P0 |
| SF-JRN-03 | Shoppable product embeds inside articles (product card inline with add-to-cart), author bio block, separate AR/EN URLs per post. | P1 |

---

# 5. Admin Panel Requirements

The Admin Panel gives the White Stitches team Shopify-Admin-equivalent control, organized into the same modules so staff onboard quickly. It is a secure, staff-only web application consuming the same backend APIs as the storefront. Every administrative write action is captured in an immutable audit log (user, action, target, before/after values, IP, timestamp). The Admin UI must also support full Arabic RTL operation.

## 5.1 Dashboard

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-DSH-01 | Today's snapshot: orders, revenue, sessions, conversion rate, average order value — each with comparison against yesterday, last week, last month, and last year. | P0 |
| AD-DSH-02 | Sales chart with selectable date range and granularity; top products, top collections, top traffic sources; recent orders feed. | P0 |
| AD-DSH-03 | Action queues: orders to fulfil, returns to process, low-stock and out-of-stock alerts, abandoned carts to recover, reviews to moderate (Phase 2). | P0 |
| AD-DSH-04 | Customizable widget layout per staff member. | P1 |

## 5.2 Orders

### User stories

- As an Operations Manager, I can find every unfulfilled paid order in one click and bulk-print packing slips.
- As a Customer Service agent, I can take an Instagram order, build a draft order, and send the customer a payment link on WhatsApp.
- As an owner, I can refund a customer to their original payment method without leaving the order page.

### Requirements

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-ORD-01 | Order list: filters for order status, payment status (pending/paid/refunded/partially refunded), fulfilment status, date range, channel, customer, tag; search by order number, customer, email, phone, or product; saved views; CSV/Excel export. | P0 |
| AD-ORD-02 | Bulk actions: print invoices and packing slips, mark fulfilled, capture payment, archive, tag, export. | P0 |
| AD-ORD-03 | Order detail: customer and address blocks, line items with variant/pricing breakdown, payment info (method, transaction ID, gateway response, captured/authorized, refund history), full event timeline with author and timestamp, internal notes. | P0 |
| AD-ORD-04 | Fulfilment from the order: enter or generate tracking (AWB) via the delivery integration, mark shipped, partial fulfilments, assign location (Phase 2 multi-location). | P0 |
| AD-ORD-05 | Refunds (full and partial) to the original payment method via Tap; cancel order with reason, optional restock and refund. | P0 |
| AD-ORD-06 | Order editing: add/remove items, change quantities, apply manual discount, with the refund/charge difference handled correctly. | P1 |
| AD-ORD-07 | Message the customer (email or WhatsApp template) directly from the order; messages logged to the timeline. | P0 |
| AD-ORD-08 | Draft orders for phone/WhatsApp/Instagram sales: custom items, manual discounts, custom shipping, and a payment link sent via email/WhatsApp/SMS for self-checkout. | P0 |
| AD-ORD-09 | Risk flags on suspicious orders: unusual amount, mismatched billing/shipping country, repeated failed payments. | P1 |
| AD-ORD-10 | Returns management: review customer requests (approve / reject / request info), generate return instructions and label, receive and inspect, restock or write off, link exchanges and refunds to the original order, return-reason reporting. Phase 1 flow is manual; Phase 2 adds self-service automation. | P0 |
| AD-ORD-11 | Abandoned checkouts: list with customer, contents, and timestamps; automated recovery email at 1h and 24h, optional consented WhatsApp nudge; recovery-rate analytics. | P1 |

## 5.3 Products, Inventory & Collections

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-PRD-01 | Product CRUD with bilingual fields (title, rich-text description, SEO meta); multiple images with drag-to-reorder and bilingual alt text; status (active/draft/archived); scheduled publish; duplicate action; organization by type, vendor, tags, collections. | P0 |
| AD-PRD-02 | Variants: option combinations (size, color, fit, length) up to 100+ per product, each with its own SKU, barcode, price, compare-at price, cost, weight, image, and inventory; spreadsheet-style bulk variant editor. | P0 |
| AD-PRD-03 | Pricing: price, compare-at, cost per item for margin tracking; per-currency market overrides (Phase 2). | P0 |
| AD-PRD-04 | Inventory: tracked quantity per variant, oversell toggle, low-stock threshold, adjustments with reasons (damage, theft, correction, received), per-variant stock history, low/out-of-stock reports, bulk stock update via CSV. | P0 |
| AD-PRD-05 | Shipping & customs fields per product: weight, dimensions, requires-shipping toggle, HS code, country of origin (required for GCC cross-border). | P0 |
| AD-PRD-06 | Bulk product import/export via CSV with template. | P0 |
| AD-PRD-07 | Collections: manual (curated, drag-and-drop sort) and smart (rules on tag, price, type, vendor, inventory with AND/OR); bilingual image, banner, description, SEO; sort orders (manual, best-selling, alphabetical, price, newest). | P0 |
| AD-PRD-08 | Hierarchical category taxonomy (e.g., Women > Dresses > Maxi) with per-category attributes, feeding the mega-menu and breadcrumbs. | P1 |
| AD-PRD-09 | Multi-location inventory: locations, per-location stock, transfers between locations, barcode printing. | P1 |
| AD-PRD-10 | Gift cards: denominations or custom amounts, issued/redeemed/expired tracking, manual issue, disable/refund individual cards. | P1 |
| AD-PRD-11 | Video and 360° asset support on products; per-channel visibility (online store, Instagram Shopping). | P1 |

## 5.4 Customers

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-CUS-01 | Customer list with search and filters (location, total spent, order count, last order date, tags, marketing consent) and bulk tag/export. | P0 |
| AD-CUS-02 | Customer profile: contact info, addresses, lifetime value, order history, abandoned carts, account events, notes, tags, and per-channel marketing consent status. | P0 |
| AD-CUS-03 | Privacy compliance tooling: process data-export and deletion requests from a single screen with audit trail. | P0 |
| AD-CUS-04 | Saved segments built by rules (e.g., "high-value Kuwait customers with no order in 60 days") usable as campaign audiences. | P1 |
| AD-CUS-05 | Customer groups (VIP / wholesale) with custom pricing. | P2 |

## 5.5 Marketing

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-MKT-01 | Discount codes: percentage off, fixed amount, free shipping, buy X get Y; eligibility by customer/segment/product/collection; usage limits (total, per customer, minimum purchase/quantity); stackable-or-exclusive combination rules; start/end schedule; usage reporting. | P0 |
| AD-MKT-02 | Automatic discounts triggered by cart conditions (e.g., spend 30 KWD, get 10% off). | P1 |
| AD-MKT-03 | Campaign builders: email (template-based — newsletter, promotion, lookbook, win-back) and WhatsApp (approved templates with merge tags); audience from segments; send now or schedule; performance reporting (opens, clicks, conversions, attributed revenue); A/B testing. | P1 |
| AD-MKT-04 | Automations with a visual flow builder (trigger → delay → condition → action) across email/WhatsApp/SMS and internal staff alerts: welcome series, abandoned cart, post-purchase, win-back, birthday, restock alert. | P1 |
| AD-MKT-05 | Promotions tooling: schedulable announcement bars and home banners; pop-ups (newsletter, exit-intent, time-on-page, scroll-depth) with frequency capping; countdown timers tied to promotions. | P1 |

## 5.6 Online Store — Content & Theme

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-CNT-01 | Static page editor with bilingual content, SEO fields, and content sections (text, image, video, gallery, FAQ accordion, contact form). | P0 |
| AD-CNT-02 | Journal management: posts with categories, tags, author, featured image, scheduled publish, bilingual content with separate AR/EN URLs; comment moderation or disable. | P0 |
| AD-CNT-03 | Navigation manager: main, footer, and mobile menus, bilingual; mega-menu builder with featured images and product links. | P0 |
| AD-CNT-04 | Storefront preferences: site SEO defaults, GA4 measurement ID / GTM container, Meta/TikTok/Snap pixels, password protection for pre-launch, 301 redirect manager, auto-generated robots.txt and sitemap.xml with override. | P0 |
| AD-CNT-05 | Theme settings: colors, typography, buttons, logo, favicon, social links; section-based page builder with drag-drop-configure; theme preview and scheduled publish; code editor restricted to Super Admin. | P1 |
| AD-CNT-06 | Media library: centralized images/videos/PDFs with used-by tracking, CDN delivery, automatic WebP/AVIF optimization, and responsive sizes. | P1 |

## 5.7 Analytics & Reports

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-ANL-01 | Basic reports at launch: sales by date/product/collection/payment method, conversion funnel (sessions → product views → add to cart → checkout → purchase), top searches and zero-result searches, export to CSV/Excel. | P0 |
| AD-ANL-02 | Real-time dashboard: visitors online, current sales, live conversion. | P1 |
| AD-ANL-03 | Advanced reports: customer cohorts and retention, LTV, inventory ABC analysis and sell-through, marketing ROAS and attribution, behavior reports (top/exit pages, session duration); scheduled emailed reports; PDF export. | P1 |

## 5.8 Settings & Administration

| ID | Requirement | Priority |
| --- | --- | --- |
| AD-SET-01 | Store details: business info, time zone, default language/currency, brand assets (logos, colors, fonts, voice notes). | P0 |
| AD-SET-02 | Users & permissions: staff invitations with predefined roles (Section 9), custom roles with per-module per-action permissions, mandatory two-factor authentication (TOTP), session management with force logout, and full activity log. | P0 |
| AD-SET-03 | Payments: Tap Payments configuration (API keys, webhook secret, sandbox toggle), enable/disable methods per market, Cash on Delivery per zone with cap and surcharge, manual payment methods with instructions. | P0 |
| AD-SET-04 | Shipping & delivery: zones (Kuwait governorates; GCC markets in Phase 2), rates per zone (flat, weight-based, price-based, free-above-threshold), methods (standard, express, same-day, scheduled, pickup), Kuwait local-delivery time slots, packaging presets, carrier integration controls. | P0 |
| AD-SET-05 | Taxes & duties: per-market tax rules (e.g., 15% KSA, 5% UAE), tax-inclusive vs exclusive pricing per market, per-product-type exemptions, tax reports. Kuwait launches at 0% but VAT-ready. | P0 |
| AD-SET-06 | Notifications: bilingual per-channel templates (email HTML+text, WhatsApp, SMS) for the full lifecycle — order confirmation, payment received, fulfilled, shipped with tracking, delivered, refund, abandoned cart, account created, password reset, back-in-stock, return approved/received — with merge tags, language selected by customer preference, and test-send. Internal staff alerts: new order, new return, low stock, failed payment, fraud alert. | P0 |
| AD-SET-07 | Integrations hub: connect/disconnect Tap, delivery partner, WhatsApp, SMTP, Instagram, GA4, pixels; API keys stored encrypted with rotation; webhook log with replay. | P0 |
| AD-SET-08 | Markets & languages: per-market configuration (language, currency, tax, payment methods, shipping zones), per-market sub-path or domain (/sa, /ae), per-market product visibility and pricing. | P1 |
| AD-SET-09 | Policies editor: bilingual refund, privacy, terms, shipping, and contact policies, auto-linked to checkout, footer, and account. | P0 |
| AD-SET-10 | Customer account controls: accounts required/optional/disabled, social and OTP login toggles, B2B approval workflow (Phase 3). | P1 |
| AD-SET-11 | Locations management (warehouse, retail, supplier) with per-location inventory and fulfilment. | P1 |

---

# 6. Integration Requirements

Every integration must have an Admin configuration screen, automated retry and error handling, audit logging, and graceful degradation: when a third-party service is down, events queue for retry and checkout is never blocked by a non-payment integration.

## 6.1 Tap Payments (P0)

| ID | Requirement | Priority |
| --- | --- | --- |
| INT-PAY-01 | Hosted checkout (redirect or popup) so card data never touches White Stitches servers; merchant stays out of PCI DSS scope. | P0 |
| INT-PAY-02 | Launch methods: KNET (with KFAST save-card), Visa, Mastercard, Apple Pay, Google Pay, with 3-D Secure 2.0 on cards. Phase 2: American Express, mada, BenefitPay, STC Pay, Tabby, Tamara, deema. | P0 |
| INT-PAY-03 | Server-to-server charge creation, capture, void, and full/partial refund; refunds triggered from the Admin order detail return to the original method. | P0 |
| INT-PAY-04 | Webhook handling with signature verification and idempotency keys; payment status drives order status. | P0 |
| INT-PAY-05 | Sandbox mode toggle with clearly labelled test transactions; reconciliation matching against Tap reports; decline reasons mapped to user-friendly AR/EN messages. | P0 |
| INT-PAY-06 | Tokenized save-card for returning customers with explicit consent; multi-currency settlement where Tap supports it. | P1 |

## 6.2 Delivery Partner (P0 — partner TBD)

The carrier (candidates: Aramex, DHL, SMSA, J&T, Mashkor, Posta Plus, Zajil) must sit behind a provider-agnostic delivery interface so it can be replaced without changing business logic.

| ID | Requirement | Priority |
| --- | --- | --- |
| INT-DLV-01 | Generate shipping label and AWB from the Admin order detail; schedule pickup; bulk label printing for daily fulfilment. | P0 |
| INT-DLV-02 | Tracking webhooks push status updates to the order timeline and power the customer tracking page and notifications. | P0 |
| INT-DLV-03 | Rate quoting at checkout in real time, with fallback to admin-configured rates; address validation against carrier coverage. | P1 |
| INT-DLV-04 | Returns label generation and COD reconciliation reporting (carrier collects, remits). | P1 |

## 6.3 WhatsApp Business API (P0 transactional)

| ID | Requirement | Priority |
| --- | --- | --- |
| INT-WAP-01 | Verified WhatsApp Business Account via the official Cloud API (directly or through a BSP); pre-approved AR/EN templates for at minimum order confirmation, shipped, and delivered at launch, expanding to payment received, abandoned cart, back-in-stock, OTP, review request, and return status. | P0 |
| INT-WAP-02 | Template language follows customer preference; strict opt-in management (explicit checkbox at checkout/account, one-click unsubscribe); conversation pricing tracked by category. | P0 |
| INT-WAP-03 | Click-to-WhatsApp on storefront and PDP for live support. | P0 |
| INT-WAP-04 | Two-way conversations routed to a unified Admin inbox; bulk campaigns to opted-in customers; optional WhatsApp Catalog. | P1 |

## 6.4 SMTP / Email (P0)

| ID | Requirement | Priority |
| --- | --- | --- |
| INT-EML-01 | Configurable SMTP/Email API provider (Amazon SES, SendGrid, Mailgun, or Postmark) with DKIM/SPF/DMARC alignment on the whitestitches domain. | P0 |
| INT-EML-02 | Bilingual, RTL-safe HTML transactional templates per AD-SET-06; UTF-8 Arabic rendering verified across major clients. | P0 |
| INT-EML-03 | Bounce, complaint, and unsubscribe handling fed back to the customer record; send logs in Admin; queueing and throttling to protect deliverability (target ≥ 98%). | P0 |
| INT-EML-04 | Drag-and-drop marketing template builder. | P1 |

## 6.5 Instagram Direct (P1)

| ID | Requirement | Priority |
| --- | --- | --- |
| INT-IGM-01 | Instagram Business account connected via Meta OAuth; inbound DMs (including story and shop-tag replies) routed to the unified inbox; outbound replies by staff with 24-hour care-window enforcement and message tags after 24h. | P1 |
| INT-IGM-02 | Quick replies for sizing/shipping/payment/returns; order-aware replies by searching customer by handle; auto-acknowledgment outside business hours with WhatsApp escalation. | P1 |
| INT-IGM-03 | Instagram Shopping product tagging via Meta Catalog sync. | P1 |

## 6.6 Google Analytics 4 (P0)

| ID | Requirement | Priority |
| --- | --- | --- |
| INT-GA4-01 | GA4 measurement ID configurable from Admin; full enhanced-ecommerce event set (view_item, view_item_list, select_item, add_to_cart, remove_from_cart, view_cart, begin_checkout, add_payment_info, add_shipping_info, purchase, refund, login, sign_up, search, view_promotion, select_promotion, add_to_wishlist) with correct currency and value. | P0 |
| INT-GA4-02 | Consent Mode v2: no analytics or marketing tags before consent; Google Tag Manager support for marketer-managed tags. | P0 |
| INT-GA4-03 | Server-side Measurement Protocol events for purchase and refund to bypass ad blockers; UTM attribution preserved through checkout. | P1 |
| INT-GA4-04 | Meta Pixel + Conversions API, TikTok Pixel + Events API, Snap Pixel, Google Search Console. | P1 |

---

# 7. Localization & Regional Requirements

| ID | Requirement | Priority |
| --- | --- | --- |
| LOC-01 | Full bilingual AR/EN across storefront, admin, and notifications. Selecting Arabic mirrors the entire interface RTL (navigation, cards, forms, cart, checkout, emails). Every product, collection, page, post, menu, policy, and template has AR and EN fields. No hard-coded strings. | P0 |
| LOC-02 | Persistent language switcher; choice remembered across sessions (cookie) and devices (profile). | P0 |
| LOC-03 | KWD primary currency displayed with three decimals (25.500 KWD); per-currency rounding rules (KWD to .005/.010 increments). | P0 |
| LOC-04 | Numerals: Western digits default for prices; Arabic-Indic numerals supported for content. | P0 |
| LOC-05 | Web-optimized Arabic typography (IBM Plex Sans Arabic / Tajawal / Cairo class) paired with the brand Latin face; both loaded with `display: swap`. | P0 |
| LOC-06 | VAT-ready tax engine for the GCC framework; multi-currency display (SAR, AED, BHD, QAR, OMR) with geo-IP default, manual override, and admin-managed exchange rates (live API or fixed). | P1 |
| LOC-07 | Optional Hijri date display alongside Gregorian for Ramadan/Eid/Hala February promotions. | P1 |
| LOC-08 | GCC customs data on cross-border orders: HS codes, country of origin, accurate descriptions; Certificate of Origin flagging for Kuwait-bound shipments above USD 1,500 CIF. | P1 |
| LOC-09 | Regulatory compliance baked in: Kuwait Consumer Protection Law (clear descriptions, accurate pricing, transparent returns, complaints channel), Electronic Transactions Law (terms acceptance at checkout, secure data handling), CBK-licensed payments only, GDPR-style privacy rights. | P0 |

---

# 8. Non-Functional Requirements

## 8.1 Performance

| ID | Requirement | Priority |
| --- | --- | --- |
| NFR-PRF-01 | Core Web Vitals on 4G mobile: LCP ≤ 2.5s, INP/FID ≤ 100ms, CLS ≤ 0.1; TTI ≤ 3.5s on mid-range mobile. | P0 |
| NFR-PRF-02 | Images in WebP/AVIF with responsive sizes and lazy loading; CDN for static assets and product images; CSS/JS minified with cache-busting query strings. | P0 |
| NFR-PRF-03 | API p95 response ≤ 300ms; storefront uptime ≥ 99.9%. | P0 |

## 8.2 Security

| ID | Requirement | Priority |
| --- | --- | --- |
| NFR-SEC-01 | HTTPS-only, TLS 1.2+, HSTS; OWASP Top 10 mitigations (SQLi, XSS, CSRF, SSRF, IDOR, broken access control). | P0 |
| NFR-SEC-02 | Rate limiting and bot protection on login, signup, checkout, and search; bcrypt/argon2 password hashing with strength rules and breach check; 2FA for all staff. | P0 |
| NFR-SEC-03 | Encryption at rest for API keys, webhook secrets, and customer PII; immutable admin audit log; daily backups with 30-day retention and tested restore; dependency scanning; penetration test before launch and annually. | P0 |

## 8.3 Reliability & Scalability

| ID | Requirement | Priority |
| --- | --- | --- |
| NFR-REL-01 | Graceful degradation: non-payment integration failures queue for retry and never block checkout; idempotency keys on payment and webhook handlers; health checks with automated failover. | P0 |
| NFR-REL-02 | Disaster recovery: RPO 1 hour, RTO 4 hours; stakeholder status page. | P0 |
| NFR-REL-03 | Designed for 10× seasonal traffic spikes (Hala February, Ramadan, Eid, White Friday): horizontally scalable app tier, background job queue for email/WhatsApp/webhooks/indexing, multi-layer caching, read replicas for reporting. | P1 |

## 8.4 Accessibility, SEO & Compatibility

| ID | Requirement | Priority |
| --- | --- | --- |
| NFR-ACC-01 | WCAG 2.1 AA: keyboard navigation everywhere, visible focus, labelled fields, alt text, 4.5:1 / 3:1 contrast, skip-to-content, reduced-motion respected, screen-reader-friendly in both languages. | P0 |
| NFR-SEO-01 | Server-rendered HTML for customer pages; Schema.org structured data (Product, Offer, BreadcrumbList, Organization, WebSite, Article, FAQ); canonical + hreflang AR/EN; OG and Twitter cards; auto XML sitemap; clean URLs; 301 redirect manager. | P0 |
| NFR-CMP-01 | Latest two versions of Chrome, Safari, Firefox, Edge, Samsung Internet; iOS Safari 15+, Android Chrome 100+; responsive from 320px to 4K with breakpoints at 768px / 1024px and a 1400px max-width container. | P0 |

---

# 9. User Roles & Permissions

Permissions are granular per module (Orders, Products, Customers, Marketing, Content, Settings, Reports) and per action (View, Create, Edit, Delete, Export, Refund, Manage Users). All roles support custom overrides; every administrative action is audit-logged.

| Role | Default capabilities |
| --- | --- |
| Customer | Self-service: shop, account, wishlist, orders, returns |
| Super Admin | Unrestricted; only role managing other admins, integration secrets, and theme code |
| Admin | Everything except Super Admin management, code editor, and destructive actions |
| Operations Manager | Orders, fulfilment, inventory, customers, returns, reports |
| Marketing Manager | Discounts, campaigns, content, analytics; read-only orders |
| Content Editor | Pages, journal, navigation, theme content; no orders or finances |
| Customer Service | Orders (read, refund up to a limit), customer profiles, conversations, draft orders |
| Inventory Staff | Products and inventory adjustments; no pricing or financial visibility |
| Read-Only Auditor | View access across modules; no edits, no PII export |

---

# 10. Release Plan

## 10.1 Phase 1 — MVP Launch (Kuwait)

Goal: a sellable, branded, bilingual store for Kuwait with payment, fulfilment, and customer communication. **All P0 requirements in this document constitute Phase 1 scope.**

- Storefront: all 30 pages live (built; final cross-page QA polish pass outstanding).
- Admin: dashboard, products & variants, single-location inventory, collections, orders & returns (manual), draft orders, customers, discount codes, basic reports, notifications, settings, users & roles with 2FA.
- Integrations: Tap Payments (KNET, Visa, Mastercard, Apple Pay), one Kuwait carrier, SMTP transactional email, WhatsApp transactional templates, GA4 with Consent Mode v2.
- Localization: Arabic + English, KWD; compliance and accessibility baselines.

## 10.2 Phase 2 — GCC Expansion & Marketing Depth

- Markets: KSA, UAE, Bahrain, Qatar, Oman with per-market currency, language, tax, payments, and shipping.
- Payments: mada, BenefitPay, STC Pay, Tabby, Tamara, deema; Cash on Delivery per zone.
- Marketing automations (welcome, abandoned cart, win-back) across email + WhatsApp; campaigns; pop-ups and promotions tooling.
- Reviews & ratings, loyalty program, gift cards, back-in-stock notifications, OTP/social login.
- Multi-location inventory and transfers; Instagram DM unified inbox; Meta/TikTok pixels with Conversions API; advanced analytics, segments, and self-service returns.

## 10.3 Phase 3 — Scale & Innovation

- B2B/wholesale portal, native mobile apps, AI recommendations and admin assistants, AR try-on, subscriptions, headless partner APIs, and marketplace channels (Instagram Shop, Facebook Shop, Google Shopping, TikTok Shop).

---

# 11. Launch Acceptance Criteria — Definition of Done (Phase 1)

- All P0 requirements pass acceptance tests on staging, in both Arabic and English.
- Lighthouse mobile scores: Performance ≥ 80, SEO ≥ 95, Accessibility ≥ 90, Best Practices ≥ 95.
- Successful end-to-end checkout through every enabled Tap method, in AR and EN, on iOS Safari and Android Chrome; refund flow verified via the Tap dashboard.
- WhatsApp templates approved by Meta and delivered for order confirmation, shipped, and delivered; email deliverability ≥ 98% with DKIM/SPF/DMARC passing and warm-up complete.
- Penetration test with no critical or high findings; backup-and-restore drill completed.
- Admin staff trained and operations runbook documented.
- Cookie consent verified: zero analytics/marketing requests before consent.
- Cross-page QA polish pass completed: focus states, animations, RTL mirroring, `[hidden]`-attribute behavior on flex/grid components, and overlay components verified on all 30 pages.

---

# 12. Risks, Dependencies & Open Questions

| Item | Type | Impact / Mitigation | Owner |
| --- | --- | --- | --- |
| Delivery partner not selected | **Blocker** | Blocks fulfilment integration design and checkout rate quotes. Decide before Phase 1 detailed design; build behind provider-agnostic interface regardless. | Anas |
| Cash on Delivery in Phase 1? | Decision | Recommended yes for Kuwait given local preference; requires COD reconciliation process with carrier. | Anas |
| WhatsApp template approval lead time | Dependency | Meta approval can take days–weeks; submit templates early with final AR/EN copy. | Marketing |
| SMTP provider selection (SES / SendGrid / Mailgun) | Decision | Affects deliverability warm-up timeline; choose 4+ weeks before launch. | Anas |
| WhatsApp BSP vs direct Cloud API | Decision | BSP simplifies onboarding at a per-message margin. | Anas |
| Static-IIS frontend vs target architecture | Risk | Phase 1 attaches APIs to the static front end; defer framework migration until after launch, isolate integration code to ease the move. | Engineering |
| Merchant-of-record legal entity for Tap & WABA | Dependency | Required before production gateway credentials. | Anas |
| Phase 1 catalogue size (SKU count) | Open question | Sizes content-load effort and infrastructure. | Anas |
| Reviews platform: build vs buy | Decision | Phase 2; evaluate Judge.me/Loox/Yotpo vs native. | Anas |
| Hosting region for backend services | Decision | Recommend AWS Bahrain or AWS UAE for GCC latency. | Engineering |

---

# 13. Appendix

## 13.1 Glossary

| Term | Definition |
| --- | --- |
| AWB | Air Waybill — carrier tracking number for a shipment. |
| BNPL | Buy Now Pay Later — Tabby, Tamara, deema installment options. |
| BSP | Business Solution Provider — Meta-approved WhatsApp API partner. |
| CBK | Central Bank of Kuwait — payment service regulator. |
| KNET | Kuwait's domestic debit network; ~85% of Kuwait online transactions. |
| KWD | Kuwaiti Dinar — three-decimal currency. |
| LCP / INP / CLS | Core Web Vitals performance metrics. |
| mada | Saudi Arabia's domestic debit network. |
| PDP | Product Detail Page. |
| PCI DSS | Payment Card Industry Data Security Standard. |
| RTL | Right-to-left text direction (Arabic). |
| SKU | Stock Keeping Unit — unique variant identifier. |
| WABA | WhatsApp Business Account. |

## 13.2 Traceability

Requirement ID prefixes map to source material as follows: `SF-*` trace to the Storefront Pages Inventory (sections 0–9) and F&R Section 4; `AD-*` trace to F&R Section 5; `INT-*` to F&R Section 6; `LOC-*` to F&R Section 3; `NFR-*` to F&R Section 7. Test cases must reference requirement IDs.

---

*End of PRD v1.0 — White Stitches, June 2026.*
