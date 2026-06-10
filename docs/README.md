# White Stitches — Documentation Index

| Document | Purpose |
| --- | --- |
| [PRD.md](PRD.md) | Product Requirements Document v1.0 — the source of truth for scope, requirement IDs (SF-*/AD-*/INT-*/LOC-*/NFR-*), priorities, and launch acceptance criteria |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Solution structure, locked technical decisions, service layer, security posture |
| [DATABASE.md](DATABASE.md) | Schema specification per module, conventions, cascade decisions, seeding, migration how-to |
| [BUILD-PLAN.md](BUILD-PLAN.md) | Phased delivery plan mapped to PRD requirement IDs — what's done, what's next |
| [HTML-CONVERSION.md](HTML-CONVERSION.md) | How the static site became Razor views: shells, transform rules, route map, front-end behavior inventory |

## Quick start

```powershell
# storefront → https://localhost:7100
dotnet run --project src\WhiteStiches.Web --launch-profile https

# admin → https://localhost:7200  (login: SeedAdmin in appsettings.json)
dotnet run --project src\WhiteStiches.Admin --launch-profile https

# apply migrations
dotnet ef database update --project src\WhiteStiches.Infrastructure --startup-project src\WhiteStiches.Web
```
