# Data Model: Email Template

**Feature**: 094-email-template | **Date**: 2026-10-12

Nothing is stored: no table, no migration. This spec adds values that live while an email is
being made, and one section of configuration.

## 1. `EmailContent` — what one email says

Made by the module that sends; given to the renderer.

| Field | Required | Rule |
|---|---|---|
| `To` | yes | the recipient's address; never logged |
| `Language` | yes | `ar` or `en`; anything else is treated as `ar` (FR-007) |
| `Subject` | yes | 1–120 characters, plain words, no tag, no platform name (FR-009); a longer one is cut at a word with "…" |
| `Eyebrow` | yes | the short line above the title; 1–60 |
| `Title` | yes | 1–200; wraps; a longer one is cut at a word with "…" rather than refused, since a notification's title is what a person or an event wrote |
| `Parts` | no | in the order they are shown (section 2) |
| `Reason` | yes | the footer's "you are receiving this because…" line; a default exists for account activity (FR-004) |

Refused when built (FR-017): an empty subject, short line, title or reason; more than one
button; a button with no address; a facts part with no row; a code that is not six digits.

## 2. Parts

A closed set; a module chooses and orders them, and cannot add a kind.

| Part | Holds | Notes |
|---|---|---|
| `Paragraph` | text | body colour; any number |
| `Code` | six digits | the tinted box; one at most; left to right (FR-015) |
| `Button` | label, address | one at most; the address is written out in full under it after the "or copy this link" words (FR-014) |
| `Facts` | rows of name and value; optional `Total` row; optional `Status` (text, tone) | value at the far edge; a rule above the total (FR-013), or — as the invoice frame draws it — no rule and only the total's value standing out |
| `Attention` | optional mark (text, tone), a line, an optional second line | the tinted box (frame `449:2`) |
| `Note` | text | small; note colour; any number |

Tones: `neutral`, `good`, `warning`, `bad`.

Every text is plain text: it is encoded when placed and cannot carry markup (FR-033). A value
may be marked as "left to right" — an address, an email, a number, a Latin name — so that it
keeps its order inside an Arabic line (FR-008).

## 3. `EmailMessage` — what the sender sends

As today, with one addition.

| Field | Was | Now |
|---|---|---|
| `To`, `Subject`, `TextBody`, `HtmlBody` | there | unchanged in meaning; both bodies come from the renderer |
| `SenderName` | — | the platform's name in the email's language (FR-009a) |

It still prints nothing of itself when logged.

## 4. Platform identity — configuration

Section `SCS_PLATFORM__`; read by the renderer and by nothing else.

| Key | Required outside local | Local default | Use |
|---|---|---|---|
| `NAME_AR` | yes | `سمارت كلينك أو إس` | footer and sender's name of an Arabic email; wherever an Arabic email names the platform |
| `NAME_EN` | yes | `Smart Clinic OS` | the same for English |
| `SUPPORT_EMAIL` | yes | `support@scs.localhost` | footer; pressed, it starts a message to support |
| `SUPPORT_PHONE` | no | (empty) | footer, beside the support email; left out when empty |
| `ADDRESS_AR` | no | (empty) | footer of an Arabic email; falls back to `ADDRESS_EN` |
| `ADDRESS_EN` | no | (empty) | footer of an English email; falls back to `ADDRESS_AR` |

Limits: names 1–80; support email a well-formed address; phone up to 40; address up to 200.
Spec 024 replaces the source of these values with its settings; the keys remain as the
deployment's defaults.

## 5. `EmailSample` — one entry of the catalogue

| Field | Rule |
|---|---|
| `Kind` | the email's name, the same as the method that builds it (`SignInCode`, `PasswordReset`, …); unique with `Audience` |
| `Audience` | `clinic`, `operators`, `sample` |
| `Language` | `ar` or `en`; both exist for every kind (FR-037, FR-039) |
| `Content` | the `EmailContent` with sample values: a sample name, clinic, code `742906`, link, times |

Sample values are fixed and made up; none is read from a database.

## 6. Relationships

```text
module ──builds──▶ EmailContent ──▶ renderer ◀── platform identity (configuration)
                                        │
                                        ▼
                                  EmailMessage (laid-out + text-only + sender's name) ──▶ sender

module ──provides──▶ EmailSample × (kind × language) ──▶ exporter ──▶ pages (catalogue)
```

No module depends on another for this; all three depend on the shared project, as they do now.
