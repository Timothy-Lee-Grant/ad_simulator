# Troubleshooting Guide: Adding Description + Embeddings (Vectorization) to Ads ✅

**Goal:** Teach a junior engineer how we added a description and an embedding vector for Ads, why certain issues appeared with EF Core & Postgres, and how to safely apply migrations and verify the app end-to-end.

---

## Overview / TL;DR

- We wanted each Ad to have a `Description` (text) and an `Embedding` (dense vector of floats).
- Development Postgres doesn't include `pgvector`, so we persisted embeddings as JSON (`jsonb`) for compatibility.
- We had two main problems:
  1. Design-time mapping errors from pgvector types when adding migrations.
  2. `dotnet ef database update` failed because tables already existed in the DB (schema drift).
- Fixes implemented:
  - Use `float[]?` on the model and serialize to `jsonb` via an EF `ValueConverter`.
  - Add a `ValueComparer` to compare `float[]` contents for EF change tracking.
  - Make the migration idempotent: `ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...` rather than blindly creating tables.
  - Seed ad descriptions/embeddings and verify the API and frontend.

---

## Concepts: Quick primer (why these changes matter)

- **pgvector vs jsonb**
  - pgvector: native vector datatype; supports indexing and KNN (fast nearest-neighbor searches). Requires the `pgvector` extension in Postgres.
  - jsonb: portable; stores arrays of floats but offers no vector indexes or KNN. It's a safe fallback for dev environments without pgvector.

- **EF ValueConverter**
  - Converts between a property type in C# (e.g., `float[]`) and a database column type (e.g., `jsonb`). This avoids needing provider-specific types at design time.

- **ValueComparer**
  - For collection types (like `float[]`), EF uses reference equality by default leading to change-tracking issues. A `ValueComparer` tells EF how to compare two arrays for equality.

- **Idempotent migrations**
  - When working with a DB that might already have tables (created outside of migrations), prefer migrations that *alter* existing tables safely (e.g., using `ADD COLUMN IF NOT EXISTS`) so migration application is robust.

---

## Step-by-step: What we changed (and why)

### 1) Domain model changes

- `Ad` model: add `Description` (string) and change `Embedding` type to `float[]?`.

Why: an array is easy to use in C# code for distances/embedding math and is serializable to JSON for storage when pgvector is not present.

### 2) EF mapping changes

- Use a ValueConverter that serializes `float[]` to JSON:

```csharp
var floatArrayToJsonConverter = new ValueConverter<float[]?, string?>(
    v => v == null ? null : JsonSerializer.Serialize<float[]>(v, null),
    v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<float[]>(v, null)
);

entity.Property(e => e.Embedding)
    .HasColumnName("embedding")
    .HasConversion(floatArrayToJsonConverter)
    .HasColumnType("jsonb");
```

- Add a `ValueComparer<float[]?>` so EF compares the array contents instead of references:

```csharp
var floatArrayComparer = new ValueComparer<float[]?>(
    (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
    a => a == null ? 0 : a.Aggregate(0, (hash, val) => HashCode.Combine(hash, val.GetHashCode())),
    a => a == null ? null : a.ToArray()
);

property.Metadata.SetValueComparer(floatArrayComparer);
```

### 3) Migration generation & safety

- Problem: `dotnet ef migrations add` initially failed because the design-time model contained provider-specific vector types (pgvector) and the provider wasn't available.

- Approach: make migrations conservative and idempotent. Replace raw `CreateTable` in the generated migration with `ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...` for the new columns. This keeps the migration safe even if tables already exist.

**Example (Up method):**

```csharp
migrationBuilder.Sql("ALTER TABLE ads ADD COLUMN IF NOT EXISTS description text;");
migrationBuilder.Sql("ALTER TABLE ads ADD COLUMN IF NOT EXISTS embedding jsonb;");
```

**Example (Down method):**

```csharp
migrationBuilder.Sql("ALTER TABLE ads DROP COLUMN IF EXISTS embedding;");
migrationBuilder.Sql("ALTER TABLE ads DROP COLUMN IF EXISTS description;");
```

Why: This prevents "relation already exists" errors when applying migrations to a DB which already has an existing schema.

---

## Commands & checks (cheat sheet)

- Generate migration (after model/mapping edits):

```bash
cd path/to/project
dotnet ef migrations add AddVectorSupportToAds --context AppDbContext --project src/BidEngine --startup-project src/BidEngine
```

- Apply migration to local Postgres (running on host port 5434):

```bash
# override connection string to point to host-local postgres
ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5434;Database=ads_db;Username=postgres;Password=postgres" \
  dotnet ef database update --context AppDbContext --project src/BidEngine --startup-project src/BidEngine
```

- Inspect migrations applied:

```bash
docker run --rm --network host -e PGPASSWORD=postgres postgres:15-alpine \
  sh -c "psql -h 127.0.0.1 -p 5434 -U postgres -d ads_db -c 'select * from "__EFMigrationsHistory" order by "MigrationId";'"
```

- Inspect ads table & seed data (psql):

```bash
docker run --rm --network host -e PGPASSWORD=postgres postgres:15-alpine sh -c \
  "psql -h 127.0.0.1 -p 5434 -U postgres -d ads_db -c 'select id, title, description, embedding from ads limit 5;'"

# Seed description and json embedding
docker run --rm --network host -e PGPASSWORD=postgres postgres:15-alpine sh -c \
  "psql -h 127.0.0.1 -p 5434 -U postgres -d ads_db -c \"update ads set description='Relaxing sunsets', embedding='[0.1,0.2,0.3]'::jsonb where id='<ad-id>';\""
```

- Verify bid API & frontend:

```bash
# API (bid engine)
curl -X POST http://127.0.0.1:8081/api/bid -H 'Content-Type: application/json' -d '{"userId":"test","placementId":"homepage_banner"}'

# Frontend homepage
curl http://127.0.0.1:3001/
```

---

## Troubleshooting: Common errors & fixes

1. Error: "The 'Vector' property 'Ad.Embedding' could not be mapped to the database type 'vector(384)'"
   - Cause: design-time provider couldn't map the `Pgvector.Vector` type without the pgvector extension/provider.
   - Fix: use `float[]` + `ValueConverter` to `jsonb` so design-time model builds everywhere.

2. Error: "extension 'vector' is not available" when `CREATE EXTENSION vector` runs
   - Cause: Postgres image doesn't include `pgvector`.
   - Fix: either install `pgvector` in the Postgres container or persist embeddings as `jsonb` instead. If you need `pgvector`, use an image with the extension or install it during container startup.

3. Error: "relation 'ads' already exists" when applying migration
   - Cause: tables were previously created (schema drift). The migration attempted to create tables that already exist.
   - Fix: make migration idempotent (use `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`) or mark the database as baseline by inserting appropriate rows into `__EFMigrationsHistory` (careful, manual step).

4. EF warning: "The property 'Ad.Embedding' is a collection or enumeration type with a value converter but with no value comparer"
   - Fix: add a `ValueComparer<float[]?>` and set it on the property metadata (see above).

---

## Practical tips and best practices

- For **dev environments**, using `jsonb` for embeddings is convenient and avoids needing `pgvector` everywhere.
- For **production**, if you want KNN and vector indexes, switch to pgvector and create a migration to add a `vector` column with an index.
- Keep migrations idempotent only when you need to apply them to pre-existing schema; ideally maintain a single migration history and avoid manual schema changes where possible.
- Add a small integration test that POSTs to `/api/bid` and asserts the response includes ad content and description — this catches regressions quickly.

---

## Next steps you can take (suggested)

1. Decide whether to keep `jsonb` for embedding in development or switch to `pgvector` for production workloads.
2. Add a test that verifies `AdContent.Description` is populated and rendered on the front end.
3. If switching to `pgvector`: update docker compose to use a Postgres image with the `pgvector` extension or install it during container boot, re-enable `HasPostgresExtension("vector")` and create a migration that adds a `vector` column.
4. Add CI steps to run migrations against a clean Postgres instance so migration regressions are caught early.

---

## Where this file lives
- Path: `docs/troubleshooting/EF-vectorization-migration.md`

---

If you want, I can:
- add a short checklist to the README or a test that exercises the flow, or
- make a `docker-compose` profile that uses a Postgres image with `pgvector` and document how to switch environments.

If you'd like changes to the wording, or want this converted into a short developer-facing playbook, tell me which format you prefer (wiki page, README section, or a GitHub issue template). ✨
