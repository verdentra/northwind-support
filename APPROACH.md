# Implementation approach

## Architecture

I kept the existing layered architecture:

- Domain contains business entities and rules.
- Application contains commands, queries, contracts and validation.
- Infrastructure contains EF Core, SQL queries, repositories, migrations and seeding.
- Presentation contains controllers, authentication, middleware and API composition.
- The React client keeps API access centralized in `client.ts` and separates authentication from page components.

## Task 1

I fixed the ticket-list filtering defect by ensuring all filter values are observed by the list hook. Search remains debounced, while non-search filters request immediately.

Filtering is applied to `IQueryable` in Infrastructure before counting, sorting and paging, so SQL Server performs the filtering and pagination.

## Task 2

I placed triage rules in a shared service used by both ticket creation and escalation. This keeps priority, SLA and assignment behavior consistent.

Escalation recalculates priority, due time and assignment, and stores an immutable history record containing before/after values and the authenticated actor.

## Task 3

Agents are the authenticated users, so their password hashes are stored on `Agent`. Passwords are hashed with `PasswordHasher<Agent>`. JWT authentication uses configuration-provided issuer, audience, signing key and lifetime values.

A global fallback authorization policy protects endpoints by default. Login and health checks are explicit anonymous exceptions. The frontend stores the token in `sessionStorage`, attaches it centrally and clears the session after a 401 response.

## Task 4

The API uses a multi-stage Dockerfile. The SDK image restores, builds and publishes the API. The final image uses the smaller ASP.NET runtime and runs as a non-root user on port 8080.

Docker Compose starts SQL Server and the API. The API connects to SQL Server using the Compose service name and waits for the SQL Server healthcheck. In Development, the API applies migrations and seeds data at startup.

The `/health` endpoint checks database connectivity and is available without authentication.

## Verification

Verification included automated backend and frontend tests, type checking, linting, generated SQL checks, manual authentication checks, Docker Compose startup with an empty database, health checks, protected endpoint checks, non-root verification and Docker image-size checks.
