# Validation: Plan Enforcement and Limit Metering

## Baseline (T001)

Spec 013's last full run (`specs/013-plans-pricing/validation.md`, with its convergence passes):

| Suite | Count |
|---|---|
| Server unit tests | 941 |
| Architecture tests | 68 |
| Integration tests | 907 |
| `npm run ci` | passed (unit tests clinic 68, ops 100, core 164, ui 179) |
| Browser tests, all three apps | 1200 passed, 4 skipped |

Not run again before this work: develop has not changed since 013 was merged (`0b32787`).

## Frames read (T002, 17 Oct 2026)

Read as pictures through the Figma connector, with the card's layers read for sizes and words:

- «Inbox / Not In Plan» RTL `6701:1002017` and LTR `6701:1002375`;
- «Financial / Not In Plan» RTL `6614:945146` and LTR `6614:945529`.

What the card is in the frames:

- **The page header stays.** It shows the title, the line and the «مقترحة» badge for the inbox,
  and the title only for the financial reports.
- **The card itself.** A white card (`#ffffff`) holds the Empty State instance `4252:59927`
  (`Context=NoData`): 320 wide, padding 32/24 and gap 12. It contains:
  - a 64 × 64 circle in `#eff9fb` with the lock;
  - the title at 16 SemiBold `#1a2024`;
  - the line at 13 Regular `#57646d`;
  - the action, a Link button (`4252:1100`).
- **The inbox card fills the page's height** (padding 64/24). **The financial card is shorter**,
  and its line runs wider than 320.
- **The link differs between frames.** The inbox draws it as a plain link and the financial frame
  underlines it.
- **The words** are as `contracts/not-in-plan-screen.md` §2–§3 give them. The Arabic financial
  frame says «الخطة»; it is read as «الباقة», by the owner's decision of 17 Oct.

Differences carried into the build, and why:

- **The card is `scs-empty-state context="not-in-plan"`.** It draws the same 64 circle and the
  lock, with the shared library's text sizes. The frame's instance uses `Context=NoData` with the
  lock placed in it by hand.
- **One height for both cards.** The two frames do not agree, so the card keeps the shared
  empty-state's height inside the page's card.
- **The link is the shared `scsLink` style for both.** The frames disagree on the underline.

These go to the designer: the frame's «الخطة»; the card without its link (undrawn); the words of
the other feature pages (undrawn); the two frames' disagreements on height and link.

## Built differently from the plan

- **Test-first was not kept in this run.** The rules, the gate, the seat checks and the meter were
  written before their tests, which the constitution asks the other way round for plan limits.
  To show the tests would have failed first, each of the five test-first parts was broken on
  purpose, its tests run, and the part restored (convergence T049):

  | Part broken | How | Tests that failed |
  |---|---|---|
  | The limit's lock (`PlanLimits`) | lock taken out | `SeatRaceTests`: 18 of 20 creations went through |
  | The entitlement rules (`Entitlements`) | a limit left out of the plan got no limit instead of its baseline | 2 of 12 `EntitlementsTests` |
  | The plan gate (`PlanGate`) | a withheld feature let through | 2 of 7 `PlanGateTests` |
  | The seat count (`Seats`) | a lapsed invitation counted | 1 of 10 `SeatTests` |
  | The meter's refusal (`MonthlyMeter`) | a use past the allowance taken | 3 of 9 `MeterTests` |

  Each part was restored, the source checked for the break, and the solution rebuilt before the
  final runs.
- **No separate `CatalogueState` collection** (analyze U1): every database test already runs in
  the one `DatabaseCollection`, one after another, so a test that marks a feature built is never
  seen by another; each restores the status when it ends.
- **The refusal's words fill their own placeholders**: `ScsProblemException.TitleValues` and the
  problem writer put the feature's catalogue name and the limit into «… من «{feature}»: {limit}».
- **`PlanLimits.EnsureRoomAsync` needs the caller's transaction** rather than opening one: the lock
  must last until the caller's write commits.
- **The seat count**: an invited account with no link counts only within an invitation's life from
  its creation, so an invitation whose email never left does not hold a seat for ever.
- **Re-issuing a password to an invited account** checks the seat inside the password setter's own
  transaction (`SetPassword.CheckSeat`), since the setter opens its own.
- **An operators' route naming a feature** is caught by `PlanGateTests`, not at start-up.
- **The meter's table**: a check `CK_MeterEntry_Key` (the four keys) was added; the give-back check
  reads `(Amount < 0 AND GivesBack IS NOT NULL) OR (Amount > 0 AND GivesBack IS NULL)`; the set is
  named `Meter` (an architecture rule reads `Entries.Add(` as an audit write).
- **No key from the two tables to `tenancy.Clinic`**, as the files engine's table has none.
- **`tenancy.Clinic.Specialty` is `varchar(40)`**, the specialties' keys being Latin.
- **The development route's record** names no actor (the trail's own rule when nobody is signed
  in), not `system:dev`.
- **006's browser tests stay on the sample sources** (only their expected words changed to the new
  card); the real source — the session's plan — is tested by `not-in-plan.spec.ts`.
- **The card's widths are 1440 and 1024**, not 390: the clinic frame is complete from 1280 and
  scrolls sideways under 1024 by design (spec 006, FR-043). SC-007 was corrected.
- **The card's test ids** are the empty-state part's own (`empty-title`, `empty-text`).
- **The card's title weight** is set to 600 on this page, as the frames draw it, without changing
  the shared empty state.
- **An undrawn page's line** says «"«{name}» ليست ميزة في باقة العيادة الحاليّة…"» / "“{name}” is not
  a feature of …", so a catalogue name of any gender or number reads correctly.
- **`max_users` is marked built only where it was still upcoming**, as database-changes.md asks.

## What was run (T045)

| Suite | Count | Against the baseline |
|---|---|---|
| Server unit tests | 960 passed | +19 (941) |
| Architecture tests | 72 passed | +4 (68) |
| Integration tests | 956 passed (24 m 52 s) | +49 (907) |
| `npm run ci` | passed — lint, `i18n:check`, tools' tests, unit tests clinic 77, ops 100, core 164, ui 179, the three builds | clinic +9 (68) |
| Browser tests, all three apps, Chromium | 1223: 1219 passed, 4 passed when run again | +23 (1200) |

- **The four browser tests run again** were the gallery's pictures of the indeterminate progress
  bar (an animation), compared while the machine was busy. They passed on their own; spec 014
  changed nothing of the gallery or `ui`.
- **SC-008**: 200 checks, median 13.4 ms, 95th percentile 19.2 ms (target ≤ 50 ms).

## The real stack (T046)

Walked with `node_modules/.tmp/walk14.mjs` after restarting the API preview, so the three
migrations applied. The operators' seeded account was not touched.

| Step | Seen |
|---|---|
| Migrations | `subscriptions` history 1 row; `tenancy.Clinic.Specialty` present; `max_users` built |
| An active plan made in the console (013's walk) | found |
| `POST /v1/dev/clinic-plan` noor → that plan | 204; an unknown plan code 404 |
| Trails | `subscription.plan-set`: one entry in noor's trail and one in the platform's; setting the same plan again added none |
| noor → no plan | 204; a second entry in each trail |
| The API's log | no error; the walk's lines carry routes, status codes and the clinic's id only |

Not walked:

- **Signing in as `owner@noor.example`** to read the session's `plan` and open «التقارير المالية».
  The password in `deploy/compose/.env` is refused: the account's password was changed since it
  was seeded. It was not reset — that would change an account the owner may be using — and no
  route creates a clinic account before 026. `SessionPlanTests` and `not-in-plan.spec.ts` cover
  these two steps.
- **The users limit**: no route creates or re-enables an account before 026. `SeatTests` and
  `SeatRaceTests` cover it.

## Pictures (T038, T055) and beside the design (T047)

- **Opened**, after the title weight was set to 600: `plan-ar-inbox`, `plan-en-inbox`,
  `plan-ar-financial`, `plan-en-financial`, `plan-ar-financial-no-link`, `plan-en-financial-no-link`
  (`node_modules/.tmp/shots014/`).
- **`design-vs-real.png`** lays the four frames beside the cards, and the drawn card beside the
  undrawn no-link card. It was opened.
- **What stays different, and why**:
  - The inbox frame's subtitle and «مقترحة» chip belong to the inbox page (061), not the shell.
  - The financial page's breadcrumb «التقارير ‹ التقارير المالية» is 006's frame.
  - The card's height and the link's underline differ between the two frames: the shared empty
    state is kept.
  - In the frame's English line, "Financial and clinical reports" wraps wider than our 320.
  - «الملف والاشتراك» may wrap inside the narrow paragraph.

## For the designer and the owner

- **Designer**:
  - the Arabic financial frame says «الخطة» (built as «الباقة», owner 17 Oct);
  - the card without its link is undrawn;
  - the words of the other feature pages are undrawn (built as «الباقة الحاليّة لا تشمل «{name}»»);
  - the two frames disagree on the card's height and the link's underline.
- **BRD**: the limit refusal's words, «بلغت العيادة حدّ باقتها من «{الميزة}»: {الحدّ}. تُرفع الباقة
  من «الملف والاشتراك».», chosen by the owner, are not yet in the BRD.
- **Local environment**: `SCS_SEED__STAFF_PASSWORD` no longer opens `owner@noor.example`.

## Not checked

- **The card on a real page.** Every feature with a page is still upcoming, so the card is seen
  only with the platform stood in for.
- **Edge.** The browser tests ran in Chromium only.
- **The meter with a real consumer.** None exists before 061, the teleconsult specs and the AI
  specs; the meter is proven through its own operations.

## Convergence pass 2 (T056, T057)

- **T056**: sets of one clinic's plan at once used to clash on the row's key (no row yet) or its
  version, and reach the caller as a 500. The test — ten sets at once on a new clinic, then ten
  on its row — was written first and failed on `PK_ClinicPlan`. `IClinicPlans.SetAsync` now takes a
  lock per clinic inside its transaction: each set waits its turn, none fails, and every change
  is in both trails. data-model.md says so.
- **T057**: the quickstart's test table names the files that exist.

Run after: unit 960, architecture 72, and the subscriptions and isolation integration tests 145,
all passed. The full integration suite (956, +1 for the new test) was run before this change; the
one class changed, `ClinicPlans`, is covered by the 145. Nothing on the web changed.

## Convergence pass 3 (T058)

- **T058**: `not-in-plan.spec.ts` opens «التقارير المالية» again with the browser offline: the card
  shows from the last session copy (the failed re-read keeps it), with no error and no sign-in
  page, in both languages. The card's browser tests: 16 passed. The file is formatted with
  Prettier and passes lint. No product code changed.
