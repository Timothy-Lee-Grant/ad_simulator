# DTM (Distributed Transactions Manager) — Onboarding Guide

**Repository:** https://github.com/dtm-labs/dtm
**Saved:** `examples/DTM_ONBOARDING.md`
**Audience:** Entry-level engineer who will read, run, and make small contributions to the DTM project

---

This document summarizes the DTM project (dtm-labs/dtm): what it is, how it is organized, how to run it locally, core concepts (saga, TCC, XA, outbox, 2-phase message, workflow), and common developer tasks.

Where possible the information below is taken directly from the project's README and repository tree. Where I inferred behavior I have clearly labeled it as an assumption — you should verify by reading the files in the repository (`main.go`, `conf.sample.yml`, `Makefile`, `sqls/`, `dtmsvr/`, etc.).

---

**Table of Contents**

1. Quick summary
2. Key features and use cases
3. High-level architecture and concepts
4. Repository layout (what each folder does)
5. How to run DTM locally (quick start + Docker)
6. Examples and quick start sample repos
7. SDKs / client languages and usage patterns
8. Developer workflows (run, debug, test)
9. Common tasks and where to change code
10. Troubleshooting and tips
11. Resources and next steps

---

## 1. Quick summary

- DTM is an open-source distributed transactions framework (Go) for guaranteeing cross-service eventual consistency.
- It supports multiple transaction modes: SAGA, TCC, XA, Workflow, Outbox, and 2-phase message.
- Multi-language client SDKs exist (Go, Java, C#, Python, PHP, Node.js). The system can coordinate multi-database, multi-service transactions.
- The project offers a transaction coordinator (server), admin UI, client libraries, and examples demonstrating common patterns.

Why it matters: DTM is a production-capable tool to coordinate long-running (distributed) transactions across microservices without requiring distributed two-phase commit across all participant services.

---

## 2. Key features and application scenarios

Highlights (from project README):

- Support for SAGA, TCC, XA, Workflow, Outbox.  
- SDKs in multiple languages to integrate with application services.  
- Better outbox implementation (2-phase messages) to guarantee event publishing with DB transactions.  
- Multi-database and multi-storage support (MySQL/MariaDB, Redis, MongoDB, Postgres, TDSQL, BoltDB for dev/test).  
- High availability and horizontal scaling friendly.  

Common application scenarios:
- Ensuring cache final consistency
- Managing inventory under flash sales (avoid oversell)
- Non-monolithic order systems across services
- Event publishing with stronger guarantees than naive outbox

---

## 3. High-level architecture & core concepts

DTM provides a central coordinator (the DTM server) that orchestrates distributed transaction workflows. Two main models appear in usage:

- Orchestrated pattern (DTM as coordinator): Application services call DTM to register branch actions (e.g., TransOut, TransIn). DTM coordinates invocation and, on failure, triggers compensation actions.
- Participant-based protocol: Services implement their forward & compensate APIs and DTM calls them as branches when executing transactions.

Key patterns explained:

- SAGA: A series of local transactions where each successful step is recorded. On failure, compensating steps are executed for already completed steps to rollback logically.
- TCC (Try-Confirm-Cancel): Each action has Try/Confirm/Cancel phases. DTM invokes Try on branches, and later Confirm or Cancel depending on outcome.
- XA: Traditional 2-phase commit across multiple DBs (supported where available).
- Outbox & 2-phase message: Ensures database update + event publish are atomic (avoids lost events). DTM's 2-phase message is presented as a robust alternative.
- Workflow: More general orchestration with branching, rollback handlers, and long-running flows.

Important implementation details used by DTM (terms you will see in code and examples):
- Branch: a sub-transaction that belongs to a global transaction.
- Barrier / BranchBarrier: constructs DTM uses to guarantee idempotency and prevent duplicate processing.
- dtmcli: the typical name of the client library (Go) used by examples to interact with the DTM server.

---

## 4. Repository layout (high level)

This is a high-level summary of top-level files and folders (based on GitHub tree):

- `main.go` — the server entry point (run `go run main.go` to start a local server).  
- `conf.sample.yml` — sample configuration used by DTM server (databases, storage engine, ports).  
- `dtmsvr/` — core server implementation; code that exposes HTTP endpoints and implements the coordinator logic.  
- `admin/` — admin UI (built with Vue, TypeScript) used to inspect transactions, dashboard, etc.  
- `charts/` — Helm charts for deploying DTM to Kubernetes.  
- `client/` — client SDKs (or code to build/test them). Many language clients or build helper scripts live here.  
- `dtmutil/` — CLI utilities and helpers.  
- `helper/` — documentation, helper scripts and translated README (e.g., Chinese docs).  
- `qs/` or `quick-start` (sometimes named `quick-start-sample` externally) — quick start sample references or scripts (some sample apps live in separate repos like `quick-start-sample`).  
- `sqls/` — SQL initialization scripts (MySQL, SQL Server, Postgres, etc.) used by the server and examples for schema setup.  
- `test/` — integration and unit tests (including Redis cluster support, storage driver tests).  
- `Makefile` — convenience tasks used by CI and developers (build, test, admin build).  
- `go.mod` / `go.sum` — Go modules.
- `README.md` — project overview (the canonical place to begin).

Notes:
- The repository also includes `releases` and a long history of tags: the project is mature (many releases).
- `admin` includes a build step; some release steps involve building the admin UI and packaging it.

---

## 5. How to run DTM locally — Quick Start

The README gives a minimal quick-start. Here are steps you can use as an entry-level developer.

Prereqs (local dev):
- Go (1.20+ recommended; use the version in `go.mod`)
- Docker (for running DBs and examples)
- Optional: Make (for Makefile convenience)

A. Run server directly (fastest for development)

```bash
# clone repo
git clone https://github.com/dtm-labs/dtm.git
cd dtm

# start DTM server locally
go run main.go

# server will start using default conf or conf.sample.yml (check main.go & args)
```

B. Using Docker (recommended for reproducing CI/dev environment)

- The project supports multiple storage backends; commonly, MySQL is used in quick-starts. Use the SQL scripts in `sqls/` to initialize schema.
- There are pre-built Docker images for `dtm` and `admin` used in CI and examples — check the project's `Makefile` or `docker` build files for exact commands.

Example: run DTM with Docker and a MySQL container (pseudo-commands — check repository for exact docker-compose or images)

```bash
# Example steps (adapt to repo's dockerfiles or examples)
# 1. Start MySQL (or use docker-compose from examples)
# 2. Configure conf.yml to point to the database
# 3. Build dtm image or run 'go run main.go' in a container
```

C. Run an example transaction flow

The README points to `quick-start-sample` repositories for full examples. Typical flow:

1. Start DTM server (`go run main.go`)  
2. Start the example microservices (for workflow-grpc or tcc examples) from their repository (e.g., `quick-start-sample/workflow-grpc`)  
3. Execute the example main program which will call DTM client APIs to register transaction branches and execute a transaction  

Example from README:

```bash
# example quick start (external repo)
git clone https://github.com/dtm-labs/quick-start-sample.git && cd quick-start-sample/workflow-grpc
go run main.go
```

This will show the console logs for TransOut, TransIn, and compensation calls when intentionally triggered.

---

## 6. Examples & sample repos

The DTM project references several example repos and a `dtm-examples` or `quick-start-sample` family. Those repositories provide:

- Simple bank transfer examples (TransOut/TransIn) demonstrating rollback and compensation.  
- Workflow examples showing `workflow.Register`, `workflow.ExecuteCtx`, and how to declare branches and rollback handlers.  
- TCC examples showing Try/Confirm/Cancel phases.  

If you're starting, clone `quick-start-sample` and run the `workflow-grpc` example after starting the DTM server.

---

## 7. SDKs / Client usage

DTM provides client SDKs for many languages. They typically expose APIs to:

- create a transaction (saga/tcc/workflow),  
- register branch endpoints (branch forward & compensating),  
- submit/execute transactions,  
- query transaction status.

Common client API patterns (Go-like pseudocode):

```go
// Create a Saga
gid := dtmcli.MustGenGid(dtmServer)
saga := dtmcli.NewSaga(dtmServer, gid)
// add step with forward and compensate URL
saga.Add(busiSvcUrl1+"/TransOut", busiSvcUrl1+"/TransOutRevert")
// add more steps
saga.Submit()
```

For TCC you will see `Try`, `Confirm`, `Cancel` registration calls.

Important notes on clients:
- Clients should handle idempotency — DTM uses barriers and deduplication on server side but client design affects failure modes.
- Look for `dtmcli` package in Go; C#, Java and other clients may be in `client/` or in separate repos maintained by the project.

---

## 8. Developer workflows (run, debug, test)

Run server locally for development

```bash
# from repo root
go run main.go
# or, build and run
go build -o dtm ./ && ./dtm
```

Run an example (quick-start-sample)

```bash
# in example repo
go run main.go
```

Running tests

- The project has a `test/` directory and test logic — use `go test ./...` to run the go tests, or check the Makefile for CI tasks.
- Some tests require external dependencies (e.g., Redis cluster) — check test readme or CI config.

Build admin UI (if you change front-end)

- `admin` folder contains a Vue/TS front-end. Building it requires `npm`/`yarn` and the admin build script. Built admin assets are served by the server for the admin pages.

---

## 9. Common contributor tasks & where to change code

- Add support for a new storage driver: check `dtmsvr` and `helper` folders for storage engine abstractions and `sqls/` for schema.
- Fix or add a client SDK: look under `client/` and check language-specific client directories or language-specific repos maintained by the community.
- Add or improve examples: `quick-start-sample` is the canonical place; submit PRs that add new example scenarios (e.g., outbox integration with Kafka).
- Admin UI improvements: edit `admin/` and follow the build steps in `Makefile` or `admin` README.

Where to look for key server code:
- Transaction coordinator & HTTP handlers: `dtmsvr/` and `main.go`
- Configuration and defaults: `conf.sample.yml`
- Schema initialization: `sqls/` (MySQL, SQLServer scripts)
- Utilities & CLI: `dtmutil/`

---

## 10. Troubleshooting & tips

- If you see errors related to database schema, ensure you ran the SQL initialization scripts in `sqls/` for the chosen DB engine.
- For Redis storage issues, verify `conf.sample.yml` points to the correct Redis host/port.
- When testing examples, start DTM before starting the sample microservices. The example applications will call DTM to register branches.
- If admin UI fails to load, check that the admin static files were built and paths in `main.go` or server config point to the correct `admin/dist` folder.
- For debugging long-running transactions, admin UI and server logs will show branch invocation history and compensation attempts.

---

## 11. Resources & next steps

- Official docs (cookbook): https://en.dtm.pub/
- Quick start examples: https://github.com/dtm-labs/quick-start-sample
- Community: Issues & PRs on GitHub: https://github.com/dtm-labs/dtm/issues
- Releases: https://github.com/dtm-labs/dtm/releases (active project; check latest tag)

Suggested steps for you now (pick one):
1. Clone DTM and run `go run main.go` then run a quick-start example from `quick-start-sample`.  
2. Open `dtmsvr/` and read the transaction coordinator HTTP handlers to understand how DTM stores transaction state and invokes branch endpoints.  
3. Run the admin UI locally (follow `admin` folder README) to inspect transaction flows visually.

---

If you'd like, I can now do one of these follow-ups for you:
- Read and summarize `main.go` and `conf.sample.yml` exactly to give the server port and default storage values used by the repo.  
- Build a precise `docker-compose.yml` recipe for running DTM + MySQL + Redis locally if one is not already present in the repo.  
- Open and summarize the `dtmsvr` folder (key files & functions) to give a mapped guide for where to change coordinator logic.

Tell me which follow-up you prefer and I'll proceed.  

---

Document generated: November 30, 2025
