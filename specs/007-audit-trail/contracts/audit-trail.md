# Contract: Writing to the Audit Trail

**Spec**: [../spec.md](../spec.md) FR-001 to FR-029 | **Data**: [../data-model.md](../data-model.md)

What every later spec does to record an action, and what the trail promises in return. Decided
once here.

## 1. To add a recorded action

1. Add a row to the catalogue (`Scs.SharedKernel/Audit/AuditActions`): code, category, names,
   sentences in Arabic and English, the facts it may carry, whether it needs before and after.
2. Add the code to `audit-actions.lock`.
3. Where the change is saved, record on the module's own context and save:

   ```text
   db.Record(new AuditRecord(action, target, facts, changes))   // once for each thing changed
   await db.SaveChangesAsync(cancellation)
   ```

   The save writes the entries in one transaction with the change (it opens one when the
   caller has none). A save that cannot be asynchronous is refused while entries wait.

4. Declare it on the route: `.Records(AuditActions.RefundIssued)`.

Nothing in the audit module changes (SC-009).

## 2. What the trail takes itself

The actor and their display name, the clinic, the address, the device, the trace and the time. A
caller cannot supply them.

## 3. What the trail refuses

| Refused | Why |
|---|---|
| a code that is not in the catalogue | FR-008 |
| a fact key the action does not allow | FR-010 |
| no changes on an action that needs before and after | FR-011 |
| recording outside a transaction that also holds the change | FR-009 |
| recording with no clinic context | Constitution II |

A refusal throws; the caller's transaction rolls back; the request fails as any unexpected
failure does. It is a defect in the caller, not something a person can cause.

## 4. What the trail skips, silently

An entry whose changes are all "before equals after" and nothing added or removed (FR-012).

## 5. Rules for the caller

| Rule | Source |
|---|---|
| One entry per thing changed; several changes to one thing are one entry | FR-013 |
| Name an account by reference; never put an email address in facts | FR-018 |
| For a clinical record, say that it changed and whose; never what | FR-017 |
| Never record a display preference | FR-019 |
| Never record a refusal | FR-019a |
| Work the platform does alone records with `BySystem = true`; with nobody signed in the platform is the actor anyway | FR-015 |

## 6. Route declarations

Every `POST`, `PUT`, `PATCH` and `DELETE` route carries one of `Records(…)`,
`RecordsNothing(reason)`, `RecordedBySignIn()`. `RouteAuditDeclarationTests` fails for a route
with none and names it. The list of today's routes is in [data-model.md](../data-model.md) §5.

## 7. Names and sentences

Read from the design (Figma `4HLWEqlna9sbn68276LHYt`, page "Activity log" `5450:4`): an action
is named by a noun, and described by one sentence in the passive. A sentence is a template with
named placeholders: `{target}`, a fact's key, and for changes `{added}`, `{removed}`,
`{before:field}`, `{after:field}`. Permissions are named from the permission catalogue in the
reader's language; a list longer than three shows the first three and «و{n} أخرى» · "and {n}
more".

| Code | Name (ar · en) | Sentence (ar) | Sentence (en) | Source |
|---|---|---|---|---|
| `signin.failed-run` | محاولة دخول فاشلة · Failed sign-in | {attempts} على حساب {target}{locked} | {attempts} on {target}'s account{locked} | frames `5632:44831`, `6589:934565` |
| `signin.succeeded` | تسجيل دخول · Sign-in | سُجِّل الدخول إلى حساب {target} | {target}'s account was signed in to | BRD catalogue; undrawn |
| `session.signed-out` | تسجيل خروج · Sign-out | سُجِّل الخروج من حساب {target} | {target}'s account was signed out | undrawn |
| `sessions.ended` | إنهاء جلسات · Sessions ended | أُنهيت جلسات {target} | {target}'s sessions were ended | undrawn |
| `password.reset-completed` | إعادة تعيين كلمة المرور · Password reset | أُعيد تعيين كلمة مرور {target} | {target}'s password was reset | BRD catalogue; undrawn |
| `password.temporary-replaced` | استبدال كلمة المرور المؤقتة · Temporary password replaced | استُبدلت كلمة المرور المؤقتة لحساب {target} | The temporary password of {target}'s account was replaced | undrawn |
| `invitation.accepted` | قبول دعوة · Invitation accepted | قُبلت دعوة {target} | {target}'s invitation was accepted | BRD catalogue; undrawn |
| `account.access-reissued` | إعادة إصدار وسيلة الدخول · Access re-issued | أُعيد إصدار وسيلة الدخول لحساب {target} | A way in was re-issued for {target}'s account | undrawn |
| `account.role-changed` | تغيير دور مستخدم · User's role changed | غُيِّر دور {target} من «{before:role}» إلى «{after:role}» | {target}'s role was changed from “{before:role}” to “{after:role}” | undrawn |
| `role.created` | إنشاء دور · Role created | أُنشئ الدور «{target}» بـ{count} | Role “{target}” was created with {count} | undrawn |
| `role.updated` | تعديل دور · Role edited | عُدِّل الدور «{target}»{changes} | Role “{target}” was edited{changes} | undrawn |
| `role.deleted` | حذف دور · Role deleted | حُذف الدور «{target}» | Role “{target}” was deleted | undrawn |

Parts that follow the number:

| Part | Arabic | English |
|---|---|---|
| `{attempts}` | محاولة واحدة · محاولتان · «٣ محاولات متتالية» to ten · «١١ محاولة متتالية» from eleven | 1 attempt · 2 attempts · "{n} attempts in a row" from three |
| `{locked}` when the run ended in a lock | ، فقُفل {minutes} دقيقة | ; locked for {minutes} minutes |
| `{count}` of permissions | صلاحية واحدة · صلاحيتين · «٣ صلاحيات» to ten · «١١ صلاحية» from eleven | 1 permission · {n} permissions |
| `{changes}` of a role | «: أُضيف {added}» · «: نُزع {removed}» · «: أُضيف {added}، ونُزع {removed}» · «: غُيِّر اسمه من «{before:name}»» | ": added {added}" · ": removed {removed}" · ": added {added}; removed {removed}" · ": renamed from “{before:name}”" |

The actor of `signin.failed-run` is read as «غير معروف» · "Unknown". The minutes of the lock
come from Identity's lock rule (15), not from the entry. A person given their first role is said
to have had «بلا دور» · "no role" before. An edit that changes several things joins them:
«: غُيِّر اسمه من «…»، وأُضيف …، ونُزع …» · ": renamed from “…”; added …; removed …".

## 8. For the operators' side

`IOperatorAuditReading` reads entries whose actor is an operator, across clinics, by the same
facets. It has no address and no caller until the operators' console exists. Entries with no
clinic have no table yet (research R5).
