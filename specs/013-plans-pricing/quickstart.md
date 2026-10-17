# Quickstart: Plans and Their Prices

**Feature**: 013-plans-pricing

How to run it, look at it and prove it. Details are in `contracts/` and `data-model.md`.

## Prerequisites

The local stack of `docs/setup.md`: the compose services, the API (5080), the operators console
(`ops`, 4203). The migrations of this spec have run (the API's start does it locally):
`plans.Plan` and `plans.PlanFeature` exist and are empty; `plans.Feature` holds 012's 35 rows.

Walk accounts only — never the seeded operator. The walk super admin and the walk viewer (a
custom role with «عرض الباقات والفواتير» only) of spec 012's quickstart, or new ones invited the
same way:

```bash
dotnet run --project src/server/tools/Scs.Migrator -c Release --no-build -- operator invite --name "Walk Admin" --email walk13-admin@heta.example
```

## Automated

```bash
dotnet test src/server/Scs.sln
```

```bash
npm --prefix src/web run ci
```

```bash
npx playwright test ops-plans ops-features
```

(from `src/web`; `SCS_SHOTS=<folder>` saves a picture of every screen and state in both languages.)

What must hold, by suite:

- **Access tests (written first)**: each address of `contracts/plans-api.md` with no session, a
  clinic's session, an operator without the permission and one with it; reads need
  `billing.view`, writes `billing.managePlans`.
- **Grid rules (written first — plan entitlements)**: an unknown key, a limit on a switch, a
  limit outside 1–1,000,000, a key twice, a feature not sold or retired newly included — each
  refused with nothing written; a retired or unsold feature the plan already had is kept; the
  save replaces the grid; an empty limit is «بلا حدّ».
- **Plan rules**: the code from the English name (`Pro Plan` → `pro-plan`, then `pro-plan-2`; a
  name of symbols → `plan`), never changed by an edit, also with two adds at once; amounts with
  two decimals at most and within range; one badge on the platform — moved by a save, removed by
  disabling, not back on enabling, refused on a disabled plan, also with two saves at once; an
  old `version` refused; disabling twice keeps the first reason and time.
- **Records**: each of the four actions in the platform's trail with who, what and when, before
  and after, the grid per feature, the change note; the badge's former holder has its own
  record; no fact named `key`.
- **Published reads**: `FindAsync` gives what a plan includes with its limits; `AssignableAsync`
  leaves disabled plans out; `IPlanSubscribers`' default answers no clinics.
- **The catalogue**: a feature in two plans reads `plans: 2`; the cards count it; «أين تُستعمل»
  lists both plans.
- **Isolation**: `plans.Plan` and `plans.PlanFeature` are in `PlatformWideTables`; the guardrail
  test passes.
- **Screens**: every screen, dialog and state of `contracts/plans-screens.md` in Arabic and
  English at 1440 and 390; no Arabic-Indic digit; no sideways scroll; nothing cut off.

## By hand, on the real stack

1. **Arrive.** Sign in as the walk admin. «الباقات» opens «الباقات» (not the catalogue): the
   empty state «لا توجد باقات»; the cards read 0 with «لا توجد بيانات»; «كتالوج الميزات» opens the
   catalogue, and its breadcrumb «الباقات» comes back.
2. **Add.** «إضافة باقة +»: try an empty name and the price `499.999` (refused at their fields);
   add «الباقة الأساسية» / "Basic Plan", 249 / 0 / 12 / 0, with «المستخدمون» on at 3, «قناة
   واتساب» on, «رصيد الرسائل» on and empty, and «العلامة التجارية المخصصة» («قادمة») on. «تمت إضافة
   الباقة.»; the row reads «249 ج.م / شهريًا», «0 عيادة», «نشطة», «حتى 3 مستخدم».
3. **Code.** Add another plan named "Basic Plan" in English: its detail shows `basic-plan-2`.
4. **Badge.** Edit the first plan with «الأفضل»; edit the second with «الأكثر مبيعًا»: the field
   names the first as holder; after saving, the first has none.
5. **Detail.** Open the first plan: «معلومات الباقة» with «249 ج.م · 12 دولار» and «بلا سعر سنوي —
   السنة 12 × الشهري»; «ميزات الباقة» by group, «حتى 3 مستخدم», «✓», «بلا حدّ»; «لا تشمل الباقة <ن>
   ميزة أخرى من الكتالوج…»; «لا توجد عيادات مشتركة في هذه الباقة حتى الآن.».
6. **Two tabs.** Edit the same plan in two tabs: the second save is refused with «تغيّرت هذه
   البيانات منذ فتحتها.».
7. **Disable and enable.** Disable the badge holder with a reason: «معطلة», the reason in the
   detail, «باقة واحدة معطلة» on the card, the badge gone; the row menu offers «تفعيل الباقة»;
   enable it: «نشطة», no reason, no badge; its form shows the badge select again.
8. **Retired feature.** In the catalogue, retire «قناة واتساب»: the first plan's form still shows it
   on, marked «موقوفة»; a new plan's form does not offer it. Sell it again.
9. **The catalogue.** «قناة واتساب» reads «باقة واحدة» (or more); «أين تُستعمل» lists the plan.
10. **The trail.** The platform's trail shows the adds, the edits (with the grid's changes and the
    note), the badge's move on both plans, the disable and the enable, by the walk admin.
11. **The viewer.** Sign in as the walk viewer: the list reads, no «إضافة باقة +», no «⋯»; a row
    opens the detail with no «تعديل»; `/plans/new` shows no-access.
12. **Look.** Each screen and dialog beside its frame in both languages; differences go to
    `validation.md`; `design-vs-real.png` is the closing picture.
