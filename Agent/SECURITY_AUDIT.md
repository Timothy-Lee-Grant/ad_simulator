# SECURITY_AUDIT

Repository security audit focused on:
- hardcoded passwords
- AWS credentials
- SQL injection risk / unsafe raw SQL
- open CORS
- authentication/authorization
- input validation
- exposed secrets/config
- Docker privilege/security posture
- plaintext connection strings
- sensitive logging

## Executive Summary

Overall risk level: **High** (with multiple **Critical** secret-management findings).

Top concerns:
1. Secrets and plaintext credentials are present in config surfaces.
2. API endpoints are unauthenticated, including admin endpoints.
3. Request validation is minimal and allows abuse paths (notably redirect handling).

---

## Critical Findings

### C1 - Plaintext credentials in application configuration
- **Category:** hardcoded passwords / plaintext connection strings / exposed secrets
- **Evidence:**
  - `src/BidEngine/appsettings.json` includes:
    - `ConnectionStrings.DefaultConnection` with username/password
    - `ConnectionStrings.aws_connection_string` with host/user/password
- **Risk:** Credential leakage via repo access, logs, screenshots, backups, and accidental commits.
- **Recommendation:** Remove secrets from tracked config, rotate exposed credentials, load at runtime from secret store or environment injection only.

### C2 - AWS DB credentials present in `.env`
- **Category:** AWS credentials / exposed secrets
- **Evidence:**
  - `.env` contains `AWS_DB_CONN` with full DB host, username, and password.
  - `docker-compose.yml` injects this into container env (`ConnectionStrings__AwsConnection=${AWS_DB_CONN}`).
- **Risk:** Direct compromise path to cloud database if leaked.
- **Recommendation:** Rotate immediately; replace with secret manager-backed injection; keep only `.env.example` with placeholders.

### C3 - Weak default service credentials in Compose
- **Category:** hardcoded passwords / exposed secrets
- **Evidence:**
  - `docker-compose.yml` sets `POSTGRES_PASSWORD=postgres`
  - `docker-compose.yml` sets `GF_SECURITY_ADMIN_PASSWORD=admin`
- **Risk:** Trivial compromise in shared/dev/prod-like deployments.
- **Recommendation:** Use unique secrets per environment; avoid committing real defaults.

---

## High Findings

### H1 - No authentication or authorization on backend APIs
- **Category:** no authentication
- **Evidence:**
  - `src/BidEngine/Program.cs` does not register auth middleware/services (`AddAuthentication`, `UseAuthentication`, `UseAuthorization` absent).
  - `src/BidEngine/Controllers/BidControllers.cs` has no `[Authorize]`.
  - Admin endpoints under `/api/admin/*` are publicly callable.
- **Risk:** Anyone with network reach can trigger admin vectorization operations and bid/click endpoints.
- **Recommendation:** Add authentication + authorization policies; protect admin routes with strict roles/scopes.

### H2 - Weak input validation and unsafe redirect behavior
- **Category:** weak validation
- **Evidence:**
  - `src/BidEngine/Controllers/BidControllers.cs` validates only `UserId` and `PlacementId` non-empty.
  - `FrontEnd/index.js` `/click` endpoint accepts user-provided `redirect` and calls `res.redirect(redirect.toString())` without allowlist/sanitization.
- **Risk:** Open redirect/phishing abuse; malformed/unbounded input can impact reliability and security controls.
- **Recommendation:** Add strong DTO validation (`[Required]`, length/range/pattern checks), and enforce redirect allowlist for trusted domains.

### H3 - Secrets exposed through runtime and tooling output
- **Category:** exposed secrets in config
- **Evidence:**
  - `docker-compose.yml` injects `ConnectionStrings__AwsConnection`.
  - `docker compose config` renders full secret value in plaintext output.
- **Risk:** Secrets leak through CI logs, terminal history, support tickets, and screenshots.
- **Recommendation:** Use Docker/K8s secrets or external secret providers; avoid printing resolved configs containing sensitive envs.

---

## Medium Findings

### M1 - Raw SQL used (parameterized) with reduced safety margin
- **Category:** unsafe raw SQL / SQL injection risk
- **Evidence:**
  - `src/BidEngine/Services/CampaignCashe.cs` uses `FromSqlInterpolated`:
    - `SELECT * FROM ads ORDER BY embedding <=> {targetVector} LIMIT 3`
- **Assessment:**
  - `FromSqlInterpolated` is generally parameterized, so direct SQL injection risk is **lower**.
  - Still raw SQL; safety depends on continued parameterized usage and query evolution.
- **Risk:** Future regressions to unsafe raw string SQL; bypass of domain-level filtering if query is modified incorrectly.
- **Recommendation:** Keep parameterized APIs only; add tests/assertions around SQL paths; consider encapsulating query in vetted repository layer.

### M2 - Sensitive user/context identifiers in logs
- **Category:** verbose sensitive logging
- **Evidence:**
  - `src/BidEngine/Controllers/BidControllers.cs` logs user/campaign/ad identifiers on click path.
  - Service logs include operational identifiers across bid and budget events.
- **Risk:** PII/user-tracking data may enter logs and downstream observability stores.
- **Recommendation:** Minimize or hash user identifiers in logs, enforce log redaction policy, and set retention/access controls.

### M3 - Docker runtime hardening is minimal
- **Category:** Docker privilege issues
- **Evidence:**
  - `src/BidEngine/Dockerfile` and `FrontEnd/Dockerfile` do not set non-root user.
  - `docker-compose.yml` lacks hardening options (`read_only`, `cap_drop`, `no-new-privileges`, restricted mounts).
- **Risk:** Greater blast radius if container is compromised.
- **Recommendation:** Run as non-root, drop capabilities, use read-only root FS where possible, and tighten volume/port exposure.

---

## Low Findings

### L1 - Open CORS not detected (but CORS policy absent)
- **Category:** open CORS
- **Evidence:**
  - No `AddCors` / `UseCors` / `AllowAnyOrigin` usage found.
- **Assessment:**
  - Not currently an "open CORS" issue.
  - If browser clients call backend directly in future, CORS policy will need explicit secure configuration.
- **Recommendation:** Define explicit origin policy before exposing browser-direct API access.

### L2 - GET used for click event mutation
- **Category:** weak API semantics (security-adjacent)
- **Evidence:**
  - `src/BidEngine/Controllers/BidControllers.cs` uses `GET /api/bid/User_Click_Event` for stateful event recording.
- **Risk:** Crawlers/prefetch/proxies can unintentionally trigger mutable action.
- **Recommendation:** Move to `POST` with CSRF-aware design (if browser-authenticated context is introduced).

---

## Category Coverage Snapshot

- **Hardcoded passwords:** Found (Critical)
- **AWS credentials:** Found (Critical)
- **SQL injection risk:** No direct high-confidence injection found; raw SQL exists with parameterized API (Medium)
- **Unsafe raw SQL:** Present (Medium)
- **Open CORS:** Not detected (Low/Informational)
- **No authentication:** Found (High)
- **Weak validation:** Found (High)
- **Exposed secrets in config:** Found (Critical/High)
- **Docker privilege issues:** Found (Medium)
- **Plaintext connection strings:** Found (Critical)
- **Verbose sensitive logging:** Found (Medium)

---

## Prioritized Remediation Plan

1. **Immediate (today):**
   - Rotate all exposed DB credentials.
   - Remove secrets from tracked config files.
   - Stop injecting plaintext secrets directly in compose env where possible.
2. **Short-term (this sprint):**
   - Add authentication/authorization and protect admin endpoints.
   - Add robust request validation and redirect allowlist.
   - Redact/hash sensitive identifiers in logs.
3. **Hardening (next sprint):**
   - Run containers as non-root + capability drops + read-only FS.
   - Formalize secret management (`.env.example`, secret manager integration, CI redaction policies).
   - Add security tests/static checks (secret scanning, authz tests, unsafe SQL linting).
