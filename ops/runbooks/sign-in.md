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
