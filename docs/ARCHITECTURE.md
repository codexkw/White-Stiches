# White Stitches — Architecture

**Status:** Living document · Last updated 2026-06-10

## 1. Decision summary

| Decision | Choice | Rationale |
| --- | --- | --- |
| Framework | **ASP.NET Core 9 MVC** (server-rendered, **no APIs**) | Owner directive. Server-rendered HTML also satisfies NFR-SEO-01. |
| Surfaces | **Two MVC apps**: `WhiteStiches.Web` (storefront) + `WhiteStiches.Admin` (back office) | Deployable/securable independently on IIS; PRD treats them as two surfaces on one backend. |
| Backend sharing | Both apps consume the **same service layer** (`WhiteStiches.Core` interfaces, `WhiteStiches.Infrastructure` implementations) via DI. **No HTTP layer between Web and Admin.** | "Web and admin using the services inside" — one backend, two heads. |
| Data | **EF Core 9 + SQL Server**, code-first migrations | Existing SQL Server at 83.229.86.221. |
| Identity | **ASP.NET Core Identity** with `Guid` keys; one user store for customers and staff, separated by roles | PRD Section 9 role matrix. |
| Package pinning | All `Microsoft.*` packages pinned to **9.0.x** explicitly | `dotnet add package` can float to net10-targeting packages. Keep everything 9.0.x. |
| Naming | `WhiteStiches.*` (matches repo `codexkw/White-Stiches` and DB `White-Stiches` spelling) | Consistency with repo/DB names, even though the brand spells "Stitches". |

## 2. Solution layout

```
White-Stiches/
├── WhiteStiches.sln
├── docs/                          ← all documentation, plans, specs
├── HTML/                          ← original static site (reference; kept verbatim)
├── scripts/
│   └── convert-html.ps1           ← one-shot HTML → Razor conversion script
└── src/
    ├── WhiteStiches.Core/         ← domain: entities, enums, service interfaces, models
    ├── WhiteStiches.Infrastructure/  ← EF Core DbContext + migrations, Identity, service implementations
    ├── WhiteStiches.Web/          ← customer storefront (MVC)
    └── WhiteStiches.Admin/        ← staff back office (MVC)
```

### Dependency direction

```
Web ──┐
      ├──► Infrastructure ──► Core
Admin ┘
```

- `Core` has **zero** infrastructure dependencies (no EF, no Identity).
- `Infrastructure` implements `Core` interfaces; owns `WhiteStichesDbContext`, migrations,
  `ApplicationUser`/`ApplicationRole`, and `DbSeeder`.
- Web/Admin reference Infrastructure only to call `AddWhiteStichesInfrastructure()` and (sparingly,
  Admin dashboards) the DbContext. Business rules belong in services.

## 3. Service layer

Interfaces live in `WhiteStiches.Core/Interfaces`, implementations in
`WhiteStiches.Infrastructure/Services`, all registered scoped in
`DependencyInjection.AddWhiteStichesInfrastructure()`:

| Service | Responsibility |
| --- | --- |
| `ICatalogService` | Product/category/collection browsing + admin CRUD + inventory adjustments |
| `ICartService` | Guest (cookie token) and customer carts, merge-at-login, totals (`CartSummary`) |
| `IOrderService` | Order creation, timeline events, status transitions, cancellation/restock, returns, order-number generation, public tracking |
| `ICustomerService` | Addresses (Kuwait structure), wishlist |
| `IContentService` | Static pages, journal posts/categories, contact messages |
| `IMarketingService` | Discount-code validation/CRUD, newsletter subscriptions |
| `ISettingsService` | Cached key-value store settings (`SettingKeys` constants) |
| `IAuditService` | Append-only audit log of admin write actions |

## 4. Web app (storefront)

- All 31 storefront pages converted from `HTML/` to Razor views (see `HTML-CONVERSION.md`).
- Three shells: `_Layout` (full storefront shell), `_CheckoutLayout` (minimal secure-checkout shell),
  and `Layout = null` standalone pages (intro splash, maintenance, design-system reference).
- Branded 404 via `UseStatusCodePagesWithReExecute("/not-found")`.
- Identity cookie: login path `/account/login`, 30-day sliding expiration.
- Views are currently the **pixel-faithful static port**; wiring views to the services
  (real products, cart, auth) is the next milestone — see `BUILD-PLAN.md`.

## 5. Admin app (back office)

- Custom brand-consistent chrome (`wwwroot/css/admin.css` on top of `design-system.css` tokens).
- **Default-deny security posture**: a global fallback authorization policy requires an
  authenticated user in a staff role (`AppRoles.StaffRoles`); `[AllowAnonymous]` only on
  `/login` and `/error`.
- `AuthController` verifies staff-role membership before sign-in and writes `staff.login`
  audit entries. Lockout: 5 failures / 15 minutes.
- `DashboardController` shows live store counts; module pages (Orders, Products, …) are the
  next milestone.
- Dev ports: Web `https://localhost:7100`, Admin `https://localhost:7200`.

## 6. Roles (PRD §9)

`SuperAdmin, Admin, OperationsManager, MarketingManager, ContentEditor, CustomerService,
InventoryStaff, ReadOnlyAuditor` (staff) + `Customer`. Seeded at startup by `DbSeeder`
together with the super-admin account (`SeedAdmin` config section), root categories,
and baseline store settings.

## 7. Configuration

- Connection string `DefaultConnection` in each app's `appsettings.json` (owner decision:
  plain appsettings, consistent with Zinah-style projects).
- `SeedAdmin:Email` / `SeedAdmin:Password` — change the default password after first login.
- Seeding runs at startup in both apps and is idempotent; failures log and do not block startup.

## 8. Deliberately deferred (tracked in BUILD-PLAN.md)

Tap Payments integration, delivery-partner interface, WhatsApp/SMTP notifications,
GA4/Consent Mode, bilingual AR/RTL content rendering from the database, smart-collection
rule evaluation, and per-page dynamic data binding.
