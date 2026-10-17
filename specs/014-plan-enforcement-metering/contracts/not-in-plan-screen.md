# Contract: the clinic console's not-in-plan state

Frames (clinic file `4HLWEqlna9sbn68276LHYt`):

- «Inbox / Not In Plan» RTL `6701:1002017`, LTR `6701:1002375`.
- «Financial / Not In Plan» RTL `6614:945146`, LTR `6614:945529`.

## 1. When it shows

A destination shows the not-in-plan card when all of these hold:

1. it names a feature (research R13);
2. the person holds its view permission, because the role's answer comes first (006);
3. every one of its keys is in `session.plan.withheld`, meaning built and not included.

A destination whose feature is upcoming never shows it. Before deciding, the guard re-reads the
session on every opening of a destination that names a feature. A plan change is
therefore seen by the next page opened (FR-029).

## 2. The page

| Part | Content | Test id |
|---|---|---|
| Page header | The destination's own title (and parent, as today), unchanged | (header's own) |
| Card | One white card, centred empty-state layout, `context="not-in-plan"` | `page-not-in-plan` |
| Icon | The lock in a light circle, as the empty-state part draws it | — |
| Title | «الباقة الحاليّة لا تشمل {title noun}» / "The current plan does not include {title noun}" | `empty-title` (the empty-state part's own) |
| Line (holder of «عرض الاشتراك»), test id `empty-text` | «{subject} ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر من «الملف والاشتراك».» / "{Subject} are not in the clinic's current plan. This is a limit of the plan, not a missing permission, and the plan is changed in Profile & subscription." | `not-in-plan-text` |
| Line (anyone else) | «{subject} ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر من «الملف والاشتراك»، ويُطلب ذلك من مالك العيادة.» / "… and the plan is changed in Profile & subscription by the clinic's owner." | `not-in-plan-text` |
| Link | «فتح الملف والاشتراك» / "Open Profile & subscription" → `/account/subscription`. Rendered only for a holder of `subscription.view` (SH-055) | `plan-link` |

## 3. The words per destination

| Destination | Title noun (ar / en) | Subject (ar / en) | Source |
|---|---|---|---|
| `inbox` | «قنوات المحادثة» / "messaging channels" | «قنوات المحادثة» / "Messaging channels" | frame `6701:1002017` |
| `reports.financial`, `reports.clinical` | «التقارير» / "reports" | «التقارير المالية والطبية» / "Financial and clinical reports" | frame `6614:945146` («الخطة» read as «الباقة», owner 17 Oct) |
| any other feature destination | the feature's catalogue name | the feature's catalogue name | `session.plan.names` (undrawn; listed for the designer) |

- **Keys**: `app.plan.notIn.title`, `app.plan.notIn.text`, `app.plan.notIn.textNoLink`,
  `app.plan.notIn.link`, and `app.plan.places.<destination>.title|subject`. All are in
  `clinic/public/i18n/{ar,en}.json`.
- **Words**: «الباقة», never «الخطة»; no gendered address (constitution VI).
- The old `app.shell.notInPlanText` is removed once nothing reads it.

## 4. States and sizes

- **Directions**: Arabic right to left, English left to right.
- **Widths**: 1440 and 1024 (the clinic frame's narrowest, spec 006 FR-043), with nothing cut or overlapping (the shell's layout checks of `shell-widths.spec.ts`).
- **No loading state of its own.** The card is decided from the session copy the shell already
  holds. The re-read happens before the route matches, and the route's own loading is the shell's.
- **Offline**: the card shows from the last session copy; the link still works.

## 5. Tests

- **Unit**:
  - the destination → feature table (R13);
  - `PlanSource.excludes` for no keys, one withheld key, and any-of with one of two withheld;
  - the card's words with and without `subscription.view`.
- **Browser** (`not-in-plan.spec.ts`, the platform intercepted through `staff-platform.ts`, which
  gains `plan` in its session answer). Each of these is covered
  in both languages at 1440 and 390:
  - the inbox and financial cards, with a picture beside each frame;
  - the card without the link;
  - the role's answer winning over the plan;
  - an upcoming feature never shown;
  - a plan change seen on the next page opened.

- **006's browser tests** (`placeholder.spec.ts`, `shell-banners.spec.ts`, `shell-states.spec.ts`)
  drive not-in-plan through the development sample sources today. They move to the session's
  `plan.withheld`. The sample sources page keeps its own override for the development gallery.
