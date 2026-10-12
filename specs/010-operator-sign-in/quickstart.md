# Quickstart: checking Operator Sign-in (spec 010)

How to prove the feature works. Addresses and shapes are in `contracts/`; tables in
`data-model.md`. Set `TEMP` and `TMP` to a folder on `E:` first, and stop the local API before
building.

## The gates

```
dotnet test src/server/Scs.sln
npm --prefix src/web run ci
npx playwright test          (in src/web)
```

All three pass with nothing else running. The counts before this spec are those of spec 009's
last run: 551 unit, 51 architecture, 712 integration; clinic 68, core 164, ui 95; 397 browser
tests and 4 skipped.

## By hand, on the real stack

Start the stack (compose project `heta-scs`: SQL Server and the local mailbox), the API, and
the operators console (`npm --prefix src/web run start:ops`, port 4203). The mailbox is at
`http://localhost:8025`.

1. **The first operator.** Run `operator invite` for a wrong email, then `operator withdraw`
   for it: its link no longer works. Run the deployment tool's `operator invite` with a name
   and an email. One email arrives. Run it again: a second email, and the first link no longer works.
2. **First password.** Open the link: the email is shown and locked. A nine-character password
   leaves its rule unmet. Save a good one: the sign-in screen, with its "account is ready"
   line.
3. **Sign in.** Wrong password: the single banner. Right password: the code step and an
   email. A wrong code, then the right one: inside the frame, name and role in the top bar.
4. **The single answer.** An unknown email, and a wrong password, give the same banner.
5. **The lock.** Five wrong passwords, then the right one: the lock warning with its time.
   "Forgot password": "check your email" counts a minute down before "send again" returns;
   sent again, a second email arrives and says so on the screen. The link, a new password: the
   lock is gone.
6. **One session.** Sign in in a second browser: the first, left untouched, shows the
   sign-in screen with the "another device" line within thirty seconds; time it. Then give the
   password in both browsers before either code: the first browser's code is still good after
   the second asked for its own, and whichever completes last is the one left inside.
7. **A new browser.** The second browser's sign-in sent a "new browser" email; signing in
   there again sends none.
8. **«تذكرني».** Unticked: close the browser, open it — the sign-in screen. Ticked: still
   inside.
9. **Idle.** Set `SCS_OPS__IDLE_MINUTES` to its lowest for the walk, leave the console
   untouched: the sign-in screen with the session-ended line, without clicking.
10. **Profile.** The account's details; the switch on and locked; this session "active now"
    and the earlier ones ended. Change the password with each wrong input, then rightly: the code arrives by
    email and only the right code changes it.
    "Sign out of all devices": the sign-in screen.
11. **Two tabs.** Sign out in one; the other shows the sign-in screen.
12. **English**, and **the keyboard alone**, through sign-in, the code and the profile. A
    browser set to English that has chosen no language opens the console in English.
13. **The other console.** With an operator's session, ask a clinic's address: no session.
    With a clinic's session, ask `/v1/ops/profile`: no session.

Record what was seen, and read the database: one open session for the account; a
`SignInRecord` for each event; no code, password, token or link in any row or in the API's
log.

## Beside the design

`SCS_SHOTS=<folder> npx playwright test ops` takes each screen at 1440 wide in Arabic and
English. Lay each beside its frame (`contracts/operator-screens.md` names them): layout,
sizes, colour, words, states. Fix what differs; record what stays different and why. Save one
picture, the Arabic sign-in beside frame `128:85`, as `design-vs-real.png` in this folder.

## Measuring

- The time from a correct password to the code's email in the local mailbox (SC-002).
- A hundred answers each for an unknown email, a wrong password and a locked account: their
  times overlap (SC-003).
- The moment an idle console is signed out, against the thirty minutes (SC-005a).

## What cannot be checked yet

- A suspended operator and a changed role: nothing suspends or changes one until spec 011.
  Their paths are tested by putting the account in the state directly.
- A blocked address: spec 025.
- The settings screen for the lock and the timeout: spec 024.
