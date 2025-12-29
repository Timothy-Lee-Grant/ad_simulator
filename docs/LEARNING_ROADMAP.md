# Learning Roadmap — AdSimulator

Purpose: A compact set of lecture-style notes and practical exercises to help you build intuition for the core architecture, patterns, and technologies used in this repository.

---

## The Big Picture ✅

At a glance — the system serves a lightweight frontend that asks the BidEngine for winning ads. The BidEngine can use semantic search when a video context exists (videoId). Embedding vectors live in Postgres (pgvector) and are generated either by a native model (AllMiniLm) or by a deterministic fallback for development.

Mermaid diagram:

```mermaid
flowchart LR
  Browser-->Frontend[Express + EJS]
  Frontend--POST /api/bid-->BidEngine[ASP.NET BidController]
  BidEngine--calls-->BidSelector
  BidSelector--(videoId present)-->CampaignCache
  CampaignCache--reads/writes-->Postgres[(Postgres + pgvector)]
  CampaignCache--generates-->Embedder[(AllMiniLm native or deterministic fallback)]
  Postgres--stores-->Vectors[vector(384)]
  NoteOverBid: Prometheus metrics + Redis caching present
```

Flow (short): Browser → Frontend index.js → POST /api/bid → BidController → BidSelector → (if VideoId) SelectWinningBidBySemanticSearch → CampaignCache finds/creates vector → query campaigns/ads → return winning ad → Frontend renders ad.

---

## Knowledge Pillars

- **Vector Embeddings & pgvector** — storing 384-d float vectors and performing similarity searches.
- **Embedding Generation** — using an on-device native embedder (AllMiniLm) when available; deterministic SHA-based fallback for dev/CI.
- **EF Core & Provider-Aware Mappings** — mapping Pgvector.Vector to `vector(384)` on Npgsql and to JSON for InMemory tests via ValueConverter/ValueComparer.
- **Semantic Search Integration** — BidSelector routes video requests to semantic selection; CampaignCache is the central place for vector access and generation.
- **ASP.NET Controllers & DI** — BidController, VideosController, Admin-style endpoints; DI used for CampaignCache, BidSelector, etc.
- **Frontend: Express + EJS** — Simple node Express server that requests bids and renders the UI, plus the new video pages.
- **Infrastructure: Docker Compose** — Postgres (pgvector), Redis, BidEngine, Frontend; Kafka optional.
- **Observability & Resilience** — Prometheus metrics, Redis connection hardening (AbortOnConnectFail=false), logging.
- **Testing Patterns** — Unit tests use EF InMemory; provider-aware mapping keeps tests green without native pgvector.

---

## Module Breakdowns (Lecture Notes)

Each section: What, How (critical snippets), and Why.

### BidEngine

- What: Core decision engine that receives bid requests and returns a winning ad.
- How: `BidController.EvaluateBidsAsync` accepts POST requests and calls `BidSelector.SelectWinningBidAsync`.

  Key snippet (conceptual):

  ```csharp
  var winningBid = await _bidSelector.SelectWinningBidAsync(request);
  if (winningBid == null) return NoContent();
  return Ok(winningBid);
  ```

- Why: Centralized place to orchestrate bidding, apply business rules (targeting, budget checks), and plug in semantic ranking later.

### BidSelector & Semantic Branch

- What: Implements selection algorithms — a 'highest CPM' algorithm and a semantic-path when `VideoId` exists.
- How: `SelectWinningBidAsync` decides path; `SelectWinningBidBySemanticSearch` is the hook for semantic matching.

  Observations: The semantic branch currently returns `null` by default (placeholder) so the frontend must gracefully handle no-ad responses (204 No Content).

- Why: It isolates selection strategies (greedy vs. semantic) and is extensible for future algorithms (HNSW-backed retrieval, re-ranking, or hybrid score).

### CampaignCache (Embedding & Search helper)

- What: Caches campaigns, exposes helpers to fetch video embeddings and generate them when missing.
- How: `FindVectorFromVideoId(Guid)` and `CreateVectorFromVideoId(Guid)` call into the embedder and persist `Video.Embedding`:

  ```csharp
  float[] embedding = embedder.GenerateEmbedding(video.Description).ToArray();
  video.Embedding = new Pgvector.Vector(embedding);
  _dbContext.Entry(video).State = EntityState.Modified; await _dbContext.SaveChangesAsync();
  ```

- Why: Keeps vector generation logic centralized and idempotent (generate only when missing); enables offline seeding.

### AppDbContext & pgvector support

- What: EF Core context; maps `Video.Embedding` to PostgreSQL's `vector(384)` when using Npgsql, and to JSON when running tests with the InMemory provider.
- How: provider-aware mapping pattern (pseudo):

  ```csharp
  var videoEmbeddingProp = entity.Property(e => e.Embedding).HasColumnName("embedding");
  if (Database.IsNpgsql()) {
    videoEmbeddingProp.HasColumnType("vector(384)");
  } else {
    videoEmbeddingProp.HasConversion(vectorToJsonConverter).HasColumnType("jsonb").Metadata.SetValueComparer(vectorComparer);
  }
  ```

- Why: Enables true pgvector usage in production while keeping unit tests simple and fast with in-memory stores.

### Embeddings & Fallbacks

- What: Prefer native AllMiniLm embedder, fallback to a deterministic SHA256→float[384] generator when model files are unavailable.
- How: Controlled by `EmbeddingOptions.AllowDeterministicFallback` and `MODEL_URL` model download attempts during seeding.

- Why: Native models give real semantic quality; deterministic fallback avoids losing dev productivity when model artifacts are unavailable, but it's opt-in to avoid masking issues.

### Frontend (Express + EJS)

- What: A small server-rendered frontend that requests bids and renders ads and videos.
- How: `FrontEnd/index.js` calls the BidEngine `/api/bid` endpoint (and recently `/api/videos`) and provides a simple video page which requests an ad with a `videoId` to trigger semantic routing.

  Example front-end bid request that triggers semantic path:

  ```js
  await fetch(`${BID_ENGINE_URL}/api/bid`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId, placementId: 'video_page', videoId })
  })
  ```

- Why: Easy to modify UI for demos and acceptance tests; EJS keeps server-rendering simple without heavy SPA tooling.

### Infrastructure & Observability

- Docker compose boots Postgres (custom pgvector extension image), Redis, BidEngine, Frontend; Kafka is optional.
- Prometheus metrics are instrumented in `BidController` (Counters and Histogram).

---

## Deep Dive Topics (Syllabus)

1. Vector Spaces & Cosine Similarity (The Math)
   - Intuition: vectors as points in space; similarity ~ angle between vectors.
   - Tasks: implement cosine similarity in C# and compare results to pgvector `<=>` operator (distance vs. similarity).
   - Reading: papers/blogs on word2vec, SBERT, and All-MiniLM embeddings.

2. Asynchronous Programming in C#
   - Concepts: Task vs Task<T>, async/await, ConfigureAwait, I/O-bound vs CPU-bound.
   - Practice: Profile `GenerateEmbeddingsForAllVideos` (I/O + CPU) and see how `await` points matter.
   - EF Core tips: AsNoTracking, streaming with `AsAsyncEnumerable()`, and proper SaveChanges patterns.

3. HNSW & ANN Indexing
   - Intuition: approximate nearest neighbor graphs (skip exhaustive scans for speed)
   - Practice: explore pgvector's HNSW support (index creation) or use a local HNSW library and compare latencies and recall.

4. EF Core provider mapping & testing strategies
   - Implement ValueConverter/ValueComparer for complex types; reason about equality and change tracking.

5. Observability & Resilience
   - Add a Prometheus query to plot `bid_latency_seconds` and interpret p95.
   - Stress-test Redis connection settings (AbortOnConnectFail/ConnectRetry) to see startup behavior.

---

## Active Investigation Challenges (Experiments)

Try these to learn by breaking things safely:

1. Replace embedding generator (temporary): change deterministic fallback to generate random vectors — re-seed and run queries; observe ranking instability vs deterministic results.

2. Add a crude SQL-based semantic re-ranker: use `FromSqlInterpolated` to run a `SELECT ... ORDER BY embedding <=> vector LIMIT 5` and display similarities. Compare IDs to your expected relevance.

3. Turn off provider-aware mapping: force `vector` column on InMemory (simulate) and run unit tests — observe the reasons tests fail and how ValueConverter fixed that.

4. Implement an HNSW index on the `videos` table (pgvector's `ivfflat` or `hnsw`) and measure latency/retrieval quality for a large seeded dataset.

5. Add an integration test (Docker Compose) that boots Postgres+BidEngine, seeds N videos, runs a `/api/bid` with a videoId, and asserts that the response is 200/204 and that vectors exist in DB.

---

## Quick Commands & Playbook

- Seed vectors from the engine (inside the BidEngine container or locally):

  ```bash
  dotnet run --project src/BidEngine -- --seed-vectors
  ```

- Check Postgres for vectors:

  ```bash
  docker compose exec postgres psql -U postgres -d ads_db -c "SELECT id, title, (embedding IS NOT NULL) as has_embedding FROM videos LIMIT 10;"
  ```

- Test a bid call for a video:

  ```bash
  curl -s -X POST http://localhost:8081/api/bid -H 'Content-Type: application/json' -d '{"userId":"user_local_123","placementId":"video_page","videoId":"<GUID>"}' -w "\nHTTP STATUS: %{http_code}\n"
  ```

---

## Further Reading & Resources

- pgvector docs: https://github.com/pgvector/pgvector
- All-MiniLM & SBERT blog posts (semantic embeddings)
- HNSW overview and NMSLIB documentation
- EF Core docs: ValueConverter, ValueComparer, provider-specific configuration
- Prometheus client for .NET and best practices for metrics

---

## Final Notes — Teaching Tips

- Keep the experiments small and iterative (one change at a time). Reproducible seeds make behavior easier to interpret.
- When you change models/dimensions, add a fast validation script that computes cosine similarity between a known positive pair and a random negative pair so you can sanity check the embedding quality quickly.

If you'd like, I can convert any of these sections into a step-by-step lab with runnable scripts and tests.
