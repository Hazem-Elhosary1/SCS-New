# Staff sign-in: what to do by hand until the screens exist

Spec 003 delivers sign-in and sessions. The screens that manage accounts and security arrive
later (users in spec 026, security settings in spec 085, the operators' security page in spec
025). Until then the operations below are run by an engineer, against the environment's
database, with the operator login (`scs_ops`). Each one is also an operation in code
(`IStaffAccounts`, `IStaffSessions`) that those screens will call.

Never paste an email address, a password or a code into a ticket or a chat. Identify an account
by its id.

## Find an account's id

```sql
SELECT Id, ClinicId, State, FailedAttempts, SpentCodes, LockedUntilUtc
FROM [identity].StaffAccount
WHERE Email = N'<the address, lower case>';
```

## Unlock an account (SH-049)

An account locks for 15 minutes after five wrong passwords, or after three sign-in codes are
used up in a row. It opens by itself when the time passes. To open it at once:

```sql
UPDATE [identity].StaffAccount
SET LockedUntilUtc = NULL, FailedAttempts = 0, LastFailedAtUtc = NULL, SpentCodes = 0
WHERE Id = '<account id>';
```

## End a person's sessions

```sql
UPDATE [identity].Session
SET EndedAtUtc = SYSDATETIMEOFFSET(), EndReason = 'ended-for-account'
WHERE AccountId = '<account id>' AND EndedAtUtc IS NULL;
```

The person is shown the sign-in form at their very next action.

## End everyone's sessions in a clinic

```sql
UPDATE [identity].Session
SET EndedAtUtc = SYSDATETIMEOFFSET(), EndReason = 'ended-for-clinic'
WHERE ClinicId = '<clinic id>' AND EndedAtUtc IS NULL;
```

Do this when a clinic is suspended or cancelled by hand. Without it, the clinic's sessions stop
only at their next renewal, up to five minutes later.

## Disable an account

```sql
UPDATE [identity].StaffAccount SET State = 'disabled' WHERE Id = '<account id>';
```

Then end its sessions as above. (The operation in code does both in one transaction.)

## A person cannot get in (spec 004)

What the person can do alone, and what to check when it does not work:

| They say | What is true | What to do |
|---|---|---|
| "I forgot my password" | «نسيت كلمة المرور؟» emails a link valid 30 minutes, to an **active** account only | Ask them to use it. The screen says the same thing for every address, by design: it never confirms an account exists |
| "No email came" | At most one reset email a minute and five an hour per account; nothing is sent to a disabled account or one that was never activated | Check `AccountEmailRequest` below. `skipped-limit`: wait. `skipped-state`: the account is not active. Waiting rows with no outcome: the mail server or the worker is down |
| "The link says it expired" | A link works once, and a newer link replaces the older one | A reset link: they ask again from the screen. A new-account link: whoever invited them sends it again |
| "I was invited and the link is too old" | A new-account link lasts 72 hours. "Forgot the password?" sends nothing to an account never activated | Send the invitation again (operation `IAccountHandover.ResendInvitationAsync`; a screen arrives with spec 026) |
| "My password stopped working" (a new account) | A password chosen by someone else is temporary: it must be replaced at first sign-in and stops working after 72 hours | They use «نسيت كلمة المرور؟», or whoever manages users re-issues access |
| "After signing in I only see a password screen" | The password is temporary. Nothing else is reachable until they choose their own | That is the product working. They choose a password |

```sql
-- What was asked for an account, and what became of it.
SELECT RequestedAtUtc, Kind, HandledAtUtc, Outcome
FROM [identity].AccountEmailRequest
WHERE AccountId = '<account id>'
ORDER BY RequestedAtUtc DESC;

-- Its links. A hash is all that is kept: a link cannot be read back or sent again from here.
SELECT Kind, CreatedAtUtc, ExpiresAtUtc, UsedAtUtc, ReplacedAtUtc
FROM [identity].PasswordLink
WHERE AccountId = '<account id>'
ORDER BY CreatedAtUtc DESC;

-- Whether it holds a temporary password.
SELECT State, MustChangePassword, TemporaryPasswordExpiresAtUtc
FROM [identity].StaffAccount
WHERE Id = '<account id>';
```

Never set a password by hand in the database, and never ask a person for theirs. Passwords are
set only by their owner from a link, or handed over as temporary ones by the operations of
`IAccountHandover`. No email the platform sends contains a password; one that claims to is not
ours.

The five emails of this area cannot be switched off: reset link, "password changed", new-account
link, "an account was created", "access was reset". A "password changed" email the owner did
not expect is a security report: end the account's sessions (above) and look at its events.

## A person says a page "is not in their role" (spec 005)

The page names the permission it needs and the role the person holds. Nothing is broken: the
role does not hold that permission. Until the roles and users screens exist (specs 026, 027)
there is no way to change it from the product; in a local or test environment:

```sql
-- What the account holds.
SELECT a.Email, r.Name, r.ReadyKey, r.HoldsEverything,
       (SELECT COUNT(*) FROM [identity].RolePermission p WHERE p.RoleId = r.Id) AS Codes
FROM [identity].StaffAccount a LEFT JOIN [identity].Role r ON r.Id = a.RoleId
WHERE a.Id = '<account id>';
```

An account with no role reaches only its own account pages. «مالك العيادة» stores no codes: it
holds the whole catalogue (`HoldsEverything`). A change to a role's codes is seen on its
holders' next request. Giving an account a different role must go through `IClinicRoles` — it
ends the account's sessions and refuses to leave a clinic without an owner; do not update
`RoleId` by hand outside a local database.

A refused request is in the logs as "Refused {route}: the permission {permission} is not held",
with the account and the clinic. "…it does not declare who may call it" is a defect: a route
was shipped without a declaration.

## See what happened to an account

```sql
SELECT OccurredAtUtc, Kind, Detail, IpAddress, TraceId
FROM [identity].SignInEvent
WHERE AccountId = '<account id>'
ORDER BY OccurredAtUtc DESC;
```

`TraceId` is the request reference: search the logs for it to see the whole request. Attempts on
addresses that match no account are in `security.AnonymousSignInAttempt`; they hold a keyed hash
of what was typed, never the text.

Sign-in events cannot be changed by anyone, and are removed only by retention.

## Rotate the secret that keys sign-in codes (`SCS_AUTH__CODE_KEY`)

The key protects the hashes of sign-in codes, which live ten minutes. Changing it makes every
code that is waiting to be typed unusable; people simply sign in again. Nothing else depends on
it except the hashes of unknown-email attempts, which stop being comparable with older ones.

1. Generate 32 random bytes and encode them in base64.
2. Put the value in the environment's secret store under `SCS_AUTH__CODE_KEY`.
3. Restart the platform (API and worker).

## Sessions stop working for everyone after a deploy

The cookies are encrypted with a key ring kept in the table `platform.DataProtectionKey`. If
that table is emptied, or a restore brought back an older copy, every cookie issued since is
unreadable and everyone is asked to sign in again. That is safe. Do not copy keys between
environments.

## Not ready for production yet

- The key ring is stored unwrapped. It must be wrapped with the cloud's key service before any
  production use (spec 001, cloud tasks).
- The limit of ten sign-in requests per minute per network address is counted per server
  instance. With more than one instance it must be shared.
- The mail service for the cloud environments is not chosen. Locally mail goes to the mail
  catcher at <http://localhost:8025>.
