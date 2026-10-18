# Contract: the operators' clinic routes and the module contracts behind them

**Feature**: [../spec.md](../spec.md) · **Research**: [../research.md](../research.md) · **Data**: [../data-model.md](../data-model.md)

All routes are under `/v1/ops`, on the operators' address, for a signed-in operator. Errors are
`application/problem+json` with `code`, and with `errors` for `VALIDATION_FAILED`. Every write
declares `.Records(...)`, and `RouteAuditDeclarationTests` lists each route.

## HTTP routes (Subscriptions, `Api/OpsClinicsEndpoints.cs`)

| Method and path | Guard | Records | Answer |
|---|---|---|---|
| `GET /clinics` | `clinics.view` | — | `ClinicsPage` |
| `GET /clinics/form` | `clinics.edit` | — | `ClinicFormOptions` |
| `POST /clinics` | `clinics.edit` | `clinic.created`, `subscription.plan-set` | 201 `{ id, slug }` |
| `GET /clinics/{id}` | `clinics.view` | — | `ClinicDetail` |
| `PUT /clinics/{id}` | `clinics.edit` | `clinic.updated`, `clinic.owner-updated` | 204 |
| `PUT /clinics/{id}/account-manager` | `clinics.edit` | `clinic.account-manager-assigned` | 200 `{ accountManager, version }` |
| `POST /clinics/{id}/invitation` | `clinics.edit` | `clinic.invitation-resent` | 200 `{ expiresAt }` |
| `GET /clinics/{id}/settings` | `clinics.view` | — | `ClinicSettingsView` |

Each `{id}` route runs `OpsClinicScope` first (research R4): it establishes the clinic with
`ClinicSource.Operator`, and answers 404 `NOT_FOUND` when the clinic does not exist, is in
`setting-up` or `discarded`, or the id is malformed. The answer is the same in every case
(FR-019).

### `GET /clinics?search=&status=&plan=&length=&endsWithin=&sort=&page=&size=`

**Query parameters**:

- `search`: ≤ 100 characters, matched in any case against the Arabic and English names and the
  owner's name, email and mobile. For the mobile it is compared with spaces and dashes removed.
- `status`: a repeatable list of `trial` and `active` (016 adds more values).
- `plan`: a plan id.
- `length`: `monthly`, `annual`, `trial` or `unset`.
- `endsWithin`: `7`, `14` or `30`.
- `sort`: `new` (the default), `ends-asc` or `ends-desc`.
- `page`: from 1, clamped.
- `size`: 8 by default, at most 50.

**Answer**:

```text
ClinicsPage {
  items: [ClinicRow], total, page, size,
  cards: { total, newThisMonth, active, activeShare, trial, needsFollowUp: { overdue, readOnly, suspended } },
  facets: { statuses: [code], plans: [{ id, nameAr, nameEn }], lengths: [code], endsWithin: [7,14,30] },
  can: { add, edit }
}
ClinicRow {
  id, slug, nameAr, nameEn, createdAt, createdBy: { id, name } | null,
  plan: { id, nameAr, nameEn } | null,
  status: { code, ar, en, tone },
  length: { code, days? } | null,      // "trial" with its days; null otherwise until 018
  endsAt: string | null,                // the trial's end while in trial
  ownerName, ownerEmail, ownerMobile    // used for the search; shown in the detail
}
```

- **The cards** count every visible clinic, ignoring the search and the filters. The other parts
  of the page are worked out after the search and the filters.
- **`facets`** list only what the clinics hold after the search. A chosen plan stays listed, so
  it can be cleared.
- **Sorting**: `ends-asc` and `ends-desc` put clinics with no date last in both directions; ties
  stay newest first.
- **`can`** says which writes the operator may make. The console uses it to remove controls.

### `GET /clinics/form`

```text
ClinicFormOptions {
  plans: [{ id, nameAr, nameEn, monthlyEgp }],   // assignable, cheapest first (IPlanBook.AssignableAsync)
  specialties: [key]                             // the fifteen, in the BRD's order
}
```

### `POST /clinics`

```text
{ nameAr, nameEn?, ownerName, email, mobile, address, planId, taxNumber?, specialty }
```

1. **Checks** run in this order, and nothing is written until all have passed (research R2,
   stage 1):
   1. the field rules of data-model §7, collected together into one `VALIDATION_FAILED`;
   2. the email is free, else `email: TAKEN`;
   3. the plan is assignable, else 409 `PLAN_DISABLED`;
   4. the plan's users limit is not 0, read from `IPlanBook.FindAsync(planId).Features` with the
      key `max_users`, else 422 `PLAN_LIMIT_REACHED` with 014's words.
2. **The unit** runs as research R2 describes.
3. **The answer** is 201 `{ id, slug }`, with `Location: /v1/ops/clinics/{id}`.
4. **The invitation** is sent after the 201 is decided. Its failure does not change the answer;
   the detail then shows the invitation `none` (FR-004, scenario 6).
5. **Other answers**:
   - an operator without `clinics.edit` gets 403 `PERMISSION_REQUIRED`;
   - the add lock busy gets 503 `LIMIT_BUSY`;
   - a fault in stage 2 gets 500, and the console shows «تعذر إنشاء العيادة.».

### `GET /clinics/{id}`

```text
ClinicDetail {
  id, slug, nameAr, nameEn, createdAt, createdBy | null, version,
  status: { code, ar, en, tone },
  plan: { id, nameAr, nameEn } | null, billingCycle: null, nextRenewal: string | null,   // trial end until 018
  address | null, taxNumber | null, specialty | null, defaultLanguage,
  signInAddress: "https://app.scs-heta.com",
  owner: { name | null, email, mobile | null, state, lastSignInAt | null,
           invitation: { state: "live"|"lapsed"|"none", at: string | null } | null } | null,
  accountManager: { id, name } | null,
  managers: [{ id, name }],   // the active operators, for «تعيين»; empty unless the caller holds clinics.edit
  doctors: 0, branches: <IBranches.CountAsync>,
  can: { edit }
}
```

- `owner` is null only for a clinic with no owner account at all. The seeded clinics have one
  (from the seed) with no name or mobile: `owner.name` and `owner.mobile` are null, and the edit
  form requires them on the first save.
- `invitation` is null once the owner is active.

### `PUT /clinics/{id}`

```text
{ version, nameAr, nameEn?, ownerName, email, mobile, address, taxNumber?, specialty | null, defaultLanguage, note? }
```

- **A clinic with no owner account** (none today) hides the owner fields: `ownerName`, `email` and
  `mobile` are not required and the owner's part is skipped. Making an owner for it is 017's.
- `taxNumber` follows R9: a missing field keeps the stored value; `""` removes it; a stored value
  sent back unchanged is accepted as it is.
- The plan is not in the request (spec 020).
- **The two parts**:
  1. The owner's part runs first, through `IClinicOwners.UpdateAsync`, in Identity's transaction.
     It writes `clinic.owner-updated`, ends sessions or links as FR-021a says, and the old address
     is told after the commit.
  2. Then the clinic's part, through `IClinicRegistry.UpdateProfileAsync`. It writes
     `clinic.updated` and checks the version.
  Nothing changed means nothing is written and no entry is recorded.
- **Answers**:
  - success is 204;
  - `VALIDATION_FAILED` as in the add;
  - 409 `CLINIC_CHANGED_ELSEWHERE`.
  The version is checked before the owner's part runs, so a stale form changes nothing.

### `PUT /clinics/{id}/account-manager`

```text
{ version, operatorId | null }
```

- `operatorId` must be an `active` operator, else 409 `OPERATOR_NOT_ACTIVE`, and nothing changes.
- The answer carries the new manager's name and the new version.

### `POST /clinics/{id}/invitation`

The answer is 200 `{ expiresAt }` (the new link's expiry, ISO 8601). The refusals, in the order
they are checked:

1. 409 `INVITATION_ALREADY_ACCEPTED` when the owner is no longer invited;
2. 429 `RESET_EMAIL_COOLDOWN` when the last new-account link is less than 60 s old;
3. 502 `ACCOUNT_LINK_NOT_SENT` when the email did not leave. Nothing is stored, and the earlier
   link still works;
4. 404 `NOT_FOUND` when the clinic has no owner.

### `GET /clinics/{id}/settings`

```text
ClinicSettingsView { defaultLanguage, timeZone: "Africa/Cairo", staffAddress: "app.scs-heta.com", can: { edit } }
```

The answer holds only the «الإعدادات العامة» card. The tab's other cards come with their specs
(FR-024).

## Module contracts (published, `*.Contracts` projects)

### Tenancy: `IClinicRegistry` (grows)

```csharp
Task<IAsyncDisposable> TakeAddTurnAsync(CancellationToken);                       // sp_getapplock 'scs:clinic-add'
Task<RegisteredClinic> RegisterAsync(ClinicRegistration registration, CancellationToken);
Task ActivateAsync(Guid clinicId, AuditRecord created, CancellationToken);        // setting-up → active + both trails
Task DiscardAsync(Guid clinicId, CancellationToken);                              // setting-up → discarded, slug retired
Task<IReadOnlyList<AuditChange>> UpdateProfileAsync(Guid clinicId, byte[] version, ClinicProfileChange change, string? note, CancellationToken);
Task<byte[]> AssignAccountManagerAsync(Guid clinicId, byte[] version, Guid? operatorId, string? beforeName, string? afterName, CancellationToken);
Task<IReadOnlyList<ClinicListItem>> ListAsync(CancellationToken);                 // exists; now omits setting-up/discarded; item grows

record ClinicRegistration(string NameAr, string? NameEn, string Specialty, string Address, string? TaxNumber, string OperatorId);
record RegisteredClinic(Guid Id, string Slug);
record ClinicProfileChange(string NameAr, string? NameEn, string Address, TaxNumberChange TaxNumber, string? Specialty, string DefaultLanguage);
record ClinicListItem(Guid Id, string ShortName, string NameAr, string NameEn, string State, DateTimeOffset CreatedAtUtc,
                      string? CreatedBy, Guid? AccountManagerId);
```

`IClinicProfile.GetOwnAsync()` returns a `ClinicProfile` that gains `Address`, `TaxNumber`,
`DefaultLanguage`, `AccountManagerId`, `CreatedBy`, `CreatedAtUtc`, `ShortName` and `Version`.

### Identity: `IClinicOwners` (new)

```csharp
Task<bool> EmailIsFreeAsync(string email, CancellationToken);                     // usp_FindAccountForSignIn; true/false only
Task<Guid> CreateAsync(string email, string fullName, string mobile, CancellationToken);   // invited, owner role, seat check; sends nothing
Task DiscardAsync(Guid accountId, CancellationToken);                             // only an invited account with no link (undo, R2)
Task<ClinicOwner?> FindAsync(CancellationToken);
Task<IReadOnlyList<AuditChange>> UpdateAsync(string fullName, string email, string mobile, string? note, CancellationToken);
Task<DateTimeOffset> ResendInvitationAsync(CancellationToken);                    // the cooldown, the two refusals, new expiry
Task<IReadOnlyList<OwnerContact>> ListForOperatorsAsync(CancellationToken);       // crossing read-clinic-owners-as-operator

record ClinicOwner(Guid AccountId, string Email, string? FullName, string? Mobile, string State,
                   DateTimeOffset? LastSignInAtUtc, OwnerInvitation? Invitation);
record OwnerInvitation(string State, DateTimeOffset? At);                         // live: expires at; lapsed: expired at; none: null
record OwnerContact(Guid ClinicId, string Email, string? FullName, string? Mobile);
```

`IAccountHandover.ResendInvitationAsync` stays the way an invitation is sent. After the add's
activation it is called for the new owner.

### Subscriptions: `IClinicPlans` (grows)

```csharp
Task StartAsync(Guid planId, DateTimeOffset trialEndsAtUtc, CancellationToken);   // PLAN_DISABLED unless assignable; subscription.plan-set (+ trial-ends)
Task<ClinicPlanState?> StateAsync(CancellationToken);                             // plan id, set at, trial end
```

`IPlanSubscribers` is implemented by `PlanSubscribers` and registered after Plans (research R13).

### ClinicData: `IBranches` (new module)

```csharp
Task<Guid> CreateMainAsync(string nameAr, string nameEn, string address, CancellationToken);   // CLINIC_HAS_MAIN_BRANCH
Task<int> CountAsync(CancellationToken);
```

### Operators: `IOperatorDirectory` (grows)

```csharp
Task<IReadOnlyList<OperatorSummary>> ActiveAsync(CancellationToken);              // active only, by name
```

### Shared kernel

- `Scs.SharedKernel/Clinics/Specialties.cs`: `All` (the fifteen keys in order) and
  `IsKnown(key)`.
- `ClinicSource.Operator = "operator"`, in the building blocks.

## Tests that pin this contract

- **Permissions** (test-first):
  - each route refuses without its permission;
  - `clinics.view` alone cannot add, edit, assign or resend;
  - `RouteDeclarationTests` adds `ClinicsView` and `ClinicsEdit` to the guarded codes;
  - `RouteAuditDeclarationTests` lists the eight routes.
- **Isolation** (test-first):
  - `clinicdata.Branch` has its sample and policy;
  - an operator route for clinic A writes nothing in clinic B;
  - `DeclaredCrossingsTests` counts 11;
  - the two new crossing types are the only users of the factory in their modules.
- **Plan limits** (test-first): an add on a plan whose `max_users` is 0 is refused and leaves
  nothing.
- **The unit**:
  - every refusal leaves no row in any of the five tables and no trail entry;
  - a fault injected after each write stage leaves nothing visible: no list row, no count, no
    `NOT_FOUND` difference, no locator answer, and the email free again;
  - 10 adds at once with one name get 10 slugs;
  - 2 adds at once with one email: one succeeds, and nothing of the other remains.
- **Error catalogue**: the seven new codes have words in both languages.
- **Audit**: the five actions in the lock and the catalogue, and the operator as actor in both
  trails.
