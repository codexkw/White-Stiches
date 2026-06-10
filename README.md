# White Stitches — E-commerce Platform

Luxury fashion storefront + back office for Kuwait/GCC. ASP.NET Core 9 MVC (server-rendered,
no APIs), EF Core 9 on SQL Server, shared service layer consumed by both apps.

```
src/
├── WhiteStiches.Core            domain entities, enums, service interfaces
├── WhiteStiches.Infrastructure  EF Core (DbContext, migrations), Identity, services
├── WhiteStiches.Web             customer storefront  → https://localhost:7100
└── WhiteStiches.Admin           staff back office    → https://localhost:7200
HTML/                            original static site (design reference)
docs/                            PRD, architecture, database spec, build plan
```

## Run

```powershell
dotnet run --project src\WhiteStiches.Web --launch-profile https     # storefront
dotnet run --project src\WhiteStiches.Admin --launch-profile https   # admin
```

Database migrations + seeding (roles, super admin, categories, settings) run from the apps /
`dotnet ef`; see [docs/DATABASE.md](docs/DATABASE.md). Admin sign-in uses the `SeedAdmin`
account from `appsettings.json` — rotate the password after first login.

## Documentation

Start at [docs/README.md](docs/README.md). The build roadmap lives in
[docs/BUILD-PLAN.md](docs/BUILD-PLAN.md); product scope in [docs/PRD.md](docs/PRD.md).
