# Karamel Developer Manual

This guide covers development and maintenance of the Karamel application. It is separate from the [user-facing README](README.md) and [User Manual](MANUAL.md).

## Prerequisites

- .NET 10 SDK
- Node.js and npm
- Visual Studio Code with the C# Dev Kit for the included debugger configurations

Run the commands below from the repository root unless a step says otherwise. In PowerShell, repository paths use backslashes.

## 1. Run the tests

Build and run the C# test projects from the repository root:

```powershell
dotnet build
dotnet test .\Karamel.Web.Tests\ -v minimal
dotnet test .\Karamel.Backend.Tests\ -v minimal
```

For the JavaScript tests, install the locked dependencies when setting up the project for the first time or after the lockfile changes, then run Vitest:

```powershell
Push-Location .\Karamel.Web\wwwroot
npm ci
npm run test:run
Pop-Location
```

`npm ci` may be skipped when dependencies are already installed and `package-lock.json` has not changed.

## 2. Bump package versions

### .NET packages

Check each project for outdated packages from the repository root:

```powershell
dotnet list .\Karamel.Web\Karamel.Web.csproj package --outdated
dotnet list .\Karamel.Backend\Karamel.Backend.csproj package --outdated
dotnet list .\Karamel.Web.Tests\Karamel.Web.Tests.csproj package --outdated
dotnet list .\Karamel.Backend.Tests\Karamel.Backend.Tests.csproj package --outdated
```

Update a package in its owning project, specifying the version being adopted:

```powershell
dotnet add .\Karamel.Web\Karamel.Web.csproj package <PackageName> --version <Version>
```

Use the corresponding project path for packages owned by the backend or test projects. Keep related Microsoft Entity Framework Core packages on compatible versions. Review package release notes and compatibility before adopting a new major version; in particular, retain intentional security overrides unless you have verified they are no longer needed.

### JavaScript packages

Check and update JavaScript dependencies from the frontend directory:

```powershell
Push-Location .\Karamel.Web\wwwroot
npm outdated
npm update <package-name>
Pop-Location
```

`npm update` respects the version ranges in `package.json`. To intentionally adopt a version outside the existing range, use `npm install <package-name>@<version>` instead. Review and commit both `package.json` and `package-lock.json`, then run `npm ci` and `npm run test:run`.

After package updates, build and run the relevant tests before committing:

```powershell
dotnet build
dotnet test .\Karamel.Web.Tests\ -v minimal
dotnet test .\Karamel.Backend.Tests\ -v minimal
```

## 3. Run locally with debugging

The repository includes VS Code launch configurations in `.vscode/launch.json`:

| Configuration | Starts |
|---|---|
| `Karamel.Backend` | Backend debugger at `http://localhost:5001` and `https://localhost:7001` |
| `Karamel.Web` | Frontend debugger at `http://localhost:5245` |
| `Karamel Full Stack` | Frontend and backend together |

Open the repository root in VS Code, select a configuration in **Run and Debug**, and start debugging (F5). Use the compound configuration to debug the full application. The frontend's development settings point to the local backend at `http://localhost:5001`. Set breakpoints in C# code; use the browser's developer tools for JavaScript breakpoints.

You can also run either project from PowerShell without the debugger:

```powershell
# Backend only
Push-Location .\Karamel.Backend
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DB_PROVIDER = "Sqlite"
$env:ConnectionStrings__DefaultConnection = "Data Source=karamel.db"
dotnet run
Pop-Location
```

```powershell
# Frontend only
dotnet run --project .\Karamel.Web\Karamel.Web.csproj --launch-profile http
```

For frontend hot reload, run this from the repository root:

```powershell
dotnet watch run --project .\Karamel.Web\Karamel.Web.csproj --launch-profile http
```

For backend hot reload, use the backend working directory so the relative SQLite database path remains consistent:

```powershell
Push-Location .\Karamel.Backend
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DB_PROVIDER = "Sqlite"
$env:ConnectionStrings__DefaultConnection = "Data Source=karamel.db"
dotnet watch run
Pop-Location
```

The frontend is available at `http://localhost:5245`. The backend development profile listens on `http://localhost:5001` and `https://localhost:7001`. Running only the frontend uses its configured backend address; run the backend as well if you need local API or SignalR functionality.

## 4. Run the deployed application

Open the deployed application at [Karamel](https://polite-grass-037bbc503.2.azurestaticapps.net/). For instructions on using it, see the [User Manual](MANUAL.md).

## 5. Azure operations

These commands affect production resources. Use **Azure Cloud Shell** (Bash) for Azure CLI commands, confirm the active subscription and resource names before running them, and do not deploy to `rg-karamel-dev`. See the [Azure deployment instructions](.github/instructions/azure-deployment.instructions.md) for resource naming and deployment safeguards.

### Start the self-hosted runner

The runner VM name may differ between environments. Confirm its current name in the Azure portal or with `az vm list --resource-group rg-karamel-prod --output table`, then substitute it below:

```bash
az vm run-command invoke \
  --resource-group rg-karamel-prod \
  --name <runner-vm-name> \
  --command-id RunShellScript \
  --scripts "cd /home/azureuser/actions-runner && sudo ./svc.sh start"
```

### Restart the backend App Service

```bash
az webapp restart \
  --resource-group rg-karamel-prod \
  --name rg-karamel-prod-api
```

### Query Application Insights traces

Run this Kusto query in the Application Insights Logs view to find recent traces matching either message:

```kusto
traces
| where timestamp > ago(1h)
| where message contains "BulkUpsert" or message contains "Adding session"
| order by timestamp desc
```

### After infrastructure changes

After deploying Bicep changes, verify that required role assignments and database access grants are still in place, and apply any pending database migrations through the approved production process. Review scripts under [infra](infra/) and their documented behavior before running them: `infra/deploy.ps1 -RunMigrations` is currently a placeholder and does not apply migrations. The production SQL server FQDN is `rg-karamel-prod-sqlsrv.database.windows.net`.

## 6. Keep the local database up to date

The backend uses SQLite by default for local development. Its default connection string is `Data Source=karamel.db`; because this path is relative, run database commands and the backend from `Karamel.Backend` to consistently use `Karamel.Backend\karamel.db`. Database files are ignored by Git and should not be committed.

The backend does **not** apply migrations automatically. Apply any pending migrations before running the backend:

```powershell
Push-Location .\Karamel.Backend
$env:DB_PROVIDER = "Sqlite"
$env:ConnectionStrings__DefaultConnection = "Data Source=karamel.db"
dotnet ef database update
Pop-Location
```

If `dotnet ef` is not installed, install the EF Core CLI tool using the same major version as the project's EF Core packages (currently version 10):

```powershell
dotnet tool install --global dotnet-ef --version 10.0.14
```

When a backend model change requires a schema change, create and review a migration, then apply it to your local database:

```powershell
Push-Location .\Karamel.Backend
$env:DB_PROVIDER = "Sqlite"
$env:ConnectionStrings__DefaultConnection = "Data Source=karamel.db"
dotnet ef migrations add <MigrationName>
dotnet ef database update
Pop-Location
```

Commit the generated migration files along with the model changes. After pulling changes that add migrations, run `dotnet ef database update` again before using the local backend. Backend tests use an in-memory SQLite database and do not update this local database.

To remove a migration, first ensure it has not been applied to any shared or deployed database. From `Karamel.Backend`, remove only the latest unapplied migration:

```powershell
dotnet ef migrations remove
```

Once a migration has been applied or shared, preserve it and create a new forward migration for subsequent schema changes instead. These commands target the local SQLite database; production SQL migrations must be handled through the reviewed Azure deployment process.
