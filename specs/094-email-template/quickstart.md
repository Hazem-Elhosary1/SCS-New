# Quickstart: Email Template

**Feature**: 094-email-template

How to run it, look at it, and prove it. The composing contract is in
`contracts/email-composition.md`; every email's words are in `contracts/emails.md`.

## Prerequisites

The local stack of `docs/setup.md`: compose project `heta-scs` (SQL Server, and the local
mailbox at `http://localhost:8025`), the API, the clinic and operators consoles. .NET 10 and
the web workspace installed.

Optional, to see a full footer locally, in `deploy/compose/.env`:

```text
SCS_PLATFORM__SUPPORT_PHONE=+20 100 000 0000
SCS_PLATFORM__ADDRESS_AR=12 شارع التحرير، القاهرة
SCS_PLATFORM__ADDRESS_EN=12 Tahrir St., Cairo
```

## Look at every email

```bash
npm --prefix src/web run emails
```

Exports the catalogue to `src/web/e2e/.emails/` and opens its index: every email by audience,
each in Arabic and English, laid out and as text only, with sample values. Nothing is sent and
no event is caused.

## Tests

```bash
dotnet test src/server/Scs.sln
npm --prefix src/web run ci
npx --prefix src/web playwright test
```

The browser tests export the catalogue first, then open each email at 320, 360 and 600 wide in
both languages, in a dark theme and with all loading blocked. To save pictures:

```bash
SCS_SHOTS=<folder> npx --prefix src/web playwright test emails
```

**Every picture is opened and looked at** before the spec is called done; the four drawn
emails and the three samples are laid beside their frames.

## By hand, on the real stack

Each step ends in the local mailbox. Open the message there, in its laid-out and its text-only
view, and compare with `contracts/emails.md`.

1. **Clinic sign-in code.** Sign in to the clinic console as a team member whose language is
   Arabic: one email, Arabic only, the code in its box; the code signs in. Repeat with a member
   whose language is English: English only.
2. **Clinic reset.** "Forgot password" on the clinic console: the reset email; its button and
   its written-out link both open the reset screen; the link is whole.
3. **Password changed.** Complete the reset: the "password changed" email, with the time in
   Cairo time.
4. **Invitation.** Add a team member with a link: the invitation, naming the clinic; the link
   lives 72 hours and opens the screen to choose a password.
5. **Account created, access reset.** Add a member with a handed-over password; reset a
   member's access: each email has a "sign in" button and no password.
6. **Notification.** Cause one event that is sent by email (spec 008): its title is the
   subject and the title; the button opens the page it names.
7. **Operator code.** Sign in to the operators console: the code email. Change the password
   from the profile: the code email that says it confirms a change.
8. **Operator reset.** "Forgot password" on the operators console.
9. **First operator.** Run the deployment tool's `operator invite` for a new address: the
   invitation, with 72 hours.
10. **New browser.** Sign in as an operator from a browser not used before: the notice, with
    the browser, the address and the time.
11. **The footer.** With phone and address unset: neither is shown and there is no gap. Set
    them and send one email again: both are shown.
12. **The sender.** In the mailbox's list each message is from the platform's name — Arabic
    for an Arabic email, English for an English one — and its subject has no tag and no
    platform name.
13. **Nothing fetched.** In the mailbox's view of a message, with the network tools open: no
    request leaves for a picture, a font or anything else.
14. **Logs.** Read the API's log of the walk: no code, no link, no recipient, no words of an
    email.

## What this does not prove

How the emails look in Outlook for Windows, Gmail, Apple Mail or a phone's mail app, and what
their own dark themes do. They cannot be opened from this machine. The owner sends the
catalogue's emails to real mailboxes and looks before release; anything found there is fixed
in the renderer, in one place.
