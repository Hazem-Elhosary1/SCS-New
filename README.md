# Smart Clinic OS

Multi-clinic cloud platform by Heta Systems: one API, one database, three front ends
(clinic console, patient surfaces, operators console).

- **Run it locally**: [docs/setup.md](docs/setup.md)
- **Rules of the project**: [.specify/memory/constitution.md](.specify/memory/constitution.md)
- **Specs**: [specs/](specs/)

| Path | What it is |
|---|---|
| `src/server` | .NET solution: modular monolith API |
| `src/web` | Angular workspace: `clinic`, `patient`, `ops` apps; `core`, `ui` libraries |
| `deploy` | Containers, local compose, infrastructure |
| `ops` | Runbooks and restore records |

Branches: `main` is production-ready only; `develop` is the integration branch. Each spec is a
branch cut from `develop` and merged back by pull request.
