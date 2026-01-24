# Reset Production Database

## Context
The EF Core migrations were initially created with SQLite provider but production uses SQL Server, causing type incompatibility errors. All migrations have been recreated from scratch with SQL Server provider.

## Steps to Reset Production Database

### Option 1: Drop and Recreate Database (Cleanest)
Run these commands from Azure CLI or Azure Portal Query Editor:

```sql
-- Connect to master database
USE master;
GO

-- Drop the existing database
DROP DATABASE IF EXISTS [rg-karamel-prod-sqldb];
GO

-- Recreate the database
CREATE DATABASE [rg-karamel-prod-sqldb];
GO
```

Then run the GitHub Actions workflow **"Run EF Migrations"** to apply the fresh migrations.

### Option 2: Drop Migration History Only (If you want to keep data)
If there's any data you want to preserve, you can drop just the migration history table:

```sql
-- Connect to the Karamel database
USE [rg-karamel-prod-sqldb];
GO

-- Drop the migration history table
DROP TABLE IF EXISTS [__EFMigrationsHistory];
GO
```

Then run the GitHub Actions workflow **"Run EF Migrations"** to apply migrations.

### Option 3: Using Azure Portal
1. Navigate to Azure Portal
2. Go to SQL Databases
3. Select `rg-karamel-prod-sqldb`
4. Click "Query editor" (requires firewall rule or managed identity)
5. Run the SQL commands from Option 1 or 2

## After Database Reset
1. Trigger the GitHub Actions workflow: `.github/workflows/run-migrations.yml`
2. The `InitialCreate` migration will create all tables with correct SQL Server types
3. Verify successful deployment in Application Insights logs

## What Changed
- **Old migrations**: Mixed SQLite types (TEXT, INTEGER) with SQL Server types
- **New migration**: `InitialCreate` - Single clean migration with SQL Server types only
  - Sessions table: `uniqueidentifier`, `nvarchar(max)`, `datetime2`, `bit`, `int`
  - Playlists table: `uniqueidentifier`
  - PlaylistItems table: `uniqueidentifier`, `int`, `nvarchar(max)`
  - Songs table: `uniqueidentifier`, `nvarchar(512)`, `nvarchar(max)`, `datetime2`

## Preventing Future Issues
When creating new migrations, always set the SQL Server provider:

```powershell
$env:DB_PROVIDER='SqlServer'
$env:ConnectionStrings__DefaultConnection='Server=(local);Database=Karamel;Trusted_Connection=True;TrustServerCertificate=True;'
dotnet ef migrations add MigrationName --project Karamel.Backend
```

The `BackendDbContextFactory` reads `DB_PROVIDER` environment variable to determine which provider to use during migration generation.
