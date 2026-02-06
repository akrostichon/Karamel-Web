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

## Resetting Migrations

If you need to regenerate migrations entirely:
1. Delete the `Karamel.Backend/Migrations` folder.
2. Run the appropriate `add` command above with the name `InitialCreate`.
