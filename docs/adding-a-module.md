# Adding a module to the platform

The platform is a modular monolith: one deployable API, one module per BRD area. The `Platform`
module (`src/server/modules/Platform`) is the template. Copy its shape, not its contents.

## The shape

```text
src/server/modules/<Name>/
├── Scs.<Name>/                 internal: nobody outside the module references this
│   ├── Domain/                 entities and rules; references the shared kernel only
│   ├── Application/            use cases, jobs, the interfaces Infrastructure implements
│   ├── Infrastructure/         EF Core context, migrations, external services
│   ├── Api/                    endpoints
│   └── <Name>Module.cs         implements IModule (and IMigratableModule if it has tables)
└── Scs.<Name>.Contracts/       the only project other modules may reference
```

Dependencies point inward: `Api` → `Application` → `Domain`, and `Infrastructure` →
`Application` → `Domain`. `Domain` references nothing but `Scs.SharedKernel`: no framework, no
database, no web type.

## Steps

1. **Create the two projects** and add them to `Scs.sln`. `Scs.<Name>` references
   `Scs.BuildingBlocks` and its own `Scs.<Name>.Contracts`; the contracts project references
   `Scs.SharedKernel` only.
2. **Write `<Name>Module`** implementing `IModule`: `AddServices` registers the module's
   services, `MapEndpoints` maps its routes onto the `/v1` group it is given.
3. **Register it** in two places, and nowhere else:
   - `src/server/host/Scs.Api/ModuleRegistration.cs` (add a project reference from `Scs.Api`);
   - `src/server/tools/Scs.Migrator/Migrator.cs`, if the module owns tables.
4. **Give it its own schema.** Follow `PlatformDbContext`: a default schema named after the
   module and its own history table `<schema>.__EFMigrationsHistory`. A module never creates or
   alters a table in another module's schema. See [database-changes.md](database-changes.md).
5. **Run the tests.** The architecture tests pick the new module up by themselves.

## Rules the build enforces

| Rule | Enforced by |
|---|---|
| `Domain` depends only on the shared kernel | `LayeringTests` |
| `Application` does not depend on `Infrastructure` or `Api` | `LayeringTests` |
| A module references another module only through its `.Contracts` project | `ModuleBoundaryTests` |
| No module uses the logging or job library directly, or writes to the console | `LoggingRulesTests` |
| Every route is refused unless it declares who may call it | Fallback policy; `FallbackPolicyTests` |
| The application login cannot alter structure | `MigrationTests` |
| No personal data or secret reaches the logs | `LogRedactionTests` (covers every mapped endpoint, including new ones) |

## Conventions to follow

These are decided once in `specs/001-platform-foundation/contracts/` and are not re-decided in
a module.

- **Refusing a request**: throw `ScsProblemException` with a code from `ErrorCodes`. Never build
  an error response by hand. A new code is added to `ErrorCodes`, to `ErrorMessages` in both
  languages, and to the table in `contracts/conventions.md`.
- **Logging**: use `ILogger` with a message template and named values, never an interpolated
  string. Log ids, not people. Mark members that hold personal data or secrets with
  `[PersonalData]` or `[Secret]`.
- **Background work**: register jobs through `IJobScheduler` with an `IRecurringJobRegistration`.
  A job is idempotent. A job that works on a clinic's data sets that clinic's context first
  (spec 002).
- **Reaching another module**: call the interface it publishes in its `.Contracts` project.
  Never read or write its tables.
- **Clinic-owned tables** (from spec 002 on): the isolation policy is in the same migration that
  creates the table.

## On the front end

A feature is a folder in the app that owns it (`src/web/projects/clinic|patient|ops`). Anything
two apps need goes into `core` (not visual) or `ui` (visual), never copied.

- Every visible text is a translation key in both `ar.json` and `en.json`; `npm run i18n:check`
  fails otherwise.
- Layout uses logical utilities (`ms-`, `me-`, `ps-`, `pe-`, `start-`, `end-`); lint rejects the
  physical ones.
- Dates and money go through `scsDate` and `scsMoney`.
- Loading, empty, error, offline, view-only and not-in-plan are shown with `<scs-state>`.
- Calls to the platform are written as `/v1/...` and go through the `core` HTTP layer.
