# Authentication and Authorization

## Overview

Northwind Support Desk uses JWT bearer authentication for API access.

- Agents sign in using their email and password.
- Passwords are verified using ASP.NET Core PasswordHasher.
- Successful login returns a JWT and agent details.
- Protected API endpoints require a valid bearer token.
- The frontend redirects unauthenticated users to /login.
- After login, users return to their originally requested page.
- Logging out clears the browser session.
- HTTP 401 responses clear authentication and redirect to login.
- Ticket escalations record the authenticated agent rather than trusting a client-provided name.

## Development configuration

The API requires the following environment variables:

- Jwt__Key: a unique random signing key of at least 32 bytes
- Jwt__Issuer: NorthwindSupportDesk
- Jwt__Audience: NorthwindSupportDesk
- Jwt__LifetimeMinutes: 60

Generate and set the key in PowerShell:

    $bytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    $env:Jwt__Key = [Convert]::ToBase64String($bytes)
    $env:Jwt__Issuer = "NorthwindSupportDesk"
    $env:Jwt__Audience = "NorthwindSupportDesk"
    $env:Jwt__LifetimeMinutes = "60"

Start the API from the same PowerShell session:

    dotnet run --project apps/api/src/SupportDesk.Presentation

The API must have access to the configured SQL Server database. Apply the existing EF Core migrations using the project's normal startup or migration procedure.

## Development agent accounts

The development database seeds six agents.

Example active account:

- Email: alex.turner@northwind-support.example
- Password: NorthwindDemo2026!

Example inactive account:

- Email: yuki.tanaka@northwind-support.example
- Password: NorthwindDemo2026!

Inactive agents must not be allowed to sign in.

These credentials are for development/testing only. The shared demonstration password must not be used in production.

## API endpoints

- POST /api/auth/login: authenticates an active agent and returns a JWT
- GET /api/auth/me: returns the currently authenticated agent

All other API endpoints require authentication, except explicitly anonymous endpoints such as login and Swagger.

## Frontend token storage

The frontend stores the JWT and agent details in sessionStorage.

Benefits:
- Authentication persists across page refreshes in the same browser tab.
- Closing the tab normally ends the tab's session.
- Tokens are not persisted in localStorage.

Trade-off:
- JavaScript can access sessionStorage, so an XSS vulnerability could expose the token.
- sessionStorage is not a replacement for secure server-side authorization.

For a production deployment, consider a secure HttpOnly cookie-based session with appropriate CSRF protections, alongside strong XSS defenses.

## Verification

Backend:

    dotnet test

Frontend:

    cd apps/web
    npm.cmd test -- --run
    npm.cmd run build

Manual checks:
1. Log in as an active agent.
2. Confirm protected ticket pages are accessible.
3. Log out and confirm redirection to login.
4. Try signing in as an inactive agent and confirm rejection.
5. Escalate a ticket and confirm the escalation history records the authenticated agent.
