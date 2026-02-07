---
description: "Guidelines for creating EF Core migrations for SQL Server and SQLite"
applyTo: "Karamel.Backend/Migrations/**"
---

# Database Migration Guidelines

**Important**: Check the database environment of existing migrations. If they do not match your target environment (e.g., switching from SQLite to SQL Server), you must **delete the existing migrations folder** before creating new ones.

To ensure the correct database provider is used when creating migrations, you must set the `DB_PROVIDER` environment variable.

## SQL Server (Production)

Use this for production migrations.

**PowerShell**:
```powershell
$env:DB_PROVIDER = "SqlServer"
dotnet ef migrations add <MigrationName> --project Karamel.Backend --context BackendDbContext
```

## SQLite (Development)

Use this for local development if not using SQL Server.

**PowerShell**:
```powershell
$env:DB_PROVIDER = "Sqlite"
dotnet ef migrations add <MigrationName> --project Karamel.Backend --context BackendDbContext
```

## Switching Between Providers

The application uses a single `DbContext` but supports multiple providers. Migrations generated for one provider (e.g., SQL Server) will not work with the other (SQLite). You likely need to switch migration sets when moving between local development (SQLite) and production deployment (SQL Server).

### Switching from SQL Server to SQLite (Local Dev)
1. **Backup/Remove** the current SQL Server migrations:
   ```powershell
   Move-Item Karamel.Backend/Migrations Karamel.Backend/Migrations_SqlServer
   ```
2. **Generate** SQLite migrations:
   ```powershell
   $env:DB_PROVIDER = "Sqlite"
   dotnet ef migrations add InitialCreate --project Karamel.Backend --context BackendDbContext
   ```
3. **Reset** the local database:
   ```powershell
   Remove-Item Karamel.Backend/karamel.db
   dotnet ef database update --project Karamel.Backend --context BackendDbContext
   ```

### Switching from SQLite to SQL Server (Deployment)
1. **Remove** the current SQLite migrations:
   ```powershell
   Move-Item Karamel.Backend/Migrations Karamel.Backend/Migrations_Sqlite
   ```
2. **Restore** or **Generate** SQL Server migrations:
   
   *Option A: Restore from backup*
   ```powershell
   Move-Item Karamel.Backend/Migrations_SqlServer Karamel.Backend/Migrations
   ```
   
   *Option B: Regenerate fresh (Warning: Drops production compatibility if applied strictly)*
   ```powershell
   $env:DB_PROVIDER = "SqlServer"
   dotnet ef migrations add InitialCreate --project Karamel.Backend --context BackendDbContext
   ```
