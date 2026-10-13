# Contract: composing an email

**Feature**: 094-email-template

What a module gives the template, and what the template promises back. The types are in
`data-model.md`; the words of each email are in `emails.md`.

## 1. What a module does

1. Decides the recipient's language (the account's; none → Arabic).
2. Fills an `EmailContent`: subject, short line, title, the parts in order, the reason.
3. Asks the renderer for the message and gives it to the sender, exactly where it sent before.
4. Provides a sample of that email, in both languages, to the catalogue.

A module never writes markup, the platform's name, the support contacts, a console's address
as fixed words, or a direction.

## 2. What the renderer promises

For every `EmailContent` it accepts, the message it returns:

**Layout** (frames `447:3`, `451:2`)
- A card of at most 600 on the light ground, fluid below that, never wider than the window.
- Header: the mark «S», white on the purple rounded square, as text. A rule.
- Body, in the centre: the short line (purple, small, spaced capitals in English); the title;
  then the parts in the order given, each as its frame draws it. A rule.
- Footer: the platform's name in the email's language; the address; the support email (a link
  that starts a message) and the phone on one line, a dot between them; the reason line. A
  line or part that is not set is not drawn.

**Language and direction**
- `lang` and `dir` on the root; Arabic right to left, English left to right.
- Values marked left to right keep their order inside an Arabic line.
- Digits are Western in both languages; the renderer writes none of its own.

**Parts**
- `Code`: one run of six digits, spaced by the box's letter spacing, never by spaces; a label
  that reads them one at a time.
- `Button`: one; under it «أو انسخ هذا الرابط في متصفحك:» / "Or copy this link into your
  browser:" and the whole address, which breaks inside the card.
- `Facts`: a bordered box; each row a name at the start and a value at the far edge; an
  optional rule and total row; an optional status mark in its tone.
- `Attention`: a tinted box with an optional mark, a line and a quieter second line.
- `Note`: small text under what it follows.

**Text-only form**
- The same content in the same order: short line, title, paragraphs, the code alone on a line,
  the button's label followed by the address on its own line, each fact as "name: value", the
  notes, then the footer's lines. No markup, no leftover symbols.

**Reading everywhere**
- Nothing is loaded: no picture, no font, no stylesheet, no address that reports an opening.
- Every colour of text is at 4.5 to 1 or better with its ground.
- The message is right without its `<style>` block; the block only adds the dark theme.
- The title is a heading; layout tables are marked as presentation.

**Care**
- Every text and value is encoded; nothing a person wrote can become markup or a link.
- The only links are the button's address, its written-out copy and the support email.
- The sender's name is the platform's name in the email's language.

## 3. What the renderer refuses

Thrown when the email is built, so a test meets it and a half-made email is never sent:

| Given | Refused as |
|---|---|
| An empty subject, short line, title or reason | incomplete email |
| Two buttons | more than one action |
| A button with no address, or an address that is not `https://` or the local `http://localhost` | not a link of the platform |
| A code that is not six digits | not a code |
| A facts part with no row | empty part |

A part with nothing in it that is optional (a note with empty text) is simply not drawn.

## 4. Rules a module's email must keep

Held by `EmailCatalogueTests` over every sample, and by `EmailRulesTests`:

- At most one button; a code's email has none.
- Every link goes to an address under the clinic console's, the operators console's or the
  patients' address from configuration, or is the support email. A later spec that needs
  another destination adds it to the allowed list in that spec.
- The subject is 1–120 characters, has no "[", and does not contain either name of the
  platform.
- The text of the email does not contain the platform's name as fixed words in the module's
  code: where an email names the platform it takes the name from the identity.
- An email that is always sent has no "unsubscribe" or "stop receiving" words.
- Every method that returns an `EmailContent` has a catalogue sample of its kind in Arabic and
  in English.
- No file outside the renderer contains email markup.

## 5. The catalogue

`npm run emails` (from `src/web`) exports and opens `e2e/.emails/index.html`: a list by
audience of every email, each with four links — Arabic, English, and the text-only form of
each. The pages are the messages exactly as they would be sent, with sample values.

The export is made by the unit-test project when `SCS_EMAILS_OUT` names a folder; the browser
tests run it before they start. It is not part of the API, the worker or the deployment tool.
