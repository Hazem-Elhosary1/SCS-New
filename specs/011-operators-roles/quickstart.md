# Quickstart: Operators and Their Roles

**Feature**: 011-operators-roles

How to run it, look at it and prove it. Details are in `contracts/` and `data-model.md`.

## Prerequisites

The local stack of `docs/setup.md`: the compose services (SQL Server, the mailbox on 8025), the
API (5080), the operators console (`ops`, 4203). The migration of this spec has run (the API's
start does it locally): three system roles exist and every operator there was holds «مسؤول
عام».

Walk accounts only. The seeded operator's password is the owner's: do not sign in as it or
reset it. Invite a walk super admin with the deployment command and work from there:

```bash
dotnet run --project src/server/tools/Scs.Migrator -c Release --no-build -- operator invite --name "Walk Admin" --email walk11-admin@heta.example
```

## Automated

```bash
dotnet test src/server/Scs.sln
```

```bash
npm --prefix src/web run ci
```

```bash
npx playwright test
```

(from `src/web`; `npx playwright test ops-operators ops-roles ops-access` for this spec alone;
`SCS_SHOTS=<folder>` saves a picture of every screen and state in both languages.)

What must hold, by suite:

- **Access tests (written first)**: for each address of `contracts/operators-roles-api.md` — no
  session, a clinic's session, an operator without the permission, an operator with it; a route
  with no declaration fails the architecture test; every code of the operators group guards a
  route.
- **Grants**: nobody ends with a permission the granter lacked, by inviting, editing an
  operator, adding, editing or copying a role (SC-006) — the rule's unit tests over every
  combination, and integration tests through the addresses.
- **The last super admin**: sequentially and with two requests at once (SC-005).
- **Sessions**: a changed role and a suspension end every session at once with their reasons; a
  changed role's permissions do not (SC-004, SC-009).
- **Links**: no answer, record or log holds one (SC-003); one a minute; the two lives.
- **Records**: each of the ten actions is in the platform's trail with who, what and when
  (SC-010); nothing secret in an entry.
- **Screens**: every screen and state in Arabic and English at 1440 and 390; no Arabic-Indic
  digit anywhere in the console; no sideways scroll; nothing cut off.

## By hand, on the real stack

1. **Arrive as a super admin.** Set the walk admin's password from the email; sign in. The side
   bar shows «الرئيسية» and «المشغلون»; the top bar says «مسؤول عام».
2. **The list.** Cards, table, search by part of a name and of an email, filter by state and by
   role, clear, page 2 if there is one. Times and counts in Western digits in Arabic.
3. **Invite.** «+ إضافة مشغل»: a viewer, Arabic. The page of the new operator opens; state
   «مدعو»; no link anywhere on the screen. Within a minute the mailbox holds «دعوة إلى لوحة
   التشغيل» naming the walk admin and the role, 72 hours. Set the password from it and sign in
   in a second browser: «المشغلون» shows, with no add button and a row menu of one item.
4. **Invite without sending.** The switch off: the account is «مدعو», no email; then «إعادة
   تعيين كلمة المرور» → «إرسال الرابط» from its page sends the invitation. Press again within a
   minute: the "less than a minute" sentence, and no second email.
5. **Refusals, drawn.** As a custom-role operator who may manage operators but not settings:
   the role list disables a role with settings and «مسؤول عام», each with its reason. Open your
   own edit page: the role field is locked with its sentence.
6. **Change a role.** As the admin, change the viewer to a custom role: their second browser is
   on the sign-in screen within 30 seconds with «تم تغيير دورك…».
7. **A role's permissions change without signing anyone out.** Add a permission to that custom
   role: the other browser stays signed in, and its next page shows what the role now allows.
8. **Suspend and reactivate.** Suspend: the other browser is out with «تم قفل حسابك…»; signing
   in with the right password says «هذا الحساب موقوف.». Reactivate: they sign in.
9. **The last super admin.** With one active super admin: changing their role and suspending
   them are both refused with their sentences. There is no suspend button on your own page.
10. **Unlock.** Lock a walk account with five wrong passwords; its page shows «فك قفل الحساب»;
    after it, the right password signs in.
11. **Roles.** Add a role from an existing one (the hint says what was left out when signed in
    as a non-super-admin); tick «عرض كل التذاكر» and see «عرض التذاكر المُسندة إليّ» lock; save;
    edit; try to delete while held (count named, button disabled); move the holder; delete.
12. **System roles.** No menu on their rows; their names follow the language.
13. **An operator's page.** Information, permissions, active sessions, and the last ten things
    the account did, each as a sentence with its time.
14. **Direct requests.** As the viewer, send `POST /v1/ops/operators` by hand: 403
    `PERMISSION_REQUIRED`. Open `/operators/new` by its address: "you can't open this page".
15. **Spec 010's screens.** Sign-in countdown, code minutes, profile sessions: Western digits.
16. **Logs.** The API's log of the walk: identifiers only — no email, name, link or reason.

Then lay every screen beside its frame at 1440 in both languages, and make
`design-vs-real.png`.

## What this does not prove

Screens for the other 22 permissions do not exist: their codes are held by roles and guard
nothing yet. Export is not built. The three performance cards other than active sessions are
not built.
