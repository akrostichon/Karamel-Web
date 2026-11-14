---
description: "SQLite-only database setup for Karamel.Backend"
applyTo: "Karamel.Backend/**"
---

# Database Setup Guidelines

This project uses SQLite only. The backend creates the schema from the EF Core model at startup (`EnsureDeleted()` + `EnsureCreated()`) instead of relying on EF migrations.

## Required behavior

- Do not add or regenerate EF Core migrations.
- Do not introduce `DB_PROVIDER`, `DB_USE_AAD`, or SQL Server-specific configuration.
- Keep the connection string default as SQLite (`Data Source=karamel.db`) unless a local override is explicitly needed.
- If you change the EF model, update the app startup schema creation behavior and validate the app locally.

## Local development

```powershell
dotnet run --project Karamel.Backend
```

The app creates the SQLite database automatically when it starts in non-Testing environments.

## When you need schema changes

1. Update the EF Core model in `Karamel.Backend/Data` or the relevant entity classes.
2. Validate the app startup path (`EnsureDeleted` + `EnsureCreated`) still creates the schema correctly.
3. Remove the local SQLite database if needed for a clean restart.

```powershell
Remove-Item Karamel.Backend/karamel.db -ErrorAction SilentlyContinue
```

## Do not do this

- Do not create migration folders or scripts for SQL Server or SQLite.
- Do not add provider-switch logic or Azure Identity plumbing.
- Do not add `DB_PROVIDER` or `DB_USE_AAD` references to backend code or tests.
