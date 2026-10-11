# Quickstart: Checking the Console Frame

**Spec**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

Setup is as [docs/setup.md](../../docs/setup.md). Seeded accounts and their roles are listed
there; the seed password is the value of `SCS_SEED__STAFF_PASSWORD`, and sign-in codes arrive in
Mailpit (`http://localhost:8025`).

## Automated

```powershell
dotnet test src/server/Scs.sln -c Debug
cd src/web; npm run ci
npx playwright test
$env:SCS_SHOTS = "shots"; npx playwright test shell-states roles-list sample-list
```

| # | Scenario | Where | Expected |
|---|---|---|---|
| 1 | Preferences are saved, read back and partial | integration `Identity/PreferencesTests` | each member alone and together; the session answer carries them |
| 2 | Preferences refuse bad values | integration | `INVALID` for a third language and for `readonly`; `REQUIRED` for an empty body |
| 3 | A banner closed today is closed; tomorrow (Cairo) it is not | integration, moved clock | |
| 4 | Preferences are a person's own and isolated | integration + `Isolation/IdentitySamples` | another account or clinic never reads them |
| 5 | The own-account list is the written three | integration `Access/RouteDeclarationTests` | |
| 6 | Roles carry their holders | integration `Access/ReadyRolesTests` | counts follow assignments, in any account state |
| 7 | Every destination is in the table once, with a permission of the catalogue | web unit `destinations.spec` | 20 + 2, unique keys and paths |
| 8 | Side bar by permission | web unit `side-bar.spec` | for each of the 20 destinations and 3 parents: present with, absent without; order unchanged; empty group gone |
| 9 | Every address opens inside the frame | Playwright `shell.spec` | built, not built, not in role, not found; none before sign-in |
| 10 | Account menu and sign-out | Playwright | menu items; sign-out ends the session; back shows nothing |
| 11 | Language switch | Playwright + web unit | at once, same page; saved; adopted at sign-in; saved choice wins |
| 12 | Folding | Playwright | 240 ↔ 72, tooltips, group from folded, saved; folded on its own under 1280 |
| 13 | Environment strip | Playwright | outside production on every screen; never in production |
| 14 | Offline | Playwright | the alert; `scsWrite` controls disabled; search works; return re-reads |
| 15 | Sources | web unit + Playwright with fakes | bell: empty, items, failed; branches: none, one, many; four banners; closing; plan |
| 16 | Roles list | Playwright `roles-list.spec` | two cards, counts from 83, holders; view only; loading; failure; no custom roles |
| 17 | List contract | Playwright `sample-list.spec` | each rule of contracts/list-contract.md §3 |
| 18 | Keyboard | Playwright | every menu and panel opens, moves, closes on Escape, returns focus |
| 19 | Widths | Playwright | 1024, 1280, 1440, 1920: nothing overlaps or is cut |
| 20 | States pictured | Playwright `shell-states` | contracts/shell-frame.md §9, both languages |

The states that belong to later specs (the bell with items, branches, the four banners, "not in
your plan", a dental clinic) are shown by giving the frame sample data before it starts:
`sampleSources(page, {…})` in `src/web/e2e/staff-platform.ts`. Production never reads it.

## By hand, on the real stack

1. Sign in as `owner@shifa.example`. The frame: strip, top bar with «عيادة الشفاء», no branch
   button, language pill, bell, chip with the email and «مالك العيادة»; the full side bar.
2. Press every item: each opens inside the frame — "not available yet", except the roles list.
3. Open «الإدارة › الأدوار والصلاحيات»: two cards, 83 · 59 · 40, holders 1 · 1 · 1.
4. Fold the side bar; reload; sign out and in again in another browser: still folded.
5. Switch to English; sign out and in again: English.
6. Sign in as `doctor@shifa.example`: «المستخدمون» and «الرخص والتغطية» are absent; the roles
   list is under «للعرض فقط».
7. Sign in as `reception@noor.example` (or use the method of spec 005's quickstart, step 5):
   «إدارة العيادة» is absent; typing `/administration/roles` shows "not in your role" inside
   the frame.
8. Press the bell: «لا إشعارات جديدة». Open the account menu: «الملف والاشتراك», «تسجيل الخروج».
9. Cut the network in the browser's tools: the offline alert; on `/dev/sample-list` the writing
   buttons are disabled and search still works. Restore: the alert goes.
10. `/dev/sample-list`: search, filter, chips, clear all, paging, row menu, delete with
    confirmation, the toast.
11. Lay the pictures from `SCS_SHOTS` beside their frames (spec, "Design source") in both
    languages; write what matched, what differed and what was not compared in `validation.md`.

## Measuring

- SC-004: time from pressing the language pill to the page being in the other language.
- SC-006: time from "offline" to the alert and the disabled controls, and back.
