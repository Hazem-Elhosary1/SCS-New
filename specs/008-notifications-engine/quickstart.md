# Quickstart: Verifying the Notifications Engine

**Spec**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

Temp folder on `E:` first, as [docs/setup.md](../../docs/setup.md) says.

## Automated

```powershell
dotnet test src/server/Scs.sln -c Debug
npm --prefix src/web run ci
npx playwright test          # from src/web, with nothing else running
```

What must pass that is new:

| Test | Proves |
|---|---|
| `Notifications/NotificationIsolationTests` | a notification of one clinic or one person is unreachable from another by every address (written first) |
| `Notifications/EmailSwitchAccessTests` | the switches refuse without `settings.view` / `settings.notifications` (written first) |
| `Notifications/RaisingTests` | an event and its change together; a forced failure on either side leaves neither; refusals of the writer |
| `Notifications/RecipientsTests` | each rule across two clinics; told once; disabled and invited accounts told nothing; later role changes |
| `Notifications/BellTests` | list, paging, ninety days, state, mark one, mark all, another person's is "not found", the two languages |
| `Notifications/EmailTests` | always / switch / never; sent after the change; retried; given up after a day; dropped for a disabled account; one language; nothing secret |
| `Notifications/EmailSwitchTests` | default on; changed for one clinic only; audit entry with before and after; not switchable refused |
| `Notifications/NewDeviceTests` | new browser → one notification and one email; same browser, first sign-in ever, failed sign-in → none |
| `Notifications/NotificationVolumeTests` | a thousand notifications read and marked in under a second; two hundred recipients in under a tenth |
| `NotificationEventsTests` (unit) | twenty events, frozen keys, two languages, permissions in the catalogue |
| isolation suite of spec 002 | the four new tables have their policies and samples |
| `e2e/notifications.spec.ts` | the bell with real answers: dot without reload, mark, open, load more, both languages |

## By hand, on the real stack

Start the stack (`deploy/compose`), the API with its worker, and the clinic app. Emails arrive
in Mailpit (<http://localhost:8025>).

1. Sign in as `owner@shifa.example` in a browser profile it has used. Open the bell:
   «لا إشعارات جديدة», no dot.
2. In a second, fresh browser (a private window), sign in as the same account.
3. Within a minute, without reloading, the first browser's bell shows a dot. Open it:
   «الدخول من جهاز جديد», under it the device («متصفح Chrome على Windows»), tinted, with its
   time. Mailpit holds one email with that subject, in Arabic, with no password and no code.
4. Press the notification: it is no longer tinted and the dot is gone; the page does not change
   (it leads nowhere yet). Within a minute the second browser shows it read too.
5. Sign out of the second browser and sign in there again: no new notification, no new email.
6. Switch the console to English and open the bell: "Sign-in from a new device", "Chrome on
   Windows".
7. Ask the switches from the browser's console as the owner:

   ```js
   await (await fetch('http://localhost:5080/v1/settings/notification-emails', { credentials: 'include' })).json()
   ```

   Four switches, all on. As `reception@shifa.example` the same address answers `403`.

## Beside the design

`SCS_SHOTS=<folder> npx playwright test notifications` saves the panel with notifications and
empty, in Arabic and English, at 1440×900. Lay them beside frames `5999:831798`, `5999:834947`,
`5999:832473`, `5999:835622` and component `4262:1056`.

## Measuring

`NotificationVolumeTests` prints the time of the panel's first page and of "mark all" at a
thousand notifications (SC-007), and what raising for two hundred recipients adds (SC-008).
