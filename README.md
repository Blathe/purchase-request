# Purchase Request System

An internal web app where employees submit purchase requests. Each request is routed to the right approvers based on amount and category, every step is recorded in an audit trail, and managers get a dashboard of pending requests and spend.

## Tech stack

- .NET 10, Blazor Web App
- Microsoft Entra ID sign-in (Microsoft.Identity.Web)
- Entity Framework Core on SQL Server
- Hosted on Azure App Service

## Documentation

Written before the code. Start here:

1. [Problem statement and stakeholders](docs/01-problem-statement.md)
2. [Current and future process maps](docs/02-process-maps.md)
3. [Requirements (FR / NFR)](docs/03-requirements.md)
4. [User stories with acceptance criteria](docs/04-user-stories.md)
5. [Business rules catalog (BR-01 to BR-05)](docs/05-business-rules.md)
6. [Data model (ERD and data dictionary)](docs/06-data-model.md)
7. [UAT cases and traceability matrix](docs/07-uat-and-traceability.md)

## Azure setup

In the Entra ID app registration:

- **App roles** (allowed member types: Users/Groups), with these values: `ITApprover`, `FinanceApprover`, `Purchasing`, `Admin`. Assign users under Enterprise applications → Purchase Request → Users and groups.
- **API permissions**: Microsoft Graph → Application → `User.Read.All`, with admin consent. Used to look up each requester's manager (BR-01).
- **Redirect URIs** (Web): `https://localhost:7087/signin-oidc` and `https://localhost:7087/signout-callback-oidc`.

Emails are written to the Email outbox page (`/outbox`) instead of being sent.

## Running locally

1. Install the .NET 10 SDK and SQL Server LocalDB.
2. The Entra tenant and client IDs are in `appsettings.json`. Set the client secret from the `PurchaseRequest` folder: `dotnet user-secrets set "AzureAd:ClientSecret" "<secret>"`.
3. Run `dotnet ef database update --project PurchaseRequest`.
4. Run `dotnet run --project PurchaseRequest --launch-profile https` and open `https://localhost:7087`.
5. Run tests with `dotnet test`.
