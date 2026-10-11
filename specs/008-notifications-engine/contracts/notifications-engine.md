# Contract: Adding and Raising a Notification Event

**Spec**: [../spec.md](../spec.md) FR-001 to FR-019b, FR-030 to FR-035a | **Data**: [../data-model.md](../data-model.md)

What every later spec does to tell people something, and what the engine promises in return.
Decided once here.

## 1. To add an event that is not in the table

1. Add its row to `Scs.SharedKernel/Notifications/NotificationEvents`: key, group, names in
   Arabic and English, mark, recipient rule, email kind, the fact keys of its line, the line in
   both languages, where it leads.
2. Add the key to `notification-events.lock`.

## 2. To raise an event

For one of the twenty events already in the table, first give it its line and its destination
(they are absent until the event has a cause). Then, where the change is saved:

```text
db.Notify(new RaisedEvent(
    NotificationEvents.TicketSupportReplied,
    new EventSubject("ticket", ticket.Id, ticket.Number),
    Facts: { "subject": ticket.Subject },
    Owners: [ ticket.OpenedByAccountId ]));
await db.SaveChangesAsync(cancellation);
```

The save writes the notifications in one transaction with the change. Nothing in the
Notifications module changes (SC-009).

## 3. What the engine takes itself

Who is told; each person's bell item; whether an email is owed, by the event's kind and the
clinic's switch; the title; the time; the clinic. A caller cannot supply them.

## 4. What the engine refuses

| Refused | Why |
|---|---|
| a key that is not in the table | FR-004 |
| an event that has no line yet | FR-045 |
| a fact key the event does not allow, or one of the never-list | FR-017, FR-018 |
| any fact on a clinical event | FR-017 |
| no owner on an event whose rule names one | FR-005 |
| raising with no clinic established, or outside a transaction that holds the change | Constitution II, FR-006 |

A refusal throws; the caller's transaction rolls back. It is a defect in the caller.

## 5. What the engine does quietly

| | |
|---|---|
| The rule finds nobody | nothing is written; nothing fails |
| The same event, subject and named occasion again | recipients already told are skipped (FR-008); with no occasion every raise tells |
| A person named by two rules | told once (FR-011) |
| An account that cannot sign in | not told (FR-012) |

## 6. Rules for the caller

| Rule | Source |
|---|---|
| Raise where the change is saved, on the same context | FR-006 |
| Name the record by the identifier a person reads; put one fact in the line | the frames |
| Nothing clinical, no password, no code, no link, no email address in a fact | FR-017, FR-018 |
| A reminder or anything a job may repeat passes an occasion | FR-008 |
| Events are never gathered: raise each one | FR-008a |
| Work the platform does alone raises the same way; no person is needed | FR-009 |

## 7. Recipient rules

| Rule | Told |
|---|---|
| `Permission(code)` | every account of the clinic that can sign in and whose role holds the code |
| `Owner` | the accounts the caller names — the owner of the record, or any person the event is for (the spec's "a named person") |
| `OwnerAnd(code)` | both of the above, each person once |
| `Everyone` | every account of the clinic that can sign in |

## 8. Words

A title is the event's name. A line is a template with named placeholders: `{label}` (the
record's identifier), a fact's key, and the forms of [../../007-audit-trail/contracts/audit-trail.md](../../007-audit-trail/contracts/audit-trail.md)
§7 for numbers. Digits are Arabic-Indic in Arabic; identifiers stay Latin.

| Event | Title (ar · en) | Line | Source |
|---|---|---|---|
| `account.new-device` | الدخول من جهاز جديد · Sign-in from a new device | `{device}` → «متصفح Chrome على Windows» · "Chrome on Windows" | BRD ن-١٨; undrawn |

The four lines the frames draw — a ticket reply, a renewal invoice, an ending subscription, a
received payment — are written by the specs that raise them, in the frames' words.

## 9. Email

One layout, one language (the recipient's): the subject is the title; the body is the title,
the line, a button to the same place the bell leads, and a line saying the message cannot be
answered. The engine reads the address when it sends; a caller never handles one.
