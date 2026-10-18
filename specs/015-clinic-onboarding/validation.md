# Validation: Clinic Onboarding

## Baseline (T001)

Spec 014's last full run (`specs/014-plan-enforcement-metering/validation.md`):

| Suite | Count |
|---|---|
| Server unit tests | 960 |
| Architecture tests | 72 |
| Integration tests | 957 (956, plus the test of the last fix) |
| `npm run ci` | passed (unit tests clinic 77, ops 100, core 164, ui 179) |
| Browser tests, all three apps | 1223 |

Not run again before this work: develop has not changed since 014 was merged (`0762992`).

## Frames read (T002, 17 Oct 2026)

Read as pictures through the Figma connector (file `SetO9EhDivNcmurAMfcflY`, page «Template —
Clinics» `591:2`), with the English detail and settings frames read layer by layer for their words:

- **List**: AR `606:776`, EN `599:680`, loading `607:120`, error `608:168`, empty `712:611`, the
  filter panel `2313:2714`.
- **Form**: add AR `655:1140`; edit AR `655:1230` and EN `649:1124`; invalid tax number `2263:2467`.
- **Detail**: AR `632:956` (whole page), EN `613:872` (layers), error `637:1012`, the sign-in
  address row in its states AR `2330:3095` and EN `2330:3050`.
- **Settings**: AR `2589:4185` and EN `2589:4007` (layers).

What the frames settle, and where they differ from the contracts:

- **Side bar**: «العيادات» sits after «الرئيسية» with the icon `fi-rr-building`, which is the
  console's existing `building`.
- **List cards**: each card has a tinted round icon (building purple, check green, clock orange,
  alert red) and its note as a coloured pill under the number.
- **List error** (`608:168`): the frame hides the cards, the toolbar and the table, and shows one
  centred error card. The contract said the cards show «—»; the frame is followed for the look.
- **List empty** (`712:611`): the frame says «لا توجد عيادات بعد» / «أضف أول عيادة للبدء.». The
  BRD (§7-1) says «لا توجد عيادات» / «جرب تعديل البحث أو الفلاتر، أو أضف عيادة جديدة للبدء.».
  The BRD's words are built (constitution: the BRD for wording); a designer item.
- **Filter panel** (`2313:2714`): the title is «فلترة النتائج», the statuses are check boxes in
  two columns, and «الباقة» and «مدة الاشتراك» are drop-downs. The frame draws «ينتهي خلال» below
  the buttons; it is built above them, with the other facets (the buttons close the panel).
- **Form**: one card about 648 wide at the start of the page, fields two to a row, and the
  buttons at the card's end. The specialty field is half width, with its hint across the card.
  - The edit frame's breadcrumb is «العيادات / تعديل العيادة» (no clinic name). The note's hint
    is «تظهر في سجل نشاط العيادة وسجل نشاط المشغل.».
  - The plan and the time zone are drawn as drop-downs. They are built disabled, as drawn (the
    owner's answer); the plan's price hint belongs to 020 and is left out.
- **Detail**: the head is avatar, name, badge, contact line, «تعديل العيادة» at the far end. The
  tabs are underlined. «معلومات العيادة» is a 560-wide card at the start, label above value,
  rows divided; «تعيين» is a small outline button beside the manager's name.
  - The info card of `632:956` has no sign-in address row; the row is drawn on its own
    (`2330:3095`). It is placed after «التجديد القادم», in §7-3's order.
  - The avatar shows Latin initials («AN») on the Arabic page too: built so, by the owner's answer.
    The list keeps the Arabic letter, as its frame draws.
  - The error frame keeps the breadcrumb with the clinic's name; when the clinic could not be read
    the name is unknown, so the breadcrumb shows «العيادات» alone.
- **Sign-in address row**: label «عنوان دخول العيادة», the address left to right with
  `https://`, a square copy button, a badge «دعوة معلّقة» (orange) or «انتهت صلاحية الدعوة» (red),
  and «إعادة إرسال الدعوة» with the send icon, outline while live and purple once lapsed.
  - The state "no invitation was ever sent" is not drawn. It is built like the lapsed state, with a
    grey badge «لا توجد دعوة سارية» (the owner's answer).
  - The frames show a per-clinic address (`al-noor.smartclinic.eg`, `noor.app.smartclinicos.com`);
    the build shows `https://app.scs-heta.com` (the owner's answer; a designer item).
- **Settings**: a light purple read-only strip, then full-width cards with a 260-wide label column;
  «الإعدادات العامة» has «تعديل العيادة ←» at its end. The English strip still mentions the time
  zone; the BRD's correction removes it in both languages.
- **English words** are taken from `599:680`, `649:1124`, `613:872`, `2330:3050` and `2589:4007`
  into `ops/public/i18n/en.json` (T028), so the contract keeps the Arabic only.

## Test-first runs (T005–T008, 17 Oct 2026)

Written before the routes, the scope, the add and the crossings, then run:

| Test | Result before the code |
|---|---|
| `OpsClinicRoutePermissionTests` (3) | all failed: no clinic route exists (404, not 401/403) |
| `RouteDeclarationTests` (owed `clinics.view`, `clinics.edit`) | failed: no route guarded by either |
| `RouteAuditDeclarationTests` (the four writes) | failed: the routes are missing |
| `OperatorClinicIsolationTests` (3) | the route test and the not-found test failed; the `CLINIC_MISMATCH` test passed (it checks 002's context, unchanged) |
| `AddClinicPlanLimitTests` (2) | both failed: no add |
| `DeclaredCrossingsTests` (12, `update-clinic` recorded) | both failed: three crossings missing |
| `IsolationBehaviourTests` with `BranchSample` | **passed at once**: the table and its policy were written together in T003, before the sample. Proven by breaking it on purpose in T066 |
| `ClinicDataRulesTests` (3) | passed at once: the module was created referenced by the host only |

The earlier failing runs above stand for T066. On 17 Oct 2026 the four were run again against the
final code and pass. The one that passed at once, `BranchSample`, was proven by breaking it on
purpose: with `CreateClinicIsolation("clinicdata", "Branch")` taken out of the `Branches` migration,
three tests failed (`IsolationBehaviourTests` for `Branch`, and both guardrail tests). With the line
put back, all 101 isolation tests pass.

## What was run (T067, 17 Oct 2026)

| Suite | 014 (T001) | Now |
|---|---|---|
| Server unit tests | 960 | 1005 |
| Architecture tests | 72 | 78 |
| Integration tests | 957 | 1007, all passing in the last full run (after the fourth convergence pass) |
| `npm run ci` | passed | passed (unit tests clinic 77, ops 143, core 164, ui 182) |
| Browser tests, all three apps | 1223 | 1275 passed, 4 skipped (as before); in the last full run the timing test of `clinic.spec.ts` («under half a second», not this spec's) missed under load and passed alone |

- **The last full integration run** (after the mixed-session fix) failed two tests:
  - `TemporaryPasswordGateTests` walks every route with a clinic session holding a temporary
    password and expected 403 on the operators' routes too. They now answer 401, as a clinic's
    session is no session there (FR-037 of 010). The test expects that now, and passes.
  - `TraceRedactionTests.Health_probes_are_not_traced` missed one span under load, and passes when
    run again. It is not touched by this spec.
- **The list's timing (T040)**: over 300 clinics, a search answers in a median of 26 ms; the slowest
  of the runs took 118 ms. The limit is one second. Run again over 50 searches, as T040 says
  (T098): a median of 64 ms, the slowest 91 ms.

### Found and fixed while finishing

- **The clinic's page asked for the clinic again and again.** The page's effect read the clinic
  while loading, so each answer started a new load. The browser test now counts the reads: without
  the fix, 150 more reads in 1.5 s; with it, one per tab.
- **A browser signed in to a clinic could not add a clinic.** Locally both consoles share
  `localhost`, so the clinic's session cookie went with the operators' requests too. The staff
  scheme established that clinic, and the add then failed with `CLINIC_MISMATCH`. The owner hit it
  on the real stack.
  - The staff scheme now takes nothing on a route that declares `OperatorRouteMetadata`.
  - The test `A_browser_also_signed_in_to_a_clinic_adds_and_reads_clinics_as_the_operator` failed
    with that 400 before the fix and passes after it.
- **The email-changed notice in Arabic** broke the masked address across the line's direction. It
  is now marked left to right, as other addresses are, and the browser test checks it.
- **Tests of built specs that assumed no clinic has a plan**:
  - `PlansReadTests`: its "no clinics" case now uses an empty stand-in.
  - `PlanBookContractTests`: revenue is null or nought, by the clarification.
  - `AuditFactsTests`: the `clinic.*` actions are written by operators.
  - `RefusalTests`: only the stored states, since `CK_Clinic_State` holds them.
  - `HousekeepingTests` and `OperatorDetailTests`: the grown profile and the new card.
- **Clinic tests that moved the clock**: they made roles dated in the future, which sorted after
  later tests' roles. The operators are now made before the clock moves.

- **Earlier browser tests moved with this spec**: four tests of 010–013 pinned the side bar without «العيادات», used `/clinics` as an unknown address, or read the role's label without its new star. They were updated. The gallery's kept pictures of the drop-down (the star) and of the two new parts were accepted anew and opened.

## The real-stack walk (T068, 17 Oct 2026)

Scripted with Playwright against the real API, database and Mailpit, in Arabic at 1440. It used the
walk's own accounts only:
- an operator invited with the Migrator, a super admin, as the editor;
- a viewer that editor invited, holding «عرض العيادات» alone;
- the owners of the clinics it added.
The seeded operator and noor's owner were not touched. Run three times; the last run, after the
fixes below, passed every step.

| # | Seen |
|---|---|
| 1 | «العيادات» after «الرئيسية»; the cards and the count read the real clinics; no Eastern digit |
| 2 | «تم إنشاء العيادة!»; the detail with «تجربة», renewal 14 days on, «0 أطباء · فرع واحد», the Saudi mobile, «دعوة معلّقة» with its 72 hours |
| 3 | **SC-001**: from «إضافة العيادة» to the invitation in Mailpit, 0.3–0.7 s. Arabic, the 72-hour link, and now the sign-in address |
| 4 | the second slug ends `-2` |
| 5 | «هذا البريد مسجَّل لحساب آخر على المنصة.» under the email; nothing added |
| 6 | the plan disabled meanwhile: «تعذر إنشاء العيادة.»; nothing added; nothing left in setting-up or discarded |
| 7 | the copy toast and the clipboard; the first resend's new expiry; the second refused within the minute; one more email |
| 8 | the viewer chosen as manager, then «غير معيّن»; both in the platform trail as the walk operator |
| 9 | «تم تحديث بيانات العيادة.»; address and tax number changed; `clinic.updated` with before, after and the note |
| 10 | the owner set a password and signed in. After the email moved: the notice at the old address, masked; the old session ended; the old email refused; the new one signed in |
| 11 | the editor's strip, the language now English, «تعديل العيادة ←» |
| 12 | the viewer sees no add, no menus, no edit, no assign, no resend; the viewer's strip; `/clinics/new` refused |
| 13 | the plan counts its 2 clinics, both on trial; revenue 0 |
| 14 | an unknown id shows the load error |

- **The English pass** stayed Arabic: the console follows the operator's own language, Arabic for an
  invited account. English is covered by the browser tests.
- **The three `setting-up`/`discarded` rows** in the local database are the owner's adds that
  failed with `CLINIC_MISMATCH` before that fix. Each was undone as designed.

### Found by the walk and fixed

- **The plan named twice**: the head line read «باقة باقة الجولة …» for a plan whose name starts
  with «باقة». A name that already says it is a plan is now written as it is (`planWords`, with a
  unit test).
- **The form's refusal banner** drew its icon above its words. The page had set it to
  `display: block`, the fault 014 fixed elsewhere. The browser test now checks icon and words share
  a line, and the state is pictured.
- **The invitation did not name the sign-in address**, as contracts/emails.md says it does. By the
  owner's answer it now has a line, «بعد تعيين كلمة المرور يكون الدخول من <host>.», after the button.
  - The host comes from the deployment's staff address: `app.scs-heta.com` in production,
    `localhost:4201` here.
  - It is written as text, not a link: the button stays the only link (the catalogue's rule).
  - This touches every staff invitation (spec 004's email). The words are proposed.
- **Three sentences addressed the reader** (BRD §5-17, §5-18). By the owner's answer they are now in
  the passive, in the console and in the server's titles:
  - «يمكن إعادة الإرسال بعد قليل.»
  - «تُراجع إعدادات البريد ثم تُعاد المحاولة.»
  - «تُعاد المحاولة بعد قليل.»

## Pictures opened (T069, T070)

- **Figma frames, through the connector**: list `606:776`, add `655:1140`, edit EN `649:1124`,
  detail `632:956`, settings `2589:4185`.
- **Real pictures**, from `SCS_SHOTS` with the fake platform:
  - opened at 1440: AR list, AR add (before and after the changes below), EN edit, AR detail, AR
    settings;
  - opened at 390: AR list, EN detail;
  - the other states were taken, and checked by the tests' look checks (nothing outside its box,
    no Eastern digits), but not opened one by one.
- **Emails**: the AR email-changed notice was opened, which is how the direction fault was seen.
- **`design-vs-real.png`**: the list, the add form, the detail and the settings tab beside their
  frames, in Arabic at 1440. Opened.

### Differences from the frames, and whose decision

- **Decided by the owner on 17 Oct 2026**:
  - a required drop-down shows the red star, in the shared `ui` control for both consoles; the
    operator form's «الدور» keeps it although that form has no other star;
  - the clinic form's buttons sit at the card's start, the main action first, as drawn;
  - the English-name field sits beside the Arabic one, leaving a gap beside the mobile;
  - Latin initials on the detail;
  - the add form's rows: the Arabic and English names, then contact · email, then the mobile at
    half width (recorded wrongly before as "the list's rows"; T085);
  - the edit form's plan and time zone are disabled drop-downs;
  - "no invitation" looks like lapsed, with a neutral badge.
- **Out of this spec's scope**, recorded and not built:
  - «إرسال إشعار», «تصدير إلى إكسل» and the row check boxes of the list;
  - the detail's KPI cards, tickets, activity, plan features, users and extra units;
  - the lifecycle buttons;
  - the settings cards other than «الإعدادات العامة».
  These come with their own specs.
- **Small, kept**:
  - the email field shows the shared envelope icon;
  - page titles use the console's shared title size, not the frames' heavier one;
  - the «يحتاج متابعة» note is green while every count is 0.
- **Recorded earlier** (in T002 above):
  - the empty state follows the BRD's words;
  - «ينتهي خلال» sits above the filter's buttons;
  - the error breadcrumb has no name;
  - the settings strip has no time zone;
  - the per-clinic address.

## Built differently from the plan

- **The overview**: built inside `clinic-detail.page.ts`, with no `clinic-overview.tab.ts`.
- **Each tab is its own route** (`/clinics/:id` and `/clinics/:id/settings`), so a tab change reads
  the clinic again.
- **The add lock** waits 10 s, not 15.
- **The seed** keeps the old `RegisterAsync`.
- **The KPI card** has a `danger` tone added.
- **The settings strip** is drawn by the page itself, not by a shared part.
- **`docs/adding-a-module.md`** describes `IClinicContext.Establish(id, ClinicSource.Operator)`.
  The plan's name `OpsClinicScope` was not used.
- **A plan's users limit cannot be 0** (1 to 1,000,000). The refusal test sets the catalogue's
  baseline `max_users` to 0 instead, and the quickstart's "a plan with `max_users` 0" cannot be made
  through the screens.
- **The sign-in refusal test of spec 002** keeps only «active» among the states a team can be in.
  `trial` and `read-only` are not stored states until spec 016.

## Convergence (T073 – T085, 17 Oct 2026)

A review of the code against every requirement found thirteen gaps. Each was closed, with tests
seen to fail first where code changed:

- **T073 — an edit is whole or nothing.** Both parts are checked before either is written:
  - the clinic's part, by `IClinicRegistry.CheckProfileAsync` on a copy nobody saves, with the
    version;
  - the owner's part, by `IClinicOwners.CheckDetails`.
  A refused field or a stale form now changes nothing, the owner included, and both parts' errors
  come back together. One window remains: a second writer between the owner's save and the
  clinic's. It is answered 409, after the owner's part.
- **T074 — a plan's clinic in trial** reads «تجربة · تنتهي خلال 4 أيام», as frame `2016:963` draws it
  (the owner's answer). The join date keeps ordering the list newest first and is not shown.
  «تتجدد <date>» for an active clinic waits for spec 018's billing cycles.
- **T075 — an owner-only change moves the clinic's version**, with no `clinic.updated` entry, so a
  form read before it is refused.
- **T076 — the tax number and the mobile take Western digits only.** Eastern-Arabic ones answer at
  their field, no longer a 500. The registration retries only on a slug clash.
- **T077 — the undo of a failed add removes the main branch too.** A plan disabled between the
  check and the plan's stage can still leave the owner's `account.role-changed` entry in the
  given-up clinic's trail. No operator can read that trail, as SC-003 allows.
- **T078 — the add's turn is let go before the invitation is sent.** A slow mail server no longer
  makes the adds behind it busy (tested with a six-second mailer).
- **T079 — the email-changed notice** is in the clinic's language as saved, not the owner's.
- **T080 — the resend's trail entry** stays in its own unit after the new link and the email, by
  the owner's answer.
  - An email that left cannot be called back, so writing the entry first would gain nothing.
  - The order guarantees the trail never says "sent" for an email that did not leave.
  - If writing the entry fails, which is rare, the email has gone without an entry.
- **T081 — a clinic with no owner account** shows no owner fields in the edit.
- **T082 — the detail:**
  - «آخر تسجيل دخول» is a Cairo date and time;
  - the manager select's label is read but not drawn (a new opt-in `labelHidden` on
    `scs-select`), and the row's own label stays;
  - «غير معيّن» is the select's nothing-chosen row;
  - a stale assignment says the clinic changed elsewhere and reads it again;
  - a manager no longer offered is not preselected.
- **T083 — the filter:**
  - «ينتهي خلال» offers only the windows some searched clinic ends in, and the chosen one;
  - the panel opens on the applied filters.
- **T084, by the owner's answer, both left to spec 016:**
  - the owner's `account.role-changed` entry is written by spec 004's roles to the clinic's
    trail only; the platform's trail has `clinic.created` with the owner;
  - a `suspended` or `cancelled` clinic would count in the total and in no other card, but
    nothing sets those states before spec 016, which counts them.
- **T085 — the pictures.** Newly opened:
  - at 1440: the English list, detail and settings, and the Arabic plan detail with its trial
    line;
  - at 390: the Arabic detail and settings, and the English edit.
  The phone form showed a blank row where a spacer kept the wide form's columns in step. The
  spacers now take no room in one column.
  - **Seen, not changed:** Arabic text typed in an English form's field takes the page's
    left-to-right direction («14 شارع …» starts at the left). That is the shared text field's
    behaviour, a designer item.

## Convergence, second pass (T086 – T092, 17 Oct 2026)

- **T086 — a fault while inviting** (not only an email that did not leave) now leaves the clinic made
  and answers 201. The detail shows no live invitation and offers «إعادة إرسال الدعوة»; the fault is
  logged.
- **T087 — «غير مسجّل» shows once** in the edit's specialties: the select's nothing-chosen row.
- **T088 — the minute between invitations counts only live links.** After an invited owner's email
  changes, the invitation goes to the new address at once.
- **T089 — a taken email comes back with the other refused fields**, in the edit's one check.
- **T090 — the undo of a failed add also takes away the plan link**, by the owner's answer. Its
  `subscription.plan-set` entries stay, as trail entries always do, for a given-up clinic nobody
  sees.
- **T091 — «مدة الاشتراك» uses the Arabic count forms**, by the owner's answer: «تجربة 14 يومًا»,
  «تجربة 7 أيام», «تجربة يومين». English keeps «14-day trial».
- **T092 — the English list at 390, with the filter panel, opened.** Cards, rows and the panel fit;
  the search's placeholder is cut by its box («Search clinics by name or cont…»), as in Arabic. The
  chips row after applying was seen at 1440 only.

## Convergence, third pass (T093 – T096, 18 Oct 2026)

- **T093 — a request given up part-way** (a dropped connection) is now undone like any fault.
  Before, a cancellation skipped the undo and left the owner account holding its email for good.
  The undo uses no token of the request, and the cancellation is passed on after it.
- **T094 — changing an active owner's email ends their sessions, sign-ins and links.** It now also
  sets aside the sign-ins the old address had started and its reset links. A code already sent to
  the old address no longer opens a session.
- **T095 — the status chip** joins its statuses with the page language's comma: «تجربة، نشطة»,
  "Trial, Active".
- **T096 — «تعديل العيادة» on the detail** is disabled while offline, as every write is.

## Convergence, fourth pass (T097 – T098, 18 Oct 2026)

- **T097 — a fault inside the activation itself** now still discards the clinic and retires its
  slug.
  - Before, the activation had already marked this request's copy of the clinic active, so the
    undo read "active" and left the row in `setting-up`, unseen, with its slug held.
  - The undo now reads the stored state.
  - The test makes the real activation fail after the clinic's row was saved: a check on the
    platform trail, added to the test's database for the test only. It failed without the fix.
- **T098 — the list's timing test** runs 50 searches, as T040 says.

## Owner and designer items

- **The per-clinic sign-in address**: the frames draw one per clinic; `https://app.scs-heta.com`
  is built (designer).
- **«رقم الهاتف»**: the frames' label; «رقم الموبايل» is built for the owner's mobile (owner).
- **The two name fields, Arabic and English**: by the clarification; the frames draw one (designer).
- **The mobile**: any country: 8–15 digits with an optional leading +, spaces and dashes dropped, by the owner's answer.
- **Words and codes**: the proposed words of `research.md` R13a and the seven new codes stand
  until the owner reads them.
- **The BRD's three imperatives** (§5-17, §5-18): built in the passive by the owner's answer of 17 Oct 2026.
- **The invitation's sign-in line**: proposed words, added to every staff invitation (owner); the link emails' frames do not draw it (designer).
- **The email-changed notice**: its words are proposed; the BRD does not word it (owner).

## Not checked

- **The narrow and English pictures** of every state were not each opened.
- **The walk in English**: the walk's operator reads Arabic, its own language; English was seen through the browser tests and the pictures only.
