# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

### Run the API with Docker

From the repository root, run `docker compose up --build` (add `-d` to detach). Compose starts
SQL Server, waits for its health check, then builds and starts the API at
`http://localhost:5080`. The API container listens on HTTP port 8080 as a non-root user; the
image contains no HTTPS certificate. It receives its database connection string, JWT settings
and ASP.NET environment at runtime from Compose/.env, not from the image. The development
`appsettings.Development.json` is excluded from the published container. Application defaults
such as the configurable SLA policy remain in `appsettings.json`. Copy `.env.example` to `.env`
to customize local values.

Compose uses the Development environment so that the API applies EF migrations and seeds demo
data on an empty SQL volume. Check database reachability and readiness at
`http://localhost:5080/health` (HTTP 200 means healthy; 503 means the database is unavailable;
the JSON response includes the database check status).
The tickets endpoint requires a token: log in at `/api/auth/login`, then send its access token
as a Bearer token to `/api/tickets`.

For local web development against the container, run `npm run web` in another terminal with
`VITE_API_PROXY_TARGET=http://localhost:5080`. In PowerShell, use
`$env:VITE_API_PROXY_TARGET = 'http://localhost:5080'; npm run web`.

Automatic migrations are a local/development convenience. A real deployment should run a
reviewed migration bundle or dedicated migration job once, before rolling out API instances.
This avoids application instances racing to modify the schema, keeps schema-change permissions
out of the runtime identity, and makes migration approval and failure handling explicit.

Docker verification:

```bash
docker compose down -v                 # destructive: also deletes the local SQL data volume
docker compose up --build -d
curl --fail http://localhost:5080/health
docker compose exec api whoami         # should not print root
```

After login, verify seeded ticket data at `/api/tickets` using the returned Bearer token. To
verify the restore cache, change a `.cs` file, run `docker compose build --progress=plain api`,
and confirm the restore layer is reported `CACHED` while the later source-copy/publish steps
run again. Compare the final API and SDK image sizes with:

```bash
docker image inspect "$(docker compose images -q api)" --format '{{.Size}}'
docker image inspect mcr.microsoft.com/dotnet/sdk:10.0 --format '{{.Size}}'
```

For the verified local build, Docker reported an API runtime image of **442.2 MiB** and a .NET
SDK image of **1,243 MiB**; the SDK image is about **2.81× larger**. The clean-volume check
returned a healthy database check, seeded 40 tickets, rejected an unauthenticated tickets call,
and accepted it after login. `whoami` inside the API container returned `app`. The rebuild after
changing `Program.cs` showed `dotnet restore` as `CACHED`. `MSSQL_PORT` can be changed in `.env`
if local port 1433 is already occupied.

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
npm run lint
npm run typecheck
npm run build
```

- Backend and frontend tests run without a database.
- Backend tests cover triage rules, SLA boundaries, escalation decisions, authentication outcomes,
  and ticket filtering via SQLite.
- Frontend tests cover search debounce, immediate filter requests, SLA labels, escalation
  validation/server errors, login, token attachment, and 401 handling.
- Deliberately not covered yet: SQL Server integration for its SLA-specific query translation and
  API-host integration proving protected-endpoint 401/200 behavior.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. |
| `GET /api/tickets/{id}` | One ticket |
| `GET /health` | Database readiness; anonymous |
| `POST /api/tickets` | Create a ticket |
| `POST /api/tickets/{id}/escalate` · `GET /api/tickets/{id}/escalations` | Escalate and inspect immutable escalation history |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |
| `POST /api/auth/login` · `GET /api/auth/me` | Sign in and get the authenticated agent |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.
All API controller endpoints require a bearer token except login. Swagger remains publicly accessible
in Development; `GET /health` is also anonymous.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |

## Task 1 — Defect and filtering

- **Root cause:** the ticket-list effect omitted dropdown filter fields from its dependencies, so
  changing a filter did not trigger a request until another dependency changed.
- **Fix:** the list hook observes every filter; search waits 300 ms after typing, while dropdown
  changes request immediately. Pending debounce timers are cancelled when superseded or unmounted.

### Server-side filtering

- `TicketQueries.ApplyFilters` composes SQL-translatable exact matches for status, priority,
  category, customer and assigned agent; unassigned-only; trimmed title/reference/customer-name
  search; and SLA status. Conditions combine with AND before `CountAsync`, ordering and paging.
- Tests cover exact priority, combined filters, customer-name search, and immediate filter
  requests versus debounced search.
- **SQL verification:** I checked in code that the same filtered query feeds `CountAsync` and
  then ordering/paging; I did not capture a SQL Server trace. Development EF Core logging is
  available to inspect generated SQL.

## Task 2 — Ticket escalation and assignment

- Creation uses the requested priority or defaults to Medium; category flags can force Critical.
- Configured base SLA windows are Low 72 h, Medium 24 h, High 8 h, Critical 4 h. Premium
  customers use the configured 0.5 multiplier, with a 1 h floor; at-risk is configured at 25%.
- Assignment requires an active agent below capacity and, for specialist categories, the matching
  specialization. Fewest open tickets wins; ties use agent ID. No eligible agent means unassigned
  with an explanation, not a failed create.
- Create returns the applied priority, due date, assigned agent or unassigned reason, and triage
  explanations; the web UI displays that result.
- Escalation raises priority one step, recalculates due time from escalation time, retains an
  eligible current assignee or reassigns using the same rule, and records before/after history.
  Reasons must be 5–500 characters; Critical and Resolved/Closed tickets are rejected.
- SLA status is derived using an injectable clock and is never stored.
- `TicketEscalations.TicketId` has an index because ticket history is queried by ticket ID; the
  foreign key cascades when its parent ticket is deleted.
- The UI uses one SLA indicator in list/detail, supports SLA filtering, and shows escalation
  controls and history.

## Design decisions

### Alternatives considered

- **Filtering:** Apply predicates in SQL instead of loading tickets and filtering in memory; this
  keeps counts and pagination correct without transferring the full ticket set.
- **Credentials:** Store an Identity `PasswordHasher<Agent>` hash on the existing Agent instead
  of adding a separate Users table or custom/plain-text password storage; each login is an agent.
- **SLA status:** Derive it from due/start/resolution times and an injectable clock instead of
  storing a status that could become stale.
- **Triage:** Reuse the same SLA and assignment rules for create and escalation rather than
  maintaining separate implementations. Assignment respects active status, specialization,
  capacity and open-ticket workload; no eligible agent leaves the ticket unassigned with a reason.
- On escalation, keep the current agent only if they remain active, have the required
  specialization and have capacity (excluding the ticket being escalated); otherwise assign an
  eligible agent using the shared triage rule.
- **Manual reassignment:** After creation, the assignment dropdown disables inactive agents,
  agents at capacity, and—when the category requires a specialist—agents without that
  specialization. It shows open/maximum ticket counts to help identify eligible agents.
- **Token storage:** Use `localStorage` for this assignment so the session survives page reloads.
  It is accessible to JavaScript, so production should use a more secure session design and
  stronger XSS defenses.

## Task 3 — Agent authentication

- Agent credentials are stored as a nullable `PasswordHash` column on the existing `Agents`
  table because each login identity belongs to exactly one agent; the credential migration is
  `AddAgentPasswordHash`.
- Passwords use ASP.NET Core Identity's salted `PasswordHasher<Agent>`. Development seeding
  provisions every agent (including existing seeded databases that predate the column) with
  the local-only password `LocalDev123!`. For example, sign in as
  `alex.turner@northwind-support.example` / `LocalDev123!`. Inactive agents cannot sign in.
- `POST /api/auth/login` returns a signed JWT, expiry and agent identity. Unknown email and wrong
  password share the same 401 response. `GET /api/auth/me` returns identity claims.
- JWT signing key is configured under `Jwt:SigningKey` (override with
  `Jwt__SigningKey`); `.env.example` contains only an explicitly local development value.
  Access tokens last 60 minutes by default. Supply a managed secret and rotate it outside
  development.
- Escalation requests accept only a reason; `escalatedBy` is populated from the authenticated
  agent's token claims, not from client input.
- The web client provides `/login`, protects application routes, restores the intended route
  after sign-in, displays the signed-in agent and clears the session on logout or any API 401.
- The browser keeps the access token, expiry and agent display data in `localStorage` under
  `northwind-support-session`, and sends the token as a Bearer credential on API requests.
  This survives reloads but is readable by JavaScript, so an XSS flaw could expose the token.
  Keep the app free of untrusted scripts and use HTTPS outside local development.

## Task 4 — Docker

- Added a multi-stage .NET 10 API image with cached restore, running as non-root on port 8080.
- Compose waits for healthy SQL Server, passes configuration at runtime, and exposes the
  migrated, seeded API on `localhost:5080`. `/health` is database-aware and anonymous.
- The web proxy can target the container with `VITE_API_PROXY_TARGET`.
- Build, run, migration strategy and verification details are in
  [Run the API with Docker](#run-the-api-with-docker).

## AI usage

- **Tool and purpose:** Copilot in VS Code assisted with implementation, test writing, debugging
  and documentation review. I reviewed the affected code and ran automated checks.
- **Workflow that worked:** inspect the implementation and tests first, request a bounded change
  with a regression test, then run the narrow relevant test suite.
- **Accepted after review:** the shared triage service, after confirming both create and escalation
  use it for the same SLA and assignment decisions.
- **Adjusted after review:** AI-assisted investigation found that a very large page could overflow
  the integer offset passed to `Skip`. I kept the fix local to page normalization instead of
  changing the query pipeline, and added a regression test for the largest safe offset.
- **Verification:** backend/frontend tests, builds, lint and type checks; Docker checks are
  described above.

## Known limitations

- No refresh tokens, roles/permissions, account lockout or rate limiting. A 60-minute token
  expiry requires signing in again; any API 401 clears the browser session.
- Agent selection currently counts open tickets per candidate agent; this can be consolidated if
  the agent pool grows.
- The SQL Server-specific SLA filter and central authorization need API/SQL Server integration
  coverage. The optional SLA summary report is not built.
