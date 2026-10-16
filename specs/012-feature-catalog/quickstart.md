# Quickstart: The Feature Catalogue

**Feature**: 012-feature-catalog

How to run it, look at it and prove it. Details are in `contracts/` and `data-model.md`.

## Prerequisites

The local stack of `docs/setup.md`: the compose services, the API (5080), the operators console
(`ops`, 4203). The migrations of this spec have run (the API's start does it locally):
`plans.Feature` holds 35 rows, every one «قادمة».

Walk accounts only — never the seeded operator. Invite a walk super admin and a walk viewer (a
custom role with «عرض الباقات والفواتير» only) as in spec 011's quickstart:

```bash
dotnet run --project src/server/tools/Scs.Migrator -c Release --no-build -- operator invite --name "Walk Admin" --email walk12-admin@heta.example
```

## Automated

```bash
dotnet test src/server/Scs.sln
```

```bash
npm --prefix src/web run ci
```

```bash
npx playwright test ops-features
```

(from `src/web`; `SCS_SHOTS=<folder>` saves a picture of every screen and state in both languages.)

What must hold, by suite:

- **Access tests (written first)**: each address of `contracts/features-api.md` with no session,
  a clinic's session, an operator without the permission and one with it; reads need
  `billing.view`, writes `billing.managePlans`.
- **Rules**: the key's pattern, uniqueness (retired included) and that it never changes; a limit
  needs both units, a switch takes no unit or baseline — refused by the server and by the
  database; order is the group's highest + 10, also with two adds at once; retire only a sold
  one, sell again only a retired one, "sold" cannot be turned off a retired one; an old
  `version` is refused.
- **Seed**: 35 rows with the keys, groups, kinds, units, specialties and baselines of
  `data-model.md`; running the migrations again adds nothing; an edited row is left as edited.
- **Records**: each of the four actions is in the platform's trail with who, what and when, the
  values before and after where listed; no fact named `key`.
- **Isolation**: `plans.Feature` is in `PlatformWideTables`; the guardrail test passes.
- **Screens**: every screen, dialog and state of `contracts/catalogue-screens.md` in Arabic and
  English at 1440 and 390; no Arabic-Indic digit; no sideways scroll; nothing cut off.

## By hand, on the real stack

1. **Arrive.** Sign in as the walk admin. The side bar shows «الباقات»; it opens «كتالوج
   الميزات». The cards read 35 · 0 · 35 · 0.
2. **Find.** Search «أسنان» and `dental`; filter by group «السريرية», type «حدّ», status «قادمة»;
   the chips and their count; «مسح الكل».
3. **Add.** «+ إضافة ميزة»: a limit without units is refused at the unit fields; a key that
   exists is refused at the key field; a valid one lands at the end of its group with its
   message.
4. **Edit.** Change its name and group; the key is read-only; it moves to the end of the new
   group. Edit the same row in two tabs: the second save is refused.
5. **Where used.** Any row: «لا تشملها أي باقة بعد.».
6. **Retire and sell again.** Retire it: «موقوفة» on the row, the menu now offers «إعادة بيع
   الميزة»; sell it again; the menu offers «إيقاف الميزة…» again.
7. **The trail.** The platform's trail shows the add, the edit, the retire and the sell again,
   by the walk admin.
8. **The viewer.** Sign in as the walk viewer: the catalogue reads, no «+ إضافة ميزة», the row
   menu offers «أين تُستعمل» only; `/plans/features/new` shows no-access.
9. **Look.** Each screen and dialog beside its frame in both languages; differences go to
   `validation.md`; `design-vs-real.png` is the closing picture.
