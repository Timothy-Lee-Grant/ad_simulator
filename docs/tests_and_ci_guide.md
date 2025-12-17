# Tests, CI, and PR Guide for ad_simulator 🧪🔁

## 📌 Purpose
This guide explains everything added in the `tests/BidEngine.Tests` project, the pull request I created, and the GitHub Actions CI workflow I added. It's written for an entry-level software engineer and includes step-by-step commands and next steps you can follow to continue development.

---

## 🔍 What I added (short summary)
- A set of **unit tests** under `tests/BidEngine.Tests/` that validate core business logic (campaign eligibility, caching, budget deduction, bid selection, and controller wiring).
- Two **integration test skeletons** under `tests/BidEngine.Tests/Integration/` (skipped by default) for Redis and concurrency checks.
- A **GitHub Actions workflow** `.github/workflows/ci.yml` that runs unit tests on push/PR and contains an optional integration job using service containers (Redis + Postgres) that can be run manually.
- A **branch** named `test/add-high-priority-unit-tests` and a PR with these changes (the PR will automatically update as new commits are pushed to that branch).

---

## 🗂️ What is in `tests/BidEngine.Tests` (file-by-file)
This test project uses **xUnit**, **Moq** for mocking, and **FluentAssertions** for readable asserts.

- `Models/CampaignTests.cs`
  - Tests the `Campaign.CanServe` business rule (daily/lifetime budgets and status). These are small, deterministic unit tests.

- `Services/CampaignCacheTests.cs` ✅ *recently added*
  - Verifies cache hit/miss behavior, that database results are stored in Redis, and that serialization is safe for objects with cycles.

- `Services/BidSelectorTests.cs`
  - Verifies that bid selection: filters campaigns that cannot serve, respects targeting rules, and chooses the highest CPM.

- `Services/BudgetServiceTests.cs`
  - Verifies budget deduction behavior, rollback on failures, and daily reset logic.

- `Controllers/BidControllerTests.cs`
  - Tests controller-level behavior: input validation, HTTP results (400, 204, 503, 200) and the flow that involves `BidSelector` and `BudgetService`.

- `Integration/CampaignCacheIntegrationTests.cs` and `Integration/BudgetServiceConcurrencyTests.cs`
  - Skeletons for integration tests. They are annotated with `[Fact(Skip="...")]` so they do not run in normal unit test runs. They are designed to run against real Redis/Postgres resources.

- `BidEngine.Tests.csproj`
  - Contains test dependencies and project references.

---

## 🧠 Quick explanation: Unit tests vs Integration tests
- **Unit tests**: fast, isolated, deterministic. They test one class or method at a time using **mocks** or in-memory databases (e.g., EF InMemory). Run in milliseconds.
- **Integration tests**: slower and exercise multiple components together (e.g., use real Redis/Postgres or Docker service containers). They catch issues that unit tests miss (e.g., SQL translation, transaction behavior).

In this project, most tests are unit tests; integration tests are present but skipped by default and will be run optionally using the CI workflow or locally when you have service dependencies available.

---

## ✅ How to run tests locally (step-by-step)
Assumes you have .NET SDK installed (at least .NET 9) and a terminal open in the repo root.

1. Run all tests (unit + integration skipped):

```bash
# from repo root
dotnet test --no-build --verbosity normal
```

2. Run a single test or test class (useful when developing or debugging):

```bash
# run tests in a given class
dotnet test --filter "FullyQualifiedName~BidEngine.Tests.Services.CampaignCacheTests"

# run a single test by method name
dotnet test --filter "DisplayName~GetCampaignAsync_StoresToCache_OnMiss"
```

3. Run integration tests locally (manual steps)
- Option A: Use Docker Compose (recommended if project repo includes a compose file):

```bash
# start required services (Redis, Postgres from the repo's docker compose)
docker compose up -d redis postgres

# set env vars so tests know how to reach services
export REDIS_URL=redis://localhost:6379
export POSTGRES_CONNECTION='Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=ad_sim_test'

# ensure integration tests are not skipped (remove the Skip attr or run specific filter if you structured them)
dotnet test --filter "FullyQualifiedName~Integration" --verbosity normal
```

- Option B: Use GitHub Actions integration job (manual trigger) — see the *CI* section below.

---

## 🔁 The Pull Request (what I did and what it means)
- Branch created: **`test/add-high-priority-unit-tests`**
- PR opened with the tests, docs updates, and CI workflow. The PR will automatically include future commits pushed to the same branch.

What you can do with the PR:
- Review the changed files in GitHub (Files changed tab).
- Run the CI actions (they will run automatically on PR creation). The **Actions** tab shows logs and results.
- Respond to code review comments by pushing commits to the same branch; the PR updates automatically.

If you need to get the branch locally:

```bash
git fetch origin
git checkout -b test/add-high-priority-unit-tests origin/test/add-high-priority-unit-tests
```

To add more changes and update the PR:

```bash
# edit files
git add <files>
git commit -m "tests: add more coverage for X"
git push origin test/add-high-priority-unit-tests
```

---

## 🛠️ GitHub Actions CI — what I added and how it works
Path: `.github/workflows/ci.yml`

### Jobs
- **unit-tests**
  - Runs on push and on PRs.
  - Does: checkout, restore, build, run `dotnet test` (integration tests are skipped by default).
- **integration-tests** (optional, manual)
  - Runs only when you manually trigger the workflow and set `run_integration=true` in the UI.
  - Starts service containers for Redis and Postgres and runs tests in the `Integration` namespace.

### How to run the integration job from GitHub UI
1. Go to **Actions** → select the CI workflow. Click **Run workflow** on the right.
2. Set `run_integration` to `true` and click **Run workflow**.
3. Check logs for `integration-tests` job and wait for the results.

### Notes & tips
- The integration job provides `REDIS_URL` and `POSTGRES_CONNECTION` env vars for tests to use.
- Integration jobs can take longer and may flake on shared CI runners — keep them gated (manual) until they are stable.

---

## 🧩 How the tests are written (patterns & tips)
- Pattern: Arrange → Act → Assert.
  - Arrange: set up inputs, mocks, in-memory DB state.
  - Act: call the method under test.
  - Assert: verify results and side effects (DB changes, cache invalidation calls, returned DTOs).

- Use **Moq** to mock external dependencies (e.g., `IConnectionMultiplexer`, `IDatabase`).
- When testing EF behavior, prefer **SQLite in-memory** if you need realistic SQL behavior; `UseInMemoryDatabase` is OK for simple tests.
- Make tests deterministic: inject seeded random or clock abstractions where possible.
- Keep tests small and focused (one assertion or one behavior per test).

Example skeleton:

```csharp
[Fact]
public async Task DeductBudgetAsync_Succeeds_WhenBudgetAvailable()
{
    // Arrange: create in-memory DB and campaign with budget

    // Act: call BudgetService.DeductBudgetAsync

    // Assert: check DB persisted values and cache invalidation was called
}
```

---

## ✅ Suggested next tasks (prioritized)
1. **Add test helper utilities (high priority)**
   - Create `tests/BidEngine.Tests/Helpers/` with common builders, in-memory DB setup, and Redis test helpers.
   - Add a deterministic `IRandom` and `IClock` test fakes.

2. **Add additional high/medium priority tests** from `docs/suggested_unit_tests.md` (Campaign edge cases, selection, budget failure modes).

3. **Stabilize integration tests**
   - Make integration tests idempotent and seed DB in job.
   - Optionally use TestContainers for platform-independent runs in CI.

4. **Add a README snippet** under `docs/tests_and_ci_guide.md` (or in `README.md`) that describes how to run tests locally and how to toggle integration tests.

5. **Add a CI gate or label** that requires the integration job to pass before merging high-risk changes (optional, depending on team policy).

---

## 📌 Troubleshooting & FAQs (quick)
- Tests failing with serialization/cycle exceptions?
  - Use `ReferenceHandler.IgnoreCycles` when serializing objects for caching. Our `CampaignCache` uses this already.

- Unit test fails that passed locally but failed in CI?
  - Check logs in **Actions**, ensure environment variables are present, and look for timing/race conditions.

- How to debug a single test in VS Code?
  - Use the C# extension and Test Explorer; set breakpoints and run the test in debug mode.

---

## Final notes
- If you'd like, I can add the test helpers and a short `README` snippet to this repo now, and I can add a small example integration test that seeds Redis/Postgres and validates a full end-to-end flow.
- Let me know which next task you'd like me to do and I will proceed.

---
*Document created by GitHub Copilot (Raptor mini (Preview)) on 2025-12-17.*
