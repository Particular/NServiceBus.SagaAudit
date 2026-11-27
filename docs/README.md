# Engineering context

## About this repository

This repository audits NServiceBus saga state changes for display in ServicePulse.

- `src/NServiceBus.SagaAudit/` — plugin implementation
- `src/NServiceBus.SagaAudit.Tests/` and `src/NServiceBus.SagaAudit.AcceptanceTests/` — unit and acceptance tests
- `.github/workflows/` — CI pipelines

## Start here

- [Component documentation](https://docs.particular.net/nservicebus/sagas/saga-audit) — configure saga auditing and understand what it captures
- [Saga diagram](https://docs.particular.net/servicepulse/saga-diagram) — how ServicePulse displays audited state changes
- [Repository README](../README.md) — package overview and support links
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Decisions and rationale

- [Architecture and design decisions](decisions/)

### Decisions recorded in pull requests

- Serialize the concrete saga data type so the audit includes properties beyond `IContainSagaData` — [#512](https://github.com/Particular/NServiceBus.SagaAudit/pull/512)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.
