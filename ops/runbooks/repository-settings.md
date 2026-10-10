# Repository settings

Settings on the source host that the pipeline relies on. They are applied by a repository
administrator, once, and re-checked when a workflow is added or renamed.

**Status: not yet applied.** The repository has no remote yet. Apply this when it is created.

## Branch protection

| Setting | `develop` | `main` |
|---|---|---|
| Require a pull request before merging | Yes | Yes |
| Required approvals | 1 | 1 |
| Dismiss stale approvals on new commits | Yes | Yes |
| Required status checks | `Server`, `Web`, `Secret scan` | `Server`, `Web`, `Secret scan` |
| Require branches to be up to date | Yes | Yes |
| Allow force pushes / deletions | No | No |
| Allow direct pushes | Nobody | Nobody |
| Merge method | Squash or merge commit | **Fast-forward only**, from `develop` |

The required check names are the `name:` of each job in `.github/workflows/pr.yml`.

### Why `main` is fast-forward only

Production must receive the same package that ran in test (FR-010). Packages are stored by
commit id when `develop` is built. If `main` moves to the *same commit* as `develop`, the
production workflow finds the tested packages for that commit and has nothing to build. A merge
commit or a squash on `main` would create a commit that was never built or tested.

On GitHub, pull requests cannot fast-forward. Release by pushing the fast-forward from a
protected release role instead:

```bash
git fetch origin
```

```bash
git push origin origin/develop:main
```

and restrict who may push to `main` to that role. The production workflow refuses a commit that
has no stored packages.

## Environments

| Environment | Required reviewers | Deployment branches |
|---|---|---|
| `test` | none | `develop` |
| `production` | at least one named reviewer, not the author of the change | `main` |

## Secrets and access

- No cloud keys are stored in the repository settings. Workflows assume a role through OIDC
  (`deploy/infra/modules/ci-role`).
- The role for `production` can be assumed only from the `production` environment.

## Checklist when applying

- [ ] Branch protection on `develop` and `main` as in the table
- [ ] The three required checks selected by name
- [ ] `main` restricted to fast-forward pushes by the release role
- [ ] Environments `test` and `production` created with their reviewers
- [ ] Open three throwaway pull requests that each break one gate and confirm each is blocked
      (quickstart.md Scenario 2a); record the result in `specs/001-platform-foundation/validation.md`
