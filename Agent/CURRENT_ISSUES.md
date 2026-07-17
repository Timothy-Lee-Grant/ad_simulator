# CURRENT ISSUES

_Last verified: 2026-07-16_

## Verification Summary
- Full Docker stack rebuilt and verified from a cold state: `postgres`, `redis`, `prometheus`, `grafana`, `bid-engine`, and `frontend` all build and run.
- `curl` checks confirmed:
  - `http://localhost:8081/api/bid/test` returns `BidEngine is running!`
  - `http://localhost:8081/api/videos` returns seeded video data
  - `http://localhost:3001/` returns the frontend homepage (`Featured Ads`)
  - `http://localhost:9090/-/healthy` returns `Prometheus Server is Healthy.`
  - `http://localhost:3000/login` returns `200 OK` for Grafana
- `scripts/e2e_smoke.sh` passes reliably on a warm stack (ran 4x consecutively).
- `dotnet test` (from repo root): **39 passed, 3 skipped, 0 failed** (42 total). Skipped tests are integration tests requiring a live DB/Redis and are expected to skip in a plain `dotnet test` run.
- `dotnet build`: 0 errors, 3 pre-existing style warnings (see below), no package version conflicts.

## Fixed This Session (2026-07-16)

1. **Critical: `bid-engine` crash-looped on startup — EF Core pending-model-changes exception**
   - Root cause: the attribution event pipeline (`AdEventLog`, `AdEventAggregate`) was added to `AppDbContext` in a prior session (2026-05-01) but no EF Core migration was ever generated for it. On boot, `db.Database.Migrate()` threw `PendingModelChangesWarning` as an unhandled exception, and the container crash-looped — meaning the *entire* backend, and therefore the frontend, has been non-functional since that session.
   - Fix: generated `src/BidEngine/Migrations/20260717021126_AddAdEventTables.cs` (`dotnet ef migrations add AddAdEventTables`), which creates the `ad_event_logs` and `ad_event_aggregates` tables. Verified the app now boots clean and both tables get created on a fresh Postgres volume.
   - **Lesson for future sessions:** whenever new `DbSet<T>` properties or entity classes are added to `AppDbContext`, immediately run `dotnet ef migrations add <Name>` in the same session/commit. This class of bug will keep recurring otherwise, since nothing catches it except an actual container boot.

2. **`scripts/e2e_smoke.sh` always reported the click metric as not incrementing**
   - Root cause: the script's `awk` filter only matched labeled Prometheus metrics (`ad_clicks_total{...}`), but `ad_clicks_total` is emitted unlabeled (`ad_clicks_total 1`). The regex never matched, so the script always read 0 regardless of the real value — a false negative, not a real product bug.
   - Fix: broadened the `awk` pattern to match both labeled and unlabeled forms.

3. **EF Core package version drift (`9.0.0` vs `9.0.2`)**
   - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` was pinned to `9.0.0` in `src/BidEngine/BidEngine.csproj` and `src/Shared/BidEngine.Shared.csproj`, while `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design` were `9.0.2`, causing a transitive `Microsoft.EntityFrameworkCore.Relational` conflict warning on every build.
   - Fix: bumped `Microsoft.AspNetCore.Identity.EntityFrameworkCore` to `9.0.2` in both projects. Confirmed via `dotnet restore --force` that the conflict warning is gone.

4. **Obsolete Compose `version: '3.8'` key in `docker-compose.kafka.yml`**
   - The main `docker-compose.yml` had already been cleaned up in a prior session; the Kafka overlay still had it. Removed it; combined `docker compose -f docker-compose.yml -f docker-compose.kafka.yml config` now validates with no warnings.

## Known, Not Fixed (deliberately left for a decision, not an oversight)

1. **Click-count race on cold start**
   - `FrontEnd/index.js`'s `/click` handler notifies BidEngine "fire and forget" — if that `fetch` fails, the error is logged and swallowed, and the user is redirected anyway (`FrontEnd/index.js:100-104`). Observed once during this session immediately after a fresh `docker compose up --build` recreated `bid-engine`: the smoke test's own click landed before the container had fully warmed up, and the click was silently dropped. On a warm stack this does not reproduce. This is the same "metrics reliability concern" already flagged in `Agent/PROJECT_MAP.md` — now confirmed live and time-correlated with cold starts, not just a theoretical risk.
2. **Security/secrets findings from `Agent/SECURITY_AUDIT.md` are still all present and unaddressed** (plaintext credentials in `.env`/`appsettings.json`, weak default Postgres/Grafana passwords, unauthenticated admin seed endpoints, open redirect on `/click`). None of these block local dev; all are pre-existing and already tracked in `Agent/ROADMAP.md` / `Agent/SECURITY_AUDIT.md`. Not touched this session since fixing them involves judgment calls (credential rotation, auth scope) that should go through the normal roadmap process rather than be silently patched.
3. **Style-only warnings** (unaffected by this session): `ASP0014` suggesting top-level route registration instead of `UseEndpoints` in `Program.cs`, and two `CS1998` async-without-await warnings. Cosmetic, non-blocking.

## Recommendation
- Adopt a rule: any PR/session that adds or changes an entity type touched by `AppDbContext` must include the matching EF Core migration, and CI should fail if `dotnet ef migrations has-pending-model-changes` (or equivalent build-time check) reports drift — this exact bug (item 1 above) will otherwise resurface silently every time.
- Consider making the `/click` → BidEngine notification synchronous-with-timeout-and-retry, or moving click recording to a durable queue, if click accuracy under cold-start/transient failure matters for the attribution pipeline's credibility as a portfolio piece.
