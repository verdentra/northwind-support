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
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

## Current implementation status

The repository currently provides ticket creation, list and detail views, status and assignment
updates, and customer, agent and category reference data. In Development, the API applies database
migrations and seeds demo data at startup.

The Task 1 defect (DEFECT-117, filter changes not triggering a request) is fixed in the web app.
Of the list filters, only `slaStatus` (Task 2) is applied in the database so far; the Task 1.2
filters (`search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`,
`unassignedOnly`) are still accepted but not applied. Task 2 - automatic triage, escalation with
immutable history, all five SLA statuses including `AtRisk`, the SQL-side SLA filter and the UI for
all of it - is implemented; see [Task 2](#task-2-ticket-escalation--assignment) below.
Task 3 - agent sign-in with hashed passwords, JWT access tokens and every endpoint closed by
default - is implemented; see [Task 3](#task-3-login-and-authentication). Task 4 - a multi-stage
API image and an `api` service in Docker Compose - is implemented; see [Task 4](#task-4-docker).
The SLA summary report is not implemented.

## Architecture

The API is split into four projects with one-way dependencies:

| Project | Responsibility |
| --- | --- |
| `SupportDesk.Domain` | Ticket, customer, category and agent models; business rules for priority, SLA, assignment and escalation. It has no application or database dependency. |
| `SupportDesk.Application` | Use-case handlers, request/response contracts, validation and abstractions such as repositories, the clock and current user. |
| `SupportDesk.Infrastructure` | EF Core mappings and migrations, SQL Server repositories, read-side projections, password hashing and development seeding. |
| `SupportDesk.Presentation` | ASP.NET Core controllers, authentication/authorization setup, configuration, middleware and API startup. |

For ticket creation, a controller delegates to an application handler. The handler loads the
required domain entities, applies the shared domain policies, updates the aggregate and commits
through the unit of work. Ticket list and detail reads use separate Infrastructure query
projections: EF Core joins labels and selects DTOs in SQL rather than loading full aggregates.
List filtering is intended to happen before count, sort and paging; currently only `slaStatus` is
implemented there, as noted above.

The web client lives in `apps/web`. Feature pages use typed API modules in `src/api`, which share a
single request client for JSON, bearer-token attachment and API errors. Ticket-list state and
filtering are owned by `useTicketList`; only search is debounced. Authentication state is managed
by `AuthProvider`, which restores the tab session, protects routes and clears the session after an
authenticated request receives `401`.

The backend unit tests are in `apps/api/tests`; architecture tests check layer dependencies,
authentication defaults and EF model/migration consistency. The web tests use Vitest and React
Testing Library. SQL-side filter behavior has not yet been verified against a running SQL Server;
the current tests do not replace that integration check.

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

Task 2 implementation order
1. Configuration and domain rules: configurable SLA windows, category flags, priority escalation rules, and SLA boundaries.
2. Automatic triage: apply priority, calculate the due date, select an eligible agent, and explain the decision.
3. Escalation and history: persist immutable before/after records through an EF Core migration.
4. API and filtering: add escalation endpoints and the SQL-side slaStatus filter.
5. React UI: reusable SLA indicator, filter, triage result, escalation panel, and history.
6. Tests and documentation: backend and frontend tests, README.md, and APPROACH.md.

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

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

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
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. `slaStatus` is applied in SQL; **the other filters are accepted but not applied yet** (Task 1.2). |
| `GET /api/tickets/{id}` | One ticket, including `slaStatus` and `canBeEscalated` |
| `POST /api/tickets` | Create and triage a ticket. `201` with the ticket plus a `triage` object |
| `POST /api/tickets/{id}/escalate` | Escalate one priority level. `200`, `400`, `404` or `409` |
| `GET /api/tickets/{id}/escalations` | Escalation history, newest first |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |
| `POST /api/auth/login` | Sign in; returns a JWT, its expiry and the agent. The only open endpoint |
| `GET /api/auth/me` | The signed-in agent, or `401` |
| `GET /health` | `200` / `503` with `{ status, checks: { database } }`. Open, no token |

Every endpoint except `POST /api/auth/login` (and Swagger) needs `Authorization: Bearer <token>`.
Errors are `ProblemDetails`: `400` validation, `401` not signed in / bad credentials, `404` not
found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Task 2: Ticket escalation & assignment

### Rules as implemented

| Rule | Behaviour |
| --- | --- |
| Priority (BR-1) | The requested priority, or **Medium** when none is sent. A category with `ForcesCriticalPriority` (Security, Outage in the seed) is always **Critical**, whatever was requested. |
| SLA window (BR-2, BR-3) | Due = start + base window for the final priority. Premium customers get the window x `PremiumCustomerMultiplier` (0.5). No window is ever shorter than `MinimumWindowHours` (1 h); the floor applies to every tier. |
| Assignment (BR-4, BR-5) | Eligible = active, fewer open tickets (not Resolved/Closed) than `MaxOpenTickets`, and - for a `RequiresSpecialist` category - specialised in it. Fewest open tickets wins; ties go to the **lowest agent id**. Nobody eligible is a normal outcome: the ticket is created unassigned (still `201`) with the reason. |
| Escalation (BR-7) | Exactly one level up (Low > Medium > High > Critical). The due date is recomputed **from the moment of escalation** with the new priority and the customer's tier, and that moment becomes the new SLA window start. The owner is kept if still eligible, otherwise re-chosen with the same assignment rule, otherwise left unassigned. One immutable history row per escalation. |
| SLA status (BR-8) | Derived, never stored, with an injectable clock. No due date: `NotApplicable`. Resolved at or before due: `Met`; after: `Breached`. Unresolved and strictly past due: `Breached`. Unresolved, not past due, with <= 25% of the window left: `AtRisk` (so exactly at the due instant is `AtRisk`, one tick later `Breached`). Otherwise `WithinSla`. |

### Configuration

The numbers live only in the `Sla` section of
`apps/api/src/SupportDesk.Presentation/appsettings.json` (moved there from
`appsettings.Development.json` in Task 4: they are business rules, not environment settings, and the
Docker image deliberately ships without the Development file):

```json
"Sla": {
  "BaseWindowHours": { "Low": 72, "Medium": 24, "High": 8, "Critical": 4 },
  "PremiumCustomerMultiplier": 0.5,
  "MinimumWindowHours": 1,
  "AtRiskThresholdPercent": 25
}
```

It is bound to `SlaOptions` and validated **at start-up** (`ValidateOnStart`): a missing priority,
a multiplier outside (0, 1], a non-positive minimum or a threshold outside 0-100 stops the API with
a message naming the key. Any value can be overridden the usual .NET way, e.g.
`Sla__BaseWindowHours__High=6`. Category
behaviour comes only from the `RequiresSpecialist` / `ForcesCriticalPriority` flags; no category
name appears in code.

### API

`POST /api/tickets` returns the same fields as `GET /api/tickets/{id}` (so existing clients keep
working) plus:

```json
"triage": {
  "priorityReason": "Security tickets are always Critical (Low was requested).",
  "slaReason": "Critical priority has an SLA window of 4 h, reduced to 2 h (x0.5) for a Premium customer. Due 2026-10-10 11:30 UTC.",
  "assignmentReason": "Assigned to Priya Nair: fewest open tickets (4 of 8) among 1 eligible agent(s)."
}
```

`POST /api/tickets/{id}/escalate` with `{ "reason": "..." }`:

| Status | When |
| --- | --- |
| `200` | Escalated. Body: `{ ticket, escalation, assignmentReason }` - the updated ticket, the history row written, and why the owner was kept/changed/cleared. |
| `400` | Reason not 5-500 characters (counted after trimming). |
| `404` | Unknown ticket. |
| `409` | The ticket is Resolved/Closed ("... is Resolved and cannot be escalated. Reopen it first ...") or already Critical ("... is already Critical, the highest priority ..."). |

`escalatedBy` is not in the request: since Task 3 it is the signed-in agent's name, taken from the
access token.

`GET /api/tickets/{id}/escalations` returns the history newest first: from/to priority, from/to
agent (id and name, or null), from/to due date, reason, who and when. `404` for an unknown ticket.

`GET /api/tickets?slaStatus=AtRisk` (or `WithinSla`, `Breached`, `Met`, `NotApplicable`) filters in
SQL, before the count, sort and page.

### Design decisions

- **Rules in small domain units shared by create and escalate.** `TicketPriorityRules` (default,
  forced Critical, one-step raise), `SlaPolicy` (window, premium multiplier, floor, at-risk) and
  `AgentAssignmentPolicy` (eligibility, fewest-open, tie-break, keep-current-owner) are plain,
  dependency-free classes in the domain, unit-tested on their own. The two handlers only load what
  the rules need and record the outcome, so create and escalate cannot drift apart.
  *Rejected:* putting the rules in the handlers (duplicated between create and escalate, and only
  testable through mocks); a `ITriageService` interface (no second implementation - an interface
  would not be a real seam).
- **Policy object (Strategy-like) built from options.** `SlaOptions` is bound and validated at
  start-up, then turned once into an immutable `SlaPolicy` singleton. Domain code never sees
  configuration types, and tests build the same policy from the same options class.
- **The ticket aggregate owns escalation.** `Ticket.Escalate(...)` checks the 409 rules, raises the
  priority, restarts the SLA window, applies the chosen owner and appends the `TicketEscalation`
  row, all in one method, so there is no way to change priority without writing history. The
  handler calls `EnsureCanBeEscalated()` first so a rejected request does no further work.
- **Immutable history.** `TicketEscalation` has no setters outside its constructor and no
  behaviour; `SupportDbContext` additionally refuses to save a modified or deleted escalation.
- **Keeping the current owner.** When re-checking an owner, the escalated ticket is already in
  their open count; it is left out when judging their capacity, because keeping a ticket they
  already hold adds nothing. An owner at exactly their limit is therefore kept; one *over* it (only
  possible through the manual assignment override) is replaced.
- **Window start is stored, status is not.** "25% of the window remaining" needs to know when the
  window started, which after an escalation is no longer the creation time. Tickets get a nullable
  `SlaStartedAtUtc`; when it is null (existing rows, the SQL seed script) the window is measured from
  `CreatedAtUtc`, which is exactly when those windows started, so no back-fill is needed.
  *Rejected:* deriving the start from the latest escalation row (a correlated subquery on every list
  read); storing the status (it changes with time and with configuration).
- **One evaluator, one SQL translation, the same arithmetic.** `SlaStatusFilter` is the evaluator's
  branches rewritten as SQL-translatable predicates. The at-risk test compares
  `DATEDIFF(second, now, due) * 100` with `threshold * DATEDIFF(second, start, due)` in floating point
  (no overflow), and `SlaEvaluator` counts whole seconds the same way DATEDIFF does, so a ticket
  filtered as `AtRisk` is always displayed as `AtRisk`. "Now" is truncated to milliseconds (the
  precision of the `datetime2(3)` columns) and the same value is used for filtering and display.
- **Response shape.** `RaisedTicketDto` extends the existing detail DTO with `triage`, so the create
  response stays compatible. The detail DTO gains `canBeEscalated`, computed from the same domain
  rule (`Ticket.IsEscalatable`), so the UI can disable the control without duplicating the rule.

### Database

Migration `20261010063000_AddTicketEscalations`:

- `Tickets.SlaStartedAtUtc datetime2(3) NULL`.
- `TicketEscalations`: `Id` (identity PK), `TicketId` (FK to Tickets, required), `FromPriority`,
  `ToPriority` (int, check constraint 1-4), `FromAgentId`, `ToAgentId` (nullable FKs to Agents),
  `FromDueAtUtc` (nullable), `ToDueAtUtc` (required), `Reason nvarchar(500)`,
  `EscalatedBy nvarchar(100)`, `EscalatedAtUtc datetime2(3)`.
- **Delete behaviour:** `Restrict` everywhere. Tickets and agents are never deleted by the
  application (agents are deactivated), and an accidental delete should fail rather than silently
  remove the audit trail.
- **Index:** `IX_TicketEscalations_TicketId_EscalatedAtUtc (TicketId, EscalatedAtUtc DESC)`. The only
  read is "this ticket's history, newest first"; this index seeks on the ticket and returns rows
  already in that order (no sort), and it doubles as the index for the `TicketId` foreign key. The
  two agent foreign keys get EF's conventional single-column indexes so an agent delete check does
  not scan the history.
- `db/schema.sql` and `db/seed.sql` drop/clear `TicketEscalations` first when it exists, so the
  reference scripts still run against a migrated database.

The migration is applied automatically when the API starts in Development (`npm run api`). To apply
it by hand, or to check it:

```bash
npm run db:migrate                                   # dotnet ef database update
npm run ef -- migrations list                        # should list ...AddTicketEscalations
npm run ef -- migrations script InitialCreate AddTicketEscalations   # review the SQL
```

`SupportDesk.ArchitectureTests` fails if the EF model and the migrations ever disagree.

### Web app

- `SlaIndicator` is the single SLA badge, used by the ticket list (`SlaCell`), the ticket summary on
  the detail page and the triage result. Each state has its own text, icon and tone ("Within SLA" ◷,
  "At risk" ⚠, "Breached" ✕, "Met" ✓, "No SLA" –), so none relies on colour.
- The filter bar has an **SLA status** select, sent as `slaStatus` through the existing
  `useTicketList` hook.
- The detail page has an **Escalate** panel: a reason field validated with the server's rule
  (trimmed, 5-500) before any request - the escalating agent comes from their sign-in - a pending state, field errors from a
  `400`, and an explicit "Not escalated" message for a `409`. On success the ticket is replaced with
  the server's response and the history is re-fetched, without a page reload; on failure nothing
  changes. The panel is disabled, with the reason, when `canBeEscalated` is false (Resolved, Closed
  or Critical), and still handles a server rejection.
- **Escalation history** lists each escalation with priority, agent and due-date changes, reason,
  who and when.
- After **Raise ticket**, the result card shows the applied priority, due date and SLA badge, the
  assigned agent or **Unassigned**, and the reason for each.

### Tests

```bash
npm test                                               # everything: dotnet test, then Vitest
dotnet test                                            # backend only (no database or Docker needed)
dotnet test --filter "FullyQualifiedName~SupportDesk.UnitTests"
npm run test --workspace apps/web                      # web only
npm run typecheck && npm run lint && npm run build     # web static checks and build
```

Backend (xUnit + Moq, `FixedClock`, `TicketBuilder`, `TicketCommandTestContext`):

- `SlaPolicyTests`, `SlaOptionsTests` - every base window, the premium halving, the 1 h floor
  (below and exactly at it), configuration validation.
- `SlaEvaluatorTests` - every status and the exact boundaries: resolved at due / one tick after,
  one second before the at-risk point / exactly at it, exactly at due (`AtRisk`) / one tick after
  (`Breached`), window measured from the escalation, whole-second counting.
- `TicketPriorityRulesTests` - default Medium, requested priority, forced Critical, one-step raise.
- `AgentAssignmentPolicyTests` - fewest open, deterministic tie-break, inactive, at/below limit,
  specialists only when required, nobody eligible (with reasons), keeping or replacing an owner.
- `TicketEscalateTests` - each transition, due date from escalation time and tier, owner change,
  every history value, Critical/Resolved/Closed rejected without side effects.
- `RaiseTicketCommandHandlerTests`, `EscalateTicketCommandHandlerTests` - the handlers apply the
  rules, save once, return the triage / history row, 404s, and refuse early.
- `EscalateTicketRequestValidatorTests` - reason length limits after trimming, missing actor.
- `SlaStatusFilterTests` - every SLA filter translates to SQL with the real SQL Server provider
  (no database opened).

Web (Vitest + React Testing Library): too-short reason never calls the API; a `409` is shown and
the ticket and history are left unchanged; success refreshes the ticket and history; pending and
disabled states; every SLA state renders a distinct label, icon and tone.

Not tested automatically: the SQL filters against a real SQL Server (no integration-test database
in this repo - the boundary arithmetic is shared and unit-tested instead), and concurrent
escalations of the same ticket.

### Known limitations (Task 2)

- No optimistic concurrency: two people escalating the same ticket at the same moment could both
  succeed from the same starting priority. A `rowversion` on `Tickets` would turn the second into a
  409.
- Workload counts are read, then the ticket is saved; two tickets raised at the same instant could
  both go to an agent with one slot left. Acceptable at this scale; a serializable check or a
  per-agent counter would close it.
- Triage reasons are English sentences built on the server, not localised.

## Task 3: Login and authentication

### Development login

Every seeded agent signs in with their seed email and the password **`LocalDev-Only-Pa55!`** - a
local fixture, set by the Development seeder only and hashed per agent at seed time. For example
`sara.lindqvist@northwind-support.example`. `yuki.tanaka@northwind-support.example` is inactive and
is refused, which shows the inactive rule. A database seeded before this change gets the
credentials on the next start-up (the seeder fills in any agent without a hash).

### How it works

| Area | Choice |
| --- | --- |
| Credential storage (AU-1) | A nullable `PasswordHash nvarchar(256)` column on `Agents` (migration `20261010080000_AddAgentCredentials`). Agents are the only users and each has exactly one credential, so a separate `Users` table would be a 1:1 join with nothing of its own; an agent without a hash simply cannot sign in. *Rejected:* a Users table (worth it only with non-agent users or several credentials per person). |
| Hashing (AU-2) | ASP.NET Core Identity's `PasswordHasher` on its own (no Identity tables): PBKDF2-HMAC-SHA512, 100,000 iterations, a random 128-bit salt per password, with the parameters stored in the hash. Wrapped in `IdentityPasswordHasher` behind `IPasswordHasher`. |
| Sign-in (AU-4) | `POST /api/auth/login` `{ email, password }` returns `{ accessToken, expiresAtUtc, agent: { id, fullName, email } }`. Unknown email, wrong password, no credentials and inactive agent all give the **same 401 and message** ("Email or password is incorrect."), and an unknown email still costs a full hash verification, so neither the message nor the timing reveals which accounts exist. |
| Token | HS256 JWT with `sub` (agent id), `name`, `email`, `jti`; issuer and audience checked; 30 s clock skew. **Lifetime: 60 minutes** (`Jwt:TokenLifetimeMinutes`), no refresh token - sign in again after it. |
| Current user (AU-5) | `GET /api/auth/me` re-reads the agent, so one deactivated after signing in gets `401` there even with an unexpired token. |
| Central authorization (AU-6) | A **fallback policy** requiring an authenticated user applies to every endpoint; only `AuthController.Login` has `[AllowAnonymous]` (an architecture test fails if anything else does). Swagger is middleware, not an endpoint, so it stays open, with an **Authorize** button for the token. An unauthenticated call gets a `401` problem document. |
| Signing key (AU-7) | `Jwt:SigningKey`, from configuration or the `Jwt__SigningKey` environment variable (in `.env.example`); validated at start-up (at least 32 bytes). Development uses an obviously fake key in `appsettings.Development.json`, like the existing local SA password; `appsettings.json` has none, so any other environment must supply one. No key is in code. |
| Actor (AU-8) | `escalatedBy` is removed from the escalate request; the handler takes the agent's name from the token through `ICurrentUser`. |

### Web app

- `/login` (AU-9): labelled email and password fields, client validation (no request when
  invalid), a pending state, and "Email or password is incorrect." on a `401`.
- Token storage (AU-10): the token is held in memory by `api/client.ts`, which attaches it to every
  call, and saved in **`sessionStorage`** so a reload keeps you signed in. Closing the tab ends the
  session and other tabs do not share it. The trade-off: any script on the page can read
  `sessionStorage`, so an XSS bug could steal the token for up to its lifetime. An HttpOnly,
  SameSite cookie would hide it from scripts but needs CSRF protection and same-site hosting; for
  this app's scope the short lifetime plus no third-party scripts was the simpler choice.
- Protected routes (AU-11): everything except `/login` is wrapped in `RequireAuth`, which sends a
  visitor to `/login` and returns them to the page they asked for (or the ticket list) afterwards.
- The header shows "Signed in as ..." with **Log out** (AU-12), which clears the session.
- Any `401` from an authenticated call (AU-13), e.g. an expired token, clears the session and
  redirects to `/login`, then back. A `401` from the sign-in call itself just means wrong
  credentials and is shown on the form.

### Tests

- `LoginCommandHandlerTests`: right password issues a token; wrong password, unknown email,
  inactive agent and no credentials all give the same 401 with no token; unknown email still
  verifies a hash.
- `IdentityPasswordHasherTests` (real PBKDF2): right password passes, others fail, hashes are
  salted and never contain the password, null or malformed hashes fail.
- `GetCurrentAgentQueryHandlerTests`: signed in, not signed in, deactivated after signing in.
- `AuthenticationTests` (architecture project): the fallback policy denies anonymous users, only
  `Login` allows anonymous access, an issued token validates and carries the agent, expired and
  tampered tokens are rejected, a short key fails validation.
- Escalation tests: the actor comes from the signed-in agent; anonymous is refused.
- Web: unauthenticated visitors are redirected to sign in and returned afterwards; invalid input
  sends no request; a refused sign-in shows the error; a saved session is restored and an expired one
  ignored; the token is attached to calls; a `401` clears the session and redirects.

### Production changes (not done here)

A key from a secret store and rotated (or asymmetric keys so the API only needs the public half),
HTTPS only, an HttpOnly cookie or a backend-for-frontend instead of script-readable storage, short
access tokens with refresh tokens, sign-in rate limiting and lockout, and audit logging of
sign-ins. Registration, password reset, MFA and roles are out of scope.

## Task 4: Docker

### Run it

```bash
cp .env.example .env                 # PowerShell: Copy-Item .env.example .env
docker compose up --build            # SQL Server + the API; migrates and seeds on an empty volume
```

| What | Where |
| --- | --- |
| API | http://localhost:5080 (Swagger at `/swagger`) |
| Health | http://localhost:5080/health |
| Web app against the container | `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`, then http://localhost:5173 (PowerShell: `$env:VITE_API_PROXY_TARGET="http://localhost:5080"; npm run web`) |

Sign in with the [development login](#development-login). `docker compose down -v` wipes the
database volume; the next `up` migrates and seeds it again.

### How it is built

- **Multi-stage `apps/api/Dockerfile`**, built from the repository root (DK-1). The SDK stage first
  copies only `global.json`, `Directory.Build.props`, `Directory.Packages.props` and the four API
  `.csproj` files and runs `dotnet restore`, then copies the source and publishes with
  `--no-restore`. Restore is its own layer, so changing a `.cs` file reuses it. The final stage is
  `mcr.microsoft.com/dotnet/aspnet:10.0`: runtime only, no SDK or source.
- **`.dockerignore`** (DK-2) keeps out `bin/`, `obj/`, `node_modules/`, `.git/`, `.env`, the web
  app, the tests, and every `appsettings.Development.json` and `launchSettings.json`.
- **Non-root, HTTP 8080, no certificates** (DK-3): `USER $APP_UID` (the `app` user the .NET images
  provide), `ASPNETCORE_HTTP_PORTS=8080`. TLS belongs to whatever sits in front of the container.
- **No configuration or secret in the image** (DK-4). The connection string, the JWT key and
  `ASPNETCORE_ENVIRONMENT` come from environment variables set by Compose from `.env`. Without
  `Jwt__SigningKey` the API stops at start-up with a message naming it. The only settings file in the image is
  `appsettings.json`, which holds logging defaults and the SLA rules (business rules, identical in
  every environment - moved there from the Development file for this reason).
- **`api` service** (DK-5): built from the Dockerfile, published on `localhost:5080`, connecting to
  `Server=sqlserver,1433` by service name, and started only once the SQL Server health check
  passes (`depends_on: condition: service_healthy`).
- **Migrations on start-up** (DK-6) happen because Compose runs the container as `Development`,
  which applies migrations and seeds demo data, exactly like `npm run api`.
- **`GET /health`** (DK-7) is a standard ASP.NET Core health check: `DatabaseHealthCheck` opens a
  connection to the database. `200 {"status":"Healthy","checks":{"database":"Healthy"}}` or `503`.
  It is mapped with `.AllowAnonymous()` in `Program.cs`, next to the fallback policy that closes
  everything else, and reports no connection details.

### Migrations in a real deployment

Applying migrations when the app starts is a development convenience. In production it would be
a separate, deliberate pipeline step - an EF Core migration bundle
(`dotnet ef migrations bundle`) or a reviewed SQL script (`dotnet ef migrations script --idempotent`)
run once before the new version rolls out. Reasons: several replicas starting together would race
to migrate; the app's runtime database login should not need DDL rights; a failed migration should
stop a release, not crash-loop the containers; and schema changes deserve review and a rollback
plan. The demo seed data would not exist in production at all.

### Verification

Run these to check each requirement (the figures below are to be filled in from your own run):

| Check | Command | Expected |
| --- | --- | --- |
| Clean start | `docker compose down -v` then `docker compose up --build` | API logs "Seeded ..." ; `curl http://localhost:5080/health` gives `Healthy` |
| Seeded data | `POST /api/auth/login`, then `GET /api/tickets` with the token | 200 with 40 tickets (unauthenticated: 401) |
| Restore cache | Edit any `.cs` file, `docker compose build api` | The `RUN dotnet restore` step shows `CACHED` |
| Image size | `docker images supportdesk-api` and `docker images mcr.microsoft.com/dotnet/sdk:10.0` | Record the measured runtime and SDK image sizes after building; not measured in this environment because Docker is unavailable. |
| Not root | `docker compose exec api whoami` | `app` |

Docker-specific checks (clean startup, seeded API response, restore-layer reuse, image sizes and
runtime user) could not be executed in this environment because the Docker CLI is unavailable.
The table gives the commands and expected results for a Docker-enabled machine; no container
build or smoke-test result is claimed here.

### Production changes (not done here)

Pin base images by digest and rebuild regularly for patches; read the JWT key and connection
string from a secret store rather than environment variables; run as `Production` with migrations
as a pipeline step; add a container `HEALTHCHECK` (or orchestrator probes) on `/health`; terminate
TLS at a reverse proxy or ingress; consider a chiseled or distroless runtime image for a smaller
attack surface.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
