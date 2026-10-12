# Contract: an operator's session (spec 010)

What every later spec of the operators console relies on. The addresses are in
`operators-api.openapi.yaml`; the tables in `../data-model.md`.

## 1. Who is asking

A route under `/v1/ops/` is given the signed-in operator through the `OperatorSession` scheme:
the operator's id and the session's id. It never reads a cookie itself.

| A route declares | It is reachable by |
|---|---|
| `AllowAnonymous()` | anyone — only what sign-in itself needs |
| `OperatorsOnly()` | any signed-in operator |
| "own account" | the signed-in operator, about their own account: profile, change password, sign out everywhere |
| nothing | nobody (Constitution III) |

Spec 011 adds "requires this operators' permission" and uses it on its own routes.

Rules held by architecture tests:

- every route under `/v1/ops/` carries one of the first three;
- no route outside `/v1/ops/` accepts the `OperatorSession` scheme, and no route under it
  accepts a clinic's session;
- the operators' cookies are read only by the operators' scheme and its sign-in addresses.

## 2. The cookies

| Cookie | Set by | Path | Flags |
|---|---|---|---|
| `__Host-scs-ops-signin` | step one | `/` | HttpOnly, Secure, SameSite=Strict; ten minutes |
| `__Host-scs-ops-session` | step two, renew | `/` | HttpOnly, Secure, SameSite=Strict; five minutes |
| `__Secure-scs-ops-renew` | step two, renew | `/v1/ops/auth` | HttpOnly, Secure, SameSite=Strict |
| `__Host-scs-ops-device` | step two, when new | `/` | HttpOnly, Secure, SameSite=Lax; 400 days |

With «تذكرني» unticked the session and renewal cookies have no expiry; ticked, the renewal
cookie expires at the session's end.

## 3. What ends a session, and what the next request gets

Every request checks the session's row. An ended session answers `401 UNAUTHENTICATED` with a
`reason`; the console shows the sign-in screen with the line for that reason.

| Ended by | `reason` | Line on the sign-in screen |
|---|---|---|
| the operator signs out | — | none |
| a sign-in completes elsewhere | `replaced` | "signed in on another device" (wording for the owner) |
| thirty minutes with no activity | `idle` | «انتهت الجلسة. الدخول من جديد للمتابعة.» |
| twelve hours from sign-in | `expired` | the same |
| a password reset completes | `password-reset` | «تمت إعادة تعيين كلمة المرور، فتم تسجيل خروجك. سجّل دخولك بكلمة المرور الجديدة.» |
| the account is suspended (011) | `suspended` | «تم قفل حسابك لأسباب أمنية، سجّل دخولك مرة أخرى.» |
| "sign out of all devices" | — | none |
| an old renewal secret is presented | `reuse` | the idle line; nothing about the cause is said |
| the role is changed (011) | `role-changed` | «تم تغيير دورك، فتم تسجيل خروجك. سجّل دخولك مرة أخرى للمتابعة.» |
| the fifth wrong current password | `locked` | the lock warning |

The reason is read once: after it is shown, the sign-in screen is plain.

## 4. Activity

The console marks a request it makes by itself — polling, a background refresh — with the
header `X-Scs-Background: 1`. Such a request, and a renewal, do not move the session's last
activity. Everything else does. The console itself asks who is signed in every twenty seconds
this way, so that a session ended elsewhere is shown within thirty seconds (FR-030); it
pauses while the tab is hidden. A later spec that adds polling must set the header; a test in
this spec fails a `GET` made from a timer without it.

## 5. For other modules — `Scs.Operators.Contracts`

| Operation | For |
|---|---|
| `IOperatorDirectory.FindAsync(id)` → name, email, language, state | showing "who did this" |
| `IOperatorSessions.EndAllAsync(operatorId, reason)` | spec 011: suspending, changing a role |
| `IOperatorAccess` → the signed-in operator's id, or none | any module with a route under `/v1/ops/` |

Nothing else of the module is public. No module reads `ops.*` tables.

## 6. Between tabs

The console posts "signed out" and "signed in" on a channel named for the operators console.
A tab that hears "signed out" shows the sign-in screen; one that hears "signed in" on the
sign-in screen goes inside. The channel carries no value of a session.
