# Runbook: operators and their sign-in (spec 010)

The people who run the platform sign in to their own console, with accounts that belong to no
clinic. Their tables are in the schema `ops`; nothing there holds a clinic's or a patient's data.

## Give the first operator a way in

There is no screen for it and no address: it is a command of the deployment tool, run where
the platform is deployed, by whoever deploys it.

1. Make sure the platform is running with its mail settings: the invitation is an email.
2. Run, with the environment's variables set (`SCS_DB__CONNECTION` at least):

   ```bash
   Scs.Migrator operator invite --name "<full name>" --email <address>
   ```

   It prints `Operator: invited.` and nothing secret. It never shows, takes or sets a password.
3. Within a minute the platform sends the invitation — an email with the subject «دعوة إلى لوحة
   التشغيل» ("Invitation to the Ops Console" for an account in English), sent under the
   platform's name: a link, good for 72 hours, where the
   operator chooses their password. If the email cannot leave, it stays owed and is tried again
   every minute; the platform's log says so, naming the account's identifier only.
4. The operator opens the link, chooses a password, and signs in with it and the code sent by
   email.

Run again for someone still invited, the command sends a new link and ends the earlier one.
For an active account it changes nothing (`already-active`, exit code 1): a forgotten password
goes through "forgot password" on the sign-in screen.

Every run is in `ops.SignInRecord` (`operator_invited_by_deployment`), with the machine user
that ran it.
A run that changes nothing — the account is already active, or there is nothing to withdraw —
is recorded too, as `operator_command_changed_nothing`, with the outcome and the machine user.

## An invitation went to the wrong address

Before the link is used:

```bash
Scs.Migrator operator withdraw --email <address>
```

The link stops working at once and the account is removed; the record of the invitation and of
the withdrawal stays. It refuses for an account that has already chosen its password
(`not-invited`): that is then somebody's account, and is dealt with as a security incident —
suspend it (spec 011) and read its records.

## An operator is locked out

- **Five wrong passwords** lock an account for fifteen minutes. The operator opens it at once
  with "forgot password". Anyone who knows the address can cause the lock; this is accepted,
  because the owner always has that way in.
- **A forgotten password**: "forgot password" on the sign-in screen. The link lives thirty
  minutes.
- **The mailbox is gone too.** Nothing in the product gets them in: the code and the link both
  go to that mailbox. Another operator replaces the account's access once spec 011 exists.
  **For the only operator there is no way in through the product.** It is then a matter for
  whoever deploys the platform: restore the mailbox, or — with the owner's written word —
  invite a second operator with the command above and suspend the lost account. Do not edit
  `ops.Operator` by hand.

## What the records say

`ops.SignInRecord` holds one line for each event: a sign-in, a wrong password (`login_failed`),
a wrong code (`otp_failed`), a lock, a sign-out, a session ended and why, a reused session value
(`session_token_reuse`), a reset asked for and completed, a password changed, a new browser.
A line holds an identifier, an address, a browser and a time; never a password, a code, a link
or an email. An attempt on an address that belongs to nobody is a line with no account.

Lines cannot be changed, and are removed only when they are older than
`SCS_OPS__RECORD_MONTHS` (twelve by default). Until the operators' audit log and security
events exist (specs 023 and 025) nobody is alerted to a lock or a reused session value: they are
read from this table.

`session_token_reuse` means a copy of a session was used by someone else. The session was
ended. Treat it as an incident: have the operator reset their password and read the lines
around it.

## Not ready for production yet

- The second step is a code by email, and the reset link goes to the same mailbox: whoever
  controls an operator's mailbox controls the account. An authenticator app would be a change
  to the BRD.
- The health of the invitation job is only its log line; nothing pages anybody.
- The lock threshold, the lock's length and the idle period are configuration
  (`SCS_AUTH__*`, `SCS_OPS__IDLE_MINUTES`) until the settings screen of spec 024.
