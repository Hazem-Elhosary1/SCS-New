# Feature Specification: Plan Enforcement and Limit Metering

**Feature Branch**: `014-plan-enforcement-metering`

**Created**: 2026-10-17

**Status**: Draft

**Input**: User description: empty. Taken from row `014-plan-enforcement-metering` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 1): "one enforcement point, the eight limits,
metering use, «غير مشمولة في باقتك»." Shared §4 · SH-014…018. Depends on 013.

**BRD source**:

- Shared volume (`BRD-0-shared.md`) §4:
  - SH-014: a feature that is not included is refused by the server with one message, and the
    clinic console shows «غير مشمولة في باقتك» with the way to upgrade.
  - SH-015: the eight counted limits.
  - SH-016: the eight are shown with the same names and numbers wherever they appear. This spec
    gives the one source; the three places are later specs.
  - SH-017: whatever raises a counted number is refused at the limit, whichever way it comes
    (adding and re-enabling, from the clinic and from the operators).
  - SH-018: the platform measures the monthly use of each limit and lowers its balance on every use.
  - Read with SH-010, SH-011, SH-012, SH-019 (extra units, spec 021), SH-020, SH-021 and SH-097.
- Ops volume, module 16 (`16-ops-clinics-plans-billing.md`), rules:
  - ق-٣٦: one read point answers what a plan includes, what a clinic has, whether X is allowed,
    and what X's limit is.
  - ق-٣٧: a specialty narrows a plan, never widens it.
  - ق-٣٨: an included limit with no number has no limit. A limit not included, or with no row,
    takes the feature's baseline.
  - ق-٣٩: a feature the catalogue does not know has a limit of 0.
  - ق-٤٠: refused when used ≥ limit before creating, with one code for every limit
    (`PLAN_LIMIT_REACHED`, carrying the feature and the limit) and one for every feature not
    included (`PLAN_FEATURE_NOT_INCLUDED`).
  - ق-٤١: a limit is never applied backwards.
  - ق-٤٧, ق-٤٨: a disabled plan and a retired feature stay with whoever has them.
  - ق-٤٩: an upcoming feature is enforced by nothing.
  - ق-١٠٣: an expired invitation takes no seat, and re-sending it is checked against the limit.
- Clinic volume, module 10 (`10-administration.md`):
  - What «المستخدمون» counts (the table "الميزة أو الحدّ" in §10): accounts not deleted and not
    disabled; an invited account whose invitation has expired is not counted (ق-٠٦).
  - CL-AD-029 and CL-AD-044: adding, and re-enabling, at the limit are refused naming the limit.
  - ق-أ-١٩: of two requests racing for the last seat, one succeeds and the other is refused.
  - ق-أ-٤٩: an expired invitation.
  - "من المشغّل": an operator's add or re-enable is checked against the same limit.
  - §2-4: a plan limit is not a permission.
- Clinic volume, module 01 (`01-shell-signin-home.md`), the not-in-plan state and its rule:
  - A switch feature not included keeps its place in the side bar.
  - Opening it shows «غير مشمولة في باقتك» because of the plan, with a link to «الملف والاشتراك»
    (أ-٥-٩).
  - CL-SH-063: every page has the state.
- The ruling repeated in the chapters (الحكم ١٠; 03 §10, 06 §10-2, 07, 12): the page keeps its
  header; the card names the plan as the reason, not a permission; it links to «الملف
  والاشتراك»; the place is not hidden.
- Three reasons are never mixed in the words: a missing permission hides the control; the
  subscription's state stops everyone; a plan limit shows the control and says what it costs
  (03 §10).

**Design source**: Figma file `4HLWEqlna9sbn68276LHYt` (the clinic console), read on 17 Oct 2026:

- **«Inbox / Not In Plan / RTL·LTR»** (page Inbox `4160:8`, frames `6701:1002017`, `6701:1002375`):
  - The page header stays: the title «الصندوق الوارد», its line and its «مقترحة» chip.
  - Under it, one white card, centred, holding:
    - a lock icon in a light circle;
    - the title «الباقة الحالية لا تشمل قنوات المحادثة»;
    - the line «قنوات المحادثة ليست في باقة العيادة الحالية. هذا حدّ الباقة لا نقصٌ في صلاحياتك،
      والباقة تُغيَّر من «الملف والاشتراك».»;
    - the link «فتح الملف والاشتراك».
  - English: "The current plan does not include messaging channels" / "Messaging channels are not
    in the clinic's current plan. This is a limit of the plan, not a missing permission, and the
    plan is changed in Profile & subscription." / "Open Profile & subscription".
- **«Financial / Not In Plan / RTL·LTR»** (page Reports: Financial `5450:2`, frames `6614:945146`,
  `6614:945529`): the same card under «التقارير المالية».
  - The Arabic frame says «الخطة» where every other frame says «الباقة»: a designer item.
  - English: "The current plan does not include reports" / "Financial and clinical reports are not
    in the clinic's current plan. …".
- **Drawn but owned by later specs** (read so this spec's words and parts match them; not built
  here):
  - «Settings / Channel Not In Plan» and «Settings / Teleconsult Not in Plan» (Settings
    `4160:12`), for specs 061 and 024.
  - «Inbox / No Assistant In Plan» (Inbox), spec 061.
  - «Users / Seats Full», «Users / Seats Over», «Users / Add Seats Full» (Users `4160:7`): the
    seats card and the «لا مقعد شاغر» banner, spec 026.
  - «Profile / Usage & Limits» (Profile & subscription `4160:10`, `6794:1063764`): «الاستخدام
    والسقوف», «سقوف هذا الشهر», «تتجدّد سقوف الباقة في ١ سبتمبر ٢٠٢٦…», spec 078.
  - «Shell / Setup Card Not In Plan» (Home `4160:3`), the clinic's home.

## Overview

Specs 012 and 013 let operators describe what a plan sells. Nothing yet acts on it: no clinic has
a plan, no server route asks whether a clinic may do something, nothing is counted, and the
clinic console's "not in your plan" state is a stand-in that always says "included".

This spec builds the one place every later spec asks. That place answers three questions:

- what does this clinic have;
- may it do this;
- how much of this limit is left.

It also builds:

- the link from a clinic to its plan, which stays empty until spec 015 creates clinics with one;
- the server's two refusals, each with one message: a feature not included, and a limit reached;
- the monthly meter for the four monthly limits;
- the users limit, applied today at every way an account is added, re-enabled or re-invited;
- the clinic console's not-in-plan state, reading the real plan.

The other seven limits have nothing to count yet. Each is plugged in by the spec that builds what
it counts: doctors (029), branches (028), message credits (061), waiting screens, teleconsult
minutes, x-ray analyses and voice-dictation minutes. Those specs use the same point and the same
refusal, and add nothing of their own.

## Clarifications

### Session 2026-10-17

- Q: No clinic has a plan until 015. Where does 014 get a clinic's plan from? → A: From a link
  that 014 adds, empty until 015. A clinic with no plan has the catalogue's baseline only. 015
  fills the link when it creates a clinic; until then, tests and the local development clinics
  set it directly.
- Q: Of the eight limits only users can be counted today. What does 014 wire? → A: The engine and
  users now. 014 builds the one enforcement point and the monthly meter for all eight. It applies
  the users limit today wherever an account is created, re-enabled or re-invited, from the clinic
  and from the operators. Each later spec plugs its own count into the same point.
- Q: What does the clinic console get in 014? → A: The real not-in-plan state. The shell's state
  reads the clinic's real plan, and its card follows the drawn frames. The usage screens stay with
  078 (the clinic) and 015 (the operators' clinic detail).
- Q: What does a person who does not hold «عرض الاشتراك» (subscription.view) see on the
  not-in-plan card, whose link opens the subscription? → A: No link, a line instead (SH-055). The
  card's line ends «… والباقة تُغيَّر من «الملف والاشتراك»، ويُطلب ذلك من مالك العيادة.» — in the
  passive, since the console's words never address one gender (no «اطلب»). And the card says
  «الباقة», never «الخطة» (the glossary lists «الخطة» as a word to avoid), so the title reads
  «الباقة الحاليّة لا تشمل …» with the link and without it.
- Q: The BRD gives the limit refusal a code but no words; which words does the server send? →
  A: Names the limit and the way out: «بلغت العيادة حدّ باقتها من «{الميزة}»: {الحدّ}. تُرفع الباقة
  من «الملف والاشتراك».» / "The clinic has reached its plan's limit of {feature}: {limit}. The plan
  is raised in Profile & subscription." (/speckit-analyze, 17 Oct: the name is quoted, «من
  «المستخدمون»», so a catalogue name never breaks the case after «من».)
- Q: How fast must the check "what does this clinic have" be? → A: Within 50 ms for 95% of
  requests on the local stack, a count included where one applies (SC-008).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The platform knows what a clinic has (Priority: P1)

Every check starts from the same answer to "what does this clinic have". The answer covers:

- each feature: included or not;
- each limit: its number, or no limit;
- for the four monthly limits: this month's allowance.

It is worked out from the clinic's plan, the feature catalogue and the clinic's specialty. It is
worked out the same way wherever it is asked.

**Why this priority**: Every refusal, count and console state in this spec, and every later
spec's check, stands on this answer. If two places worked it out differently, a clinic could be
refused on one screen and allowed on the next.

**Independent Test**: Give a clinic a plan, change the plan and the catalogue, and check that the
answer follows each rule. The rules are: no plan; an included limit with no number; a limit left
out; an unknown feature; a feature of another specialty; an upcoming feature; a disabled plan; a
retired feature.

**Acceptance Scenarios**:

1. **Given** a clinic with no plan, **When** what it has is asked:
   - every switch is not included, except the core («الحجوزات والمرضى والكشف والروشتة»), which
     every clinic has;
   - every limit is its feature's baseline: users with no limit, doctors 1, branches 1, waiting
     screens 0, the four monthly allowances 0 (ق-٣٨).
2. **Given** a plan that includes «المستخدمون» with no number, **When** a clinic on it is asked
   for its users limit, **Then** it has no limit (ق-٣٨).
3. **Given** a plan that leaves out «الأطباء», **When** a clinic on it is asked, **Then** its
   doctors limit is the baseline, 1 (ق-٣٨).
4. **Given** a key the catalogue does not know, **When** it is asked:
   - as a switch, it is not included;
   - as a limit, its limit is 0 (ق-٣٩).
5. **Given** a plan that includes «خريطة الأسنان» (dental only), **When** a dermatology clinic on
   that plan is asked, **Then** it is not included for that clinic; a dental clinic on the same
   plan has it (ق-٣٧).
6. **Given** a feature still «قادمة» (not built), included or not, **When** a clinic is asked
   whether it may use it, **Then** nothing refuses it: an upcoming feature is not enforced
   (ق-٤٩).
7. **Given** a clinic on a plan that an operator has since disabled, **When** it is asked,
   **Then** it still has that plan's features and limits (ق-٤٧).
8. **Given** a feature retired in the catalogue after a clinic's plan included it, **When** the
   clinic is asked, **Then** it still has it, with its limit (ق-٤٨).
9. **Given** an operator saves a change to a plan, **When** a clinic on it is asked next, **Then**
   the answer follows the change; no clinic needs a new session to see it.

---

### User Story 2 - A feature the plan does not include is refused by the server (Priority: P1)

Any server action that belongs to a feature names that feature. When the clinic's plan does not
include it, the action is refused with the one message «غير مشمولة في باقتك». It is refused
whichever screen or tool sends it.

**Why this priority**: This is what makes a plan mean something (SH-014). A hidden button is not
enough: the server must refuse.

**Independent Test**: Mark an action as belonging to a built feature. Call it for a clinic whose
plan includes the feature and for one whose plan does not, and compare the answers.

**Acceptance Scenarios**:

1. **Given** an action that belongs to a built feature and a clinic whose plan does not include
   it, **When** the action is sent:
   - it is refused with `PLAN_FEATURE_NOT_INCLUDED`, naming the feature;
   - the message is «غير مشمولة في باقتك» / "Not included in your plan";
   - nothing is changed.
2. **Given** the same action and a clinic whose plan includes the feature, **When** it is sent,
   **Then** it goes on to its usual checks.
3. **Given** a person who lacks the action's permission, at a clinic whose plan does not include
   it, **When** the action is sent, **Then** the permission refusal is the one given. A person who
   may not do something is not told about the plan.
4. **Given** the action belongs to a feature that is still «قادمة», **When** it is sent by any
   clinic, **Then** the plan does not refuse it (ق-٤٩).
5. **Given** an action marked with a feature name the catalogue does not know, **When** the
   platform starts or its route checks run, **Then** the mistake is caught before release. An
   unknown feature can never quietly let everyone through.

---

### User Story 3 - A clinic cannot add a user past its plan's limit (Priority: P1)

A clinic's plan says how many users it may have. Adding an account is refused once the clinic is
at that number. So are re-enabling a disabled account and re-sending an expired invitation. The
refusal names the limit, and it applies whether the clinic or an operator asks. «المستخدمون» is
marked built.

**Why this priority**: The users limit is the one counted limit with something to count today
(staff accounts, from 003 and 004). It proves the whole path end to end, and every users screen
(026) and every operator's users action (015, 017) inherits it.

**Independent Test**: Give a clinic a plan with a users limit of 3, then:

- fill it;
- try each way of adding and re-enabling;
- let an invitation expire;
- send two requests for the last seat at the same moment.

**Acceptance Scenarios**:

1. **Given** a plan with a users limit of 3 and a clinic with 3 counted accounts, **When** a new
   account is created by any of the three handover ways (an emailed link, a generated password, a
   typed password):
   - it is refused with `PLAN_LIMIT_REACHED`, naming «المستخدمون» and the limit 3;
   - no account is created and no email is sent.
2. **Given** the same clinic, **When** a disabled account is re-enabled, **Then** it is refused the
   same way, and the account stays disabled.
3. **Given** the clinic at its limit and an invited account whose invitation has expired, **When**
   the invitation is re-sent, **Then** it is refused the same way (ق-١٠٣).
4. **Given** the clinic at its limit, **When** an account is disabled, **Then** one seat is free
   and the next add succeeds.
5. **Given** an invited account whose invitation has not expired, **When** the seats are counted,
   **Then** it takes a seat. Once its invitation has expired, it does not.
6. **Given** one free seat and two add requests arriving together, **When** both are handled,
   **Then** one succeeds and the other is refused with `PLAN_LIMIT_REACHED` (ق-أ-١٩).
7. **Given** a clinic with 5 counted accounts, **When** its plan's limit is lowered to 3:
   - all five stay as they are;
   - the next add is refused (ق-٤١).
8. **Given** an operator adds or re-enables a clinic's account, **When** the clinic is at its
   limit, **Then** it is refused the same way (SH-017).
9. **Given** a clinic whose plan has no users limit, or a clinic with no plan (the baseline is no
   limit), **When** accounts are added, **Then** none is refused for the plan.

---

### User Story 4 - The four monthly limits are metered and run down with use (Priority: P2)

Each clinic has this month's allowance of the four monthly limits:

- «رصيد الرسائل»;
- «دقائق الكشف عن بُعد»;
- «تحليلات الأشعة»;
- «دقائق الكتابة الصوتيّة».

Each use takes from the allowance, and a use the allowance cannot cover is refused. Each month
starts again from the plan's number. What a clinic used in earlier months is kept.

**Why this priority**: SH-018 asks for it, and specs 021 (extra units), 061 (messages), 078 (the
usage screen) and the teleconsult, x-ray and dictation specs stand on it. No feature that uses it
is built yet, so it is proven through its own operations here.

**Independent Test**: For a clinic on a plan with 1,000 message credits:

- use 400;
- try to use 700;
- use what is promised beyond the limit;
- move the clock past midnight on the 1st, Cairo time;
- read the months.

**Acceptance Scenarios**:

1. **Given** a plan with «رصيد الرسائل» 1,000 and a clinic that has used none this month, **When**
   400 are used, **Then** 600 are left and the use is recorded with its time and what it was for.
2. **Given** 600 left, **When** a use of 700 is asked, **Then**:
   - it is refused with `PLAN_LIMIT_REACHED`, naming «رصيد الرسائل» and this month's allowance;
   - nothing is taken.
3. **Given** a use that must complete whatever is left, because it was already promised (a
   teleconsult call already booked runs past the minutes), **When** it is recorded, **Then** it is
   taken even beyond the allowance, and what is left reads 0 with the overrun recorded. Only a use
   that says so may go over.
4. **Given** the clock passes 00:00 on the 1st of a month, Cairo time, **When** the allowance is
   read, **Then**:
   - it starts again from the plan's number;
   - last month's use is kept as it was, for reading.
5. **Given** a use is cancelled before it was delivered (a message that never went out), **When**
   it is given back, **Then** what is left rises by that amount in the month it was taken.
6. **Given** the clinic's plan changes in the middle of a month, **When** the allowance is read,
   **Then** it is the new plan's number, and what was used this month still counts.
7. **Given** a clinic with no plan, **When** a monthly use is asked, **Then** it is refused: the
   baseline is 0.

---

### User Story 5 - The clinic console shows a page the plan does not include as such (Priority: P2)

When a person opens a page that belongs to a built feature the clinic's plan does not include,
the page keeps its header. Under the header it shows the drawn card: a lock, «الباقة الحاليّة لا
تشمل {the feature}», the line saying this is a limit of the plan and not a missing permission, and
the link «فتح الملف والاشتراك» for whoever may open the subscription. The page stays in the side
bar.

**Why this priority**: The server already refuses. This is what keeps a person from being
confused by a page that does nothing. It replaces 006's stand-in, which always said "included".

**Independent Test**: Give a clinic a plan without a built feature that has a page. Open that page
as someone who may view it, then as someone who may not. Then give the clinic a plan that includes
it and open it again.

**Acceptance Scenarios**:

1. **Given** a clinic whose plan does not include a built feature with a page, and a person who
   holds that page's view permission and «عرض الاشتراك», **When** they open it:
   - its header shows as usual;
   - under it is the card as drawn («Inbox / Not In Plan», «Financial / Not In Plan»):
     - the lock;
     - «الباقة الحاليّة لا تشمل {الميزة}»;
     - «{الميزة} ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر
       من «الملف والاشتراك».»;
     - «فتح الملف والاشتراك», which opens «الملف والاشتراك»;
   - the same in English.
2. **Given** the same page and a person who holds its view permission but not «عرض الاشتراك»,
   **When** they open it, **Then** the card has no link, and its line reads «{الميزة} ليست في باقة
   العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر من «الملف والاشتراك»،
   ويُطلب ذلك من مالك العيادة.» / "… and the plan is changed in Profile & subscription by the
   clinic's owner."
3. **Given** the same page, **When** the side bar is drawn, **Then** the page is still listed (the
   place is not hidden for the plan).
4. **Given** a person who does not hold the page's view permission, **When** they open it,
   **Then** the permission's answer is the one shown, never the plan's.
5. **Given** the clinic's plan comes to include the feature, **When** the page is opened next,
   **Then** it shows its content. No sign-in again is needed; at most the page is reloaded.
6. **Given** a page that belongs to a feature still «قادمة», **When** it is opened, **Then** it is
   never shown as not in the plan.
7. **Given** a person who holds the page's view permission and nothing that changes it (view
   only), **When** they open a page not in the plan, **Then** they see the same card as anyone who
   may view it.
8. **Given** the clinic console asks for the clinic's plan, **When** it is answered, **Then** the
   answer holds what the console needs to draw the not-in-plan state and nothing about another
   clinic (FR-025).

---

### User Story 6 - One answer for the eight limits (Priority: P3)

One read gives every place that shows the eight limits the same information. That information is
each limit's name in both languages, its unit, its number (or no limit), and what is used of it.
For the four monthly limits it is this month's allowance, what is used, and when it renews.

**Why this priority**: SH-016 asks for the same names and numbers in three places. Those places
are later specs: the clinic's settings (024), «الاستخدام» in the profile and subscription (078),
and the clinic's detail in the operators console (015). This spec gives them one source so they
cannot drift.

**Independent Test**: Read a clinic's eight limits after setting its plan and its use, and compare
with the counts and the meter.

**Acceptance Scenarios**:

1. **Given** a clinic on a plan with users 10 and 4 counted accounts, **When** its limits are read,
   **Then** «المستخدمون» reads 4 of 10.
2. **Given** a monthly limit with 400 of 1,000 used, **When** it is read, **Then**:
   - it reads 400 of 1,000;
   - it gives the date the allowance renews, the 1st of next month.
3. **Given** a limit nothing counts yet (doctors before spec 029), **When** it is read, **Then**:
   - it gives its number;
   - its use is not given. It is never shown as 0, because nothing has counted it.

---

### Edge Cases

- **No plan at all** (every clinic until 015): the baseline only.
  - No built switch is included except the core.
  - Users have no limit; doctors 1; branches 1; waiting screens 0; the monthly allowances 0.
  - Today every feature but «المستخدمون» is «قادمة», so no page shows as not in the plan.
- **A plan whose included limit has no number**: no limit; the meter records use and never refuses.
- **A limit made a switch in the catalogue** (013's edge case): read as included with no limit
  until the plan is saved again. **A switch made a limit**: read as no limit.
- **A clinic's specialty is not set yet** (until 015 sets it): a feature that follows a specialty
  is not included.
- **Two operators save the same plan while a clinic is mid-request**: the request is decided by
  the plan as it was read at that moment. The next request reads the saved plan.
- **A clinic's plan is lowered below its use** (ق-٤١):
  - nothing is removed or disabled;
  - the next add of that kind is refused;
  - a monthly allowance already overrun reads 0 left.
- **Midnight on the 1st falls during a use**: the use counts in the month its time falls in,
  Cairo time.
- **A clinic is suspended, cancelled or read-only**: those states are spec 016's. This spec
  neither refuses nor allows for them.
- **The operators console**: it has no plan. Operators' own actions are never refused for a plan.
  Only actions on a clinic's countable records are checked against that clinic's plan.
- **An expired invitation re-sent while a seat is free**: it is re-sent, and the account takes the
  seat again.
- **An account that activates its invitation**: it already held its seat, so nothing is checked
  again.

## Requirements *(mandatory)*

### Functional Requirements

**What a clinic has**

- **FR-001**: The platform MUST keep each clinic's link to one plan. The link may be empty. It MUST
  be the only place a clinic's plan is read from. Spec 015 sets it when it creates a clinic, and
  later specs change it (020).
- **FR-002**: The platform MUST give one answer to "what does this clinic have", worked out from
  the clinic's plan link, the feature catalogue and the clinic's specialty. Every check in this and
  later specs MUST ask it rather than work it out again (ق-٣٦).
- **FR-003**: In that answer:
  - a switch MUST be included only if the plan includes it, it is built, and the clinic's
    specialty does not exclude it (ق-٣٧);
  - the core (`core_clinic`, not sold in plans) MUST always be included.
- **FR-004**: In that answer, a limit MUST be:
  - the plan's number when the plan includes it with one;
  - no limit when the plan includes it without one;
  - the feature's baseline when the plan leaves it out or the clinic has no plan (ق-٣٨);
  - 0 for a key the catalogue does not know (ق-٣٩).
- **FR-005**: A feature still «قادمة» MUST never be refused and never shown as not in the plan
  (ق-٤٩). A clinic's plan MUST keep meaning what it says when the feature is later marked built.
- **FR-006**: A disabled plan and a retired feature MUST keep applying to the clinics that have
  them (ق-٤٧, ق-٤٨).
- **FR-007**: A saved change to a plan or to the catalogue MUST apply to the next check of every
  clinic on it, without the clinic signing in again.

**Refusing a feature**

- **FR-008**: Each clinic server action that belongs to a feature MUST name that feature. When the
  clinic's plan does not include it, the action MUST be refused with `PLAN_FEATURE_NOT_INCLUDED`,
  naming the feature. The message MUST be «غير مشمولة في باقتك» / "Not included in your plan", and
  nothing MUST change (SH-014).
- **FR-009**: The permission check MUST be decided before the plan check: a person refused for
  permission is never told about the plan.
- **FR-010**: Naming a feature the catalogue does not know on an action MUST be caught by the
  platform's route checks before release.

**Refusing at a limit**

- **FR-011**: Anything that raises a counted limit MUST be refused, before it is created, when the
  current count is already at or above the clinic's limit. The refusal is `PLAN_LIMIT_REACHED`,
  naming the feature and the limit, and nothing MUST change (SH-017, ق-٤٠).
  - The message (owner, 17 Oct 2026): «بلغت العيادة حدّ باقتها من «{الميزة}»: {الحدّ}. تُرفع الباقة
    من «الملف والاشتراك».» / "The clinic has reached its plan's limit of {feature}: {limit}. The plan
    is raised in Profile & subscription." It names the limit and the way out (SH-014), and addresses
    no one.
  - Each screen may add its own drawn words around it (026's «لا مقعد شاغر في باقتك»).
- **FR-012**: A limit MUST never be applied backwards (ق-٤١):
  - what exceeded it before stays as it is;
  - only the next raise is refused.
- **FR-013**: Two requests competing for the last unit MUST never both succeed (ق-أ-١٩).
- **FR-014**: Each counted limit MUST be counted by the spec that owns its records, through one
  shared way of counting and checking. The refusal, its code and its message MUST be the same for
  all eight. This spec plugs in «المستخدمون». Doctors (029), branches (028), waiting screens and
  the rest plug in when built.

**The users limit**

- **FR-015**: «المستخدمون» MUST count a clinic's accounts that are active, plus invited accounts
  whose invitation has not expired. Disabled accounts and invited accounts whose invitation has
  expired are not counted (ق-٠٦, ق-١٠٣).
- **FR-016**: These MUST be checked against the users limit, and refused at it:
  - creating an account by any handover way;
  - re-enabling a disabled account;
  - re-sending an expired invitation;
  - re-issuing access to an account that is not counted.
  The same check MUST apply whether the clinic or an operator asks (SH-017).
- **FR-017**: Activating an invitation, changing an account's role, and actions on an account
  already counted MUST NOT be checked against the limit.
- **FR-018**: «المستخدمون» MUST be marked built in the feature catalogue by this spec.

**The monthly meter**

- **FR-019**: For each of the four monthly limits, the platform MUST keep:
  - each clinic's use, one entry per use or give-back: the amount, the time, what it was for, and
    who or what took it;
  - this month's allowance, worked out from the clinic's plan at the moment of reading (FR-004).
  Spec 021 adds extra units on top.
- **FR-020**: A use MUST be refused with `PLAN_LIMIT_REACHED`, naming the feature and this month's
  allowance, when what is left cannot cover it, and nothing MUST be taken (SH-018).
  - The exception: a use that says it completes what was already promised MUST be taken even
    beyond the allowance, and the overrun recorded.
- **FR-021**: A use cancelled before delivery MUST be able to be given back, in the month it was
  taken.
- **FR-022**: A month MUST run from 00:00 on its 1st to the end of its last day, Cairo time
  (SH-001):
  - each month starts again from the allowance;
  - nothing carries over;
  - past months' use stays readable.
- **FR-023**: Two uses arriving together MUST never take more than is left, except a use that
  completes a promise.

**Reading the limits**

- **FR-024**: The platform MUST give one read of a clinic's eight limits. For each limit it gives:
  - its key;
  - its name and unit in both languages;
  - its number or no limit;
  - its use, given only where the limit has a count. A limit with nothing counting it yet says so
    and is never shown as 0;
  - for the four monthly limits: this month's allowance, the use, and the renewal date.
  It is the one source for the three places SH-016 names.
- **FR-025**: The clinic console MUST be able to ask for its own clinic's plan, and only its own:
  - which built features its plan does not include, and their names;
  - the plan's name in both languages;
  - nothing else about plans or other clinics.

**The clinic console**

- **FR-026**: Each page of the clinic console that belongs to a feature MUST name it. The console
  MUST treat a page as not in the plan only when the clinic's answer says the feature is built and
  not included. This replaces 006's stand-in, which always said "included".
- **FR-027**: A page not in the plan MUST keep its header and show, under it, the drawn card:
  - the lock;
  - «الباقة الحاليّة لا تشمل {الميزة}»;
  - «{الميزة} ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر
    من «الملف والاشتراك».»;
  - «فتح الملف والاشتراك», opening «الملف والاشتراك», only for a person who holds «عرض
    الاشتراك». For anyone else the link is removed (SH-055) and the line ends instead «… والباقة
    تُغيَّر من «الملف والاشتراك»، ويُطلب ذلك من مالك العيادة.» / "… and the plan is changed in
    Profile & subscription by the clinic's owner.";
  - the English twins as drawn.
  {الميزة} is the page's own drawn words where its frame draws them («قنوات المحادثة», «التقارير»).
  Otherwise it is the feature's name from the catalogue. The card always says «الباقة», never
  «الخطة», and its words never address one gender.
- **FR-028**: A page not in the plan MUST stay in the side bar. A person without its view
  permission MUST get the permission's answer, never the plan's (006).
- **FR-029**: The console's plan answer MUST follow the clinic's plan without the person signing
  in again: at most a page reload or the next opening of a page.

**Trail and development**

- **FR-030**: Setting or changing a clinic's plan link MUST be written to the clinic's audit trail
  and the platform's, naming both plans. The screens that set it are 015 and 020.
- **FR-031**: In local development only, a way MUST exist to set a seeded clinic's plan, so the
  console's states and the users limit can be walked. It MUST NOT exist outside local development.

### Key Entities

- **Clinic's plan link**: which plan a clinic is on. At most one per clinic, possibly none. It is
  set by 015, changed by 020, and read by everything here.
- **What a clinic has** (worked out, not stored): for each feature, whether it is included, and
  for each limit, its number or no limit. It comes from the plan, the catalogue and the specialty.
- **Counted limit**: one of the eight. It has a counter owned by the spec that owns its records
  (users here). It is either standing (users, doctors, branches, waiting screens: counted from the
  records as they are) or monthly (the four: counted from the meter).
- **Meter entry**: one use or give-back of a monthly limit by a clinic: the amount, the time, what
  it was for, who or what took it, and whether it completed a promise beyond the allowance.
- **Refusal**: `PLAN_FEATURE_NOT_INCLUDED` (with the feature) or `PLAN_LIMIT_REACHED` (with the
  feature and the limit). These are the only two plan refusals.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every clinic action that names a feature is refused for a clinic whose plan does not
  include that built feature, in 100% of the platform's route checks, and passes for a clinic
  whose plan includes it.
- **SC-002**: Of 20 add requests sent together for a clinic with one free seat, exactly 1
  succeeds and 19 are refused with the limit named. The same holds for 20 monthly uses that only
  one can be covered for.
- **SC-003**: A clinic never holds more counted users than its limit through any way of adding,
  re-enabling or re-inviting, from the clinic or from an operator. The only exception is accounts
  that were already there before the limit was lowered.
- **SC-004**: The answer to "what does this clinic have" agrees with the BRD's rules (ق-٣٧ … ق-٤١,
  ق-٤٧ … ق-٤٩) in every one of the acceptance scenarios of User Story 1.
- **SC-005**: A plan saved by an operator applies to the very next check of every clinic on it.
  The clinic console shows the change by the next page opened, without signing in again.
- **SC-006**: A monthly allowance restarts at 00:00 Cairo time on the 1st. The previous month's
  use reads the same after the restart as before it.
- **SC-007**: A page not in the plan is shown with its header and the drawn card in both
  languages, at 1440 and at 1024 wide — the clinic frame's narrowest; below it the frame scrolls
  sideways by design (spec 006, FR-043) — with nothing cut or overlapping. Beside its frames, it shows no
  difference that is not recorded.
- **SC-008**: Checking a clinic's plan adds no wait a person can notice: the check (the plan, the
  catalogue, the specialty, and a count where one applies) answers within 50 ms for 95% of requests
  on the local stack.

## Assumptions

- Spec 015 creates clinics with their plan and specialty and sets the link through this spec's
  operation. Until then every clinic has no plan and no specialty, and works from the baseline.
- Spec 016 owns the subscription's states (trial, active, awaiting payment, read-only, suspended,
  cancelled). This spec does not refuse or allow for them.
- Spec 021 adds extra units on top of a monthly allowance. Spec 020 changes a clinic's plan. Spec
  026 builds the users screens and their «لا مقعد شاغر» words. Spec 078 builds the usage screen.
  All of them read this spec's answer and add no rule of their own.
- Spec 061 and the teleconsult, x-ray and dictation specs decide what one use of their limit is
  and round it (for instance, dictation minutes rounded up on the first accepted field, CL-PT-152).
  They take from the meter through this spec.
- "Counted" means what each owning spec says. For users, this spec follows the BRD (FR-015).
- The core (`core_clinic`) and the specialty exam modules are not sold in plans (012). The core is
  always included. The specialty exam modules follow the clinic's specialty, which is spec 015's
  and later specs'.
- The pages of the clinic console that belong to features, and which feature each belongs to, are
  listed in the plan from the BRD's per-chapter tables. Each is enforced only once its feature is
  built.
- The Arabic «الخطة» in «Financial / Not In Plan» is taken as «الباقة», like every other frame and
  the BRD. It is raised with the designer.
