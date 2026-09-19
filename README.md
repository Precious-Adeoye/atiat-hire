# ATIAT Vehicle Hire: Request and Operations Prototype

ASP.NET Core 8 MVC + EF Core (SQLite). Implements the project documentation:
customer request portal, staff operations dashboard, fleet availability with conflict flags,
WhatsApp handoff, and a demand-insights layer.

## Run

    dotnet run

Open the URL printed in the console. Staff sign in at /Account/Login with `ops` / `ChangeMe!123`
(Development only; the app refuses to start outside Development with this password).
On first run it creates `atiat-hire.db`, seeds a fleet and about 60 fictional demo requests.
Set `Seed:DemoData` to `false` in appsettings.json to start empty.

## Configuration

| Setting | Purpose |
|---|---|
| `WhatsApp:BusinessNumber` | ATIAT's number, international format, digits only (e.g. 2348012345678) |
| `Staff:Users` | Staff logins. Use user-secrets or environment variables, not committed files |
| `ConnectionStrings:Default` | Database. Swap the EF provider for SQL Server/PostgreSQL in production |

    dotnet user-secrets init
    dotnet user-secrets set "Staff:Users:0:Username" "ops"
    dotnet user-secrets set "Staff:Users:0:Password" "a-long-random-password"

## Documentation to code map

| Documentation section | Where it lives |
|---|---|
| 4. Customer portal + reference number (ATIAT-HR-00125) | `RequestsController`, `HireRequestForm`, `HireRequestService.CreateAsync` |
| 4. Operations dashboard, status workflow | `DashboardController`, `RequestWorkflow`, `Views/Dashboard` |
| 5. Operations intelligence (demand, response time, cancellations) | `AnalyticsService`, `Views/Analytics` |
| 6. Request-to-WhatsApp | `WhatsAppLinkBuilder` (customer and staff links) |
| 7. Fleet availability + "Potential scheduling conflict" | `FleetController`, `ConflictService` |
| 8. Three levels | Level 1 = portal, Level 2 = dashboard and fleet, Level 3 = Insights page + CSV export |

## Decisions to confirm with ATIAT (discovery phase)

- **Cancel from any open status.** The doc shows Pending -> Cancelled only. Customers realistically cancel after a quote, so `RequestWorkflow` allows it from any open state.
- **Conflicts are flags, not blocks.** Only Quoted and Confirmed requests count as commitments.
- **Vehicle status is manual.** Availability conflicts are time-based; the Available / On hire / Maintenance label is set by staff.
- **Locations are free text.** Insights group them case-insensitively; a pick-list of common Lagos locations would make the numbers cleaner.
- **Response time** = submission to the first staff action that isn't a cancellation.

## Before production

- Replace config-based staff login with ASP.NET Core Identity or ATIAT's SSO, with roles and password hashing.
- Replace `EnsureCreated` with EF migrations (`dotnet ef migrations add Initial`).
- Configure forwarded headers behind a reverse proxy so rate limiting sees real client IPs.
- Send email/SMS on submission and status changes (a notification service would slot into `HireRequestService`).
- Add automated tests for `RequestWorkflow`, `ConflictService` and `AnalyticsService` (all are plain, easily testable classes).
