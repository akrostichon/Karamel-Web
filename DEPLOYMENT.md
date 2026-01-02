# Deployment checklist (Azure App Service)

This document lists minimal steps and configuration needed to deploy Karamel.Backend to Azure App Service with Azure SQL.

1. Build and publish the backend as a self-contained or framework-dependent app.

2. Provision Azure SQL and create a connection string. Update `Karamel.Backend/appsettings.Production.json` with the production `DefaultConnection` or set the `ConnectionStrings__DefaultConnection` app setting in App Service.

3. Configure App Service settings:
   - `KARAMEL-TOKEN-SECRET`: Provide a secure 32+ byte secret (do not check in).
   - `DB_PROVIDER`: Set to `SqlServer`.
   - `ASPNETCORE_ENVIRONMENT`: Set to `Production`.
   - `ConnectionStrings__DefaultConnection`: (optional, overrides config file)

4. Enable WebSockets on the App Service (Networking -> WebSockets: On) so SignalR can use WebSockets.

5. Configure scaling and instance size appropriate for expected concurrent sessions.

6. Deploy the app (ZIP deploy, GitHub Actions, or Docker container). If using Docker, see `Karamel.Backend/Dockerfile`.

7. Run EF Core migrations against the production DB (CI job or manual step). Ensure backups and migration rollback plan exist.

8. Monitor logs and configure Application Insights for production telemetry.

Notes:
- Do not store `KARAMEL-TOKEN-SECRET` in source control. Use Azure Key Vault or App Service environment variables.
- For container deployments, ensure the container registry credentials are configured in the App Service.
