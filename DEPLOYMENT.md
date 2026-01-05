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

Run-migrations workflow note:
- The repository includes a manual GitHub Actions workflow to run EF Core migrations: `.github/workflows/run-migrations.yml`.
- This workflow is `workflow_dispatch` (manual trigger) and requires the `AZURE_CREDENTIALS` secret (service principal JSON) with permission to read deployment outputs and, if needed, access Key Vault secrets.
- Before running the workflow, ensure the following secrets are configured in the GitHub repository settings:
   - `AZURE_CREDENTIALS` (required): Azure service principal JSON used by `azure/login` action.
   - `SQL_ADMIN_PASSWORD` (recommended): used by infra deploy to build admin connection string if present.
   - If your Key Vault contains `SqlAdminConnectionString`, ensure the service principal has `get` access to that Key Vault secret.

To trigger migrations from the Actions UI, go to GitHub → Actions → Run EF Migrations → Run workflow and provide `resource_group`, `deployment_name`, and `project_path`.
