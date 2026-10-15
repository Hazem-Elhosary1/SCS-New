# Contract: the addresses of operators and roles

**Feature**: 011-operators-roles

All under `/v1/ops/`, on the operators' session (spec 010). Each names its permission; the gate
answers before the handler runs (`operator-access.md`). Bodies are JSON; times are UTC instants;
errors are the platform's problem shape with a `code`.

## 1. Changed from spec 010

| Address | Change |
|---|---|
| `GET /auth/session` | adds `role: { id, name, systemKey }` and `permissions: string[]` |
| `GET /profile` | adds the role's name and system key (the top bar and profile show it) |

## 2. Reads

| Address | Permission | Answers |
|---|---|---|
| `GET /operators?search=&state=&roleId=&page=` | `operators.view` | `items[]` (id, fullName, email, role { id, name, systemKey }, lastSignInAtUtc, state, twoStep), `total`, `page`, `pageSize` (8), `cards` { total, active, activatedThisMonth, invited, twoStepOn }, `filters` { states[], roles[] } — only the values that exist |
| `GET /operators/{id}` | `operators.view` | the row's fields plus mobile, department, jobTitle, language, createdAtUtc, invitedBy { id, name } or null, `firstOperator` (bool: made by the deployment command before anyone else), `locked` (bool), `permissions[]` of the role, `performance[]` (key, value — today `activeSessions`), `activity[]` (the last ten: `kind` — `signed-in` or an action's code —, `name`, `role` or `roleKey`, `device`, `atUtc`), `version`, and `can` { edit, suspend, reactivate, sendLink, unlock } for this caller |
| `GET /roles` | `operators.view` | `items[]` (id, name, systemKey, description, operators, inUse, permissions count, `can` { edit, delete }), `cards` { total, inUse, custom, system } — system roles first, then custom oldest first |
| `GET /roles/{id}` | `operators.manageRoles` | name, description, systemKey, `codes[]`, `version`, `readOnlyReason` (`own-role` · `system` · null), and `copy` { `codes[]`, `left[]` }: what a new role based on this one starts with for this caller, and what was left out (FR-017) |
| `GET /roles/options` | `operators.manage` | for the role picker: `items[]` (id, name, systemKey, `codes[]`, `grantable`, `reason`: `holds-more` · `super-admin-only` · null) |
| `GET /permissions` | `operators.manageRoles` | the catalogue in its nine groups: code, group, `needs`, and `mayGrant` for this caller |

`404 NOT_FOUND` for an id that does not exist. A deleted role is not found.

## 3. Operators — writes

| Address | Permission | Body | Success | Records |
|---|---|---|---|---|
| `POST /operators` | `operators.manage` | fullName, email, roleId, mobile?, department?, jobTitle?, language, twoStep, sendInvitation | 201 `{ id }`; state `invited`; an invitation owed when `sendInvitation` | `operator.invited` |
| `PUT /operators/{id}` | `operators.manage` | the same without email and sendInvitation; `note?`; `version` | 204; sessions of the account ended when the role changed | `operator.edited`, `operator.role-changed` |
| `POST /operators/{id}/suspend` | `operators.manage` | `reason?` (≤500) | 204; state `suspended`; sessions ended | `operator.suspended` |
| `POST /operators/{id}/reactivate` | `operators.manage` | — | 204; state `active`; tries and lock cleared | `operator.reactivated` |
| `POST /operators/{id}/password-link` | `operators.manage` | — | 202; a reset link (active) or the invitation anew (invited) is sent | `operator.link-sent` |
| `POST /operators/{id}/unlock` | `operators.manage` | — | 204; tries and lock cleared | `operator.unlocked` |

### Refusals

| Status · code | When | Words (Arabic · English are in `operator-screens.md`) |
|---|---|---|
| 400 `VALIDATION_FAILED` | a field is missing, too long, or not of its kind; `errors` names the field | «هذا الحقل مطلوب.» and the field's own |
| 409 `OPS_EMAIL_TAKEN` | the email is an operator's already | at the email field |
| 403 `OPS_ROLE_NOT_GRANTABLE` | the role holds a permission the caller does not, or is the super admin's and the caller is not one | «لا يمكنك منح دور به صلاحيات ليست لديك.» |
| 403 `OPS_OWN_ROLE` | the caller changes the role of their own account | «لا يمكنك تغيير دورك.» |
| 409 `OPS_LAST_SUPER_ADMIN` | the change or the suspension would leave no active super admin | the two sentences of §5-هـ-3 and §5-هـ-4 |
| 403 `OPS_OWN_ACCOUNT` | the caller suspends their own account | (the button is not shown) |
| 409 `OPS_STATE_CHANGED` | the account is not in the state the action needs (suspend a non-active, reactivate a non-suspended, unlock a non-locked, link for a suspended) | «تعذّر …» of the action |
| 409 `OPS_LINK_TOO_SOON` | a link was sent to the account less than a minute ago; nothing is sent or recorded | «أُرسل رابط لهذا الحساب منذ أقل من دقيقة…» |
| 409 `OPS_CHANGED_ELSEWHERE` | `version` is not the row's | the form offers to reload |
| 503 `OPS_LINK_NOT_SENT` | the reset email could not be handed to the mail server | «تعذّر إرسال رابط إعادة التعيين.» |

An invitation that cannot leave is not a refusal of `POST /operators`: the account exists, the
invitation stays owed and is tried again each minute (spec 010).

## 4. Roles — writes

| Address | Permission | Body | Success | Records |
|---|---|---|---|---|
| `POST /roles` | `operators.manageRoles` | name, description?, codes[], basedOnId? (recorded; the codes sent are what is saved) | 201 `{ id }` | `opsrole.created` |
| `PUT /roles/{id}` | `operators.manageRoles` | name, description?, codes[], note?, version | 204; holders keep their sessions | `opsrole.edited` |
| `POST /roles/{id}/delete` | `operators.manageRoles` | note? | 204 | `opsrole.deleted` |

What is saved is the given codes plus every code they need (data-model §1); the answer of a
later read shows them.

| Status · code | When |
|---|---|
| 400 `VALIDATION_FAILED` | name empty or over 100; description over 500; no code; a code not in the catalogue |
| 409 `OPS_ROLE_NAME_TAKEN` | another role that is not deleted has the name, or it is a system role's |
| 403 `OPS_PERMISSIONS_NOT_HELD` | the body adds codes the caller does not hold; `permissions[]` names them — «لا يمكنك إضافة صلاحيات ليست لديك: {الصلاحيات}.» |
| 403 `OPS_OWN_ROLE_EDIT` | the caller is not a super admin and holds the role |
| 403 `OPS_SYSTEM_ROLE` | the role is one of the three |
| 409 `OPS_ROLE_IN_USE` | deleting a role that operators hold; `operators` gives the count |
| 409 `OPS_CHANGED_ELSEWHERE` | `version` is not the row's |

## 5. The deployment command (spec 010), after this spec

`operator invite --name … --email …` gives the super admin role, sets no inviter, and records
`operator.invited` with `by: deployment` beside its own sign-in record. Its other answers are
unchanged. `operator withdraw` is unchanged.

## 6. What no address does

No address returns a link, a token, a password or a password hash. No address deletes an
operator. No address changes an email. No address lists what a role's holders are doing.
