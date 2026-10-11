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

## Adding a clinic-owned table

Almost every table a later spec adds belongs to a clinic. Three steps, all in the same change:

1. **The entity** lives in the module's `Domain` folder and implements `IClinicOwned`
   (`Guid ClinicId { get; }`). Do not assign `ClinicId`: it is stamped from the clinic context
   when the entity is saved. The module's context derives from `ClinicAwareDbContext` and calls
   `ApplyClinicIsolation(modelBuilder)` at the end of `OnModelCreating`.
2. **The migration** that creates the table calls
   `migrationBuilder.CreateClinicIsolation("<schema>", "<Table>")` right after `CreateTable`,
   and `DropClinicIsolation` in `Down`. `ClinicId` is `uniqueidentifier NOT NULL`, with a foreign
   key to `tenancy.Clinic(Id)`, and leads the table's lookup indexes. Unique constraints include
   `ClinicId` unless the spec says a value is unique across clinics.
3. **The test project** gets an `IIsolationSample` for the entity
   (`src/server/tests/Scs.IntegrationTests/Isolation`), which inserts one valid row.

Forget step 2 and `IsolationGuardrailTests` fails naming the table — also when the policy is
added in a *later* migration, because migrations are checked one at a time. Forget step 3 and
`IsolationBehaviourTests` fails naming the entity.

**A table that belongs to no clinic** (a list of specialties, say) is declared by name, with a
reason, in `Scs.BuildingBlocks/Tenancy/PlatformWideTables.cs`. That file is the only place such
tables are declared. A platform-wide table is visible to every clinic's work and must never hold
patient data.

**Never** open a `SqlConnection`, call `sp_set_session_context`, or write `CREATE SECURITY
POLICY` in a module; the architecture tests fail if you do. Report queries written by hand use
`IClinicConnection`, which is read-only and already carries the clinic.

**Do not edit** `ClinicIsolationSql` once a release has applied a migration that uses it: the
helpers produce SQL at migration time, so a change there would silently change what an old
migration does. Change isolation with a new migration.

## Rules the build enforces

| Rule | Enforced by |
|---|---|
| `Domain` depends only on the shared kernel | `LayeringTests` |
| `Application` does not depend on `Infrastructure` or `Api` | `LayeringTests` |
| A module references another module only through its `.Contracts` project | `ModuleBoundaryTests` |
| No module uses the logging or job library directly, or writes to the console | `LoggingRulesTests` |
| Every route declares who may call it — `AllowAnonymous()`, `OwnAccountOnly()`, `Requires(Permissions.X)` or `RequiresOrOwner<TCheck>(Permissions.X)` — and exactly one of them; a route that declares nothing is refused for everyone | The access gate; `RouteDeclarationTests`, `FallbackPolicyTests` |
| A code a route names is a permission of the catalogue | Compile time (`Permissions` constants); `RouteDeclarationTests` |
| Only the shared gate refuses for a missing permission; a module never answers `PERMISSION_REQUIRED` itself | `IdentityRulesTests` |
| `OwnAccountOnly()` is used only for what a person does to their own account, and the list of such routes is written down | `RouteDeclarationTests`, `IdentityRulesTests` |
| Nothing decides by the name of a role | `IdentityRulesTests` |
| The web sites' permission catalogue is the server's | `CatalogueFileTests` (regenerate with `Scs.Migrator catalogue`) |
| The application login cannot alter structure | `MigrationTests` |
| Every table is isolated or declared platform-wide, after every migration | `IsolationGuardrailTests` |
| One clinic cannot read or change another's rows; no clinic means nothing | `IsolationBehaviourTests` (every `IClinicOwned` entity, found by reflection) |
| The application login cannot switch isolation off or widen its own access | `LoginPermissionTests` |
| No module opens a connection or sets the clinic on one | `TenancyRulesTests` |
| No personal data or secret reaches the logs | `LogRedactionTests` (covers every mapped endpoint, including new ones) |
| An endpoint behaves for a signed-in person of a clinic | `factory.SignInAsAsync(clinicId)` gives an `HttpClient` that carries a real session |

## Who may call a route (spec 005)

Every route says who may call it, where it is mapped:

```csharp
v1.MapGet("/patients", ListAsync).Requires(Permissions.PatientsView);
v1.MapPost("/patients", CreateAsync).Requires(Permissions.PatientsManage);
```

The permission is checked before the handler runs, so the handler never checks it again, and a
refused request has read nothing. Inside a handler, ask `IStaffAccess`:

- `HasAsync(Permissions.X)` — for a part of an answer that needs a further permission;
- `ReachOfAsync(Permissions.BookingsView)` — `Own` or `All`, for a list that a scope permission
  widens. Never compare role names: a role is whatever set of permissions a clinic gave it.

For a record its owner may always reach, declare `RequiresOrOwner<TCheck>(Permissions.X)` and
implement `IRecordOwnerCheck`. On the front end, guard a destination with `canMatch:
[holds('code')]` followed by the no-access page on the same path, and leave a control out of the
page with `*scsCan="'code'"`. Plan entitlements, the read-only clinic and a record's state are
not permissions; they have their own answers. The full contract is
`specs/005-permissions-engine/contracts/access-engine.md`.

## Recording what was done (spec 007)

Every action a person would later ask "who did this?" about leaves an entry in the audit trail,
written in the same transaction as the change: both take effect, or neither does. Four steps,
and nothing in the audit module changes:

1. **Add the action to the catalogue** — `src/server/shared/Scs.SharedKernel/Audit/AuditActions.cs`:
   its code, its category (security, changes, records, users), its name and one sentence in the
   passive, in Arabic and English, the facts it may carry, and whether it needs the value before
   and after (it does when it moves money, changes a permission or changes a plan).
2. **Add the code to `audit-actions.lock`** beside it. A code, once used, is never renamed.
3. **Record it where the change is saved**, on your own context:

   ```csharp
   db.Record(new AuditRecord(
       AuditActions.RefundIssued,
       new AuditTarget("invoice", invoice.Id, invoice.Number),
       Facts: new Dictionary<string, object?> { ["amount"] = amount },
       Changes: [AuditChange.Of("status", "paid", "refunded")]));
   await db.SaveChangesAsync(cancellation);
   ```

   The entry is written by that save. Who did it, for which clinic, from which address and
   device, and when, are taken by the trail itself — you cannot pass them. With nobody signed
   in, the platform is the actor; work the platform does alone says so with `BySystem = true`.
4. **Declare it on the route**: `.Records(AuditActions.RefundIssued)`. A route that changes
   nothing a person would trace says `.RecordsNothing("why")`; a scheduled job carries
   `[Records(...)]` or `[RecordsNothing("why")]`. `RouteAuditDeclarationTests` fails for a
   `POST`, `PUT`, `PATCH` or `DELETE` route, or a job, that says nothing.

Rules the trail enforces, and refuses otherwise: the code is in the catalogue; every fact key is
one the action allows; values are short text, numbers, yes/no or lists of codes; before and
after are there when the action needs them. Rules it cannot enforce and you keep: one entry per
thing changed; an account is named by reference (`AuditTarget.ForAccount(id)`), never by its
email address; for a clinical record say that it changed and whose, never what; never record a
display preference or a refusal; never put a password, a code, a token or a link anywhere in an
entry. An entry is never changed or removed. The contract is
`specs/007-audit-trail/contracts/audit-trail.md`.

## Telling people (spec 008)

When something happens that a person should hear about, the module raises an event and the
notifications engine does the rest: who is told, the bell, the email. The notification is
written in the same transaction as the change — both take effect, or neither does — and nothing
in the Notifications module changes.

1. **The event is in the catalogue** — `src/server/shared/Scs.SharedKernel/Notifications/NotificationEvents.cs`:
   its key, group, names in Arabic and English, mark, who is told (holders of a permission, the
   owner of the record, both, or everyone), whether it is emailed (always, by the clinic's
   switch, never), the fact keys its line may carry, the line in both languages, and where
   pressing it leads. The twenty events of the BRD are there already; one that has no cause yet
   has no line, and gets its line and destination from the spec that raises it. A new event also
   goes in `notification-events.lock` beside it; a key, once used, is never renamed.
2. **Raise it where the change is saved**, on your own context:

   ```csharp
   db.Notify(new RaisedEvent(
       NotificationEvents.TicketSupportReplied,
       new EventSubject("ticket", ticket.Id, ticket.Number),
       Facts: new Dictionary<string, object?> { ["subject"] = ticket.Subject },
       Owners: [ticket.OpenedByAccountId]));
   await db.SaveChangesAsync(cancellation);
   ```

   The clinic, the moment, who is told and whether an email is owed are taken by the engine —
   you cannot pass them. An account that was disabled or removed is not told.
3. **A reminder a job raises again and again** names its occasion —
   `Occasion: "7-days"` — and is told once for that record and that occasion however often the
   job runs. Without an occasion every raise tells.

Rules the engine enforces, and refuses otherwise (the save fails, and the change with it): the
key is in the catalogue and has a line; every fact key is one the event allows; values are short
text, numbers or yes/no; an event told to its owner names one; a clinical event carries no
facts at all — the record alone. Rules it cannot enforce and you keep: never put a password, a
code, a link or an email address in a fact; never read or write the `notify` tables yourself
(`NotificationRulesTests` fails); do not gather several events into one; save with
`SaveChangesAsync`, since the synchronous save refuses a pending event. The contract is
`specs/008-notifications-engine/contracts/notifications-engine.md`.

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
  A job is idempotent.
- **Background work for a clinic**: implement `IClinicJob` and schedule it through
  `IClinicJobScheduler`: `EnqueueForClinic<TJob>(clinicId)` for one clinic, or
  `AddOrUpdateRecurringPerClinic<TJob>(jobName, cron)` for every clinic on a schedule. The clinic
  is established before the job is created, so the job sees that clinic's rows only, exactly as
  a request does; each clinic's turn is its own job with its own retries, so one clinic's
  failure does not stop the others. A job never sets or changes the clinic itself, and a job
  scheduled through `IJobScheduler` has no clinic and sees no clinic-owned rows.
- **Who is asking** (from spec 003): every endpoint needs a signed-in person unless it is
  marked `.AllowAnonymous()` and listed in its spec. Inject `ICurrentStaff`
  (`Scs.Identity.Contracts`) for the account, the session and the clinic. The clinic context is
  already established from the session before the endpoint runs: never take the clinic from the
  request. A request that changes something must carry `X-Scs-Request: 1`; the front ends add
  it for you. Permissions arrive with spec 005.
- **Never log an email address**, a password, a code or a cookie value. Log `accountId`,
  `sessionId` and `clinicId`; signed-in requests carry all three on every entry already.
- **Sending an email**: `IEmailSender` in the shared project, never a mail library directly.
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

### A page in the clinic console (spec 006)

Every page after sign-in is shown inside the console's frame. The frame, the side bar and the
routes are all drawn from one table, `src/web/projects/clinic/src/app/shell/destinations.ts`.

- **Give a destination its page**: add one line to `shell/shell.routes.ts`, naming the page by
  how to load it —
  `registerDestinationPage('<key>', () => import('../your/your.page').then((file) => file.YourPage))`.
  Until then the destination shows "not available yet". The side bar, the address, the
  permission check, "not in your role" and "not in your plan" are already there; nothing in the
  frame changes. The page is downloaded when it is first opened, so the frame does not grow
  with it; do not import a destination's page anywhere else.
- **What is downloaded first is the sign-in form alone.** The frame and everything inside it is
  one download for a signed-in person, and every other page before sign-in its own
  (`app.routes.ts`). The build warns when the first download passes 500 kB; keep it quiet by
  loading on demand, never by raising the limit.
- **Start the page with `<scs-page-header>`** (title, the group before it, one line under it,
  header buttons projected into it). It is not optional: while there is no connection the
  frame's warning is shown by the page header, under the title — a page without one would not
  show it.
- **Fill a part of the frame that waits for your data** — the bell, the branch switch, the
  subscription banner, what the plan leaves out, the inbox count, a dental clinic, an account
  linked to a doctor — by providing the source from `core` (`projects/core/src/lib/shell/sources.ts`):
  `{ provide: NotificationsSource, useClass: YourNotifications }`. The frame's components do
  not change.
- **Every control that changes data carries `scsWrite`**: `[scsWrite]="online.online()"`. It is
  disabled, with its reason, while there is no connection. A control guarded by a permission is
  absent (`*scsCan`), not disabled.
- **Tell the person something happened** with `ToastService.show(title)`; where it appears is
  decided once, by the frame.
- **Build a list from the shared parts** — `scs-card`, `scs-tabs`, `scs-list-toolbar`,
  `scs-applied-chips`, `table[scsTable]`, `scs-table-footer`, `scs-skeleton`, `scs-empty-state`,
  `scs-filter-popover` (the filter panel's body, with «تطبيق» and «مسح الكل»), `scs-confirm` (a
  confirmation whose button names the action and waits for `ready`), `scs-dialog`, the `scsMenu`
  directives — and keep the 23 rules of
  `specs/006-app-shell/contracts/list-contract.md`. `/dev/sample-list` shows every one of them
  working; `e2e/sample-list.spec.ts` is one test per rule.
- **Something that opens over the page** (a menu, a popover) is built on `@angular/cdk`
  (`cdkMenuTriggerFor`, `cdkConnectedOverlay`) with the looks in `ui/src/lib/overlay/overlay.css`.
- **In a browser test**, sample data for the frame's sources is given with
  `sampleSources(page, { … })` from `e2e/staff-platform.ts`
  (`projects/clinic/src/app/dev/sample-sources.ts` reads it — never in production).
