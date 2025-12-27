# Kafka (optional)

## Should you use Kafka?

Short answer: yes, if you need a durable, scalable, and asynchronous event stream between services (click events, impression logs, asynchronous enrichment, ML feature pipelines). If you only need simple short-lived message passing or a single-process queue, Kafka adds operational complexity and may be unnecessary for this project now.

When to consider Kafka:
- High write volume for events (clicks, impressions) that need to be processed asynchronously.
- Multiple consumers/processes need to independently consume the same events (analytics, audit, ML, downstream services).
- You want durable, replayable event streams for rebuilding state or training models.

When to avoid Kafka (for now):
- You have a low throughput application or simple synchronous database writes.
- You want to minimize infrastructure complexity during development.

## How to enable Kafka locally (development)

We keep Kafka and Zookeeper as an optional service. To run Kafka locally, you can:

1. Use the provided override compose file:

```bash
docker compose -f docker-compose.yml -f docker-compose.kafka.yml up -d
```

2. Or uncomment the `kafka` and `zookeeper` services in `docker-compose.yml` and run `docker compose up -d`.

## Integration points

- Use the Confluent .NET client (`Confluent.Kafka`) or `kafka-net` to produce/consume messages.
- Recommended patterns:
  - Outbox pattern for reliable writes from Postgres to Kafka.
  - Use compacted topics for change-log style data.
  - Use separate topics for clicks, impressions, conversions, and model feature streams.

## Minimal example (producer)

See `docs/EXAMPLES.md` for a small snippet showing how to create an `IProducer<Null, string>` using the Confluent client and publish messages.

## Operational notes

- Running Kafka locally adds 2 services (zookeeper + kafka). It uses additional CPU and memory.
- For CI/integration tests prefer Testcontainers (or a dedicated service which is spun up only for the integration test) instead of running Kafka in the main compose cluster.

---

If you'd like, I can add an integration test that spins up Kafka (using Testcontainers) and validates an end-to-end flow (e.g., produce click events, consume them, verify DB side effects).
