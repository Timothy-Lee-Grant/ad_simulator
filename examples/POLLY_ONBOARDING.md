
# Polly (App-vNext/Polly) — Deep Onboarding & Reference

This document is an exhaustive onboarding reference for the App-vNext/Polly repository (v8 API surface). It is intended for engineers who want to master Polly for production use, study the internals, run the samples, contribute to the project, or migrate from older Polly versions.

Contents (quick navigation)
- **Part A — Overview & high-level architecture**
- **Part B — Key concepts & terminology**
- **Part C — Packages & versioning**
- **Part D — Quick start examples**
- **Part E — DI integration & advanced samples**
- **Part F — Core API reference (selected classes & methods)**
- **Part G — Strategy configuration reference**
- **Part H — Internals: code structure & implementation notes**
- **Part I — Tests, specs & testing helpers**
- **Part J — Building, benchmarking & running samples**
- **Part K — Contributing, release process & community**
- **Part L — Migration notes (v7 → v8)**
- **Part M — Performance, diagnostics & telemetry**
- **Part N — Common pitfalls & troubleshooting**
- **Part O — Appendix: sample code, checklists, glossary**

For maintainers: this doc references source files that existed on the `main` branch at the time of authoring. When in doubt, open the file under `src/Polly` or consult `pollydocs.org` for the canonical docs.

---

PART A — Overview & high-level architecture
===========================================

What Polly is
- Polly is a comprehensive resilience and transient fault-handling library for .NET applications. It provides composable resilience strategies (Retry, Circuit Breaker, Timeout, Hedging, Rate Limiter, Bulkhead, Fallback, Caching, etc.) that you can combine into pipelines.

Why Polly matters
- In distributed systems downtime, transient failures, slow responses and overloads are common. Polly codifies resilience best practices and provides tested strategies for dealing with these scenarios.

High-level architecture
- The repository is organized into several projects under `src/`:
    - `Polly` — the main public API surface and default implementations. This contains the builder types, policy abstractions (Policy, PolicyBase, PolicyBuilder), strategy implementations and glue code that constructs pipelines.
    - `Polly.Core` — core abstractions used across the Polly ecosystem (in older versions this might be combined with `Polly`).
    - `Polly.Extensions` — DI and telemetry integration helpers to wire resilience pipelines into `IServiceCollection`.
    - `Polly.RateLimiting` — integration with `System.Threading.RateLimiting` APIs for proactive rate limiting.
    - `Polly.Testing` — testing helpers to facilitate deterministic tests for retry/circuit-breaker behaviours.
    - `samples/` — runnable samples demonstrating common and advanced scenarios.
    - `test/` — unit and behavioural tests (xUnit, Shouldly, etc.).

Design principles
- Composition: strategies are composed into pipelines via the `ResiliencePipelineBuilder` fluent API.
- Observability: strategies emit events, and `Polly.Extensions` integrates telemetry hooks.
- Testability: `Polly.Testing` provides deterministic testing helpers to simulate failures and timing behaviours.

Deployment / packaging
- Polly is delivered via several NuGet packages: `Polly.Core`, `Polly.Extensions`, `Polly.RateLimiting`, `Polly.Testing` and a legacy `Polly` package.
- The repo maintains build scripts (`build.cake`, `build.ps1`), a slnx solution and CI workflows.

Security / license
- Licensed under BSD-3-Clause. See `LICENSE` in the root for terms.

PART B — Key concepts & terminology
=================================

- Resilience pipeline: A sequence of strategies (policies) that a call is executed through. Implemented by `ResiliencePipeline` and constructed with `ResiliencePipelineBuilder`.
- Resilience strategy: An individual policy/behavior such as retry, timeout or circuit breaker.
- Outcome/Outcome<TResult>: Represents either a result or exception (used for generic pipelines that inspect results in addition to exceptions).
- PredicateBuilder / Predicate: Used to declare which results or exceptions a strategy should handle.
- Hedging: Executing multiple actions in parallel to mitigate high tail latency.
- ManualControl / StateProvider: Types used to observe and control circuit breaker state programmatically.

PART C — Packages & versioning
==============================

Top-level packages
- `Polly.Core` — core abstractions, builders and essential strategies; intended to be the minimal dependency for programmatic resilience.
- `Polly.Extensions` — adds DI helpers (`AddResiliencePipeline`) and telemetry integration.
- `Polly.RateLimiting` — integrates with the runtime `System.Threading.RateLimiting` API for sliding window, token bucket and concurrency limiting.
- `Polly.Testing` — utilities to emulate failures and time progression in tests.
- `Polly` — compatibility wrapper exposing pre-v8 APIs for legacy consumers.

Versioning notes
- Major versions are significant (v7 → v8 introduced API changes). The repo maintains separate documentation for v7 compatibility.
- Releases include changelogs; always read release notes for breaking changes when upgrading.

PART D — Quick start examples
=============================

Minimal programmatic pipeline (non-DI)
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;

static async Task Main()
{
        var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(1)
                })
                .AddTimeout(TimeSpan.FromSeconds(5))
                .Build();

        await pipeline.ExecuteAsync(async token =>
        {
                // Simulated call
                Console.WriteLine("Trying...");
                await Task.Delay(100, token);
                throw new InvalidOperationException("Simulated failure");
        }, CancellationToken.None);
}
```

Using DI (Polly.Extensions)
```csharp
var services = new ServiceCollection();

services.AddResiliencePipeline("http-pipeline", builder =>
{
        builder.AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2 })
                     .AddCircuitBreaker(new CircuitBreakerStrategyOptions { FailureRatio = 0.5, SamplingDuration = TimeSpan.FromSeconds(10) });
});

var sp = services.BuildServiceProvider();
var provider = sp.GetRequiredService<ResiliencePipelineProvider<string>>();
var pipeline = provider.GetPipeline("http-pipeline");

await pipeline.ExecuteAsync(async token => { /* http call */ }, CancellationToken.None);
```

Samples worth running
- `samples/Intro` — simplest example to exercise `ResiliencePipelineBuilder`.
- `samples/Retries` — examples demonstrating retry strategies and backoff.
- `samples/DependencyInjection` — shows how to register pipelines.
- `samples/Chaos` — demonstrates `Simmy`-style chaos injection along with pipelines.

PART E — DI integration & advanced samples
=========================================

AddResiliencePipeline helper
- `AddResiliencePipeline("name", builder => { ... })` registers a named pipeline in `IServiceCollection` and hooks up a `ResiliencePipelineProvider` to resolve it.

Key behaviors when integrating
- Pipelines are typically registered as singletons; they are thread-safe and meant to be reused.
- You can use keyed services to maintain multiple typed pipelines with different generics (e.g., `ResiliencePipeline<T>`).

Advanced sample patterns
- Generic pipelines: `ResiliencePipeline<T>` supports handling outcomes of type `T` and lets you configure `ResultPredicates`.
- Extending Polly: `samples/Extensibility` shows how to implement a custom strategy and plug it into the builder API.

PART F — Core API reference (selected classes & methods)
========================================================

Note: This is a pragmatic reference based on the code under `src/Polly`. For full API, consult the XML docs or IntelliSense in the IDE.

ResiliencePipelineBuilder
- Purpose: Fluent builder to compose strategies and produce a `ResiliencePipeline`.
- Common methods:
    - `AddRetry(RetryStrategyOptions options)` — adds retry behavior.
    - `AddTimeout(TimeSpan timeout)` / `AddTimeout(TimeoutStrategyOptions)` — adds timeout.
    - `AddCircuitBreaker(CircuitBreakerStrategyOptions options)` — adds circuit-breaker behavior.
    - `AddRateLimiter(RateLimiterStrategyOptions options)` — if RateLimiting package present.
    - `AddFallback(FallbackStrategyOptions<TResult> options)` — fallback strategy.
    - `Build()` — build a `ResiliencePipeline` instance.

ResiliencePipeline
- Purpose: Execution surface for invoking an action through the configured strategies.
- Execution methods:
    - `ExecuteAsync(Func<CancellationToken, ValueTask> callback, CancellationToken ct)`
    - `ExecuteAsync<TResult>(Func<OutcomeArguments<TResult>, CancellationToken, ValueTask<Outcome<TResult>>> callback, CancellationToken ct)` — generic outcome-aware execution.

Policy / PolicyBase
- `Policy` is an abstract base for synchronous policies; `PolicyBase` contains common logic used by both sync & async variants.

PolicyBuilder / PolicyBuilder<TResult>
- Used for specifying exception or result predicates when creating policies via older-style API.
- Internals: `ExceptionPredicates`, `ResultPredicates<TResult>` are used to collect the conditions a policy should handle.

PredicateBuilder / Predicate types
- Used across many strategy options to express which exceptions or results should be handled. Examples: `.Handle<HttpRequestException>()`, `.HandleResult(r => r.StatusCode == HttpStatusCode.InternalServerError)`.

Manual control & state providers
- `CircuitBreakerManualControl` and `CircuitBreakerStateProvider` are available to programmatically inspect and alter circuit breaker state.

PART G — Strategy configuration reference (detailed)
====================================================

RetryStrategyOptions
- Fields you will commonly set:
    - `MaxRetryAttempts` (int)
    - `BackoffType` (enum: Fixed, Exponential)
    - `Delay` (TimeSpan, base delay)
    - `UseJitter` (bool)
    - `DelayGenerator` (Func<RetryDelayGeneratorArguments, ValueTask<TimeSpan?>>) — advanced custom delay
    - `ShouldHandle` (PredicateBuilder) — which exceptions/results to handle
    - `OnRetry` (delegate for telemetry/side-effects)

CircuitBreakerStrategyOptions
- Common fields:
    - `FailureRatio` (double 0..1) — ratio of failures to total attempts over sampling window
    - `SamplingDuration` (TimeSpan) — sliding window for measuring failures
    - `MinimumThroughput` (int) — ignore windows with low throughput
    - `BreakDuration` (TimeSpan) or `BreakDurationGenerator` — duration to keep the circuit open
    - `StateProvider` — observe circuit state
    - `ManualControl` — programmatically isolate/close circuit

TimeoutStrategyOptions
- Typical fields:
    - `TimeoutGenerator` (Func<TimeoutGeneratorArguments, ValueTask<TimeSpan>>)
    - `OnTimeout`
    - `TimeoutOptions` that choose cooperative cancellation vs hard timeout

RateLimiterStrategyOptions
- Integrates with `System.Threading.RateLimiting` constructs and supports `ConcurrencyLimiter`, `SlidingWindowLimiter`, etc.

FallbackStrategyOptions
- Fields:
    - `FallbackAction` — function to provide an alternative result
    - `ShouldHandle` — predicate specifying when fallback should kick in
    - `OnFallback` — side-effect when fallback occurs

HedgingStrategyOptions
- Key fields:
    - `MaxHedgedAttempts`
    - `Delay` between hedged attempts
    - `ActionGenerator` — choose how to create hedged actions

PART H — Internals: code structure & implementation notes
=========================================================

Where code lives (map)
- `src/Polly` — core public API and many strategy implementations.
- `src/Polly/Retry` — retry family implementations and helper types.
- `src/Polly/CircuitBreaker` — circuit breaker file(s) and state management.
- `src/Polly/Timeout` — timeout strategy implementations.
- `src/Polly/RateLimit` — rate limiting integration.
- `src/Polly/Registry` — registry types for named/resolvable strategies.

Key source files to read (recommended order)
1. `Policy.cs`, `PolicyBase.cs` — core abstractions for policies.
2. `PolicyBuilder.cs` — how the library configures predicates.
3. `ResiliencePipelineBuilder` (file under `src/Polly`) — builder implementation showing how strategies are stored and executed in sequence.
4. Strategy folders like `Retry` and `CircuitBreaker` — concrete behavior.
5. `samples/` and `test/` — usage and behavioural verification.

Execution model (conceptual)
- Pipeline creation: The builder collects a list of strategy factories and their configuration objects.
- Execution: The pipeline composes strategies into an execution delegate that invokes each strategy's execute wrapper in the correct order (e.g., outermost policy calls inner execution which calls the user delegate).
- Outcome handling: For generic pipelines, outcome objects carry result/exception and metadata; strategies decide whether to handle based on predicates.

Thread-safety and performance
- Pipelines and policies are designed to be reused across threads; avoid per-call allocations by reusing pipelines.
- Pay attention to allocations in `OnRetry` handlers — keep them low-latency.

Internal helpers
- `DelegateResult`, `ResultPredicate`, `ExceptionPredicates` — utilities used widely to implement predicate evaluation.

PART I — Tests, specs & testing helpers
=====================================

Testing philosophy
- The project contains unit tests and behaviour tests verifying strategy semantics and edge-cases (e.g., half-open recovery, jitter correctness, timeout cancellation behavior).

Polly.Testing
- Provides deterministic time advancement and failure injection to make tests reliable and fast.

Where to look
- `test/` — test projects. Look for `Polly.Specs` or `Polly.Tests` for behaviour-driven tests.
- Use `Polly.Testing` to emulate clock and timeouts and to assert retry attempts without waiting.

Writing effective tests
- Use `Polly.Testing` helpers to fake the clock and network responses.
- Assert state transitions for circuit breakers using `CircuitBreakerStateProvider`.

PART J — Building, benchmarking & running samples
===============================================

Prerequisites
- .NET SDK (matching the `global.json` SDK version in repo). The repo periodically updates .NET target; check `global.json`.
- Optional: Cake build (if you prefer the repo build scripts), or simply use `dotnet build` and `dotnet test`.

Build steps (local quick start)
```bash
git clone https://github.com/App-vNext/Polly.git
cd Polly
dotnet build Polly.slnx
dotnet test ./test/Polly.Tests/Polly.Tests.csproj
```

Running samples
```bash
cd samples/Intro
dotnet run --project Intro.csproj
```

Benchmarking
- Benchmarks may exist under `bench/` — run with `dotnet run` or via BenchmarkDotNet harness as described in repo docs.

CI and code quality
- The repo uses GitHub Actions for builds and CodeQL for security scanning. Mutation testing (Stryker) is used to monitor test quality.

PART K — Contributing, release process & community
=================================================

Contributing guidelines
- See `CONTRIBUTING.md` in the repo root. Important points include code style, adding tests for behavior changes, and following the branching and PR guidelines.

Release process
- The project maintains tags and changelogs for each release. Releases typically contain breaking changes notices and migration steps.

Community
- Many contributors and maintainers are active; issues and PRs should follow templates. Sponsorships are documented in the README.

PART L — Migration notes (v7 → v8)
=================================

High-level differences
- The v8 API introduces `ResiliencePipeline` and builder patterns with more explicit pipeline semantics. Legacy `Policy`-style APIs are available under compatibility packages.

Common migration steps
1. Replace usage of `PolicyWrap` and `Policy<T>` with `ResiliencePipeline` where appropriate.
2. Translate configuration objects: e.g., `RetryStrategyOptions` replaces older overloads and puts more emphasis on delay generators and jitter.
3. Tests: update `Polly.Testing` usage if the test harness changed; verify behavior of circuit-breaker thresholds.

Code examples (migration)
- Example converting a v7 Retry policy to v8 pipeline

PART M — Performance, diagnostics & telemetry
===========================================

Observability
- Use `OnRetry`, `OnTimeout`, `OnFallback` events to emit telemetry to your logging/tracing system.
- `Polly.Extensions` provides telemetry integration hooks — wire them into your `OpenTelemetry` or `ApplicationInsights` pipelines.

Performance tuning
- Prefer building pipelines once and reusing them.
- Avoid expensive allocations in event callbacks.
- Use `RateLimiter` strategies to protect downstream systems under load and reduce tail latencies.

Benchmarking tips
- Use `bench/` harness or integrate BenchmarkDotNet to measure the overhead of strategies and different composition orders.

PART N — Common pitfalls & troubleshooting
=========================================

Pitfall: forgetting to honor CancellationToken in your callbacks
- Timeouts rely on cooperative cancellation. Ensure your downstream calls accept and respect `CancellationToken`.

Pitfall: creating pipelines per request
- Pipelines are thread-safe; instantiate them once at startup to avoid allocations and state thrash.

Pitfall: misconfigured jitter/backoff leading to hot loops
- Use `UseJitter = true` with exponential backoff for distributed systems to avoid thundering retries.

Troubleshooting tips
- If circuit breaker seems to open too frequently, inspect `FailureRatio`, `MinimumThroughput`, and `SamplingDuration` settings.
- Use unit tests with `Polly.Testing` to reproduce timing-related issues deterministically.

PART O — Appendix: sample code, checklists, glossary
===================================================

Checklist for adding Polly to a microservice
1. Add `Polly.Core` to the microservice project.
2. Define a named pipeline for each category of downstream call (e.g., `http-pipeline`, `db-pipeline`).
3. Use DI to provide `ResiliencePipelineProvider`.
4. Register health checks that integrate circuit breaker state if desired.
5. Add telemetry hooks to `OnRetry` / `OnFallback` to emit useful metrics.

Glossary (short)
- Pipeline — composed resilience strategies.
- Hedging — competing parallel attempts to reduce latency tail.
- Jitter — randomization added to backoff delays to avoid synchronized retry storms.

Useful repo paths
- `README.md` — conceptual overview and quick start.
- `samples/` — runnable examples.
- `src/Polly` — core implementation.
- `test/` — tests.

Further reading
- Official docs: https://www.pollydocs.org
- Samples: https://github.com/App-vNext/Polly-Samples

---

Authoring notes
- This file was expanded from the earlier short summary. It is designed to be a near-complete onboarding reference for engineers. If you want, I will now:
    - Add runnable extracts of `samples/Intro` and `samples/Retries` into your local `examples/` directory and make them runnable in the `ad_simulator` workspace.
    - Generate a migration checklist file `examples/POLLY_MIGRATE_V7_TO_V8.md` with concrete code transforms.

