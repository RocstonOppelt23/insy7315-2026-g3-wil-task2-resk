# feature/backend-support (Development Branch)
## How The Session Works

When a user logs in through the web app, ASP.NET Core Identity creates an auth
cookie. API controllers read that cookie to find the current user id.

Protected API calls follow this server-side order:

1. Check that the request has a valid logged-in session.
2. Load the current `User` and the user's selected `AccessRole`.
3. Check that the user account status is `Active`.
4. Check `SystemFeatureSettings` to make sure the feature is enabled.
5. Check the role permission, for example `UsersView` or `ProposalsCreate`.
6. Apply the role scope before returning data:
   - `Own`: only the current user's records.
   - `SelectedRoles`: only records connected to allowed target roles.
   - `All`: all records for that feature.
7. Apply search filters, sorting and paging.

This logic is intentionally on the service/controller side. The front end can
hide buttons or menu items, but it is not trusted for security because Postman,
Swagger, or a changed browser request can still call the API directly.

## Roles

A user has one custom access role at a time:

- `User.RoleId`
- `User.Role`

The custom role is separate from ASP Identity's internal role tables. The
project currently uses Identity mainly for login sessions and password storage,
while `AccessRole` controls CTTV permissions.

## Swagger Or Postman May Fail

Most protected endpoints will fail in Swagger or Postman unless the request has
a real logged-in Identity session cookie for an active user with the correct
role permission.

Common results:

- `401 Unauthorized`: no valid login session cookie was sent.
- `403 Forbidden`: the user is logged in, but access is blocked. Common causes
  are inactive account status, no assigned role, disabled system feature, missing
  permission, or restricted scope.
- `404 Not Found`: the record may exist, but the user's scope does not allow the
  endpoint to return it.
- '500 Internal Server Error': the error shows but please igrnore it, it is not a real error

Swagger is still useful for checking route shapes and anonymous endpoints, but
role-protected endpoints normally need a browser login session or a test setup
that sends the same auth cookie.

## Temporary API Testing Bypass

For local Swagger/Postman testing, the project has one true/false setting in
`appsettings.Development.json`.

To activate testing mode:

1. Open `appsettings.Development.json`.
2. Change:

```json
"ApiTesting": {
  "BypassServiceLogic": false
}
```

to:

```json
"ApiTesting": {
  "BypassServiceLogic": true
}
```

3. Restart the app.

To turn real permission logic back on, change it back to `false` and restart.

When `BypassServiceLogic` is `true`, `Program.cs` makes the API authorization
policies pass for local testing, and `AccessControlService` skips feature,
role, and scope restrictions.

Use this only for local testing:

1. Do not commit `BypassServiceLogic: true`.
2. Do not use bypass code in production.
3. Change the setting back to `false` after testing.
4. Keep business logic active, for example `ProposalWorkflow.TryMove`.

What testing mode does:

- `Controllers/API/ApiUserController.cs`
  - Allows Swagger/Postman calls without a login cookie.
  - Lists and gets users without role scope.
  - Creates, updates and changes user status without manager permission checks.
  - Treats Swagger's example `roleId: 0` as `null` in testing mode.
- `Controllers/API/ApiProposalController.cs`
  - Allows Swagger/Postman calls without a login cookie.
  - Lists and gets proposals without owner checks.
  - Creates proposals using the first user in the database as the test producer.
  - Updates and submits by proposal id without owner or role permission checks.
- `Controllers/API/ApiSearchController.cs`
  - Allows Swagger/Postman calls without a login cookie.
  - Keeps filters, sorting and paging.
  - Skips current-user loading, permission checks and role scope filters.
- `Controllers/API/ApiDashboardController.cs`
  - Allows Swagger/Postman calls without a login cookie.
  - Returns unscoped proposal/user counts.
  - Uses the first user only for the `MyProposals` count when a user exists.
- `Controllers/API/ApiReviewController.cs`
  - Allows Swagger/Postman calls without a login cookie.
  - Skips reviewer permission checks.
  - Keep `ProposalWorkflow.TryMove` active.
- `Controllers/API/ApiSystemController.cs`
  - `GET /api/system` is already public.
  - `PUT /api/system/update` allows Swagger/Postman calls without the settings
    permission in testing mode.

Important: testing mode bypasses session and permission checks only. It does
not fix database/migration problems. If a table or column is missing, the API
can still return `500`.

## Useful API Endpoints

Anonymous:

- `GET /api/proposals/types`
- `GET /api/system`

Protected:

- `GET /api/search/proposals`
- `GET /api/search/users`
- `GET /api/dashboard/summary`
- `GET /api/proposals`
- `GET /api/proposals/{id}`
- `POST /api/proposals/create`
- `PUT /api/proposals/{id}/update`
- `POST /api/proposals/{id}/submit`
- `GET /api/users`
- `GET /api/users/{id}`
- `POST /api/users/create`
- `PUT /api/users/{id}/update`
- `PATCH /api/users/{id}/change/status`
- `GET /api/review/queue`
- `POST /api/review/{id}/start`
- `POST /api/review/{id}/approve`
- `POST /api/review/{id}/decline`
- `POST /api/review/{id}/request-changes`
- `PUT /api/system/update`

## Search Design

Proposal search:

- Uses `AccessControlService.ApplyProposalScope` before filters.
- Supports proposal type, status, date range, search type and sorting.
- CTTV proposal types are fixed in code:
  - `IPPF`
  - `MVSF`
  - `CPAF`
  - `PAF`
  - `TDLA`

User search:

- Uses `AccessControlService.ApplyUserScope` before filters.
- Returns summary information only.
- Does not return password hashes, MFA codes, ID numbers, phone numbers,
  addresses, or other private profile/security fields.

## Rocston's Code Integration

Rocston's workflow/review API code is integrated through:

- `Services/ProposalWorkflow.cs`
- `Controllers/API/ApiReviewController.cs`
- `Models/SecurityHelper.cs`

Changes were made so Rocston's workflow code uses this project's custom
one-role-per-user `AccessRole` permission model and the shared
`AccessControlService` gateway.

## Running Locally (not sure about this, generate by AI without checking)

Build:

```powershell
dotnet build RESK.WIL.slnx
```

Run tests:

```powershell
dotnet test RESK.WIL.Tests\RESK.WIL.Tests.csproj
```

Run the web app: (i only used this to run the app for testing api)

```powershell
dotnet run --launch-profile http
```

Swagger is enabled only in Development mode. Check the console output or
`Properties/launchSettings.json` for the actual local port, then open:

```text
http://localhost:<port>/swagger
```
