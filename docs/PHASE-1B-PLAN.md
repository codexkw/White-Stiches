# Phase 1B — Admin Modules: Implementation Plan

Eight modules built in parallel with **disjoint file ownership**. Shared scaffolding
(below) was pre-built so no two modules touch the same file. Requirement IDs reference
`docs/PRD.md`; phase tracking lives in `docs/BUILD-PLAN.md`.

## Shared scaffolding (pre-built, owned by the orchestrator)

| Piece | Where | Notes |
| --- | --- | --- |
| File storage | `IFileStorage` / `FileStorageService` | Saves to `Storage:Root` (solution `/storage`, gitignored); returns `/media/...` web paths; both apps serve `/media` via `StorageSetup.UseWhiteStichesMedia` |
| Admin DI | `AdminServicesRegistration.AddWhiteStichesAdminServices()` | Registers the 5 admin-only services; called from Admin `Program.cs` only |
| Admin service stubs | `Core/Interfaces/Admin/*` + `Infrastructure/Services/Admin/*` | Empty; each module fills its own pair |
| Slug helper | `Core/Utils/Slug.Generate` | Latin lowercase handles |
| Admin shell | `_Layout.cshtml` | Full sidebar nav (`ViewData["Nav"]` keys), `TempData["Ok"]`/`TempData["Err"]` toasts, `Styles`/`Scripts` sections |
| Pager | `Views/Shared/_Pager.cshtml` + `PagerInfo` | Preserves query string, swaps `page` |
| CSS kit | `wwwroot/css/admin.css` | Cards, panels, tables, fields, buttons + badges, toolbar, form grid, thumbs, pager, toast |
| Assets bridge | Admin `Program.cs` | Serves Web's `wwwroot/assets` at `/assets` so seeded product images render in the back office |

## Module ownership map

Every module owns: its controller(s), its `Views/<Module>/` folder, its
`Core/Interfaces/Admin/I<X>AdminService.cs` + `Infrastructure/Services/Admin/<X>AdminService.cs`
(or the listed existing service pair), optional `wwwroot/css/modules/<module>.css` and
`wwwroot/js/modules/<module>.js`. **Nothing else.**

| # | Module | Routes | Service files owned | Requirements |
| --- | --- | --- | --- | --- |
| 1 | Products + Categories | `/products*`, `/categories*` | `ICatalogService` + `CatalogService` (extend) | AD-PRD-01..05 |
| 2 | Collections | `/collections*` | `ICollectionAdminService` + impl | AD-PRD-07 |
| 3 | Orders + Drafts | `/orders*` | `IOrderAdminService` + impl | AD-ORD-01..05, 08 |
| 4 | Returns queue | `/returns*` | `IReturnAdminService` + impl | AD-ORD-10 |
| 5 | Customers | `/customers*` | `ICustomerAdminService` + impl | AD-CUS-01/02 |
| 6 | Discounts + Newsletter | `/discounts*`, `/newsletter` | `IMarketingService` + `MarketingService` (extend) | AD-MKT-01 |
| 7 | Content (Pages/Journal/Inbox) | `/pages*`, `/journal*`, `/inbox*` | `IContentService` + `ContentService` (extend) | AD-CNT-01/02 |
| 8 | Settings/Staff/Audit/2FA | `/settings*`, `/staff*`, `/audit`, `/profile/2fa*`, Auth 2FA step | `IStaffAdminService` + impl, `AuthController` | AD-SET-01/02/04/06 |

## Conventions (all modules)

- Fallback authorization already requires a staff role; sensitive modules add
  `[Authorize(Roles = ...)]` (Staff/Settings: SuperAdmin+Admin; others per PRD §9).
- Every mutation: `IAuditService.LogAsync(...)` + `TempData["Ok"]`/`["Err"]` + POST-redirect-GET.
- Antiforgery on all POSTs (`[ValidateAntiForgeryToken]`).
- No schema changes — the 27 entities cover Phase 1B; no new migrations.
- Money is KWD with 3 decimals: `@value.ToString("0.000")`.
- Lists: `PagedResult<T>` + `_Pager` partial; filters as GET query params.
- Uploads via `IFileStorage.SaveAsync(stream, fileName, "<folder>")`.
- Agents never run `dotnet build/run` (file locks); the orchestrator integrates.

## Verification

- `scripts/smoke-admin.ps1` — scripted back-office journey (login → product CRUD →
  collection → discount → order ops → returns → customers → content → settings/staff/audit).
- `scripts/smoke-e2e.ps1` — storefront journey re-run to catch service-layer regressions.
