# Quickstart: checking clinic onboarding

**Feature**: [spec.md](spec.md) · **API**: [contracts/ops-clinics-api.md](contracts/ops-clinics-api.md) · **Screens**: [contracts/clinics-screens.md](contracts/clinics-screens.md)

## Prerequisites

- The local stack of `docs/setup.md`: SQL Server, Mailpit, the API, and the ops console on 4203.
- The migrations applied, through `tools/Scs.Migrator`: `ClinicProfile`, `OwnerDetails`, `Trial`
  and `Branches`.
- On this machine: `TEMP` and `TMP` point at `src/web/node_modules/.tmp`, and
  `PLAYWRIGHT_BROWSERS_PATH=0`.
- Walk accounts only:
  - one operator holding «تعديل بيانات العيادات» and one holding «عرض العيادات» alone, both
    invited with the operators' invite command;
  - the seeded operator is never used or reset.
- At least two active plans, made through the plans screens (013); one of them may have
  `max_users` 0 for the refusal.

## Automated suites (all must pass)

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

The ones written first and seen to fail before the code (constitution VII):

- **Permissions**: each `/v1/ops/clinics` route without its permission.
- **Isolation**:
  - `BranchSample`;
  - the guardrail;
  - an operator route for A writing nothing in B;
  - the crossings count.
- **Plan limits**: an add on a plan whose users limit is 0.

## Walk (real stack, both languages)

| # | Step | Expected |
|---|---|---|
| 1 | Sign in as the editing walk operator. Open «العيادات». | The section shows; the cards read the real counts; empty state if no clinic was added yet. |
| 2 | «إضافة عيادة +». Fill both names, contact, email, a mobile from another country (`+9665…`), address, a plan, `123456789`, a specialty. «إضافة العيادة». | «تم إنشاء العيادة!»; the detail opens with «تجربة», «التجديد القادم» 14 days on, «0 أطباء · 1 فرع», «دعوة …» live. |
| 3 | Open Mailpit. | One invitation in Arabic, naming `app.scs-heta.com`, with a 72-hour link. |
| 4 | Add a second clinic with the same English name. | Its slug ends `-2`. |
| 5 | Add one with the first clinic's owner email. | «هذا البريد مسجَّل لحساب آخر على المنصة.» under the email. Nothing new in the list. |
| 6 | Disable the chosen plan in another tab, then save an add. | «تعذر إنشاء العيادة.»; nothing new. |
| 7 | On the detail: «نسخ عنوان دخول العيادة». «إعادة إرسال الدعوة» twice within a minute. | The copy toast. The first resend gets the new-expiry toast and the second is refused with the minute's words. Mailpit has one more email. |
| 8 | «تعيين» → choose an operator → «حفظ». Then «غير معيّن». | The toast, then the name, then «غير معيّن». Both are in the platform trail as the walk operator. |
| 9 | «تعديل العيادة»: change the address, clear the tax number, change the language, add a note, «حفظ التغييرات». | «تم تحديث بيانات العيادة.». The detail shows the change. The trail has `clinic.updated` with before and after and the note. |
| 10 | Accept the invitation from Mailpit (set a password in a separate browser). Back in ops, edit the owner's email. | The old session is ended. Mailpit has the notice at the old address. The new email signs in; the old one does not. |
| 11 | «الإعدادات». | The strip in its editor's form, the card, «تعديل العيادة ←». |
| 12 | Sign in as the viewing walk operator. | No «إضافة عيادة +», no «⋯», no «تعديل العيادة», no «تعيين», no resend. The viewer's strip. `/clinics/new` shows no access. |
| 13 | Open the plans list (013). | The plan's clinic count and «على التجربة» match; its revenue reads 0. |
| 14 | Open `/clinics/<random id>`. | The error state, the same as a failed load. |

## Design check

Lay each screen and state of `contracts/clinics-screens.md` beside its frame, AR and EN, at 1440
and at the phone width:

- the list (normal, loading, empty, error, filter open);
- add and edit (normal, invalid tax number);
- the detail (normal, loading, error, view only, the three invitation lines);
- the settings tab (both forms).

Fix the differences, or record them. Then write `validation.md` and the one `design-vs-real.png`
for this spec.

## What is checked only by tests

- The undo after an injected fault (research R2): the tests inject a failure after each write
  stage.
- Ten adds at once with one name, and two with one email (SC-002).
- The list's timing over a few hundred clinics (SC-007), seeded by the test.
