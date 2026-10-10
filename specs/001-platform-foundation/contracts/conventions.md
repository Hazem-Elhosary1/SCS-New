# Contract: Platform Conventions

Rules every later spec inherits from the platform. Changing one of these is a change to this
contract, not a local decision in another spec.

## 1. Routes

- Business routes are under `/v1`. Health routes are at the root and are not versioned.
- In test and production the API is reached as `/api` on each front end's own address
  (`app.scs-heta.com/api/v1/...`); the edge strips `/api` before the request reaches the API.
  Browsers therefore never call the API across origins outside `local`.
- Every route is denied unless it explicitly declares who may call it.
- Instants are ISO 8601 UTC (`2026-10-10T09:30:00Z`). Money is a decimal string with the
  currency implied (EGP); no floating-point amounts.

## 2. Request headers

| Header | Rule |
|---|---|
| `traceparent` | Sent by front ends; continued by the server. Absent → the server starts one |
| `Accept-Language` | `ar` or `en`. Absent or anything else → `ar` |

## 3. Response headers

| Header | Rule |
|---|---|
| `X-Trace-Id` | On every `/v1` response, success or failure |
| `Content-Language` | Language of any message in the body |
| `Strict-Transport-Security` | On every response outside `local` |

## 4. Error shape

Every failure is `application/problem+json`:

```json
{
  "type": "urn:scs:error:validation-failed",
  "title": "البيانات المدخلة غير صحيحة",
  "status": 400,
  "code": "VALIDATION_FAILED",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": { "email": ["REQUIRED"] }
}
```

- `code` is stable and is what clients branch on. `title` is for display and may be reworded.
- No stack trace, file path, query text, type name or dependency name ever appears.
- `errors` holds field codes, not sentences; the front end owns field wording.

### Codes defined by this spec

| Code | Status | When |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Request body or parameters invalid |
| `MALFORMED_REQUEST` | 400 | Body could not be read at all |
| `UNAUTHENTICATED` | 401 | No valid identity (fallback policy, until spec 003) |
| `FORBIDDEN` | 403 | Reserved for spec 005 |
| `NOT_FOUND` | 404 | No such resource. A route that does not exist answers `UNAUTHENTICATED` to a caller with no identity, so routes cannot be probed |
| `PAYLOAD_TOO_LARGE` | 413 | Body over the route's limit |
| `RATE_LIMITED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Anything unexpected |
| `SERVICE_UNAVAILABLE` | 503 | A dependency is down |

Later specs add codes (for example `FEATURE_NOT_IN_PLAN`, `CLINIC_READ_ONLY`) to this table by
amending this contract; they do not invent a second shape.

## 5. Operational log entry

One JSON object per line.

| Field | Rule |
|---|---|
| `@t` | UTC instant |
| `@l` | `Information`, `Warning`, `Error`, `Fatal` (`Debug` in `local` only) |
| `@mt` | Message template with named holes; never an interpolated string |
| `env` | `local`, `test`, `production` |
| `service` | `api`, `worker`, or `web-clinic`, `web-patient`, `web-ops` for client errors |
| `version` | Release version |
| `traceId` | Present on every entry caused by a request or a job |
| `route` | Route template (`/v1/patients/{id}`), never the actual path |
| `status`, `durationMs` | On the request-completed entry |
| `jobId`, `jobName` | On entries from background work |
| `@x` | Exception type and stack; message passed through the scrubber |

Never logged, whatever the level: request or response bodies, query strings, header values,
path values, cookies, tokens, connection strings, and any member marked `[PersonalData]` or
`[Secret]`. Identifiers (ids) may be logged; the data they point to may not.

## 6. Background work

- A job is registered through the building-block interface, not against Hangfire directly.
- A job is idempotent: running it twice has the same result as once.
- Failures retry with back-off up to five times, then are logged at `Error` with `jobId`.
- A job that works on clinic data sets the clinic context first (delivered by spec 002).
- Every attempt is logged with `jobId` and `jobName`; a job that has used up its retries gets one
  `Error` entry, and each failed attempt adds one to the `scs.jobs.failed` counter.
- The jobs dashboard exists in `local` only until operator sign-in exists (spec 010).

## 7. Traces

Traces follow the same rule as logs. A request span carries the route template (`http.route`)
and is named `<METHOD> <route template>`. The actual path, the query string, the user agent and
the caller's address are removed before a span leaves the process.
