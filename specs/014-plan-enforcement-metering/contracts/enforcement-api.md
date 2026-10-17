# Contract: the enforcement point, the meter and their addresses

Everything later specs use to check a clinic's plan. All of it is in-process (`Scs.Subscriptions.Contracts`,
`Scs.BuildingBlocks`), except the session field (§6) and the development route (§7).

## 1. `IClinicEntitlements` — what the established clinic has (Scs.Subscriptions.Contracts)

```csharp
public interface IClinicEntitlements
{
    /// What the established clinic has, worked out once per request (research R3, R4).
    Task<ClinicEntitlements> CurrentAsync(CancellationToken cancellation);

    /// Throws PlanFeatureNotIncludedException (→ PLAN_FEATURE_NOT_INCLUDED) when the feature is
    /// enforced and not included. Never throws for an upcoming feature (ق-٤٩).
    Task EnsureIncludedAsync(string featureKey, CancellationToken cancellation);

    /// The limit: null = no limit; 0 for a key the catalogue does not know (ق-٣٩).
    Task<int?> LimitOfAsync(string featureKey, CancellationToken cancellation);
}

public sealed record ClinicEntitlements(
    Guid? PlanId, string? PlanNameAr, string? PlanNameEn, IReadOnlyDictionary<string, Entitlement> Features)
{
    public Entitlement Of(string key);          // the unknown-key answer when absent
    public IReadOnlyList<string> Withheld { get; } // built switches not included (the session's list)
}

public sealed record Entitlement(
    string Key, string Kind, bool Included, int? Limit, bool Enforced, bool Monthly,
    string NameAr, string NameEn, string? UnitAr, string? UnitEn);
```

- **Rule**: it requires an established clinic (`IClinicContext.Require()` → `CLINIC_REQUIRED`).
  The operators console never calls it except inside a clinic it is acting on.

## 2. `IClinicPlans` — the link (Scs.Subscriptions.Contracts)

```csharp
public interface IClinicPlans
{
    Task<Guid?> CurrentAsync(CancellationToken cancellation);

    /// Sets the established clinic's plan (null = none) and records subscription.plan-set in the
    /// clinic's and the platform's trail, in one transaction. An unknown plan id is refused
    /// with NOT_FOUND (404). The same plan again changes nothing and records nothing.
    Task SetAsync(Guid? planId, CancellationToken cancellation);
}
```

- **Callers**: 015, 020, and the development route. No HTTP route in 014 sets it.

## 3. Refusing a feature on a route (Scs.BuildingBlocks)

```csharp
app.MapPost("/v1/…", Handler)
   .Requires(Permissions.Reports.View)          // spec 005, decided first
   .RequiresFeature(FeatureKeys.AllowAnalytics); // this spec
```

- **`app.UseScsPlanGate()`** is placed right after `app.UseScsAccessGate()` in `Program.cs`. For an
  endpoint with `PlanFeatureMetadata` it does three things:
  - asks `IPlanAnswers.IsWithheldAsync(key)`, which the `Subscriptions` module implements;
  - when the answer is withheld, writes **403 `PLAN_FEATURE_NOT_INCLUDED`** with
    `{ "feature": key }` and the request's language words;
  - otherwise calls the next step.
- **Rules**:
  - A route carries at most one `RequiresFeature`. "Any of" is `RequiresAnyFeature(a, b)`, used
    for the inbox's channels.
  - An operators' route (`OperatorRouteMetadata`) carrying it fails at start-up.
  - `PlanFeatureDeclarationTests`: every declared key is a key of `FeatureSeed` (FR-010).
  - No 014 route of the product carries it: all its features are upcoming or not route-shaped. A
    test-only probe route (`ProbeEndpoints`) carries `allow_analytics` for the tests.

## 4. Refusing at a limit (Scs.BuildingBlocks + the owning module)

```csharp
// Inside the owning module, on its own DbContext, before it writes:
await PlanLimits.EnsureRoomAsync(
    db, FeatureKeys.MaxUsers,
    count: ct => CountSeatsAsync(ct), adding: 1, cancellation);
```

- **What it does**, in order:
  1. Requires the caller's open transaction (the lock lasts until the caller's write commits).
  2. Takes `sp_getapplock 'plan-limit:{clinicId}:{key}'`, exclusive, owned by the transaction,
     with a 10 s timeout. A timeout is `LIMIT_BUSY`, 503: the request is refused and the person tries again. No caller
     retries by itself, since the lock has already waited ten seconds (T053).
  3. Reads the limit: null means it returns at once.
  4. Runs `count`.
  5. When `count + adding > limit`, throws `PlanLimitReachedException(key, limit)` →
     **409 `PLAN_LIMIT_REACHED`** with `{ "feature": key, "limit": n }`.
- **Rule**: an upcoming limit (`Enforced = false`) never refuses.
- **Who calls it in 014**: Identity's `Seats.EnsureSeatAsync`, from the five places of research
  R8.

## 5. `IMonthlyMeter` and `IClinicLimits` (Scs.Subscriptions.Contracts)

```csharp
public interface IMonthlyMeter
{
    /// Takes from this month's allowance. Refused with PLAN_LIMIT_REACHED (feature, limit =
    /// the allowance) when what is left cannot cover it, unless completesPromise.
    Task<Guid> UseAsync(MeterUse use, CancellationToken cancellation);

    /// Returns part or all of a use, in the use's month. Refused with METER_GIVE_BACK_TOO_MUCH
    /// (409) beyond what is left of the use.
    Task GiveBackAsync(Guid useId, int amount, CancellationToken cancellation);

    Task<MeterReading> ReadAsync(string featureKey, string? month, CancellationToken cancellation);
}

public sealed record MeterUse(string FeatureKey, int Amount, string Purpose, string? Reference, bool CompletesPromise);
public sealed record MeterReading(string FeatureKey, string Month, int? Allowance, int Used, int Left, int Overrun, DateOnly RenewsOn);

public interface IClinicLimits
{
    /// The eight, in the BRD's order (SH-015), each with its catalogue words, its limit, and
    /// its use (null when nothing counts it yet).
    Task<IReadOnlyList<LimitReading>> ReadAsync(CancellationToken cancellation);
}

public sealed record LimitReading(
    string Key, string NameAr, string NameEn, string? UnitAr, string? UnitEn,
    int? Limit, int? Used, bool Monthly, DateOnly? RenewsOn);

/// Registered by the module that owns the records of a standing limit.
public interface ILimitCounter
{
    string FeatureKey { get; }
    Task<int> CountAsync(CancellationToken cancellation);
}
```

- **Rules**:
  - `UseAsync` refuses a key that is not one of the four monthly keys (`ArgumentOutOfRange`, a
    programming error) and an `Amount` ≤ 0.
  - With no plan the allowance is 0, so every use is refused unless it completes a promise.
  - `Allowance` null means no limit: never refused.

## 6. The session answer gains the plan (Identity, `GET /v1/auth/session`)

```jsonc
{
  "accountId": "…", "email": "…",
  "clinic": { "nameAr": "…", "nameEn": "…", "state": "active" },
  // … unchanged …
  "plan": {
    "nameAr": "الباقة الاحترافية",       // null with no plan
    "nameEn": "Professional Plan",       // null with no plan
    "withheld": ["allow_analytics"],     // built switches the plan does not include
    "names": { "allow_analytics": { "ar": "التقارير المالية والطبية", "en": "Financial & medical reports" } }
  }
}
```

- **Rules**:
  - `withheld` lists only built (enforced) switches that are not included. Today it is always
    empty, because every switch is upcoming.
  - `names` holds the catalogue names of the withheld keys, for a card whose page has no drawn
    words.
  - The route stays `OwnAccountOnly()` and its answer still concerns only the person's own
    clinic.

## 7. Local development only: `POST /v1/dev/clinic-plan`

- **Body**: `{ "clinic": "noor" | "shifa", "planCode": "professional" | null }`.
- **Answers**:
  - 204 when done;
  - 404 `NOT_FOUND` for an unknown clinic or plan code.
- **Mapping**: only when `ScsOptions.IsLocal`, with
  `.AllowAnonymous().HandlesOwnSession().RecordsNothing("local and test environments only")`.
  It goes into the development list of `RouteAuditDeclarationTests`, beside
  `/v1/dev/staff-invitations`.
- `IClinicPlans.SetAsync` still records `subscription.plan-set`. Only the route itself declares
  nothing.

## 8. Codes added

| Code | Status | Extensions | Where |
|---|---|---|---|
| `PLAN_FEATURE_NOT_INCLUDED` | 403 | `feature` | the plan gate; `EnsureIncludedAsync` |
| `PLAN_LIMIT_REACHED` | 409 | `feature`, `limit` | `PlanLimits`; the meter |
| `LIMIT_BUSY` | 503 | — | the limit lock timed out |
| `METER_GIVE_BACK_TOO_MUCH` | 409 | — | a give-back beyond its use |

Each has Arabic and English words in `ErrorMessages`, and the error catalogue test's pin moves
from 69 to 73. An unknown plan on
`IClinicPlans.SetAsync` answers the existing `NOT_FOUND` (404).

## 9. What the routes of earlier specs now refuse

| Route / operation | Spec | New refusal |
|---|---|---|
| `POST /v1/dev/staff-invitations` (and every future caller of `IAccountHandover.CreateWith*`) | 004 | `PLAN_LIMIT_REACHED` (`max_users`) |
| `IStaffAccounts.CreateAsync` (invited / active) | 003 | the same |
| `IStaffAccounts.SetStateAsync(…, active)` from disabled | 003 | the same |
| `IAccountHandover.ResendInvitationAsync` / `ReissueAsync` for an account not counted | 004 | the same |
