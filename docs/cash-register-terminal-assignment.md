# Cash register and POS assignment

## Current design

- A `Branch` represents a physical branch.
- A `CashRegister` represents one checkout workstation within a branch.
- Each cash register can have one default `PosTerminal`; a terminal can be assigned to at most one register.
- SadadBridge installation identity is stored independently of the workstation IP address. IP addresses are not stable identities.
- The installation secret must be generated per installation and stored server-side only as a cryptographic hash. Do not reuse the general `SadadBridge__ApiKey` as the long-term identity of every workstation.

## Important implementation status

The domain entities and EF Core model configuration are committed on branch
`feature/cash-register-terminal-assignment`. The database migration and admin screens
have **not** been generated or tested yet. Do not run `database update` until the migration
has been generated, reviewed, and tested against a development database.

## Next implementation steps

1. Generate an EF Core migration for `Branch` and `CashRegister` using the repository's
   installed .NET SDK and EF Core version.
2. Review the migration and model snapshot; confirm filtered unique indexes are supported by
   the target SQL Server and that existing data is not affected.
3. Add authenticated admin pages for branch and register CRUD, including terminal assignment.
4. Add a per-installation registration/credential flow for SadadBridge. Never expose terminal
   IPs or bridge credentials to arbitrary browser input.
5. Implement a payment-intent record with a unique idempotency key and explicit states such as
   Pending, Succeeded, Failed, and Unknown. Validate order amount on the server and reconcile
   ambiguous terminal responses before allowing a retry.
6. Add localhost-only communication between the browser and SadadBridge with origin checks,
   anti-CSRF protections, and a one-time/short-lived request token. Origin checking alone is not
   sufficient authentication.
7. Test two checkout PCs with two separate terminals before any deployment.

## Suggested migration command

Run from the repository root after pulling this branch:

```powershell
dotnet ef migrations add AddBranchesAndCashRegisters --project src/CakeOrders.Infrastructure --startup-project src/CakeOrders.Web
```

Inspect the generated migration and snapshot before applying it to a development database.
