# Architecture decisions

## Current approach
Modular monolith with separate Domain, Application, Infrastructure, and Web projects. The first deployment target is one Windows Server host with SQL Server already installed. Keep database access local/restricted; do not expose SQL Server publicly.

## Order channels
`OrderChannel` distinguishes in-person and online orders. Both use the same `Order` aggregate so the operator can manage both channels from one interface. Online order review uses `PendingReview`, `Confirmed`, and `Rejected`; later states cover production and delivery.

## Future integrations
Rahkaran identifiers are stored separately from the internal order ID. Integration is not enabled in this starter. The future storefront should call a documented API rather than connect directly to the database.

## Security notes
- Never commit secrets or production connection strings.
- Store uploaded images outside the public web root and validate size, extension, and file signature.
- Restrict database access to the application identity/login and local/private network.
- Add authentication, authorization, antiforgery, audit history, and backups before production use.

## Database schema management
EF Core Code First is the source of truth for the database schema. Configure entities in `CakeOrdersDbContext`, create reviewed migrations, commit migration source files, and apply them with `dotnet ef database update` in development or a reviewed SQL migration script in deployment. Avoid maintaining a parallel hand-written schema as the primary workflow.
