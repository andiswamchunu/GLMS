# GLMS – Part 2: Core Prototype & Unit Testing

Global Logistics Management System for TechMove Logistics, built as an **ASP.NET Core MVC monolith** (.NET 8) with
**Entity Framework Core + SQL Server**, following the design in the Part 1 report (TOGAF, Factory/Strategy/Observer patterns).

## Run it

Requirements: .NET 8 SDK, and SQL Server LocalDB (installed with Visual Studio) or any SQL Server.

```
dotnet restore
dotnet run --project GLMS.Web
```

Open the URL shown (https://localhost:7141). On first start the database `GlmsDb` is created and seeded with demo clients,
contracts (Draft / Active / On Hold / Expired) and requests. To use another server, change `ConnectionStrings:GlmsDb`
in `GLMS.Web/appsettings.json`.

Visual Studio: open `GLMS.sln`, set **GLMS.Web** as the startup project and press F5.

### Using EF Core migrations instead of EnsureCreated (optional)
```
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project GLMS.Web
```
then set `"Database": { "UseMigrations": true }` in `appsettings.json`.

## Run the tests

```
dotnet test
```
Visual Studio: **Test > Test Explorer > Run All Tests**, then take your screenshot (evidence for the brief).

## What maps to the brief

| Requirement | Where |
|---|---|
| SQL Server + EF Core, entities Client / Contract / ServiceRequest | `GLMS.Web/Models`, `Data/GlmsDbContext.cs` |
| Contract status Draft / Active / Expired (+ On Hold, used by the workflow rule) | `Models/Enums.cs` |
| PDF "Signed Agreement" upload, saved to a simulated file server, downloadable | `Services/LocalFileStorage.cs` (App_Data/SignedAgreements), `ContractsController.UploadAgreement / DownloadAgreement`, Contract details page |
| Only `.pdf` accepted | `Services/AgreementFileValidator.cs` (extension, size and `%PDF-` content signature) |
| No service request on Expired / On Hold contract | `Services/ContractRules.cs` + `ServiceRequestService.CreateAsync` |
| LINQ search by date range and status | `Services/ContractSearch.cs` (`ApplyFilter`), Contracts index page |
| HttpClient currency API, auto-calculated ZAR cost | `Services/LiveApiConversionStrategy.cs` (open.er-api.com, no key needed), `wwwroot/js/service-request.js`, `ServiceRequestsController.Rate` |
| Async/await (LU4) | All EF queries, HTTP calls and file I/O are `async` with `CancellationToken` |
| Strategy pattern from Part 1 | `ICurrencyConversionStrategy`: live API first, then last-known / configured fallback rate |
| xUnit test project | `GLMS.Tests` (about 150 test cases) |

### Design decisions
* The ZAR cost is **recalculated on the server** when saving; the browser value is only a preview.
* Date-range search finds contracts whose period **overlaps** the chosen range.
* Contracts whose end date has passed are automatically moved to Expired (on search / dashboard load), and a request is
  also refused if an "Active" contract is past its end date.
* Draft contracts also cannot receive requests (they are not yet in force – Part 1, Phase B).
* Contract status only changes through allowed transitions (Draft→Active→On Hold→Expired; Expired is final).
* There is no login yet; the "Admin" screens are open. Authentication belongs with the later parts.

## Tests (GLMS.Tests)
* `CurrencyConverterTests` – USD→ZAR maths for a given rate, rounding, invalid input
* `AgreementFileValidatorTests` – `.exe` and other types rejected, renamed executables rejected, size limits, valid PDFs pass
* `ContractRulesTests` – request creation rules per status/date, status transitions
* `ContractSearchTests` – date range / status / client filters, via LINQ and via EF Core
* `ContractServiceTests` – create/update, status workflow, auto-expiry, agreement upload/replace/download
* `ServiceRequestServiceTests` – Expired / On Hold / Draft blocked, ZAR cost saved, validation, status workflow
* `CurrencyServiceTests` – API parsing, caching, API failure fallback, strategy ordering (HttpClient faked)
* `LocalFileStorageTests` – saving, reading, deleting, path-traversal protection
