# Changing the database structure

Every change to the shape of the database is an EF Core migration in the module that owns the
table (`src/server/modules/<Module>/Scs.<Module>/Infrastructure/Migrations`). Migrations are
applied by the migrator, in order, the same way in every environment. The application never
changes structure: its login cannot.

## The rule: expand, then contract

**A release must run correctly against the structure of the release before it.**

The pipeline applies migrations first and starts the new release second. For a while the old
release is still serving users against the new structure, and if the release is rolled back, the
old release serves against the new structure for as long as it takes to fix forward. So a
migration may only *add*; anything that takes away ships one release later.

| You want to | Release N | Release N+1 |
|---|---|---|
| Add a nullable column, a table, an index | Add it and use it | — |
| Add a required column | Add it nullable or with a default; write it | Make it required |
| Rename a column | Add the new column; write both; read the new one, falling back to the old | Backfill, then drop the old column |
| Drop a column or table | Stop reading and writing it | Drop it |
| Change a type | Add a new column of the new type; write both | Backfill, switch reads, drop the old |

Rolling back never rolls the structure back. If a migration itself is wrong, the fix is a new
migration.

## Worked example: renaming `LastRunVersion` to `ReleaseVersion`

Release N:

1. Migration adds `ReleaseVersion nvarchar(40) NULL`.
2. The job writes both columns.
3. Readers use `ReleaseVersion ?? LastRunVersion`.

The release before N ignores the new column, so it still runs. Rolling N back is safe.

Release N+1:

1. Migration copies `LastRunVersion` into `ReleaseVersion` where it is null, makes it
   `NOT NULL`, and drops `LastRunVersion`.
2. The code no longer mentions `LastRunVersion`.

Release N already reads and writes `ReleaseVersion`, so rolling N+1 back is safe too.

## Clinic-owned tables

A table that holds a clinic's data gets its isolation policy **in the migration that creates
it** (constitution, Principle II). The two are one change and are never split across merges.
Spec 002 adds the check that fails the build when a clinic-owned table has no policy.

## Rows the product ships with (spec 012)

Some rows are part of the product, not of any clinic: the feature catalogue's 35 first features
(`plans.Feature`). They are written by a migration of their own, **each only where its key is
absent**, and that migration's Down removes nothing — an operator may have edited a row since,
and plans may hold it. The list a migration applies is frozen with it (`FeatureSeed` for spec
012's). So:

- **A later spec that adds seeded features** writes a new migration with its own rows, inserted by
  key in the same way. It never edits an earlier seed.
- **A spec that builds a feature marks it «مبنية»** (`Status = 'built'`) in a migration of its own,
  by key, and says so in its tasks (FR-024). If an operator has already changed the status, the
  operator's value stands: update only where the status is still `upcoming`.

**Plans ship empty** (spec 013): no migration writes a plan, a price or a limit — the BRD fixes
none (SH-020). Operators make them in the console.

## Data another module counts for this one (spec 013)

The plans' screens show the clinics on each plan, but clinics are given plans by spec 015 and
subscribe by 016, which depend on the plans — never the other way. So `Scs.Plans.Contracts`
publishes `IPlanSubscribers`, and the `Plans` module registers a default that answers "no
clinics". The module that gives clinics their plans registers its own implementation; it wins
whether it is registered before the `Plans` module (`TryAdd` there) or after it. Its counts follow
OP-A-189: a clinic billed monthly counts its monthly price, one billed annually its annual price
÷ 12, a clinic in trial, expired or suspended nothing.

## A clinic's plan and its meter (spec 014)

The `subscriptions` schema holds two clinic-owned tables, under isolation from the migration that
makes them: `ClinicPlan` (one row per clinic, keyed by it; no row is no plan) and `MeterEntry`
(the four monthly limits, one row per use or give-back, never changed or removed).
`tenancy.Clinic` has a nullable `Specialty`, set by clinic creation (015). Spec 014 marked
`max_users` built (`MaxUsersBuilt`, only where it was still upcoming).

## A clinic's onboarding (spec 015)

- **`tenancy.Clinic`** has two states nobody sees: `setting-up`, while an add is not complete, and
  `discarded`, an add that failed part-way and was given up. `CK_Clinic_State` holds the stored
  states (`setting-up`, `active`, `suspended`, `cancelled`, `discarded`); spec 016 adds its own.
  New columns: `Address`, `TaxNumber` (nine digits), `DefaultLanguage` (`ar` or `en`),
  `AccountManagerId`, `CreatedBy` and a `RowVersion` that a stale edit is refused by.
- **`clinicdata`** is a new schema, of the new `ClinicData` module, for a clinic's own records. Its
  first table, `Branch`, is clinic-owned under isolation from the migration that makes it, with
  at most one main branch a clinic (`UX_Branch_OneMain`).
- **`identity.StaffAccount`** has `FullName`, `Mobile` (international, `CK_StaffAccount_Mobile`)
  and `LastSignInAtUtc`, filled for existing accounts from their sessions by the migration.
- **`subscriptions.ClinicPlan`** has `TrialEndsAtUtc`. A clinic without it is past its trial or
  never had one.

## Creating a migration

```bash
cd src/server
```

```bash
dotnet ef migrations add <Name> --project modules/<Module>/Scs.<Module> --startup-project modules/<Module>/Scs.<Module> --output-dir Infrastructure/Migrations
```

Then run the tests: `MigrationTests` applies every migration to an empty SQL Server and checks
that the application login still cannot alter structure.

Each module has its own schema and its own history table (`<schema>.__EFMigrationsHistory`).
A module never creates or alters a table in another module's schema.
