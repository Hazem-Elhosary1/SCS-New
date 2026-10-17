# Quickstart: checking spec 014

How to prove plan enforcement and metering work: automatically, then by hand on the local stack.
Contracts: [enforcement-api.md](contracts/enforcement-api.md),
[not-in-plan-screen.md](contracts/not-in-plan-screen.md). Rules: [data-model.md](data-model.md).

## Prerequisites

- The local environment of `docs/setup.md`. Set `TEMP`/`TMP` to
  `E:\Heta_SCS\src\web\node_modules\.tmp`; SQL Server and Mailpit run in Docker.
- The API preview runs Release, so tests run in Debug (its DLLs are locked).
- After pulling, restart the API preview so the three migrations apply:
  - `Subscriptions` (the two tables with their row level security);
  - `Tenancy` (`Specialty`);
  - `Plans` (`MaxUsersBuilt`).

## 1. Automated

```bash
dotnet test src/server/tests/Scs.UnitTests -c Debug
```

```bash
dotnet test src/server/tests/Scs.ArchitectureTests -c Debug
```

```bash
dotnet test src/server/tests/Scs.IntegrationTests -c Debug
```

```bash
npm --prefix src/web run ci
```

```bash
npx --prefix src/web playwright test
```

What must pass, beyond what already passed:

| Suite | Proves |
|---|---|
| Unit `Entitlements` | Every row of the rules table in data-model.md (User Story 1, SC-004) |
| Unit `CairoMonth` | `yyyy-MM` at 21:59 and 22:00 UTC on the last day (00:00 Cairo) |
| Integration `PlanGateTests` | A probe route with `RequiresFeature(allow_analytics)` (marked built in the test database): refused 403 `PLAN_FEATURE_NOT_INCLUDED` without the feature, passes with it; permission refused first; upcoming never refused |
| Integration `PlanGateTests` (declarations) | Every declared key is in the catalogue (FR-010); no operators' route names a feature; a route names its feature once |
| Integration `SeatTests` | Limit 3: the three creations, re-enable, re-invite and re-issue are refused at 3. Disabling frees a seat. An expired invitation takes no seat. Lowering to 2 keeps all 3 and refuses the 4th. No plan and no limit refuse nothing |
| Integration `SeatRaceTests` | 20 creations together with one free seat: exactly 1 succeeds (SC-002) |
| Integration `MeterTests` | Use, refuse, promise beyond the allowance, give back, a mid-month plan change, no plan, and the month restart through a fake clock (SC-006). 20 uses together that only one fits |
| Integration `ClinicPlansTests` | Set, the same again, unknown plan, the trail in both, isolation (another clinic's row is invisible) |
| Integration `DevClinicPlanTests` | The development route sets and clears a clinic's plan locally, refuses an unknown clinic or plan, and does not exist in test or production (FR-031) |
| Integration `SeatTests` (activation) | An invitation activated by its link at the limit is not refused (FR-017) |
| Integration `ClinicPlansTests` (at once) | Ten sets of one clinic's plan at once take their turns; none fails (T056) |
| Integration `ClinicLimitsTests` | The eight in order, users counted, the others `Used` null, monthly renewal date |
| Integration `SessionPlanTests` | `plan.withheld` lists only built switches not included |
| Integration `EntitlementTimingTests` | 200 checks: the 95th percentile ≤ 50 ms (SC-008) |
| Architecture | `Subscriptions` reaches others only through contracts; nothing references it but through `Scs.Subscriptions.Contracts`; building blocks reference no module |
| Isolation guardrail | `subscriptions.ClinicPlan` and `subscriptions.MeterEntry` carry the policy |
| Vitest | `PlanSource`, the destination → feature table, the card's words with and without `subscription.view` |
| Playwright `not-in-plan.spec.ts` | The cards in both languages at 1440 and 390, without the link, the role first, upcoming never shown, a plan change on the next page |

## 2. By hand on the local stack (a walk account, never the seeded operator)

What can be walked today. No route creates an account or re-enables one before spec 026: the
development route of spec 004 only sends an existing invitation again. So the users limit is shown
by `SeatTests` and `SeatRaceTests`, not walked.

1. In the operators console, sign in with a walk operator (invited with the Migrator; never
   `operator@heta.example`). Add a plan «باقة الجولة» with «المستخدمون» set to 2 and «التقارير
   المالية والطبية» included, and note its code.
2. Put the seeded clinic noor on it:

   ```bash
   curl -s -X POST http://localhost:5080/v1/dev/clinic-plan -H "Content-Type: application/json" -H "X-Scs-Request: 1" -d "{\"clinic\":\"noor\",\"planCode\":\"<the code>\"}"
   ```

   It answers 204.
3. Read noor's trail and the platform's: one `subscription.plan-set` in each, with the plan
   before (none) and after.
4. Sign in to the clinic console as `owner@noor.example` (password in `SCS_SEED__STAFF_PASSWORD` of
   `deploy/compose/.env`, code from Mailpit). `GET /v1/auth/session` carries `plan` with the
   plan's names and `withheld: []`: every feature with a page is still upcoming.
5. Open «التقارير المالية»: it shows "not available yet", never the not-in-plan card.
6. Put noor on no plan (`"planCode": null`). One more `subscription.plan-set`, and the session's
   `plan` has null names.
7. Read the API's log of the walk: no error, no personal data.

## 3. Beside the design

Lay each picture beside its frame:

- the inbox card, RTL and LTR (`6701:1002017`, `6701:1002375`);
- the financial card, RTL and LTR (`6614:945146`, `6614:945529`);
- the card without the link (undrawn, compared with the drawn card minus its link).

Each is taken at 1440 and 390. The differences go into `validation.md`, and the side-by-side
picture into `design-vs-real.png`.
