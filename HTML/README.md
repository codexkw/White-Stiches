# White Stitches — Fashion KW

Front-end implementation of the White Stitches storefront homepage and design system, built per the Features &amp; Requirements document.

---

## How to view

1. **Unzip** the folder anywhere on your computer.
2. Double-click **`index.html`** to open the homepage in your browser.
   For the design-system reference page, open **`design-system.html`**.
3. Everything works offline — fonts load from Google Fonts CDN, images from Unsplash CDN.

> **Tip:** For the cleanest experience, drag `index.html` into Chrome / Safari / Firefox.

---

## Deploying to IIS

1. Copy the entire `white-stitches/` folder contents (including the `web.config` file) into your IIS site root.
2. The included **`web.config`** registers all needed MIME types (SVG, WOFF, WebP, etc.) and sets the right charset on CSS/JS so iOS Safari doesn't misrender them.
3. **Hard-refresh on iPhone after deploying:** Settings → Safari → Clear History and Website Data, OR open the site, tap the share button → "Reload Page Without Cache". iOS Safari is aggressive about caching CSS and won't pick up updates otherwise.
4. Asset URLs use cache-busting query strings (`?v=1.1`) so subsequent updates push through automatically — increment the version when you change CSS or JS.

---

## Folder structure

```
white-stitches/
├── index.html              ← Homepage
├── design-system.html      ← Design tokens + component reference
├── README.md               ← This file
│
├── css/
│   ├── design-system.css   ← Tokens (colors, type, spacing, motion)
│   └── site.css            ← Homepage components (header, hero, products…)
│
├── js/
│   └── site.js             ← Mega-menu, scroll reveals, AR/EN toggle
│
└── assets/
    ├── logo-mark.svg       ← The geometric mark, vector
    ├── logo-full.png       ← Full lockup with wordmark (your file)
    └── logo-original.jpg   ← Original brand sheet (your file)
```

---

## What's wired up

### Homepage (`index.html`)
- Sticky header with logo mark, primary nav, language toggle, account/wishlist/cart.
- **Mega-menu** for **Women**, **Men**, **Accessories** — opens on hover (desktop) and click (touch). Three sub-category columns plus a featured tile per menu.
- Hero with editorial veil, geometric pattern overlay, CTAs and meta-stats.
- Italic serif marquee strip.
- Asymmetric category grid (4 tiles).
- 4-up product cards with image-swap on hover, badges, swatches, quick-add.
- Side-by-side editorial feature.
- 4 GCC trust signals (delivery, payment, returns, WhatsApp).
- Instagram feed grid (6 tiles).
- Newsletter band on patterned ink-2 surface.
- Full footer with country/currency/language selectors, payment badges, social icons.
- Floating WhatsApp button (bottom right).

### Design System (`design-system.html`)
- 6 design principles
- 4 logo lockups (dark / inverse / horizontal / mark-only)
- Full color palette with tokens and hex values
- Type scale samples from 11px to 96px
- 12-step spacing scale
- 2 pattern variants (paper / gold)
- 8 component demos (buttons, eyebrow, link-arrow, input, swatches, badges, checkbox)
- 4 motion duration tokens

---

## Behavior notes

- **Mega-menu:** hover `Women` / `Men` / `Accessories` — the panel slides down with a 220ms ease-out. On click, it toggles. Click outside or press `Escape` to close.
- **Language toggle:** click the `AR` button in the header to flip the entire layout to RTL. Click again to flip back.
- **Sticky header:** shrinks subtly on scroll past 80px.
- **Reveal animations:** sections fade and rise into view on scroll using `IntersectionObserver`.
- **Reduced motion:** respects `prefers-reduced-motion` system setting and disables all animations.

---

## Design tokens (excerpt)

```css
--c-ink:    #0a0a0a;   /* primary background */
--c-paper:  #f5f1ea;   /* warm ivory */
--c-accent: #c8a96a;   /* muted dusty gold */

--font-display: 'Cormorant Garamond', serif;
--font-sans:    'Inter Tight', sans-serif;
--font-arabic:  'Tajawal', sans-serif;

--d-base:  220ms;
--e-out:   cubic-bezier(0.22, 0.61, 0.36, 1);
```

Full list in `css/design-system.css`.

---

## Built per the spec

- **Bilingual ready** — all text in `index.html` swaps to RTL via `dir="rtl"` on `<html>`.
- **Black-first** — every section sits on `--c-ink` per the brief.
- **Pattern texture** — the logo geometry tiles into 3 different SVG patterns at low opacity (hero, editorial, newsletter).
- **Compliance-ready footer** — country selector primed for KWD / SAR / AED / BHD / QAR / OMR.
- **Performance** — hero pattern is inline SVG (no extra request); images use Unsplash's `auto=format&fit=crop` for WebP/AVIF and responsive sizing; fonts use `display=swap`.

---

## Next pages to build (recommendation order)

1. **Product Detail Page (PDP)** — gallery, size selector, variant swatches, "complete the look"
2. **Collection / Category page** — filter sidebar, product grid, sort dropdown
3. **Cart + Checkout** — express buttons (KNET, Apple Pay, Tabby), single-page checkout
4. **Customer Account** — orders, addresses, wishlist
5. **Admin panel skin** — back-office Shopify-grade UI

---

© 2026 White Stitches Fashion · CR placeholder
