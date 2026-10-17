# Research: Plan Enforcement and Limit Metering

Decisions taken while planning spec 014. Each gives what was chosen, why, and what was turned
down. Code read on 17 Oct 2026 at develop `0b32787`.

## R1 — A new `Subscriptions` module holds the link, the meter and the one answer

- **Decision**: add a module `Subscriptions` with:
  - schema `subscriptions`;
  - a project `Scs.Subscriptions` (domain, infrastructure, API);
  - a project `Scs.Subscriptions.Contracts`.
  It owns three things:
  - the clinic's plan link;
  - the monthly meter;
  - the one answer to "what does this clinic have" (`IClinicEntitlements`), with the checks built
    on it.
- **Why**:
  - The BRD area is the clinic's subscription (Shared §4–§5, Ops module 16). Specs 015, 016, 018,
    019, 020 and 021 add its states, invoices, payments, plan changes and extra units, so they all
    grow this one module.
  - The `Plans` module cannot hold the link. 013 published that "the plans module never reads a
    clinic itself: the clinic depends on the plan, never the reverse" (`PlanContracts.cs`).
  - `Tenancy` is the clinic's identity and isolation (002), not its commercial terms.
- **Rejected**:
  - The link as a `PlanId` column on `tenancy.Clinic`. That mixes isolation with billing, and the
    meter and 016's states would follow it into `Tenancy`.
  - A module named `Entitlements`. It would be emptied again by 016–021, which are about the
    subscription.

## R2 — The link is a clinic-owned row, one per clinic

- **Decision**: a table `subscriptions.ClinicPlan`:
  - `ClinicId` is the primary key, and the row is `IClinicOwned`;
  - `PlanId` (nullable `uniqueidentifier`, no foreign key across schemas);
  - `SetAtUtc`;
  - `Version` (rowversion).
  The row level security policy is written in the migration that creates the table (constitution
  II). A clinic with no row, or a row with no plan, has no plan.
  - Written through `IClinicPlans.SetAsync(planId?)`, for the established clinic only. 015 calls
    it from its provisioning step, which runs with the new clinic established
    (`IClinicProvisioner`); 020 calls it when a change takes effect.
  - Every set writes `subscription.plan-set` to the clinic's trail and the platform's, naming the
    plan before and after (FR-030).
- **Why**: clinic-owned data under the database's isolation is the rule (constitution II). One row
  keeps the read a single seek. A plan id that no longer resolves reads as "no plan" (FR-004's
  baseline) rather than failing.
- **Rejected**: a history table of plan periods. That belongs to 018/020 (invoices priced on a
  plan, scheduled downgrades) and needs their rules.

## R3 — The answer is worked out per request, never cached across requests

- **Decision**: `ClinicEntitlements` reads three things:
  - the link (one seek);
  - `IPlanBook.FindAsync(planId)` (013);
  - the whole feature catalogue, through a new `IFeatureCatalogue.AllAsync()`. It returns every
    row, retired and unsold included, because the core and a retired feature still matter
    (FR-003, FR-006).
  It also reads the clinic's specialty (R5). It works the answer out once per request (a scoped
  memo) and never keeps it across requests.
- **Why**:
  - FR-007 and SC-005 ask for a saved plan to apply to the very next check, so there is nothing
    to invalidate.
  - Four small indexed reads per request fit well inside SC-008's 50 ms at the 95th percentile.
    An integration test times 200 checks.
  - The memo means a request that checks three times reads once.
- **Rejected**:
  - A memory cache with invalidation on plan save. That is cross-process invalidation for a
    one-API deployment that may scale out, and it only saves about a millisecond.
  - Copying the plan's grid into the clinic's row. It would drift from the plan, and 013's edits
    must apply at once.

## R4 — The rules, in one pure function

- **Decision**: `Entitlements.Work(plan?, catalogue, specialty?)` in the module's domain. It
  returns, for every catalogue key, `Included`, `Limit` (`int?`, null = no limit), `Enforced`
  (`Status == built`), `Kind` and `Monthly`.
  - A switch is included when all hold: the plan includes it, the clinic's specialty does not
    exclude it, and it is not the specialty exam modules key without a specialty. `core_clinic`
    is always included.
  - A limit takes the plan's number; null when the plan includes it with no number; the
    feature's `Baseline` when the plan leaves it out or there is no plan.
  - A key that is not in the catalogue answers `Included = false`, `Limit = 0` (ق-٣٩).
  - A limit made a switch, or a switch made a limit, reads as 013 left it (included, no limit).
  - An upcoming feature is never refused (`Enforced = false`), but its row still says what the
    plan holds, so the day it is built nothing changes.
- **Why**: every check, the session answer and the eight-limit read call the same function, so
  they cannot disagree (FR-002, SC-004). A pure function is unit-tested against every scenario
  of User Story 1.

## R5 — The clinic's specialty: a nullable field on the clinic, set by 015

- **Decision**: `tenancy.Clinic` gains `Specialty nvarchar(40) NULL` in a `Tenancy` migration, and
  `ClinicProfile` gains `string? Specialty`. Nothing sets it until 015, which validates it against
  the fifteen keys. Null means a feature that follows a specialty is off (spec edge case).
- **Why**:
  - A specialty is a fact about the clinic (SH-013), so it lives with the clinic.
  - The engine reads it through `IClinicProfile`, which it already may use.
  - Adding the column now saves 015 from changing 014's engine.
- **Rejected**:
  - Keeping the specialty in the subscription row. A dental clinic stays dental across plans.
  - A placeholder interface answered "none". 015 would have to replace it anyway.

## R6 — Refusing a feature: endpoint metadata read by a gate after the access gate

- **Decision**:
  - `.RequiresFeature(FeatureKeys.X)` adds a `PlanFeatureMetadata` to a clinic route.
  - `app.UseScsPlanGate()` runs right after `UseScsAccessGate()`. For a clinic route with the
    metadata, it asks `IClinicEntitlements` and writes `PLAN_FEATURE_NOT_INCLUDED` (403, extension
    `feature`) when the feature is enforced and not included.
  - Operators' routes never carry it (spec edge case).
  - The gate lives in `Scs.BuildingBlocks` and depends only on a small interface,
    `IPlanAnswers`, that the `Subscriptions` module implements. So building blocks never
    reference a module.
- **Why**:
  - The permission is decided first, in the gate that already runs first (FR-009, 006's rule).
  - Like `.Requires(permission)`, the declaration sits on the route and is checked before the
    handler, so nothing is read or changed when it refuses (FR-008).
- **FR-010**: a new `PlanFeatureDeclarationTests` reads every endpoint's `PlanFeatureMetadata` and
  fails when a key is not in the seeded catalogue (`FeatureSeed`). A test-only probe route proves
  the gate end to end.
- **Rejected**: a call inside each handler. It would be forgotten, untested by declaration, and
  it would run after reads.

## R7 — Refusing at a limit: one helper, a lock per clinic and feature, the owner counts

- **Decision**: `PlanLimits.EnsureRoomAsync(db, key, count, adding)` in
  `Scs.BuildingBlocks` (the extension over `DbContext`), used by the module that owns the
  records. Inside the caller's transaction (it begins one when there is none) it:
  1. takes `sp_getapplock` on `plan-limit:{clinicId}:{key}`, exclusive, owned by the transaction
     (the pattern of `ClinicRoles` and `PasswordLinks`);
  2. reads the limit from `IPlanAnswers.LimitOfAsync(key)` (null when there is no limit or the
     feature is upcoming);
  3. runs the owner's count;
  4. throws `PlanLimitReachedException` → `PLAN_LIMIT_REACHED` (409, extensions `feature`,
     `limit`) when `count + adding > limit`;
  5. returns, so the owner writes while still holding the lock.
  The refusal is written for both consoles by the existing problem writer.
- **Why**:
  - The lock lives on the caller's connection, so it serialises exactly the competing writes
    (ق-أ-١٩, SC-002) and nothing else.
  - The count is the owner's because only it knows what "counted" means (FR-014).
  - The refusal, the code and the message are shared (FR-011).
  - The comparison `count + adding > limit` is ق-٤٠'s "used ≥ limit before creating" for one
    unit, and it is never backwards (ق-٤١): nothing is counted down.
- **Rejected**:
  - Locking the `subscriptions.ClinicPlan` row. It is another module's table on another
    connection, so it is not one transaction.
  - A serializable transaction. It deadlocks under the 20-request test instead of queueing.

## R8 — The users limit: what counts, and the five places checked

- **Decision**: an account counts when it is `active`, or `invited` with either a live
  `NewAccount` link or no link yet (created a moment ago, its link not yet written). These do not
  count: `disabled`; `invited` whose links are all used, replaced or expired (ق-٠٦, ق-١٠٣).
- **The check** sits in `Identity`, in one internal `Seats.EnsureSeatAsync(accountId?)`, called
  wherever an uncounted account is about to count:
  1. `IStaffAccounts.CreateAsync`, for `invited` and `active`;
  2. `IStaffAccounts.SetStateAsync`, `disabled` → `active`;
  3. `IAccountHandover.ResendInvitationAsync`, when the account does not count (its invitation
     expired);
  4. `IAccountHandover.ReissueAsync`, for an `invited` account that does not count, by any method
     (a link re-invites it; a password makes it `active`);
  5. the handover creations, through 1, inside their transaction.
- **Not checked**: activation by link, role changes, and actions on a counted account (FR-017).
- Because the check is in the contracts' implementations, 026's screens and 015/017's operator
  actions inherit it (SH-017) with no code of their own.
- **Why "no link yet" counts**: `CreateWithLinkAsync` writes the account, then its link. Without
  that rule, a second request in between would see a seat that is not free.
- **Rejected**: an `InvitationExpiresAtUtc` column on the account. It duplicates the link's expiry
  and needs a migration of 004's table for no gain.

## R9 — `max_users` marked built by this spec

- **Decision**: a `Plans` migration `MaxUsersBuilt` sets `plans.Feature.Status = 'built'` for
  `max_users` (012's clarification: the spec that builds a feature marks it, in its own
  migration). The seed keeps `upcoming` for a fresh catalogue before the migration, and the
  migration runs after the seed's.
- **Why**: FR-018. An upcoming feature is not enforced (ق-٤٩), so without this the users limit
  would never refuse.

## R10 — The meter: an append-only ledger, the month worked out in Cairo

- **Decision**: a table `subscriptions.MeterEntry`, clinic-owned, with row level security. Its
  columns:
  - `Id`, `ClinicId`, `FeatureKey`;
  - `Month` (`char(7)`, `yyyy-MM` in Cairo);
  - `Amount` (`int`, positive for a use, negative for a give-back);
  - `AtUtc`, `Purpose` (`nvarchar(100)`), `Reference` (`nvarchar(200)` null);
  - `By` (`nvarchar(100)`: an account id, an operator id or a job name; never personal text);
  - `BeyondAllowance` (bit);
  - `GivesBack` (`uniqueidentifier` null, the use it returns).
  - An index on `(ClinicId, FeatureKey, Month)`.
- **Operations**, in `IMonthlyMeter`:
  - `UseAsync(key, amount, purpose, reference, completesPromise)` takes the same lock as R7 on
    `meter:{clinicId}:{key}`, sums the month, compares with this month's allowance, and inserts.
    It is refused with `PLAN_LIMIT_REACHED` (`feature`, `limit` = the allowance), unless
    `completesPromise`, which inserts with `BeyondAllowance`. An included limit with no number
    never refuses.
  - `GiveBackAsync(entryId, amount)` adds a negative row in the use's month, never more than the
    use's amount minus what was already given back.
  - `ReadAsync(key, month?)` returns the allowance, the used, what is left, and the renewal date.
- `Month` is computed from `TimeProvider` in `Africa/Cairo`, the way `Words`, `Preferences` and
  the operators directory already do.
- Nothing is deleted. Old months stay readable (FR-022); retention is a later platform setting
  (SH-095).
- **Only `Scs.Subscriptions` writes the meter**: consumers call the contract, never the table.
- **Why**:
  - A ledger keeps every use with its reason, so 078's «سقوف هذا الشهر» and 021's extra units read
    from it.
  - A running balance column would need to be rebuilt at every month change and on every plan
    change.
  - Working the month out on write makes the 1st-of-month restart a non-event: there is no job
    and no reset to fail.
- **Allowance**: this month's limit from R4. 021 will add its live extra units to the same sum.
- **Rejected**: a monthly reset job. It is one more thing that can fail at midnight, and a plan
  change mid-month would need to rewrite the balance.

## R11 — The eight-limit read

- **Decision**: `IClinicLimits.ReadAsync()` returns eight `LimitReading` rows. Each row has:
  - `Key`, `NameAr`, `NameEn`, `UnitAr`, `UnitEn` (from the catalogue);
  - `Limit` (`int?`);
  - `Used` (`int?`, null when nothing counts it yet);
  - `Monthly`, and for a monthly limit `RenewsOn` (the 1st of next month, Cairo).
  Counts come from `ILimitCounter` services that the record-owning modules register (Identity
  registers `max_users`). Monthly use comes from the meter.
- The eight keys are a constant list in `Scs.SharedKernel` (`FeatureKeys.CountedLimits`), the
  BRD's fixed eight (SH-015). Their names and numbers stay in the database.
- **Why**: SH-016's three places (024, 078, 015) read this one call. "Not counted yet" is a null,
  never a 0 (User Story 6, scenario 3).

## R12 — The clinic console learns its plan from the session answer, re-read on opening a page

- **Decision**: `GET /v1/auth/session` (Identity, already open to the signed-in person) gains
  `plan`:
  - `nameAr`, `nameEn` (null when no plan);
  - `withheld`: the keys of the built features the clinic's plan does not include.
  Identity asks `IClinicEntitlements`. The console's `PlanSource` reads `session.plan.withheld`.
  The not-in-plan guard re-reads the session before deciding, on every opening of a destination
  that names a feature (and only those),
  so a plan change shows by the next page opened (FR-029, SC-005). The 10-minute refresh and the
  refresh on reconnection keep it fresh otherwise.
- **Why**:
  - No new route open to every signed-in person. That list is guarded by 005's route test and
    its architecture rule, and the plan is part of "who am I here", like the permissions it sits
    beside.
  - The console already refreshes that answer on the same moments it must refresh the plan.
- **Rejected**:
  - `GET /v1/clinic/plan` open to any signed-in person. It means widening 005's own-account list
    and its architecture rule for a read that the session already carries.
  - Behind `subscription.view`: every member of the team must see the state, and most do not
    hold it.

## R13 — Which console pages name which feature

- **Decision**: a `feature` (one key, or "any of" several) on the destinations in
  `clinic/src/app/shell/destinations.ts`, from the BRD's per-chapter "ميزة باقة" tables:

| Destination | Feature(s) | Source |
|---|---|---|
| `inbox` | any of `allow_whatsapp`, `allow_telegram` | 07 CL-IB-166 («باقة بلا قناة») |
| `reports.financial`, `reports.clinical` | `allow_analytics` | 01 §5 (the reports example), Shared §4 catalogue |
| `reports.weekly` | `weekly_summary` | Shared §4 catalogue |
| `reports.feedback` | `patient_feedback` | Shared §4 («تقييمات المرضى») |
| `administration.licences` | `licences_registry` | 10 §10 («الرخص والتغطية … الصفحة كلّها») |
| `data.consents` | `informed_consent` | 04 §10 («الموافقة المستنيرة») |
| `dental-lab` | `dental_lab` | 04 §10, SH-012 |

  Every other destination is core or not a plan feature: home, bookings, patients, invoices,
  tickets (08: «التذاكر ليست ميزة باقة»), users, roles, doctors, branches, services, settings,
  subscription, activity log. A page is not in the plan when every one of its keys is withheld.
- **Why**: FR-026. All seven keys are upcoming today, so nothing shows as not in the plan until
  each feature's spec marks it built. Then it works with no change here. A unit test pins the
  table.

## R14 — The card as drawn, built from the empty-state part

- **Decision**: `NotInPlanPage` keeps its page header and, in place of `<scs-state
  state="not-in-plan">` (whose title is the fixed «غير مشمولة في باقتك»), shows `<scs-empty-state
  context="not-in-plan">`, the same part the state uses inside. The card holds:
  - the lock;
  - the title «الباقة الحاليّة لا تشمل {الميزة}»;
  - the line, which has one of two endings by `subscription.view`;
  - «فتح الملف والاشتراك» as a link to `/account/subscription`, rendered only for a holder of
    `subscription.view` (SH-055, the owner's answer).
- {الميزة}: each feature destination has two words in `clinic/public/i18n`, a title noun and the
  line's subject:
  - `inbox`: «قنوات المحادثة» / «قنوات المحادثة»;
  - reports: «التقارير» / «التقارير المالية والطبية»;
  - the rest, until drawn: the catalogue's name for both, read from the session's plan.
  English as drawn.
- **Why**: FR-027 and the frames `6701:1002017` and `6614:945146`. The empty-state part already
  draws the lock circle and the centred layout, so no new shared component is needed. If the
  frame's padding needs an input, it is added to `ui` by hand with its gallery entry.
- **Rejected**: changing `scs-state`'s fixed title. Ops and the gallery use it as it is.

## R15 — Local development: setting a seeded clinic's plan

- **Actor**: the record names `system:dev`, since the route has no identity (/speckit-analyze U2).

- **Decision**: `POST /v1/dev/clinic-plan` with `{ clinic: "noor" | "shifa", planCode: string |
  null }`. It is mapped only when `ScsOptions.IsLocal`, like `/v1/dev/staff-invitations`, and is
  `.AllowAnonymous().HandlesOwnSession().RecordsNothing("local and test environments only")`. It
  establishes the named clinic and calls `IClinicPlans.SetAsync`. Documented in `docs/setup.md`.
- **Why**: FR-031. Plans ship empty (013) and clinics have no plan until 015, so a walk needs a
  way to set one.

## Things this plan changes in built specs' code (constitution 1.3.0)

The built specs' documents are not touched. This spec's own documents record each change:

- **002**: `ClinicProfile` and `tenancy.Clinic` gain the nullable `Specialty` (R5).
- **003, 004**: account creation, re-enabling, re-inviting and re-issuing are checked against the
  users limit (R8). The session answer gains `plan` (R12).
- **006**: `PlanSource` stops being a stand-in. Destinations name their feature. `NotInPlanPage`
  draws the frames' card (R13, R14).
- **012**: `IFeatureCatalogue` gains `AllAsync()`. `max_users` becomes built (R3, R9).
- **013**: `IPlanBook` is unchanged and is read as published.
