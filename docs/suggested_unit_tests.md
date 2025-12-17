# Suggested Unit Tests for ad_simulator

## 📋 Overview

This document lists a comprehensive, professional, and exhaustive set of unit tests I recommend adding to the `ad_simulator` repository. For each test I provide:
- A clear name you can copy into your test suite
- A concise description of what the test asserts
- The reason the test is necessary (what bug or regression it prevents)
- Setup/mocking guidance and important implementation notes
- Priority (High, Medium, Low)

I also include the testing philosophy and thought process that guided the list so you can extend the tests in the future with confidence.

---

## 🎯 Testing Philosophy & Thought Process

Good unit tests focus on: correctness, intent, edge cases, invariants, and interactions with external systems (via controlled test doubles). When writing tests:
- Test **behavior** (what the class/service should do), not internal implementation details.
- Cover **happy paths**, **edge/boundary conditions**, and **failure modes** (exceptions, nils, invalid input).
- Prefer small, single-purpose tests with descriptive names (Arrange / Act / Assert).
- Use mocks/fakes for external dependencies (DB, Redis, network) and confirm the right interactions happen (e.g., cache invalidation called).
- Avoid randomness and time-dependence in unit tests; make them deterministic by seeding or injecting time/random providers.

Prioritize tests that prevent regressions in business logic (budgeting, eligibility, bid selection) and those that guard critical side effects (database writes, cache invalidation).

---

## 🧰 Tools & Test Setup Recommendations

- Test framework: xUnit
- Assertion library: FluentAssertions (optional but improves readability)
- Mocking: Moq or NSubstitute
- Test data setup: builder/factory methods or AutoFixture for concise test object creation
- EF Core: prefer `Sqlite` in-memory mode for realistic relational behavior (use `EnsureCreated()` and stop tracking changes with a new context per test). Use `UseInMemoryDatabase` only for simple unit tests but prefer SQLite for verifying SQL behaviors.
- Redis: mock `IConnectionMultiplexer` and `IDatabase` using Moq/NSubstitute. For integration tests use a real redis container (TestContainers or GitHub Actions service).
- Randomness: consider adding a small `IRandom` abstraction so tests can inject deterministic seeds, or call internal selection logic with deterministic setup.
- CI: run `dotnet test --no-build --verbosity normal` in your pipeline; fail builds for test regressions.

---

## ✅ Tests by Component (suggested test cases)

Each section lists concrete tests for the component.

### 1) Campaign Model (BidEngine.Models.Campaign)

Goal: Validate the `CanServe` business invariant for daily & lifetime budgets and status.

- Test: `Campaign_CanServe_ReturnsTrue_WhenActiveAndWithinBudgets`
  - Description: New active campaign with SpentToday < DailyBudget and LifetimeSpent < LifetimeBudget (or LifetimeBudget == null) should return true.
  - Why: Ensures positive path remains correct; prevents regressions from inverted comparisons.
  - Setup: Create Campaign instances with appropriate fields.
  - Priority: High

- Test: `Campaign_CanServe_ReturnsFalse_WhenNotActive`
  - Description: Campaign with `Status` != "active" returns false.
  - Why: Guards the required check for paused/ended campaigns.
  - Priority: High

- Test: `Campaign_CanServe_ReturnsFalse_WhenSpentEqualsDailyBudget`
  - Description: SpentToday == DailyBudget should return false (no budget remaining).
  - Why: Boundary condition that often reveals off-by-one errors.
  - Priority: High

- Test: `Campaign_CanServe_ReturnsFalse_WhenLifetimeSpentEqualsLifetimeBudget`
  - Description: LifetimeSpent == LifetimeBudget should return false.
  - Why: Ensures lifetime budget logic is strict and consistent.
  - Priority: High

- Test: `Campaign_CanServe_TreatsNullLifetimeBudgetAsUnlimited`
  - Description: Null lifetime budget should not prevent serving (i.e., treated as unlimited).
  - Why: Confirms the intended behavior for campaigns without a lifetime cap.
  - Priority: Medium

### 2) CampaignCache (BidEngine.Services.CampaignCache)

Goal: Ensure correct cache hit/miss logic, JSON serialization behavior, and TTL/key invalidation.

- Test: `CampaignCache_GetCampaignAsync_ReturnsCachedValue_WhenPresent`
  - Description: If Redis returns JSON for `campaign::<id>` then DB should not be called and the deserialized Campaign returned.
  - Why: Confirms cache shortcut path and avoids superfluous DB queries.
  - Setup: Mock `IDatabase.StringGetAsync` to return JSON; assert `_dbContext` is not queried (or mock DB call and ensure it was not invoked).
  - Priority: High

- Test: `CampaignCache_GetCampaignAsync_StoresToCache_OnMiss`
  - Description: If Redis miss, DB returns campaign; service serializes campaign and stores it to Redis with `campaign::<id>` key and TTL.
  - Why: Verifies persistence to cache and usage of serialization options (cycle-safe) to avoid runtime serialization exceptions.
  - Setup: Mock `IDatabase.StringGetAsync` to return no value, provide a DB-backed campaign (e.g., Sqlite in-memory), assert `StringSetAsync` is called with JSON and correct TTL.
  - Priority: High

- Test: `CampaignCache_GetActiveCampaignsAsync_ReturnsCachedList_WhenPresent`
  - Description: If Redis returns JSON for `campaigns::active::all` then DB should not be hit and the list should be deserialized.
  - Why: Prevents unnecessary DB queries and verifies list serialization/deserialization.
  - Setup: Mock Redis `StringGetAsync` to return JSON list; assert DB query not invoked.
  - Priority: High

- Test: `CampaignCache_GetActiveCampaignsAsync_StoresToCache_OnMiss`
  - Description: On Redis miss, DB should be queried for active campaigns, the result serialized (with ReferenceHandler.IgnoreCycles), stored in Redis, and returned.
  - Why: Prevents regressions in storing the active campaign set and ensures cycle-safe serialization.
  - Priority: High

- Test: `CampaignCache_InvalidateCampaignAsync_DeletesKeys`
  - Description: When invalidating, both `campaign::<id>` and `campaigns::active::all` should be deleted from Redis.
  - Why: Ensures cache coherence after changes to campaign budgets/metadata.
  - Setup: Spy on `IDatabase.KeyDeleteAsync` to assert two keys are deleted.
  - Priority: High

- Test: `CampaignCache_Serialization_HandlesCyclesWithoutThrowing`
  - Description: Ensure that a Campaign with Ads referencing Campaign (bi-directional) serializes successfully (no exception thrown).
  - Why: Regression guard for the earlier circular serialization runtime error.
  - Priority: High

### 3) BidSelector (BidEngine.Services.BidSelector)

Goal: Validate filtering (CanServe), targeting rule matching, selection logic, and failure modes.

- Test: `BidSelector_SelectWinningBidAsync_ReturnsNull_WhenNoActiveCampaigns`
  - Description: When `GetActiveCampaignsAsync` returns empty list, the selector returns null.
  - Why: Avoids NPEs and ensures the no-bid flow is explicit.
  - Setup: Mock `CampaignCache.GetActiveCampaignsAsync` to return empty list.
  - Priority: High

- Test: `BidSelector_SelectWinningBidAsync_FiltersOutCampaigns_ThatCannotServe`
  - Description: Campaigns with `CanServe == false` get filtered out and not considered.
  - Why: Ensures budget/status checks are respected early.
  - Priority: High

- Test: `BidSelector_SelectWinningBidAsync_FiltersByTargetingRules`
  - Description: Campaigns with targeting rules not matching the request should be excluded (country/device rules specifically).
  - Why: Core correctness for targeted bids.
  - Setup: Build campaigns with `TargetingRules` and a `BidRequest` with mismatching country/device and assert null or exclusion.
  - Priority: High

- Test: `BidSelector_MatchesTargetingRules_ReturnsTrue_WhenNoRules`
  - Description: Campaigns without rules match all requests.
  - Why: Documented behavior: no rules means universal matching.
  - Priority: Medium

- Test: `BidSelector_SelectWinningBidAsync_SelectsHighestCpm_WhenMultipleEligible`
  - Description: When multiple eligible campaigns exist, the one with highest `CpmBid` is chosen.
  - Why: Ensures selection logic orders by CPM correctly.
  - Setup: Two eligible campaigns with different CpmBid; assert winner is the one with higher bid.
  - Priority: High

- Test: `BidSelector_SelectWinningBidAsync_ReturnsNull_WhenWinningCampaignHasNoAds`
  - Description: If highest-bid campaign has zero ads, it should be treated as no valid winner and null returned (and logged appropriately).
  - Why: Prevents returning invalid bid responses.
  - Priority: Medium

- Test: `BidSelector_SelectWinningBidAsync_ReturnsAdAndCampaignInfo_WithDeterministicAdSelection_WhenSeededRandom`
  - Description: Verify a chosen ad and the `BidResponse` contents (AdContent fields, BidPrice, CampaignId). If randomness cannot be injected, test by making a single ad to avoid randomness.
  - Why: Verifies the response shape and fields are correct.
  - Priority: High

- Test: `BidSelector_MatchesTargetingRules_IsCaseInsensitive`
  - Description: country/device matching should be case-insensitive.
  - Why: `StringComparison.OrdinalIgnoreCase` must be honored.
  - Priority: Medium

### 4) BudgetService (BidEngine.Services.BudgetService)

Goal: Ensure budget deduction logic is correct, side effects (DB save & cache invalidation) happen, and failed deductions are reverted.

- Test: `BudgetService_DeductBudgetAsync_ReturnsFalse_WhenCampaignNotFound`
  - Description: If DB returns null for the campaign Id, method returns false and logs error.
  - Why: Prevents null-reference and incorrect success responses.
  - Priority: High

- Test: `BudgetService_DeductBudgetAsync_SucceedsAndPersists_WhenBudgetAvailable`
  - Description: When budgets allow deduction, SpentToday and LifetimeSpent are increased by cost-per-impression, DB.SaveChangesAsync is called, and `CampaignCache.InvalidateCampaignAsync` is called for the campaign id.
  - Why: Verifies critical side effects and ensures cache coherence.
  - Setup: Use Sqlite in-memory DB, insert a campaign, call DeductBudgetAsync; assert DB values changed and that `InvalidateCampaignAsync` was called once.
  - Priority: High

- Test: `BudgetService_DeductBudgetAsync_ReturnsFalse_AndReverts_WhenDailyBudgetExceeded`
  - Description: If deducting would exceed DailyBudget, then the method returns false and DB changes aren't saved (or are rolled back), and no invalidation occurs.
  - Why: Prevents incorrectly going over daily budget and saves state integrity.
  - Priority: High

- Test: `BudgetService_DeductBudgetAsync_ReturnsFalse_AndReverts_WhenLifetimeBudgetExceeded`
  - Description: Similar to daily budget but for lifetime budget.
  - Why: Prevents lifetime overspend.
  - Priority: High

- Test: `BudgetService_DeductBudgetAsync_HandlesExceptions_ReturnsFalse`
  - Description: Simulate DB or cache exceptions and ensure method catches exceptions and returns false.
  - Why: Robustness and defensive programming.
  - Priority: Medium

- Test: `BudgetService_ResetDailyBudgetAsync_SetsSpentToZero_ForAllCampaigns`
  - Description: After call, every campaign's SpentToday should equal 0 and UpdatedAt should be updated.
  - Why: Verifies scheduled maintenance behavior.
  - Priority: Medium

### 5) BidController (BidEngine.Controllers.BidController)

Goal: Validate input validation, status codes, and integration with `BidSelector` and `BudgetService`.

- Test: `BidController_EvaluateBidsAsync_ReturnsBadRequest_WhenMissingUserIdOrPlacementId`
  - Description: If request has null or empty `UserId` or `PlacementId`, return 400 with correct metric label increment.
  - Why: Guards API contracts and early validation.
  - Priority: High

- Test: `BidController_EvaluateBidsAsync_ReturnsNoContent_WhenNoWinningBid`
  - Description: If `BidSelector` returns null, the controller returns 204 and increments `no_bid` metric.
  - Why: Ensures controller maps domain result to HTTP semantics.
  - Priority: High

- Test: `BidController_EvaluateBidsAsync_ReturnsServiceUnavailable_WhenBudgetDeductionFails`
  - Description: If `BudgetService.DeductBudgetAsync` returns false, controller returns 503.
  - Why: Ensures the area acknowledging side-effect failures is surfaced to callers properly.
  - Priority: High

- Test: `BidController_EvaluateBidsAsync_ReturnsOkAndCallsBudgetService_WhenSuccess`
  - Description: On success, the controller should call `DeductBudgetAsync` once and return 200 with the `BidResponse` body.
  - Why: Confirms the overall happy path wiring.
  - Priority: High

- Test: `BidController_TestEndpoint_ReturnsOk`
  - Description: GET `/api/bid/test` returns 200 "BidEngine is running!".
  - Why: Basic health check for smoke tests.
  - Priority: Low

### 6) Misc / Cross-cutting tests

- Test: `Serialization_Options_AreUsed_Correctly_InCache`
  - Description: Force objects with cycles and assert cache write does not throw and contains valid JSON.
  - Why: Ensure `ReferenceHandler.IgnoreCycles` remains in place.
  - Priority: High

- Test: `Concurrency_DeductBudgetAsync_HandlesConcurrentRequestsSafely` (Integration)
  - Description: Simulate multiple concurrent calls to `DeductBudgetAsync` and assert invariants (not exceeding budgets).
  - Why: Critical for production correctness in high-throughput environments.
  - Priority: Medium (integration-level test)

- Test: `Query_Includes_ActiveCampaigns_LoadsAdsAndTargetingRules`
  - Description: Verify that the code that queries campaigns includes Ads and TargetingRules as expected (use SQLite to run actual LINQ query and assert navigation properties are populated).
  - Why: Guards EF Core query correctness and prevents lazy-loading surprises.
  - Priority: Medium

---

## 🧩 Designing Deterministic Tests for Random and Time-Based Behavior

- Random Ad selection: wrap `Random` in an `IRandom` interface (or accept a seeded `Random` via constructor) so tests can inject deterministic behavior. Until then, test by creating campaigns with a single ad or test the set membership instead of exact selection.
- Time: for time-sensitive assertions (UpdatedAt), allow injecting clock or use an assertion that checks `UpdatedAt` changed and is within a reasonable time window (e.g., within 5 seconds) rather than an exact match.

---

## 🧪 Integration vs Unit Tests — What to Keep Where

- Unit tests: all behavioral tests using mocks for the DB and Redis. Fast, isolated, and deterministic.
- Integration tests: tests that verify EF Core mappings, transaction behavior, and concurrency using SQLite in-memory or a real DB container; Redis integration tests should use an actual redis instance in CI using TestContainers or GitHub Actions service containers.

Aim for fast unit tests as the bulk of your suite and a smaller number of integration tests that exercise the real persistence layers.

---

## 🧭 Test Naming & Organization Pattern

- Folder: `tests/BidEngine.Tests/` grouped by subject: `Models`, `Services`, `Controllers`, `Integration`
- Class naming: `CampaignTests`, `CampaignCacheTests`, `BidSelectorTests`, `BudgetServiceTests`, `BidControllerTests`
- Test naming: `MethodName_StateUnderTest_ExpectedBehavior` (e.g., `DeductBudgetAsync_WhenDailyBudgetExceeded_RevertsAndReturnsFalse`).

---

## 💡 Tips for Implementation

- Use Type-safe builders for test objects, e.g., `CampaignBuilder.WithDailyBudget(10).WithSpentToday(0).Build()` to keep test setups readable.
- Use `AutoFixture` for nullable fields but keep explicit values for tests asserting boundary behavior.
- For mocking `IDatabase` (StackExchange.Redis), mock `StringGetAsync`, `StringSetAsync`, and `KeyDeleteAsync`. Validate call counts and key names.
- For EF Core `DbContext`, prefer a fresh real `Sqlite`-in-memory DB per test to catch LINQ translation and relational edge cases.
- Log verification: if you want to assert that certain warnings/errors are logged, use a test logger (e.g., a custom `ILogger` test harness) and assert log messages exist for certain flows.

---

## 📈 Coverage & Prioritization

Start with High priority tests (model invariants, caching, selection, budgeting, controller input validation). These will cover the core business logic and prevent the most serious regressions.

- SLO: aim for 70–80% test coverage in the critical service layer (`Services/`), with lower coverage acceptable for trivial DTOs.
- Keep tests small and fast: most unit tests should run in sub-100ms each.

---

## ✅ Next Steps (Suggested Implementation Plan)

1. Add core unit tests (High priority group) for `Campaign`, `CampaignCache`, `BidSelector`, `BudgetService`, and `BidController`.
2. Add integration tests for `BudgetService` and `CampaignCache` using SQLite and a Redis test container.
3. Add a small concurrency integration test to verify `DeductBudgetAsync` invariants under parallel load.
4. Add CI jobs to run unit & integration tests (integration tests can be optional or gated separately to speed up PR feedback).

---

## Appendix: Example Test Skeletons (xUnit + Moq)

```csharp
// Example: Campaign_CanServe_ReturnsTrue_WhenActiveAndWithinBudgets
[Fact]
public void Campaign_CanServe_ReturnsTrue_WhenActiveAndWithinBudgets()
{
    var c = new Campaign
    {
        Status = "active",
        DailyBudget = 100,
        SpentToday = 50,
        LifetimeBudget = 1000,
        LifetimeSpent = 200
    };

    c.CanServe.Should().BeTrue();
}
```

```csharp
// Example: CampaignCache_GetCampaignAsync_StoresToCache_OnMiss
[Fact]
public async Task GetCampaignAsync_StoresToCache_OnMiss()
{
    var db = new Mock<IConnectionMultiplexer>();
    var redisDb = new Mock<IDatabase>();
    db.Setup(d => d.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDb.Object);
    redisDb.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(RedisValue.Null);

    // Setup sqlite db context with a campaign...
    // Call GetCampaignAsync and assert redisDb.StringSetAsync was called and returned campaign.
}
```

---

If you'd like, I can now generate a PR with skeleton test files (only tests; no production code changes) following the above plan, or I can stop here and let you implement the tests yourself. Which do you prefer? ✅

---

*Document created by GitHub Copilot (Raptor mini (Preview)) — recommended tests updated on 2025-12-17.*
