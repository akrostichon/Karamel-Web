# Quickstart for Admin Session Control

1. **Start a session** from the Home page using the main tab (with library access).
2. **Copy the admin link** displayed once the session is created; this link includes an `admin=true` query parameter that grants privileges to the tab that opens it.
3. Open the admin link in one or more tabs/devices. Each admin tab will display a segmented control on the Playlist page with two options: `Playlist` and `Session Control`.
4. Click `Session Control` to reveal the boxed panel. Use the `▶️|` button to pause playback, and the `▶️` button to resume. Click `Next` to advance the queue immediately.
5. In the same panel you may toggle configuration switches (Singer name required, Allow reorder) and set a numeric pause between songs; changes propagate instantly to all open tabs and are saved in the session DTO.
6. Non-admin tabs and singer views will not see the segmented control, and read-only singer playlists are accessible via a toggle on SingerView.

To run tests while developing:

```powershell
# Frontend component tests
dotnet test Karamel.Web.Tests --filter DisplayName~AdminControls

# Backend hub and repository tests
dotnet test Karamel.Backend.Tests --filter PlaylistHubTests

# JavaScript tests (in project root)
cd Karamel.Web\wwwroot
npm run test:run -- --testNamePattern="admin"
```