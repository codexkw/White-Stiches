# White Stitches — Database Specification

**Server:** `83.229.86.221,1433` · **Database:** `White-Stiches` · **ORM:** EF Core 9 (code-first)

Migrations live in `src/WhiteStiches.Infrastructure/Data/Migrations`. Apply with:

```powershell
dotnet ef database update --project src\WhiteStiches.Infrastructure --startup-project src\WhiteStiches.Web
```

## Global conventions

- **Keys:** `int` identity on all domain entities (`BaseEntity`); Identity tables use `Guid`.
- **Timestamps:** `CreatedAtUtc` (set in code), `UpdatedAtUtc` (set automatically on modify in `SaveChangesAsync`).
- **Money:** every `decimal` maps to `decimal(18,3)` — KWD uses three decimals (LOC-03).
- **Bilingual content:** paired `…En` / `…Ar` columns on every customer-visible text field (LOC-01).
- **User references from domain entities** are plain `Guid` columns (no FK navigations into Identity tables) to keep `Core` free of Identity dependencies.

## Modules

### Catalog
| Table | Notes |
| --- | --- |
| `Categories` | Hierarchical (`ParentId`, Restrict), unique `Slug`, bilingual names. Seeded: jackets/dresses/suits/tops. |
| `Products` | Unique `Slug`, status (Draft/Active/Archived) + `PublishAtUtc`, bilingual title/description/material-care/size-fit, type/vendor/tags, SEO fields, `IsFeatured`. Category FK SetNull. |
| `ProductImages` | Ordered, bilingual alt text, optional `ColorName` to bind PDP gallery to color variants. Cascade from product. |
| `ProductOptions` | Option axes (Size/Color/Fit/Length), `Position` 1–3 maps to variant `Option1..3`, `ValuesCsv` keeps display order. |
| `ProductVariants` | Shopify-style option-combination rows: unique filtered `Sku`, price / compare-at / cost, weight, stock, low-stock threshold, oversell flag, optional image (no DB action — both legs already cascade from product). |
| `Collections` | Manual or smart (`IsSmart` + `RulesJson`), sort-order enum, unique `Slug`, SEO fields. |
| `CollectionProducts` | Composite PK (CollectionId, ProductId) + manual `Position`. |
| `InventoryAdjustments` | Immutable signed stock movements with reason enum + staff user — the per-variant stock history (AD-PRD-04). |

### Customers & Identity
| Table | Notes |
| --- | --- |
| `AspNetUsers` (`ApplicationUser`) | Guid PK; name, preferred language/currency, per-channel marketing opt-ins, `IsStaff`, login timestamps. |
| `AspNetRoles` (`ApplicationRole`) | PRD §9 roles, seeded with descriptions. |
| `Addresses` | Kuwait structure: governorate/area/block/street/building/floor/apartment/directions; `IsDefault` invariant kept by `CustomerService`. |
| `WishlistItems` | Unique (UserId, ProductId). |

### Cart
| Table | Notes |
| --- | --- |
| `Carts` | `Token` (Guid, unique) is the guest cookie value — int PK never leaves the server. `UserId` set at login (merge handled by `CartService.MergeGuestCartAsync`). Gift wrap, note, optional discount code (SetNull). |
| `CartItems` | Unique (CartId, ProductVariantId); quantity only — prices always read live from the variant. |

### Orders
| Table | Notes |
| --- | --- |
| `Orders` | Unique `OrderNumber` ("WS-1xxxx"), nullable `UserId` (guest checkout), full **shipping-address snapshot** columns, amount breakdown (subtotal/shipping/tax/discount/gift-wrap/total), three status axes (`Status`, `PaymentStatus`, `FulfillmentStatus`), channel + `IsDraft` (draft orders, AD-ORD-08), language snapshot for notifications. |
| `OrderItems` | **Snapshot rows** — bilingual title, variant description, SKU, image, unit price; `ProductId`/`ProductVariantId` are plain columns with **no FK** so catalog deletions never corrupt history. |
| `OrderEvents` | Append-only timeline (kind, description, author) per AD-ORD-03. |
| `Payments` | Tap transactions: method, `GatewayTransactionId`, status, raw `ResponseJson`. |
| `Refunds` | Full/partial, links order + optional payment (NoAction to avoid cascade cycles). |
| `Shipments` | Provider-agnostic: carrier, AWB, tracking URL, status timeline (INT-DLV). |
| `ReturnRequests` / `ReturnItems` | RMA number (unique), status flow Pending→Approved→Received→Refunded/Rejected (SF-ACC-09); `ReturnItems.OrderItemId` NoAction (cascade-path conflict). |

### Marketing
| Table | Notes |
| --- | --- |
| `DiscountCodes` | Unique code, type (percentage/fixed/free-shipping), min purchase/quantity, total & per-customer usage limits, schedule, `EligibilityJson` for future scoping (AD-MKT-01). |
| `NewsletterSubscribers` | Unique email, WhatsApp opt-in, language, source, soft unsubscribe. |

### Content
| Table | Notes |
| --- | --- |
| `StaticPages` | Unique slug, bilingual title/body (HTML), SEO fields, publish flag (AD-CNT-01). |
| `JournalCategories` / `JournalPosts` | Unique slugs, bilingual, scheduled publish, tags, author, hero image, reading time (SF-JRN). |
| `ContactMessages` | Contact-form inbox with read/replied tracking (SF-STA-02). |

### System
| Table | Notes |
| --- | --- |
| `StoreSettings` | Key-value with group; well-known keys in `SettingKeys` (free-shipping threshold 50 KWD, shipping rates, gift-wrap fee 3.5 KWD, maintenance mode…). Cached 5 min by `SettingsService`. |
| `AuditLog` | Append-only admin action log: action, entity, before/after JSON, IP (NFR-SEC-03). |

## Cascade-behavior decisions

SQL Server rejects multiple cascade paths, so:
- `ProductVariants.ImageId` → no action (variant *and* image already cascade from product).
- `Refunds.PaymentId` → no action (refund *and* payment already cascade from order).
- `ReturnItems.OrderItemId` → no action (both already cascade from order).
- `Categories.ParentId` → restrict (self-reference).
- Deleting these parents requires clearing references first — intentional friction for destructive admin actions.

## Seeding (idempotent, runs at startup of either app)

1. The 9 PRD roles, with descriptions.
2. Super admin from `SeedAdmin` config (default `admin@whitestiches.kw` — **change the password**).
3. Root categories: Jackets/جاكيتات, Dresses/فساتين, Suits/بدلات, Tops/بلوزات.
4. Baseline `StoreSettings` (shipping rates, thresholds, store identity).
