# Run Smart Clinic OS on your machine

One command starts the database, the platform API and the three front ends. Nothing here needs
a cloud account or a production credential: every value used is a sample that ships in the
repository.

## 1. Prerequisites

| Tool | Version | Check |
|---|---|---|
| Git | any recent | `git --version` |
| .NET SDK | 10.0.x | `dotnet --version` |
| Node.js | 24.15+ or 26+ (see `src/web/.nvmrc`) | `node --version` |
| Docker Desktop | running | `docker info` |

About 4 GB of free disk space is needed, most of it for the SQL Server image, which is
downloaded the first time (allow several minutes on a slow connection).

## 2. Start

Windows (PowerShell):

```powershell
./scripts/dev.ps1
```

macOS or Linux:

```bash
./scripts/dev.sh
```

The script does seven things, in order:

1. creates `deploy/compose/.env` from `.env.example` if it is not there;
2. starts SQL Server in a container (host port **14333**) and creates the database and the four
   logins: `scs_app` (the application), `scs_report` (read-only, for reports), `scs_ops`
   (operators, the only one that may cross clinics) and `scs_migrator` (structure changes);
3. starts a mail catcher in a container: every email the platform sends locally lands in its
   inbox at <http://localhost:8025> and goes nowhere else;
4. applies the database structure with the migrator;
5. registers two made-up clinics, `noor` and `shifa`, and eight made-up staff accounts, if they
   are not there;
6. starts the platform API on port 5080;
7. installs the front-end packages if needed and starts the three apps.

To start only the database and the API, add `-NoWeb` (PowerShell) or `--no-web` (bash).

## 3. What you should see

| Address | Expected |
|---|---|
| <http://localhost:5080/health/live> | `{"status":"Healthy"}` |
| <http://localhost:5080/health/ready> | `Healthy`, with checks `database` and `jobs` both `Healthy` |
| <http://localhost:5080/v1/meta/version> | `"environment":"local"` |
| <http://localhost:4201> | The staff sign-in form: «تسجيل دخول فريق العيادة» |
| <http://localhost:8025> | The mail inbox, where sign-in codes arrive |
| <http://localhost:4202/noor> | Patient surfaces placeholder for «عيادة النور» / "Al Noor Clinic" |
| <http://localhost:4202/shifa> | The same for «عيادة الشفاء» / "Al Shifa Clinic" |
| <http://localhost:4202/nosuch> and <http://localhost:4202> | The "clinic not available" page, the same for every unknown name |
| <http://localhost:4203> | Operators console placeholder: «المنصّة متاحة» |
| <http://localhost:5080/jobs> | Background jobs dashboard (local only) |
| <http://localhost:5080/openapi/v1.json> | Interface description (never served in production) |

Each placeholder page shows the platform version and the environment it reached.

### Sign in to the clinic console

1. Open <http://localhost:4201>. Sign in as `owner@noor.example` with the password in
   `SCS_SEED__STAFF_PASSWORD` of `deploy/compose/.env`.
2. Open the inbox at <http://localhost:8025> and type the six-digit code from the newest message.

To try the password screens (spec 004):

- **Forgot the password**: press «نسيت كلمة المرور؟» on the sign-in form, type a seeded address,
  and open the link that arrives in the inbox a few seconds later.
- **A temporary password**: sign in as `temp@noor.example` with the same seed password. After
  the code, the screen that replaces a temporary password is shown instead of the console. The
  seed gives that password three more days every time it runs, until somebody replaces it.
- **A new account**: `new@shifa.example` was never activated. Send its invitation to the inbox
  with the address below, which exists only when the environment is `local`:

  ```bash
  curl -X POST http://localhost:5080/v1/dev/staff-invitations -H "Content-Type: application/json" -H "X-Scs-Request: 1" -d '{"email":"new@shifa.example"}'
  ```
3. The landing page shows «عيادة النور» and the account, with «تسجيل الخروج».

Other made-up accounts, all with the same password: `reception@noor.example`,
`owner@shifa.example`, `doctor@shifa.example`; `former@noor.example` is disabled and
`new@shifa.example` was never activated, to show those two refusals. Five wrong passwords lock
an account for 15 minutes; `ops/runbooks/sign-in.md` says how to open it at once. More than ten
sign-in requests in a minute are refused for the rest of that minute.

### Roles and what each person sees (spec 005)

Every seeded account holds a role. Open <http://localhost:4201/administration/roles> as each of
these to see the three things the address can show:

| Account | Role | At that address |
|---|---|---|
| `owner@noor.example`, `owner@shifa.example` | «مالك العيادة» (every permission) | the page, with no notice |
| `doctor@shifa.example` | «طبيب» (may read roles, not change them) | «للعرض فقط» above the page |
| `reception@noor.example`, `reception@shifa.example` | «فريق العيادة», a custom role | «هذه الصفحة ليست في دورك» |
| `temp@noor.example`, `new@shifa.example` | «مدير العيادة» | the page, once the password is their own |

The page lists the clinic's roles for reading — each with its holders and how much of the
catalogue it carries; creating and editing them is spec 027. A change to what a role holds is seen
on the holder's next request; no one signs in again. The seed gives a role only to a seeded
account that has none, so a role you changed by hand is kept.

### The console's frame (spec 006)

After sign-in every page is inside one frame: the top bar (clinic, language, bell, account), the
side bar, and a strip across the top saying the data is not real (never shown in production).

| Signed in as | The side bar shows |
|---|---|
| an owner | every destination: الرئيسية … الإعدادات, with التقارير, الإدارة and إدارة البيانات opening to their pages |
| `doctor@shifa.example` | what «طبيب» may open |
| `reception@noor.example`, `reception@shifa.example` | الرئيسية, الحجوزات, المرضى, الفواتير والتحصيل |

A destination whose page is not built yet says "not available yet" inside the frame. The
language and the folded side bar are kept for the person, on any browser. The chip shows the
email until accounts have names (spec 026).

Two addresses exist outside production only:

- <http://localhost:4201/dev/sample-list> — a list of made-up rows showing every rule of lists:
  search, filter, chips, tabs, paging, the row menu, the confirmation and the passing notice.
  Add `?state=loading`, `?state=empty` or `?state=error` to see a state, `?as=viewer` to see
  it with the view alone.
- <http://localhost:4201/dev/platform> — the platform-status panel of spec 001.

The bell, the branch switch and the subscription banner have no data until their specs. Their
filled states are shown by the browser tests, which give the frame sample data before it starts
(`sampleSources` in `src/web/e2e/staff-platform.ts`): `SCS_SHOTS=<folder> npx playwright test
shell-states` saves a picture of every state in both languages.

### The audit trail (spec 007)

Everything that changes who may do what, and everything that happens at sign-in, is recorded in
one table, `audit.Entry`. There is no page for it yet («سجل الأنشطة والأمان» arrives with spec
059); it is read at one address by anyone who holds «عرض سجل الأنشطة» — an owner or a manager:

```text
GET http://localhost:5080/v1/audit/entries?category=security&page=1&size=25
```

Signed in at <http://localhost:4201>, open the browser's console and ask it:

```js
await (await fetch('http://localhost:5080/v1/audit/entries', { credentials: 'include', headers: { 'Accept-Language': 'en' } })).json()
```

Type a wrong password twice for `doctor@shifa.example`, sign in as `owner@shifa.example`, and
the answer holds «محاولتان على حساب doctor@shifa.example» and the owner's own sign-in. Words
follow `Accept-Language`. The facets are `from`, `to` (ninety days when not given, a year at
most), `category`, `actor`, `action`, `q`, `page` and `size`.
`reception@shifa.example` and `doctor@shifa.example` are refused: their roles do not hold
the permission. What the platform itself did — the roles a clinic starts with — is in the table
and is not part of what a clinic reads.

After changing the permission catalogue (`src/server/shared/Scs.SharedKernel/Access`), write
the file the web sites read, or `CatalogueFileTests` fails:

```powershell
dotnet run --project src/server/tools/Scs.Migrator -- catalogue src/web/projects/core/src/lib/access/permission-catalogue.json
```

### Check the "not ready" behaviour

```bash
docker compose -f deploy/compose/docker-compose.yml stop sqlserver
```

`/health/live` still answers `Healthy`; `/health/ready` answers 503 and names `database`;
reloading a placeholder page shows an error with a reference instead of a blank page. Start the
database again with:

```bash
docker compose -f deploy/compose/docker-compose.yml start sqlserver
```

## 4. Stop

Press Ctrl+C in the terminal that runs the script. That stops the API and the front ends. The
database container keeps running so the next start is fast; to stop it:

```bash
docker compose -f deploy/compose/docker-compose.yml down
```

Add `--volumes` to that command to also delete the local database.

## 5. Run the checks a pull request runs

```bash
dotnet test src/server/Scs.sln
```

```bash
dotnet format src/server/Scs.sln --verify-no-changes
```

```bash
npm --prefix src/web run ci
```

`ci` runs lint (including the rule against left/right layout utilities), the translation check,
the unit tests and the three production builds.

The browser tests need a browser once:

```bash
cd src/web && PLAYWRIGHT_BROWSERS_PATH=0 npx playwright install chromium
```

```bash
npm --prefix src/web run e2e
```

To lay every state of the sign-in and password screens beside its frame in the design file, the
same tests can save a 1440x900 picture of each state (96 pictures: 48 states in two languages):

```bash
cd src/web && SCS_SHOTS=/absolute/folder npx playwright test sign-in-states password-states
```

The folder must exist. Without `SCS_SHOTS` nothing is saved and the tests run as usual.

On Windows PowerShell, set the variable first: `$env:PLAYWRIGHT_BROWSERS_PATH = '0'`. The browser
tests start the three front ends themselves and answer the platform's calls, so the API does not
need to be running.

The server tests start their own SQL Server container, so Docker must be running. Without
Docker, the tests that need a database fail and the rest still run.

## 6. Common problems

| Symptom | Cause and fix |
|---|---|
| `docker: error during connect` | Docker Desktop is not running. Start it and wait until it says it is running. |
| The database step hangs, then fails | First start downloads the image. Run the script again; it continues where it stopped. |
| `/health/ready` is 503 with `jobs` unhealthy | The structure was not applied. Run the script again, or run the migrator: `dotnet run --project src/server/tools/Scs.Migrator`. |
| The API stops at start with "Missing required configuration value SCS_…" | `deploy/compose/.env` is incomplete. Delete it and run the script again to recreate it. This is needed once after taking the clinic isolation work (spec 002), which added the report and operator logins. |
| <http://localhost:4202/noor> shows "clinic not available" | The made-up clinics are not registered. Run `dotnet run --project src/server/tools/Scs.Migrator -- seed`. |
| Sign-in answers «لم نستطع إرسال رمز التحقق إلى بريدك» | The mail catcher is not running. Start it: `docker compose -f deploy/compose/docker-compose.yml --env-file deploy/compose/.env up -d mailpit`. |
| A reset link never arrives in the inbox | Links are sent by the background worker, a few seconds after asking, and only to an active account — at most one a minute and five an hour. Check the jobs page at <http://localhost:5080/jobs>. |
| A link in an email opens the wrong address | `SCS_AUTH__STAFF_APP_URL` in `deploy/compose/.env` says where links lead; locally it defaults to `http://localhost:4201`. |
| The API stops at start naming `SCS_AUTH__CODE_KEY` or `SCS_MAIL__HOST` | `deploy/compose/.env` predates sign-in (spec 003). Delete it and run the script again, or copy the new sections from `.env.example`. |
| The support hours in the help window are unreadable | The `.env` file was saved in another encoding. It must be UTF-8. |
| A placeholder page shows «تعذّر الاتصال بالمنصّة» | The API is not running, or it is still building. Check <http://localhost:5080/health/live>. |
| Port already in use | Something else uses 5080, 4201–4203 or 14333. Change `SCS_SQL_PORT` in `.env` for the database; stop the other program for the rest. |
| A blank page saying the page could not start | `config.json` is missing or wrong in `src/web/projects/<app>/public/`. |

## 7. Where things are

- Unlocking an account, ending sessions and other sign-in operations by hand: [`ops/runbooks/sign-in.md`](../ops/runbooks/sign-in.md)
- Never copy production patient data to your machine: [`ops/runbooks/data-handling.md`](../ops/runbooks/data-handling.md)
- Rules of the project: [`.specify/memory/constitution.md`](../.specify/memory/constitution.md)
- What this foundation delivers: [`specs/001-platform-foundation/`](../specs/001-platform-foundation/)
- Conventions every later feature inherits: [`contracts/`](../specs/001-platform-foundation/contracts/)
