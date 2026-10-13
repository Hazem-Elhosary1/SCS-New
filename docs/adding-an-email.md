# Adding an email

Every email the platform sends is words given to one template (spec 094). A module never
writes markup, a footer, a direction, the platform's name or a support contact: it says what
the email says, and the template lays it out, in Arabic or English, in a laid-out form and a
text-only form from the same content.

The template is `src/server/shared/Scs.BuildingBlocks/Mail/`; its contract is
`specs/094-email-template/contracts/email-composition.md`.

## Steps

1. **Write the words in your spec's contract**, in both languages: subject, the short line
   above the title, the title, the parts in order, the reason line when the default does not
   fit. Mark what comes from a frame and what the owner still has to confirm.

2. **Build an `EmailContent`** in the module's `Infrastructure` folder, beside the others
   (`AccountEmails`, `OperatorEmails`, `NotificationEmails`): a static method named after the
   email, taking the recipient, the language and the values, returning `EmailContent`.

   ```csharp
   public static EmailContent InvoiceIssued(string to, string language, string number, string due, string amount, string payAddress) =>
       language == Words.English
           ? new(to, "en", "Your invoice is ready", "SUBSCRIPTION INVOICE", "Your invoice is ready",
               [
                   new EmailParagraph("Here is the subscription invoice for your clinic."),
                   new EmailFacts(
                       [new EmailFact("Invoice", number), new EmailFact("Due date", due)],
                       total: new EmailFact("Amount due", amount), ruled: false),
                   new EmailButton("Pay with Fawry", payAddress),
               ])
           : new(to, "ar", "فاتورتك جاهزة", "فاتورة الاشتراك", "فاتورتك جاهزة", [ /* the same parts in Arabic */ ]);
   ```

   The parts are a closed set: `EmailParagraph`, `EmailNote`, `EmailCode` (six digits),
   `EmailButton` (one at most), `EmailFacts` (rows, an optional total and status mark) and
   `EmailAttention` (the tinted box). If an email needs something none of them draws, that is
   a change to the template and to the design, not markup in a module.

3. **Pass the recipient's language**: the one the account keeps; `Words.Language(...)` turns
   anything else into Arabic. One email is in one language.

4. **Name the platform with `EmailContent.Platform`**, never as words: the template writes the
   configured name of the email's language in its place. A subject does not name the platform
   at all — the sender's name does.

5. **Mark values that read left to right** inside an Arabic line — an invoice number, an
   address, an IP — with `EmailContent.LeftToRight(value)`.

6. **Write digits as Western digits** in both languages. `Words.Western(...)` converts words
   made for a screen (`Words.Moment`, a notification's line).

7. **Render and send** where the module sent before:
   `await mail.SendAsync(renderer.Render(content), cancellation)`, with `EmailRenderer` taken
   from the constructor. Never log the content or the message; both print only their type name.

8. **Add both languages to the module's samples** (`…EmailSamples.cs`, implementing
   `IEmailSamples`), with made-up values, the kind named exactly as the method. The
   architecture tests fail an email that has no sample in Arabic and in English.

9. **Look at it.** From `src/web`: `npm run emails` exports the catalogue and opens it — each
   email in Arabic, in English and as text only. `npx playwright test emails` checks every one
   at the design's width, on a phone, in a dark theme, with nothing loaded and with its styles
   taken away. Lay the pictures (`SCS_SHOTS=<folder>`) beside the frame.

## What the template does for you

- The card, the mark, the rules, the footer with the platform's name, address and support
  contacts from configuration (`SCS_PLATFORM__*`), and the reason line.
- `lang` and `dir`, right to left for Arabic; the sender's name in the email's language.
- The text-only form: the same words in the same order, the button as its label and address.
- The written-out address under a button; the line a mail program shows beside the subject.
- A dark theme for programs that ask for one; colours at 4.5 to 1 or better.
- Encoding: nothing a person wrote can become markup or a link.

## What it refuses, when the email is built

An empty subject, short line, title or reason; two buttons or two codes; a button whose address
is not `https://` (or the local `http://localhost`); a code that is not six digits; a box of
facts with no row; a subject that names the platform. A long subject or title is cut at a word.
A paragraph or a note given nothing is not refused: it is left out, with no gap, so an email
need not check an optional line before adding it.

A job that sends email treats an email that cannot be composed like one the mail server
refused for good: it is given up or left owed, counted in the job's log line, and the next
email is sent.

## Rules the tests hold over every email

At most one button, and none beside a code; links only to the consoles' addresses and the
support email; a subject of 1–120 characters with no "[" and no platform name; no "unsubscribe"
words on an email that is always sent; no Arabic-Indic digits; nothing loaded from anywhere;
nothing of an email in the logs.
