# Approach

## Task 1 — Defect fix and server-side filtering

- I traced the missing request to the ticket-list effect: it did not depend on every dropdown
  filter. I updated the hook to observe all filter fields, keep search debounced, and apply
  dropdown changes immediately.
- I kept filtering in the query as composable database predicates. That lets the count and the
  page use the same filtered set and avoids loading all tickets into application memory.
- Tests cover the reported request behavior, debounce, exact priority, combined filters and
  customer-name search. I also added a regression test after review found that an extreme page
  value could overflow the integer offset used by `Skip`.
- Limitation: the query tests use SQLite; they do not prove SQL Server-specific SLA predicate
  translation. I inspected the query composition but did not capture a SQL Server trace.

## Task 2 — Ticket triage, escalation and SLA

- I kept priority, SLA-window and assignment decisions in shared application logic so ticket
  creation and escalation use the same policy rather than duplicating rules.
- Category flags determine specialist and forced-Critical behavior; SLA windows, premium
  multiplier, minimum window and at-risk threshold are configuration.
- Assignment filters for active agents, required specialization and available capacity, then
  selects the eligible agent with the fewest open tickets and a deterministic ID tie-break.
  When none qualifies, creation succeeds unassigned with an explanation.
- Escalation recalculates the deadline from the escalation time, retains the current agent only
  if eligible, and records before/after values in history. SLA status is derived using the clock,
  not stored.
- I added the history table through an EF migration and indexed `TicketId` for history lookups.
- Tests cover the main triage, escalation and SLA boundaries. The SQL Server-specific SLA filter
  still needs provider-level integration coverage.

## Task 3 — Login and authentication

- Since each user is an existing agent, I chose a `PasswordHash` column on `Agent` rather than a
  separate Users table. Passwords are hashed with ASP.NET Core Identity's
  `PasswordHasher<Agent>`, including in development seeding.
- A central fallback authorization policy protects API controllers. Login and health are
  explicitly anonymous; escalation identity is taken from authenticated claims.
- The frontend keeps the JWT session in `localStorage` and centralizes bearer headers and 401
  cleanup in the shared API client. This is straightforward for the assignment and survives
  reloads, but JavaScript can read the token.
- Tests cover credential outcomes and frontend login/session behavior. An API-host test for
  protected endpoint 401/200 behavior remains a gap.

## Task 4 — Docker

- I used a multi-stage Dockerfile: restore follows copying project and shared build files, while
  source is copied later so ordinary code changes can reuse the restore layer. The runtime stage
  uses the ASP.NET image and a non-root user.
- Compose starts the API only after SQL Server is healthy, supplies connection and JWT settings
  at runtime, and publishes the API at `localhost:5080`.
- Development container startup migrates and seeds for a one-command local setup. For a real
  deployment, I would run a reviewed migration bundle or a dedicated migration job before API
  rollout to avoid concurrent schema changes and separate migration permissions from runtime.
- The README records the local health, seed, cache, image-size and non-root checks. These runtime
  checks are documented results; they were not rerun as part of writing this approach document.
