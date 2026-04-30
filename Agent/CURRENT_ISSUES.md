# CURRENT ISSUES

## Verification Summary
- Full Docker stack verified: `postgres`, `redis`, `prometheus`, `grafana`, `bid-engine`, and `frontend` are all running.
- `curl` checks confirmed:
  - `http://localhost:8081/api/bid/test` returns `BidEngine is running!`
  - `http://localhost:3001/` returns the frontend HTML homepage
  - `http://localhost:9090/-/healthy` returns `Prometheus Server is Healthy.`
  - `http://localhost:3000/login` returns `200 OK` for Grafana
- Database and cache connectivity confirmed:
  - `docker compose exec postgres pg_isready -U postgres -d ads_db` passed
  - `docker compose exec redis redis-cli ping` returned `PONG`
- `scripts/e2e_smoke.sh` executed successfully.
- `dotnet test --no-restore` succeeded with:
  - total: 24, passed: 21, skipped: 3
  - skipped tests are integration-focused and expected in this local verification context.

## Findings
- The refactored `BidSelector`/`IBiddingStrategy` architecture builds and runs.
- All major runtime services are healthy and responsive.
- Unit test suite now passes after updating tests to use the new `IBiddingStrategy`-based `BidSelector` constructor.

## Issues / Action Items
1. **Frontend startup transient error**
   - Recent `frontend` logs include `npm error signal SIGTERM` during startup.
   - The container currently remains `running` and `http://localhost:3001/` is reachable, but this startup behavior should be investigated and stabilized.

2. **EF Core package version warning**
   - `dotnet test` reports a version conflict between `Microsoft.EntityFrameworkCore.Relational 9.0.0` and `9.0.2`.
   - This is a warning only, but package versions should be aligned to remove dependency drift.

3. **ASP.NET analyzer warning**
   - `src/BidEngine/Program.cs` triggers `ASP0014`: suggest using top-level route registration instead of `UseEndpoints`.
   - Not fatal, but worth cleaning up for modern ASP.NET Core style.

## Recommendation
- Investigate the frontend container startup path and determine whether a clean `npm start` lifecycle or Compose healthcheck signal is causing the SIGTERM.
- Align EF Core package references in project files to a single consistent version.
- Consider switching `Program.cs` to top-level route registration to eliminate the analyzer warning.
