# Quickstart: Checking Files and Attachments

**Spec**: [spec.md](spec.md) | **Contracts**: [contracts/files-engine.md](contracts/files-engine.md),
[contracts/files-api.openapi.yaml](contracts/files-api.openapi.yaml)

How to see that the feature works. Details are in the contracts and the data model.

## The gates

```powershell
dotnet test src/server/Scs.sln
npm --prefix src/web run ci
npx --prefix src/web playwright test
```

New suites: `Scs.IntegrationTests/Files/*`, `Isolation/FileSamples`, `Scs.UnitTests/Files/*`,
`Scs.ArchitectureTests/FileRulesTests`; `core/src/lib/files/*.spec.ts`,
`ui/src/lib/files/*.spec.ts`; `e2e/files.spec.ts`.

## By hand, on the real stack

Start the stack (`deploy/compose`), the API with its worker, and the clinic app. With no
scanner address set, the local scanner knows the standard test signature only.

Sample files are made by the script, never fetched: a small PDF, a PNG, a JPEG with a place
recorded in it, a program renamed `scan.pdf`, an 11 MB PDF, and a text file holding the
standard test signature renamed `report.pdf` with a PDF header in front.

1. Sign in as `owner@shifa.example` and open `http://localhost:4201/dev/files`.
2. The zone says «ملف PDF أو صورة · حتى ١٠ ميجابايت». Drop the small PDF: a card with its
   progress, then its size. «حفظ» is disabled while it uploads.
3. Press «حفظ». The card reads «قيد الفحص» and, within a minute, has its download. Download it:
   it arrives under its own name and is identical to what was uploaded.
4. Drop the renamed program: refused, «نوع الملف غير مدعوم — PDF أو صورة». Drop the 11 MB PDF:
   refused with its size and the limit. The zone is ready again each time.
5. Add files up to three: the zone takes no more and says the limit.
6. Drop the file with the test signature and save: it reads «قيد الفحص», then «مرفوض»; it
   cannot be downloaded; the bell has «ملف مرفوض» with its name; and
   `GET /v1/audit/entries?category=security` has its entry.
7. Copy the address a download used; open it in a private window: nothing. Wait five minutes
   and open it in the signed-in window: nothing.
8. Sign in as `reception@shifa.example` in another browser and ask for the owner's file by its
   id (`POST /v1/files/{id}/access` from the console): `404`.
9. As the owner, `GET /v1/audit/entries?category=records`: one «تنزيل ملف» for each download of
   step 3, none for steps 7 and 8.
10. Remove a file on the page: it is gone from the list of files and listed under "removed"
    with the day it can be restored until. Restore it: it downloads again.
11. Switch to English and repeat steps 2 and 4: the same, in English, with Western digits.
12. Use the keyboard only: reach the zone, choose a file, hear its state, remove it.

With the ClamAV profile (`docker compose --profile scan up -d`, `SCS_SCAN__HOST=localhost`),
repeat steps 3 and 6; then stop the scanner, upload and save a file — it stays «قيد الفحص»
and cannot be downloaded — start it, and see the file become available within five minutes.

## Beside the design

`SCS_SHOTS=<folder> npx playwright test files` saves the zone in its six states and the card
in its states, in Arabic and English. Lay them beside section "File Upload" (`4260:814`) and
"File Attachment Card" (`4252:63169`), and put one pair in `specs/009-file-storage/design-vs-real.png`.

## Measuring

`FileVolumeTests` prints the time to accept a 10 MB file and to start its download (SC-001,
SC-006), and how long a hundred waiting files take to be scanned once the scanner returns
(SC-007).

## What cannot be checked yet

- The five kinds of the BRD have no owner until their specs: they are exercised through kinds
  made for the tests and through `dev.sample`.
- The store for the cloud and the alarm on waiting files: with spec 001's cloud tasks.
- Retention by age: with periods set by the tests; no real value exists.
