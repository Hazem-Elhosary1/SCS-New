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
