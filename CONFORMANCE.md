# Conformance

Scenario-by-scenario status of this SDK against the LogTide SDK contract.
Each scenario ID is stable across all official SDKs; "n/a" entries explain
why a scenario does not apply. TODO entries are tracked work.

| ID | Scenario | Status | Test reference |
|---|---|---|---|
| C01 | basic log: one POST to /api/v1/ingest with X-API-Key, {logs:[...]} body, RFC 3339 time, metadata.sdk | ✅ | `LogTideClientTests`, `SdkMetadataTests` |
| C02 | batch by size: batchSize entries flush automatically, order preserved | ✅ | `BatchTransportTests` (size flush) |
| C03 | batch by interval: entries delivered without explicit flush | ✅ | `BatchTransportTests` (timer flush) |
| C04 | wire format strictness: SDK fields nested in metadata, only contract fields top-level | ✅ | `LogEntryTests` (snake_case wire fields) |
| C05 | exception capture: structured metadata.exception with type/message/language/frames/cause | ✅ | `ExceptionSerializationTests` (canonical shape, frames, cause) |
| C06 | exception chain cap: cause depth ≤ 10, no infinite loop on cycles | ✅ | `ExceptionSerializationTests` + MaxCauseDepth cap |
| C07 | retry on 5xx with growing backoff | ✅ | `RetryPolicyTests` (5xx retried) |
| C08 | no retry on permanent 4xx (400/401/403/413) | ✅ | `RetryPolicyTests` (400/401/403/413 not retried) |
| C09 | Retry-After overrides computed backoff | ✅ | `RetryPolicyTests` (Retry-After overrides backoff) |
| C10 | circuit breaker opens after threshold failures | ✅ | `CircuitBreakerTests` |
| C11 | circuit breaker half-open probe and recovery | ✅ | `CircuitBreakerTests` (half-open single probe) |
| C12 | buffer cap: drops beyond maxBufferSize, counted, never throws | ✅ | `BatchTransportTests` (BufferFullException policy) |
| C13 | flush on close; capture after close is a silent no-op | ✅ | `LogTideClientTests` (DisposeAsync flush, idempotent dispose) |
| C14 | DSN parsing incl. base path; invalid DSN fails at init | ✅ | DSN parsing in `LogTideClientTests` |
| C15 | inbound traceparent lands on entry trace_id | ✅ | `W3CTraceContextTests`, middleware tests |
| C16 | no PII by default; API key never logged | ✅ | sensitive header filtering (`LogTideMiddleware`) |
| C17 | serialisation robustness: circular/unserialisable values never throw | partial | System.Text.Json; add cyclic-metadata fallback |
| C18 | timestamp fidelity: time reflects capture, not delivery | ✅ | time from `LogEntry` creation |
| C20 | scope isolation across concurrent requests | ✅ | `LogTideScopeTests` (AsyncLocal isolation) |
| C21 | breadcrumb ring buffer eviction, oldest first | ✅ | `BreadcrumbBufferTests` (ring buffer) |
| C22 | beforeSend can mutate or drop entries | ✅ | `HooksTests` |
| C23 | sampling: rate 0 sends nothing (logs) / no-op spans (traces) | ✅ (logs) | `HooksTests`; trace sampling TODO |
| C24 | OTLP span export with service.name resource | ✅ | `SpanManagerTests`, OTLP transport |
| C25 | outbound traceparent injection on instrumented HTTP clients | partial | traceparent emitted on responses; outbound DelegatingHandler TODO |
| C26 | log/trace correlation: active span ids on entries | ✅ | `LogTideScopeTests` (span ids on entries) |
| C27 | middleware error capture rethrows after logging | ✅ | `LogTideErrorHandlerMiddleware` (rethrows) |
| C28 | logging-bridge level mapping and scope context | ✅ | `LogTideLoggerProviderTests` (ILogger mapping, scopes), `LogTideSinkTests` |
