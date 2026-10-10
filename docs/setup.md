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

The script does five things, in order:

1. creates `deploy/compose/.env` from `.env.example` if it is not there;
2. starts SQL Server in a container (host port **14333**) and creates the database and the two
   logins, `scs_app` and `scs_migrator`;
3. applies the database structure with the migrator;
4. starts the platform API on port 5080;
5. installs the front-end packages if needed and starts the three apps.

To start only the database and the API, add `-NoWeb` (PowerShell) or `--no-web` (bash).

## 3. What you should see

| Address | Expected |
|---|---|
| <http://localhost:5080/health/live> | `{"status":"Healthy"}` |
| <http://localhost:5080/health/ready> | `Healthy`, with checks `database` and `jobs` both `Healthy` |
| <http://localhost:5080/v1/meta/version> | `"environment":"local"` |
| <http://localhost:4201> | Clinic console placeholder: «المنصّة متاحة» |
| <http://localhost:4202> | Patient surfaces placeholder: «المنصّة متاحة» |
| <http://localhost:4203> | Operators console placeholder: «المنصّة متاحة» |
| <http://localhost:5080/jobs> | Background jobs dashboard (local only) |
| <http://localhost:5080/openapi/v1.json> | Interface description (never served in production) |

Each placeholder page shows the platform version and the environment it reached.

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
| The API stops at start with "Missing required configuration value SCS_…" | `deploy/compose/.env` is incomplete. Delete it and run the script again to recreate it. |
| A placeholder page shows «تعذّر الاتصال بالمنصّة» | The API is not running, or it is still building. Check <http://localhost:5080/health/live>. |
| Port already in use | Something else uses 5080, 4201–4203 or 14333. Change `SCS_SQL_PORT` in `.env` for the database; stop the other program for the rest. |
| A blank page saying the page could not start | `config.json` is missing or wrong in `src/web/projects/<app>/public/`. |

## 7. Where things are

- Never copy production patient data to your machine: [`ops/runbooks/data-handling.md`](../ops/runbooks/data-handling.md)
- Rules of the project: [`.specify/memory/constitution.md`](../.specify/memory/constitution.md)
- What this foundation delivers: [`specs/001-platform-foundation/`](../specs/001-platform-foundation/)
- Conventions every later feature inherits: [`contracts/`](../specs/001-platform-foundation/contracts/)
