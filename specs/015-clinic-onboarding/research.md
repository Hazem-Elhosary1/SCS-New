# Research: Clinic Onboarding

**Feature**: [spec.md](spec.md) · **Plan**: [plan.md](plan.md) · **Date**: 2026-10-17

The code facts below were read on 17 Oct 2026, from `develop` at `0762992` (014 merged).

## What exists, and what 015 has to add

| Need | Today | Gap |
|---|---|---|
| Create a clinic | `IClinicRegistry.RegisterAsync(shortName, nameAr, nameEn, operatorId)` on the crossing connection. One `SaveChanges`, then the provisioners (roles only) in their own scope. No production caller. | No slug derivation, no profile fields, no setting-up state. |
| Clinic fields | `tenancy.Clinic`: names (200), `State` (`active` default), `Specialty` (40, nullable, no setter), `CreatedAtUtc`. The application login is denied writes on schema `tenancy`. | Address, tax number, language, account manager, the operator who added it. |
| Owner account | `identity.StaffAccount`: email (platform-unique, "never changes"), state, role. No name, mobile or last sign-in. `IAccountHandover.CreateWithLinkAsync` commits, then emails. | Name, mobile, email change, invitation status, cooldown, `ACCOUNT_LINK_NOT_SENT`. |
| Owner role | `ReadyRoles` creates «مالك العيادة» through `RolesProvisioner`. `IClinicRoles.AssignAsync` assigns. | Nothing new. |
| Plan link | `subscriptions.ClinicPlan(ClinicId, PlanId, SetAtUtc)`; `IClinicPlans.SetAsync` with a lock and `subscription.plan-set`. | Trial end; refusing a disabled plan. |
| Branch | Nothing on the server. | A table and a module. |
| Operator in a clinic | No operator request establishes a clinic. `ClinicSource` is `patient-address`, `account` or `job`. The clinic trail records operators as `system`. | An operator source; the operator as actor. |
| Plans' figures | `IPlanSubscribers` answers "no clinics" (`NoSubscribers`, `TryAddScoped`). | The real answer. |
| Ops console | Sections home, plans, operators. The `ui` library has the toolbar, filter popover, chips, table, footer, KPI card, tabs, menu, badge, breadcrumb and select. | A sortable column header, a copy button, the clinics pages. |

## R1 — The ops clinic screens live in `Subscriptions`; each part stays with its owner

- **Decision**:
  - The operators' clinic routes (`/v1/ops/clinics…`) and the onboarding flow are in the
    `Subscriptions` module, under `Api/OpsClinicsEndpoints.cs` and
    `Infrastructure/ClinicOnboarding.cs`.
  - Each piece of data stays with the module that owns it. Each is written only through that
    module's published contract:
    - **Tenancy**: the clinic, its names, slug, specialty, address, tax number, language, state,
      account manager and who added it;
    - **Subscriptions**: the plan link and the trial;
    - **ClinicData** (new, R3): the main branch;
    - **Identity**: the owner's account, name, mobile, role and invitation;
    - **Operators**: the list of active operators;
    - **Plans**: the assignable plans.
- **Why**:
  - Specs 016–021 put their actions on these same screens: lifecycle, invoices, payments, plan
    change and extra units. 014 (R1) already chose `Subscriptions` as the module those specs grow.
  - Tenancy is the clinic's identity and isolation. It should not learn about plans, owners or
    branches.
- **Rejected**:
  - A separate `Clinics` ops module. It would only orchestrate, and 016–021 would split their ops
    actions between it and `Subscriptions`.
  - Putting the flow in Tenancy. Tenancy would then reference Identity, Subscriptions and
    ClinicData, the reverse of today's direction.

## R2 — One add across five modules: check first, a setting-up state, then make it visible

The add touches tables owned by four modules. They sit on two database logins: the crossing login
for `tenancy`, and the application login with row level security for the rest. No connection or
transaction can span them. There is no shared unit of work: 47 places open their own transaction.
A distributed transaction is not available on Linux containers (ECS Fargate).

- **Decision**: the add is "all or nothing as anyone sees it". It runs in four stages.
  1. **Refuse before writing.** Under one platform-wide add lock (R11), every check that can
     refuse the add runs before anything is written:
     - the fields;
     - the specialty is one of the fifteen;
     - the plan is assignable, else `PLAN_DISABLED`;
     - the plan's users limit is not 0;
     - the owner's email belongs to no account on the platform (R6).
     So a refusal (FR-003, SC-003) leaves nothing: no clinic, branch, account, link or trail entry.
  2. **Write, invisible.**
     1. Tenancy registers the clinic in the stored state **`setting-up`**, with its profile and
        derived slug, in one `SaveChanges` on the crossing connection. The roles provisioner then
        runs as today.
     2. In the new clinic's context (R4), each module writes its part in its own transaction, in
        this order:
        - Identity: the owner account, invited, with its name, mobile and the owner role. It goes
          first, because it is the one write another writer can still refuse (`email: TAKEN`,
          R6), and nothing of the add has been recorded when it does. The seat is safe: stage 1
          checked that the plan's users limit is not 0, and the new clinic holds no account;
        - ClinicData: the main branch;
        - Subscriptions: the plan, the trial end and `subscription.plan-set`, in one transaction
          (constitution IV), last before activation.
        So a refusal in stage 2 comes before any trail entry of the add. What remains is the
        `discarded` clinic row, its retired slug and its ready roles (written by the roles
        provisioner, `bySystem`). None of it is reachable: it is in no list or count, and its id
        answers `NOT_FOUND` (the owner's answer of 17 Oct 2026: "reorder: nothing at all").
  3. **Make it visible.** Tenancy changes `setting-up` to `active` and writes `clinic.created` to
     both trails, in one transaction (R5).
  4. **Email.** After that, the invitation is sent (R7). A failed email does not undo the add
     (FR-004).
- **A clinic in `setting-up`**:
  - it is in no list, count, figure or job (`ListAsync`, `ListClinicIdsForJobsAsync`, R13);
  - the locator refuses it (`ClinicStates.NotPubliclyAvailable`);
  - an operator route answers `NOT_FOUND` for it;
  - its owner account was never sent a link, so nobody can sign in to it.
- **A fault after stage 1** (a database or network failure, not a refusal): the operator sees
  «تعذر إنشاء العيادة.». A best-effort undo, in reverse order, removes what the attempt wrote:
  - Identity removes the invited owner account that never received a link, which frees the email
    (`IClinicOwners.DiscardAsync`);
  - Tenancy marks the clinic `discarded` and retires its slug.
  If the undo fails too, the clinic stays in `setting-up`, still invisible. The id and the failed
  stage are logged, with no personal data.
- **The spec's words**: FR-004 and the edge case are reworded to say what anyone sees. The
  constitution is unaffected: every recorded change is still in the same unit as its record.
- **Rejected**:
  - **One connection per request scope shared by every module context**: it changes how every
    module opens transactions.
  - **The whole add on the crossing connection**: one module would write the others' tables
    (constitution: boundaries).
  - **`TransactionScope`**: no distributed transactions on Linux.

## R3 — The main branch: a new module `ClinicData`

- **Decision**:
  - A new module `ClinicData` (schema `clinicdata`), for the clinic volume's chapter 11 «إدارة
    البيانات». It owns one clinic-owned table, `clinicdata.Branch`:
    - `Id`, `ClinicId`, `NameAr`, `NameEn`, `Address`, `IsMain`, `CreatedAtUtc`;
    - a filtered unique index `UX_Branch_OneMain` on `ClinicId WHERE IsMain = 1`;
    - row level security in the migration that creates it.
  - One contract, `IBranches`:
    - `CreateMainAsync(nameAr, nameEn, address)`, refused with `CLINIC_HAS_MAIN_BRANCH` when one
      exists;
    - `CountAsync()`, for «عدد الأطباء / الفروع».
  - When the clinic's address changes later, the main branch keeps its own: the BRD's "equal at
    creation" (§3-4).
- **Why**:
  - 028 (branches) and 029 (doctors) are chapter 11 and grow this module.
  - `max_branches` is counted by the module that owns branches (`docs/adding-a-module.md`).
- **Rejected**:
  - A branch table in `tenancy`: the application login cannot write there, and 028's clinic
    staff must.
  - Putting branches in Identity: they are not accounts.

## R4 — An operator works inside a clinic: `ClinicSource.Operator`

- **Decision**:
  - `ClinicSource` gains `Operator = "operator"`.
  - Every `/v1/ops/clinics/{id}…` route goes through one filter, `OpsClinicScope`, before its
    handler:
    1. `IClinicContext.Establish(id, ClinicSource.Operator)`;
    2. `IClinicProfile.GetOwnAsync()`, under ordinary isolation;
    3. no row, or a clinic in `setting-up` or `discarded`: `NOT_FOUND` (FR-019).
  - From then on, all reads and writes in other modules happen under row level security for
    that clinic. They are the same calls the clinic's own staff would make.
  - The add establishes the new clinic in its request scope after registering it.
- **Why**:
  - Constitution II: no bypass where isolation can serve. An operator acting on one clinic is
    just a request in that clinic.
  - Only reading across clinics (the list and the plans' figures) needs a crossing (R13).
- **Rejected**: reading one clinic's detail through crossings. That is wider than needed.

## R5 — The trails: the operator is the actor in the clinic's trail too

- **Decision**:
  - `AuditWriter.AmbientAsync` today reads only the staff claim. It learns the operator claim:
    an ops request writing to a clinic trail records the actor kind `operator`, its id, and the
    name from `IOperatorActorNames`. The database already allows `operator`
    (`CK_Entry_ActorKind`).
  - For the one write made on the crossing connection, the activation in R2 stage 3, a new
    `AuditWriter.RecordForClinicAsync(db, clinicId, record)` writes the clinic-trail entry. It
    runs inside the transaction on that connection, under the declared crossing
    `register-clinic`.
  - New actions in `AuditActions.cs` and `audit-actions.lock`, appended:

| Code | Category | Facts | Before/after | Written by |
|---|---|---|---|---|
| `clinic.created` | changes | `slug`, `plan`, `specialty` | yes (each field from empty) | Tenancy, on activation |
| `clinic.updated` | changes | `note` | yes | Tenancy |
| `clinic.owner-updated` | users | `note` | yes (name, email, mobile) | Identity |
| `clinic.account-manager-assigned` | changes | — | yes | Tenancy |
| `clinic.invitation-resent` | users | — | no | Identity |

  - **`clinic.owner-updated`** is the owner's part of an edit. It is in Identity's transaction,
    with the change it records. The clinic's part is `clinic.updated` in Tenancy's. An edit that
    changes both writes two entries carrying the same note. FR-021 and FR-027 are reworded to say
    so (one transaction cannot hold both, R2).
  - The email in `clinic.owner-updated` is recorded as before/after values (FR-021a's "the trail
    keeps both"). `email` is on `NeverKeys` for facts only; changes are values, and spec 007
    already records account emails as changes.
- **Rejected**: one `clinic.updated` written after both parts. If the second part failed, the
  first change would have no record in its own unit (constitution IV).

## R6 — "This email has no account anywhere": a narrow read, no new crossing

- **Decision**: Identity's new contract `IClinicOwners.EmailIsFreeAsync(email)`. It calls
  `identity.usp_FindAccountForSignIn`, the database crossing declared for sign-in
  (`DeclaredDatabaseCrossings`), and answers only true or false. The caller is a signed-in
  operator holding «تعديل بيانات العيادات», so nothing is revealed before sign-in (SH-043).
- **Race**: two adds with the same email are serialised by the add lock (R11), so the second
  sees the email taken in stage 1.
- **Other writers**: an account made at the same moment by a clinic (026, later) could still win.
  Then `UX_StaffAccount_Email` refuses the owner, the first write of stage 2, and the undo of R2
  runs. The operator sees the email refusal on the field. No branch, plan link, account or trail
  entry of the add remains; only the unreachable `discarded` row, its slug and its ready roles.
- **Rejected**: a new cross-clinic read of `identity.StaffAccount`. The procedure already answers
  this.

## R7 — The invitation: issued after activation; status, cooldown and failure made explicit

- **Decision**:
  - `IClinicOwners.CreateAsync(email, fullName, mobile)` creates the invited account and assigns
    the owner role, in one Identity transaction with the seat check. It sends nothing.
  - After activation, `IAccountHandover.ResendInvitationAsync(accountId)` sends the 72-hour link.
    It already sends before storing, so a failed email stores nothing and `EmailSent` is false.
  - **Status**: `IClinicOwners.FindAsync()` returns the clinic's owner: id, email, name, mobile,
    state, last sign-in, and the invitation, which is one of:
    - `live` (expires at);
    - `lapsed` (expired at);
    - `none` (never sent, or nothing live).
    These are BRD §4-7's three states, worked out from `identity.PasswordLink` (kind
    `new-account`).
  - **Re-sending from the detail** (`POST …/invitation`), through
    `IClinicOwners.ResendInvitationAsync()`:
    - an owner who is not `invited` gets `INVITATION_ALREADY_ACCEPTED`;
    - a new-account link issued less than 60 s ago gets `RESET_EMAIL_COOLDOWN` (BRD §3-3: one
      email a minute per account). Today's cap counts reset links only; the invitation now has its
      own check;
    - an email that did not leave gets `ACCOUNT_LINK_NOT_SENT` (BRD §5-18), and the earlier link
      still works;
    - success replaces every earlier link and answers the new expiry.
  - **The owner** is the clinic's earliest account holding the role «مالك العيادة»
    (`ReadyKey = owner`).
- **Why**: the BRD names all three codes and their words (§5-18). Today a failure is only a
  boolean.

## R8 — The owner's name, mobile and email change

- **Decision**:
  - `identity.StaffAccount` gains:
    - `FullName nvarchar(200) NULL`;
    - `Mobile varchar(20) NULL`, kept as typed without spaces and dashes (`+` allowed);
    - `LastSignInAtUtc datetimeoffset NULL`, written when a session opens (BRD §3-2: the latest
      session opened).
    The account's "email never changes" comment is replaced.
  - **`IClinicOwners.UpdateAsync(fullName, email, mobile, note)`**, in one Identity transaction:
    - updates the account;
    - writes `clinic.owner-updated` with the before and after values;
    - for an `invited` owner whose email changed: replaces all live links;
    - for an `active` owner whose email changed: ends every session
      (`EndForAccountCoreAsync`, reason `email-changed`, a new `SessionEndings` value).
    After the commit, the notice of `contracts/emails.md` goes to the old address. Its failure is
    logged and changes nothing (FR-021a).
  - **The email**: trimmed and lower-cased. A taken email answers `email: TAKEN`, through
    `UX_StaffAccount_Email` as today.
  - 026 adds a name to every account. It reads the same column.
- **The mobile rule** (clarified): an optional `+`, then 8 to 15 digits. Spaces and dashes are
  ignored. No country is assumed.

## R9 — The clinic's new fields, and the slug

- **Decision**: `tenancy.Clinic` gains:
  - `Address nvarchar(300) NULL` (required at creation; the seeded clinics have none);
  - `TaxNumber char(11) NULL` (`NNN-NNN-NNN`);
  - `DefaultLanguage varchar(2) NOT NULL DEFAULT 'ar'`, checked `IN ('ar','en')`;
  - `AccountManagerId uniqueidentifier NULL` (an operator id, no foreign key across schemas);
  - `CreatedBy nvarchar(100) NULL` (the operator's id, as `RegisterAsync` already receives it);
  - `ClinicStates` gains `SettingUp = "setting-up"` and `Discarded = "discarded"`. Both join
    `NotPubliclyAvailable`.
- **Domain methods**: `Clinic.Create` takes the profile. `UpdateProfile(…)` returns the changed
  fields. `AssignAccountManager(id?)` and `Activate()` are added.
- **The slug**, `ShortName.Derive(englishName, arabicName)`, a pure function:
  1. Take the English name, or the Arabic when the English is empty.
  2. Lower-case it and keep runs of `[a-z0-9]`, joined by `-`.
  3. Drop leading digits and dashes, so it starts with a letter.
  4. Cut to 33 characters, so `-NN` still fits in 40.
  5. Use `clinic-<6 of [a-z0-9]>` when the result is under 3 characters, reserved, or empty.
  The registry tries the candidate, then `-2`, `-3` and so on against `tenancy.ClinicDirectory`,
  which keeps retired names too, so a slug is never reused. A clash at insert (`TAKEN`) retries
  with the next number, at most 20 times.
- **Writes**:
  - Tenancy writes through `IClinicRegistry`, the crossing connection. The application login is
    denied `tenancy` writes, by 002's design.
  - New operations:
    - `RegisterAsync(ClinicRegistration)`, which replaces the four-argument form (the seed moves
      to it);
    - `ActivateAsync(clinicId, record)`;
    - `DiscardAsync(clinicId)`;
    - `UpdateProfileAsync(clinicId, ClinicProfileChange, note)`;
    - `AssignAccountManagerAsync(clinicId, operatorId?)`.
  - Declared crossings: `update-clinic` (recorded) covers the profile, the manager, activation
    and discarding.
- **The tax number**: nine digits, with or without dashes, kept as `NNN-NNN-NNN`. In an edit, a
  stored value that has not changed is accepted as it is (FR-022). An empty value sent on purpose
  removes it. A missing field keeps it.

## R10 — The trial: an end date on the plan link, and a length from configuration

- **Decision**:
  - `subscriptions.ClinicPlan` gains `TrialEndsAtUtc datetimeoffset NULL`.
  - `IClinicPlans.StartAsync(planId, trialEndsAtUtc)` refuses a plan that is not assignable
    (`PLAN_DISABLED`). In one transaction it writes the link and `subscription.plan-set` (now with
    the fact `trial-ends`).
  - **The length**: `ScsOptions.Subscriptions.TrialDays`, from `SCS_SUBSCRIPTIONS__TRIAL_DAYS`,
    default 14, between 1 and 90, validated at start. 024 moves it to a platform setting.
  - **The badge** comes from a pure function, `ClinicStatus.Of(storedState, trialEndsAtUtc, now)`:
    `trial` before the end, `active` after. It answers `{code, ar, en, tone}`; 016 adds the other
    codes.
  - «ينتهي في» is the trial's end while in trial, and empty after (018 adds renewal).
  - «مدة الاشتراك» is `trial` with its days, and empty («غير محددة») after.
- **Why**: the clarified answer. The end is fixed at creation, so a later change of the setting
  leaves existing clinics alone (edge case).

## R11 — Locks

- **The add**:
  - `sp_getapplock 'scs:clinic-add'`, exclusive, 15 s, held on the crossing connection for the
    whole flow. Exposed as `IClinicRegistry.TakeAddTurnAsync()`, which returns an
    `IAsyncDisposable`.
  - Adds by hand are rare, so a few seconds of queueing is fine. SC-002's ten at once finish one
    after another.
  - A timeout answers `LIMIT_BUSY` (503), as 014 does.
- **The plan link**: 014's `scs:clinic-plan:{id}`.
- **The seat**: 014's `scs:plan-limit:{clinic}:max_users`.
- **The invitation**: Identity's `scs:password-link:{account}`.
- **The account manager and the edit**: optimistic. The detail carries the clinic's
  `RowVersion`. A stale save answers `CLINIC_CHANGED_ELSEWHERE` (409) with «تغيّرت بيانات هذه العيادة
  بعد فتحها» (proposed, the pattern of 013's `PLAN_CHANGED_ELSEWHERE`), and the form's reload
  button as in 013.

## R12 — Who may be the account manager: a published list of active operators

- **Decision**:
  - `IOperatorDirectory` gains `ActiveAsync()`, which returns `OperatorSummary` for every `active`
    operator, ordered by name.
  - The assign route checks the chosen id against `FindAsync` (state `active`), else
    `OPERATOR_NOT_ACTIVE` (BRD §5-17, in the passive: «تعذر تحديث مسؤول الحساب. تُعاد المحاولة بعد قليل.»).
  - The detail shows the manager's name through `FindAsync`, even when that operator is suspended
    later (edge case).
- **Why**: the list must be readable by any holder of «تعديل بيانات العيادات». That person may not
  hold «عرض المشغّلين», which guards `GET /v1/ops/operators`.

## R13 — Reading across clinics: three narrow crossings, all reads

The list (FR-007 … FR-013) and the plans' figures (FR-029) need every clinic's row.

- **Decision**: three declared crossings, each a read with no writes, each on
  `ICrossClinicConnectionFactory` with `IgnoreQueryFilters`, as `OperatorAuditReading` does:

| Operation | Type | Recorded | Reads |
|---|---|---|---|
| `list-clinics` (exists) | `ClinicRegistry` | yes | `tenancy.Clinic` + current slug: names, state, created at/by, manager. Now skips `setting-up` and `discarded`. |
| `read-clinic-plans-as-operator` | `Scs.Subscriptions.Infrastructure.OperatorClinicPlans` | no | `subscriptions.ClinicPlan`: clinic, plan, trial end, set at |
| `read-clinic-owners-as-operator` | `Scs.Identity.Infrastructure.OperatorClinicOwners` | no | the owner of each clinic: name, email, mobile (no hash, no secrets) |

- **The list endpoint**:
  - It reads the three, joins them by clinic id in memory, then searches, filters, sorts, works
    out the cards and pages.
  - SC-007 asks for a few hundred clinics under a second: three indexed scans and an in-memory
    join of a few hundred rows.
  - A volume past roughly 5,000 clinics would move this to a read model. This is recorded, not
    built.
- **`IPlanSubscribers`**:
  - Subscriptions registers `PlanSubscribers` with `AddScoped`, after Plans, which replaces
    `NoSubscribers`.
  - It reads through `OperatorClinicPlans` and Tenancy's `ListAsync` (for names, state and join
    date).
  - It counts a clinic per plan when its state is `active`.
  - `OnTrial` counts clinics before their trial end. `NewThisMonth` uses the Cairo month of
    `SetAtUtc`.
  - `MonthlyRevenue` is 0 for every clinic until 018: a clinic with no cycle counts nothing (the
    clarified answer).
  - `PlanClinic`'s state words come from `ClinicStatus.Of`.
- **Tests**:
  - `DeclaredCrossingsTests` goes from 9 to 11; the recorded set adds `update-clinic`.
  - Each new type is the only one using the factory in its module.

## R13a — Words used for one thing

- "slug" (spec) = `ShortName` (code) = the clinic's path on `scs-heta.com/<slug>`.
- "account manager" (code) = «المسؤول من SCS» (frame) = «مدير الحساب» (BRD §2-2).

## R14 — Specialties: one published list

- **Decision**:
  - The fifteen keys move from `Plans`' internal `Specialties` to
    `Scs.SharedKernel/Clinics/Specialties.cs`: `All`, in the BRD's order, and `IsKnown`.
    `Plans` uses it.
  - The database check `CK_Feature_Specialty` is unchanged (same values).
  - Tenancy checks `Clinic.Specialty` with the same list. A new check constraint `CK_Clinic_Specialty`
    allows NULL (the seeded clinics, and «غير مسجّل»).
  - The web reuses `app.plans.specialty.<key>` from 013. The edit form adds «غير مسجّل» as the
    empty choice (FR-020).
- **Why**: it is a fixed BRD list (SH-013), not catalogue data. The kernel already holds
  `FeatureKeys`.

## R15 — The ops console: pages, new shared pieces, words

- **Routes** (`ops/src/app/app.routes.ts`), each through `guarded()`:
  - `clinics` (`clinics.view`);
  - `clinics/new` (`clinics.edit`), placed before `clinics/:id`;
  - `clinics/:id` (`clinics.view`), with the child tabs `overview` and `settings`;
  - `clinics/:id/edit` (`clinics.edit`).
- **Side bar**: `{ key: 'clinics', path: '/clinics', icon: 'building', permission: 'clinics.view' }`,
  after `home`. `building` is in the icon registry; if the frame's side bar draws another icon, that
  one is added from the Figma kit (095's rule).
- **Pages**, modelled on `plans/*`:
  - `clinics-list.page.ts`, `clinic-form.page.ts` (add and edit), `clinic-detail.page.ts`,
    `clinic-overview.tab.ts`, `clinic-settings.tab.ts`;
  - `clinics.service.ts`, `clinic-words.ts`.
- **New shared pieces in `ui`**, each with a gallery entry (constitution: a missing component goes
  in the shared library):
  - `button[scsSortHeader]`: a header that sorts, with `aria-sort` and the ▾/▴ the frame draws;
  - `scs-copy-button`: `navigator.clipboard.writeText`, a toast on success, and the refusal words
    when the browser refuses.
- **The inline account manager**: a `scs-select` with a visually hidden label, as the frame draws
  it, plus «حفظ» and «إلغاء».
- **Words**:
  - under `app.clinics.*` in `ops/public/i18n/{ar,en}.json`, quoted from the BRD and the frames;
  - the specialty names under `app.plans.specialty.*`;
  - dates through `ops-format.ts`. The ops formatters use the browser's time zone today; 015's
    dates (joined, trial end, invitation expiry) go through a new `opsCairoDate` / `opsCairoDateTime`
    that fixes `Africa/Cairo` (SH-001), with Western digits.

## R16 — What changes in built specs' code (their documents stay untouched)

| Built spec | Code changed by 015 |
|---|---|
| 002 Tenancy | `Clinic` fields, states, `ShortName.Derive`, registry operations, crossings, seed call |
| 003/004 Identity | `StaffAccount.FullName/Mobile/LastSignInAtUtc`, `IClinicOwners`, invitation status and cooldown, session ending `email-changed`, `usp_FindAccountForSignIn` reused |
| 007 Audit | `AmbientAsync` learns the operator; `RecordForClinicAsync`; five actions |
| 009/010 Operators | `IOperatorDirectory.ActiveAsync` |
| 011 ui | `scsSortHeader`, `scs-copy-button`, gallery |
| 012/013 Plans | `Specialties` moved to the kernel; `IPlanSubscribers` gets a real implementation (registered by Subscriptions); `PlanClinic.TrialEndsAtUtc` and the plan detail's trial line (T074) |
| 014 Subscriptions | `ClinicPlan.TrialEndsAtUtc`, `IClinicPlans.StartAsync`, `PLAN_DISABLED`, the ops clinic routes |
| 003 building blocks | the staff session takes nothing on an operators' route (found on the real stack: both cookies on one host) |
| 011 ui (since) | the required drop-down's star; the drop-down's opt-in `labelHidden` |
| 010/013 ops web | sections, routes, `ops.spec.ts`'s pinned section list |

## R17 — Error codes

New in `ErrorCodes.cs` and `ErrorMessages.cs`, with words in both languages:

| Code | Status | Arabic words | Source |
|---|---|---|---|
| `PLAN_DISABLED` | 409 | «تعذر إنشاء العيادة.» | BRD §5-2 |
| `RESET_EMAIL_COOLDOWN` | 429 | «أُرسلت دعوة لهذا البريد قبل أقل من دقيقة. يمكن إعادة الإرسال بعد قليل.» | BRD §5-18 |
| `ACCOUNT_LINK_NOT_SENT` | 502 | «تعذر إرسال البريد، فلم يصل شيء إلى المالك. تُراجع إعدادات البريد ثم تُعاد المحاولة.» | BRD §5-18 |
| `INVITATION_ALREADY_ACCEPTED` | 409 | «مالك هذه العيادة قَبِل دعوته بالفعل.» | BRD §5-18 (code proposed) |
| `OPERATOR_NOT_ACTIVE` | 409 | «تعذر تحديث مسؤول الحساب. تُعاد المحاولة بعد قليل.» | BRD §5-17 in the passive (code proposed) |
| `CLINIC_CHANGED_ELSEWHERE` | 409 | «تغيّرت بيانات هذه العيادة بعد فتحها» | proposed (R11) |
| `CLINIC_HAS_MAIN_BRANCH` | 409 | «للعيادة فرع رئيسي بالفعل.» | proposed (R3) |

Field codes under `VALIDATION_FAILED` and their words:

- `REQUIRED`: «هذا الحقل مطلوب.»
- `TAX_NUMBER`: «أدخل رقم التسجيل الضريبي المكوّن من 9 أرقام.»
- `MOBILE`: «رقم الموبايل غير صحيح.» (proposed)
- `TAKEN` on the email: «هذا البريد مسجَّل لحساب آخر على المنصة.» (proposed)
- `TOO_LONG`, `INVALID`: as today.

Three of the BRD's own sentences addressed the reader with an imperative: «انتظر قليلًا وحاول»,
«راجع … ثم أعد المحاولة» and «حاول مرة أخرى» (§5-17, §5-18). By the owner's answer of 17 Oct 2026
they are built in the passive (SH-098). The words proposed here use none. «تحقق من اتصالك وحاول مرة
أخرى» of the load errors stays: the console's shared words.
