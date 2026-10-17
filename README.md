# Northwind Support Desk

A support ticket management system built as a .NET 10 API, React 19/TypeScript web app, and SQL Server database.

## Implemented: Tasks 1–4

Tasks 1–3 provide SQL ticket filtering, shared create/escalation triage, SLA indicators, escalation history, JWT login, protected routes, logout and 401 handling.

Task 4 adds a multi-stage Dockerized API, Docker Compose support, and a public database-backed health endpoint.

## Business rules

| Rule | Behavior |
| --- | --- |
| Priority | Requested priority is used, with Medium as the default. Security and Outage categories force Critical priority. |
| SLA | Low: 72 hours, Medium: 24 hours, High: 8 hours, Critical: 4 hours. Due time starts at creation or escalation. |
| Premium customers | The SLA window is multiplied by 0.5, with a minimum of one hour. |
| Assignment | Specialist categories require a matching specialization. Agents must be active and below capacity. The agent with the fewest open tickets is selected, with agent ID used as the tie-breaker. |
| Escalation | Priority increases by one level and the SLA is recalculated. Critical, Resolved and Closed tickets cannot be escalated. The reason must contain 5–500 characters. |
| SLA status | SLA status is derived and never stored. A ticket can be NotApplicable, Met, Breached, AtRisk or WithinSla. |
| History | Escalation history records the previous and new priority, agent, due date, reason, authenticated actor and time. |

## Design decisions

`TicketTriageService` contains the priority, SLA-window and assignment rules shared by ticket creation and escalation. This keeps the rules in one testable location and ensures escalation recalculates the SLA consistently.

`PasswordHash` belongs on `Agent` because agents are the users who sign in. A separate Users table would duplicate identity information for this assessment. ASP.NET Core Identity `PasswordHasher<Agent>` provides salted password hashing without creating a custom hashing algorithm.

JWT authentication uses a global fallback authorization policy, so new API endpoints are protected by default. Login is explicitly anonymous, and Swagger is public in Development. Escalation actors are resolved from the authenticated token through `ICurrentUser`, rather than trusting a request-body value.

The browser stores the access token in `sessionStorage`. This survives page reloads during the browser session and is cleared when the session ends. However, it remains accessible to JavaScript and therefore has an XSS risk. In production, secure HttpOnly SameSite cookies with CSRF protection or another deployment-appropriate approach would be considered.

## Defect and filtering

**DEFECT-117 root cause:** the ticket-list effect depended on search, page and page size, but omitted filters such as status. The fix observes all filter changes, requests non-search changes immediately, and debounces search input.

The API applies status, priority, SLA status, category, customer, agent, unassigned-only and search predicates to `IQueryable` before `CountAsync`, `Skip` and `Take`. SQL Server performs filtering, sorting, counting and paging.

To add a new filter, extend `TicketQuery`, add the predicate to `TicketQueries` before counting and paging, update the frontend if required, and add tests for returned items and the filtered count.

## Run locally

Prerequisites:

- .NET SDK 10
- Node.js 20.19 or later
- Docker

```bash
cp .env.example .env
npm install
npm run db:up
npm run api
npm run web
```

The API runs at:

```text
https://localhost:7043
http://localhost:5043
```

Swagger is available at:

```text
https://localhost:7043/swagger
```

The web app runs at:

```text
http://localhost:5173
```

### Development login

```text
Email: alex.turner@northwind-support.example
Password: DevOnly-Password1!
```

This is a fake local development credential. The seeder hashes the password at seed time and fills only missing hashes. It does not overwrite existing hashes. Inactive agent Yuki Tanaka cannot log in.

## API and authentication

| Endpoint | Purpose |
| --- | --- |
| `POST /api/auth/login` | Validates credentials and returns a JWT, expiry and agent identity. Invalid credentials return the same generic 401 response. |
| `GET /api/auth/me` | Returns the authenticated agent's ID, name and email. |
| `GET /api/tickets` | Returns a filtered and paged ticket list. |
| `POST /api/tickets` | Creates and triages a ticket. |
| `POST /api/tickets/{id}/escalate` | Escalates a ticket and records immutable history. |
| `GET /api/tickets/{id}/escalations` | Returns escalation history newest-first. |
| `GET /api/tickets/{id}` | Returns ticket details. |
| `PATCH /api/tickets/{id}/status` | Changes ticket status. |
| `PATCH /api/tickets/{id}/assignment` | Changes ticket assignment. |
| `GET /api/reports/sla-summary?groupBy=priority` | Returns the SLA summary grouped by priority. |
| `GET /api/reports/sla-summary?groupBy=agent` | Returns the SLA summary grouped by agent. |
| `GET /health` | Public database-backed health endpoint used by Docker. |

The SLA summary is grouped and aggregated in SQL. It reports total tickets, breached, at-risk, within-SLA and met counts, breach rate and average resolution hours.

The report filters tickets by `CreatedAtUtc` using the half-open range `[from, to)`. The breach rate uses tickets with an SLA due date as its denominator.

`PasswordHash` is stored on `Agent` and added by migration `20261016170000_AddAgentPasswordHash`.

JWT settings are read from:

```text
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
Jwt__TokenLifetimeMinutes
```

Development tokens last 60 minutes and contain the agent ID, email, name and token ID. There are no roles or permissions.

The global fallback policy requires authentication for API endpoints. Login is anonymous. Swagger remains public in Development.

The frontend stores the access token in `sessionStorage`. `apps/web/src/api/client.ts` centrally attaches the Bearer token to API calls. If any API call returns 401, the client clears the session and redirects the user to `/login`.

## Docker

The API uses a multi-stage Docker build.

The build stage uses the .NET 10 SDK image to restore, compile and publish the API. The final stage uses the smaller ASP.NET 10 runtime image and runs as the non-root `app` user.

The container listens on HTTP port 8080. TLS is expected to be terminated by a reverse proxy in a real deployment.

Start SQL Server and the API:

```bash
docker compose down -v
docker compose up --build
```

The `-v` option removes the local development database volume. This allows migrations and seeding to be tested against an empty database. Do not use it when you need to preserve local data.

The containerized API is available at:

```text
http://localhost:5080
```

The health endpoint is:

```text
http://localhost:5080/health
```

The API waits until the SQL Server healthcheck succeeds. Because the Compose environment is `Development`, the API automatically applies migrations and seeds demo data during startup.

In a real deployment, migrations should run as a separate controlled release or deployment job before the application starts. This avoids multiple application replicas attempting migrations at the same time and allows migrations to use separate database permissions.

Runtime configuration is supplied through environment variables:

```text
MSSQL_SA_PASSWORD
ASPNETCORE_ENVIRONMENT
ConnectionStrings__Default
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
Jwt__TokenLifetimeMinutes
```

No real database credentials or JWT secrets are baked into the image. The committed values are development-only placeholders.

To run the web app against the containerized API:

```bash
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

In PowerShell:

```powershell
$env:VITE_API_PROXY_TARGET = "http://localhost:5080"
npm run web
```

Docker verification performed:

- `/health` returned `Healthy`.
- SQL Server reported `healthy` in `docker compose ps`.
- `docker compose exec api whoami` returned `app`, not `root`.
- An unauthenticated request to `/api/tickets` returned 401.
- An authenticated request returned 200 with seeded tickets.
- The .NET SDK image used approximately 1.3 GB of disk space.
- The final `supportdesk-api` image used approximately 455 MB of disk space.
- A rebuild reused the cached restore layer.

## Tests and checks

```bash
dotnet build
dotnet test
npm run typecheck
npm run test --workspace apps/web
npm run lint
```

Last recorded results:

- 63 backend tests passed.
- 21 frontend tests passed.
- Docker health, authentication and protected endpoint checks passed manually.

Backend tests cover authentication, password hashing and seeding, protected requests, escalation rules, escalation history and token-derived actors.

Frontend tests cover login validation, login success, return-to-requested-route behavior, protected routes, Bearer token attachment, 401 handling and logout.

The tests do not cover production key rotation, external identity providers, load testing or deployment-specific browser security policies.

## AI usage

I used ChatGPT for code suggestions, explanations, test ideas, debugging support and learning unfamiliar concepts. I also used OpenCode to implement reviewed changes more efficiently, especially repetitive code and file updates.

A workflow that worked well was asking the AI to inspect the existing project before making changes. I then provided the relevant files and requested one small change at a time. After each change, I reviewed the diff, ran the relevant checks and continued only after the result was verified.

A useful prompt was:

> The login page has too much empty vertical space between the email field, password field and Sign in button, making the panel unnecessarily tall. Please inspect `apps/web/src/index.css` and adjust only the login-page styles so the fields keep a natural compact spacing without affecting the other forms. The fix should remain responsive and accessible.

I accepted the suggested centralized authentication approach largely as-is. The access token is kept in `sessionStorage`, attached to requests through `client.ts`, and removed automatically when a request returns 401. I chose this approach because it keeps authentication behavior in one place instead of duplicating it across React components.

One AI suggestion I changed was related to ticket search. When I noticed that an API request was firing for each keystroke during medium-speed typing, AI initially suggested that it might be an implementation error. After checking the Network panel and explaining that the pauses between keystrokes were longer than the original 300ms debounce delay, I increased the delay to 500ms and updated the related tests.

AI also helped identify an SLA query issue. A LINQ variable was initially named `group`, which caused a C# syntax error because `group` is a reserved query keyword. I renamed it to `bucket`. An SQL translation test initially depended on an internal EF Core projection alias, so I changed it to check stable SQL fragments instead.

Before committing, I verified the generated code using:

- `dotnet build`
- `dotnet test`
- `npm run typecheck`
- `npm run test --workspace apps/web`
- `npm run lint`
- `git diff --check`
- EF Core SQL translation tests
- Manual login and protected endpoint checks
- Manual SLA report checks
- Docker Compose startup with an empty database volume
- `/health` database connectivity verification
- Non-root container verification with `docker compose exec api whoami`
- Docker image-size and restore-cache checks
- Manual browser verification of the containerized API

## Known limitations

- The seeded credentials are local development fixtures and must not be reused in production.
- `sessionStorage` is readable by JavaScript and has an XSS exposure risk.
- Registration, password reset, email verification, OAuth/SSO, refresh-token rotation, MFA, account lockout and a roles-and-permissions matrix are not implemented.
- Docker Compose is intended for local development and assessment verification.
- Production deployments should use controlled migrations, external secret management and TLS termination outside the API container.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | Run `npm run db:up`, wait about 20 seconds, then check `docker compose ps`. |
| Port 1433 is already in use | Change the port in `docker-compose.yml` and the connection string. |
| Browser warns about the HTTPS certificate | Run `dotnet dev-certs https --trust`, or use the web app's API proxy. |
| Web app cannot load tickets | Confirm the API is running and `VITE_API_PROXY_TARGET` points to the API port. |