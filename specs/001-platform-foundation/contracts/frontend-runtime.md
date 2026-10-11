# Contract: Front-End Runtime

What the three front ends (`clinic`, `patient`, `ops`) share. Provided by the `core` and `ui`
libraries; an app does not reimplement any of it.

## 1. `config.json`

Fetched once before the app starts. The only file that differs between environments.

```json
{ "environment": "test", "apiBaseUrl": "/api" }
```

| Field | Rule |
|---|---|
| `environment` | `local`, `test` or `production` |
| `apiBaseUrl` | `/api` in test and production (same origin as the app); an absolute `http://localhost` address is allowed in `local` only |

Addresses per environment:

| App | Production | Test |
|---|---|---|
| `ops` | `hetasystems-scsadmin.app` | `test.hetasystems-scsadmin.app` |
| `clinic` | `app.scs-heta.com` | `app.test.scs-heta.com` |
| `patient` | `scs-heta.com/<clinic>` | `test.scs-heta.com/<clinic>` |

The `patient` app is served for any first path segment that is not `api`; which clinic the
segment names is resolved in spec 002. `api` is a reserved word and can never be a clinic name.

Missing or invalid → the app shows the error state and starts nothing else. No secret is ever
placed in this file.

## 2. `version.json`

```json
{ "app": "clinic", "version": "0.1.0+3f9c2ab", "commit": "3f9c2ab…", "builtAt": "2026-10-10T09:30:00Z" }
```

Served without caching. The app re-reads it every 10 minutes; when `commit` differs from the one
it started with, it reloads at the next navigation, never in the middle of a form.

## 3. Language and direction

- Languages: `ar` (default) and `en`. The choice is kept per browser until spec 003 ties it to
  the user.
- Switching sets `<html lang dir>`; `ar` is `rtl`, `en` is `ltr`. No reload.
- Every visible string is a translation key. Keys are `area.element.purpose`
  (`shell.language.switch`). A key present in one language file and not the other fails CI.
- Layout uses logical spacing and alignment only. Physical left/right utilities fail lint.
- A missing translation shows the other language's text and is reported as a client error.

## 4. Formatting

| Pipe | Input | Output |
|---|---|---|
| `scsDate` | UTC instant, format name (`date`, `time`, `dateTime`) | Cairo time in the active language |
| `scsMoney` | Decimal string | Egyptian pounds in the active language |

No component calls a date or number formatter directly.

## 5. Screen states

One component, `<scs-state>`, renders every non-content state. A screen declares which state it
is in; it does not draw its own.

| State | When | Must show |
|---|---|---|
| `loading` | Waiting for the first answer | Skeleton or spinner per the design file |
| `empty` | Answer arrived with nothing in it | What is empty and the primary next action, if any |
| `error` | Request failed | Message, the request reference, a retry action |
| `offline` | Browser has no connection | That work is paused; retries when connection returns |
| `view-only` | User may view but not act | Content without action controls |
| `not-in-plan` | Feature not in the clinic's plan | «غير مشمولة في باقتك» and the upgrade path |

- Appearance comes from each app's design file (clinic file for `clinic` and `patient`, ops file
  for `ops`); the component is the same, the tokens differ.
- `view-only` and `not-in-plan` are defined and demonstrable here; specs 005 and 014 supply the
  conditions that trigger them.

## 6. Talking to the platform

- All calls go through the `core` HTTP client, which adds `traceparent` and `Accept-Language`.
- A failure is parsed as the Problem shape in [conventions.md](conventions.md) §4. The UI
  branches on `code`, shows `title`, and shows `traceId` as the reference.
- A response that is not a Problem (network failure, proxy page) is treated as
  `SERVICE_UNAVAILABLE` with a locally generated reference.

## 7. Unhandled failures

The global error handler posts to `POST /v1/client-errors`
([platform-api.openapi.yaml](platform-api.openapi.yaml)) with the route template, never the full
address, and never form values. At most one report per distinct message per minute per tab.

## 8. Placeholder page

Each app ships exactly one page for this spec:

- shows the app name, its version and the environment;
- shows whether the platform is reachable (calls `/v1/meta/version`);
- has a language switch;
- shows one date and one amount through the pipes;
- can force each of the six states (outside `production` only).
