# Feature Specification: Files and Attachments

**Feature Branch**: `009-file-storage`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: empty. Taken from row `009-file-storage` of
`docs/reference/brd/SCS-SpecKit-Phases.md` (Phase 0, The Foundation): "uploading, checking the
type from the content, checking for malware, downloading, retention periods."

**BRD source** (`docs/reference/brd/source/`): `BRD-0-shared.md` — SH-095 (a retention period
for each class of data is a platform setting; conditional on the lawyer), SH-004 and SH-104
(each clinic's data is isolated, in the database itself), SH-091 (no secret in an interface or
a log), SH-094 (no personal data in operational logs), SH-097 (the states of every screen).
Chapter 08, tickets: §3-3 (the attachment), §5-9 and §5-11 (attaching and downloading), rules
ق-٢٠, ق-٢٤, ق-٢٦ and ق-٣٦, requirements CL-TK-070 … CL-TK-087. Chapter 03, patients: §3-8 (the
file), §4-5 and §5-17 (the upload dialog and its rows), rules ق-١٤, ق-٤٣ and ق-٤٤, requirements
CL-PT-096 … CL-PT-106. The same rules from the operators' side: Chapter 17 ق-٣٤, OP-B-083 …
OP-B-086, OP-B-388, OP-B-389. Other places that attach a file and will use this engine:
Chapter 04 (the signed copy of a consent, CL-PL-020), Chapter 03 §6-14 (the report of a
result), Chapter 11 (a doctor's photo, CL-DM-034), Chapter 10 (licence documents, CL-AD-128).

**Design source**: Figma file `4HLWEqlna9sbn68276LHYt`, page "Components — Inputs" (`4228:3`),
section "File Upload" (`4260:814`), read on 11 Oct 2026. What it fixes: a drop zone 320 wide
with a dashed border, an upload mark in a round tint, a title and a hint; six states in each
direction — Default, Hover, DragActive (tinted, brand border), Uploading (a file mark, «جارٍ
رفع الملف…» · "Uploading the file…", the hint, and a progress bar), Error (danger border and
mark, «تعذّر رفع الملف» · "The file could not be uploaded", and the reason «الحجم يتجاوز ١٠
ميجابايت — اختر ملفًا أصغر» · "Larger than 10 MB — choose a smaller file") and Disabled. Title
«إفلات الملف هنا أو النقر للاختيار» · "Drag a file here or click to browse"; hint «ملف PDF أو
صورة · حتى ١٠ ميجابايت» · "PDF or image · up to 10 MB". The component's own notes: the types
and the size are said before choosing, not after; an error keeps the zone ready for another
try; uploaded files are listed under it as file attachment cards.

## Overview

Many parts of the product hold files: a screenshot on a support ticket, an X-ray in a
patient's record, the signed copy of a consent, the report of a lab result, a doctor's photo.
Each chapter of the BRD describes its own, and they agree on almost everything — and where one
is silent (only the tickets chapter says a file is checked for malware), the silence is a gap,
not a decision.

This spec builds the one place all of them store and fetch files. It does five things:

1. **Takes a file in.** A person chooses a file; it is uploaded at once and waits, unshared,
   until what they are writing is saved.
2. **Checks it.** Its size, its type read from its content and not from its name, how many
   there are — and a scan for malware. The same checks for every file, whatever it is for.
3. **Gives it out** only to someone who may see the thing it belongs to, of the same clinic,
   through a way in that stops working after a few minutes.
4. **Does not keep it for ever.** A file never attached is removed; a file whose owner removes
   it can be brought back for thirty days and is then erased; and a retention period for each
   kind of file is a setting the platform applies once the lawyer has given its value.
5. **Gives every page the same upload control**, from the design, in both languages.

It has no page of its own. Tickets, patient files, consents and the rest are built by their
specs; each names its kind of file here and places the control.

## Clarifications

### Session 2026-10-11

- Q: Should this spec also build the shared upload control from the design, with no page yet
  to place it on? → A: Yes. It goes into the shared library, is laid beside its design in both
  languages, and is shown on a builders-only page until the first page uses it.
- Q: The BRD requires reading the type from the content and a malware scan only for ticket
  attachments; which rule applies to the others? → A: Every upload gets the same checks. One
  rule, no weaker door. The BRD's other chapters are the owner's to update.
- Q: What happens when the malware scanner is down or slow? → A: The upload is accepted and
  can be attached, but nobody can download the file until its scan passes; it is scanned when
  the scanner returns.
- Q: Should the platform record each time someone downloads a file? → A: Yes, every download
  of every private file is written to the audit trail: who, which file, of which record, when.
  Public files are not.
- Q: When someone removes a file from a record, can it be brought back if it was a mistake? →
  A: Yes, for thirty days. It cannot be downloaded from the moment it is removed; the owning
  page can restore it within thirty days; after that its content is erased for good.
- Q: When the scan finds malware in a file, who is told? → A: The person who uploaded it, in
  their bell, and the audit trail under security. Nothing is emailed, and the people who run
  the platform are not alerted for each one.
- Q: What happens to a file the scanner cannot look inside — a password-protected PDF, a
  damaged file? → A: It is refused, with its own reason, "could not be checked", and is never
  given out; its uploader is told to send it without a password.
- Q: Should a genuine PDF that carries scripts or other files be refused? → A: No. PDFs are
  accepted as they are and scanned; a private file is only ever downloaded, never opened
  inside the product. That scripts inside a PDF are not looked for is a stated limit.
- Q: Should there be a limit on files a person uploads and leaves unsaved? → A: Yes: at most
  thirty uploaded and not yet saved at one time, and at most sixty uploads in any ten minutes;
  beyond either an upload is refused with "try again later".
- Q: Should a record's files be protectable from erasing during a dispute or a legal request?
  → A: Yes. The owning page can put a record's files on hold; nothing of them is erased until
  the hold is lifted.
- Q: Should hidden details of photos — place, device — be removed? → A: A private file is kept
  exactly as uploaded, byte for byte; an image of a public kind has its hidden details removed
  before anyone can see it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A person attaches a file to what they are writing (Priority: P1)

Someone writing a ticket, or adding to a patient's record, chooses a file. It starts uploading
at once and shows its progress. When they save what they were writing, the file becomes part
of it. If they walk away without saving, the file was never shared with anyone and is cleaned
up later.

**Why this priority**: without it nothing else in this spec has anything to act on.

**Independent Test**: choose a file for a kind that allows it, see it accepted, save the
record that owns it, and find the file listed on that record with its name, size, who uploaded
it and when.

**Acceptance Scenarios**:

1. **Given** a kind of file that allows PDF up to 10 MB, **When** a person chooses a 2 MB PDF,
   **Then** it is uploaded at once, its progress is shown, and it is theirs alone until saved.
2. **Given** an uploaded file not yet saved with a record, **When** anyone else — a colleague,
   a person of another clinic — asks for it, **Then** it is as if it did not exist.
3. **Given** an uploaded file, **When** the record it was chosen for is saved, **Then** the
   file belongs to that record from the same moment: both are there, or neither is.
4. **Given** an uploaded file, **When** the person removes it before saving, **Then** it is
   gone and is not part of what is saved.
5. **Given** a file attached to a record, **When** the record is shown, **Then** the file
   reads with the name it was uploaded with, its size rounded as people read it, who uploaded
   it and when.
6. **Given** a person without the permission the owning page asks for, **When** they try to
   upload a file of its kind, **Then** they are refused as for anything their role does not
   open.

---

### User Story 2 - Only a file that is what it says, and is clean, is ever given out (Priority: P1)

A file renamed from `virus.exe` to `scan.pdf` is refused: the platform reads what the file is
from its content. A file larger than its kind allows is refused, and so is one more than the
count allows. A file that passes those is scanned for malware; one that fails the scan is
never given to anyone.

**Why this priority**: a clinic's staff and the support team open each other's files. One
infected file passed along is the worst thing this feature can do.

**Independent Test**: upload a program renamed as a PDF, a file over the size, one file too
many, and a file the scanner marks as malware; each is refused by its own reason and none can
be downloaded.

**Acceptance Scenarios**:

1. **Given** a kind that allows PDF, PNG and JPG, **When** a file whose content is something
   else is uploaded under an allowed name, **Then** it is refused, and the reason says the
   type is not accepted and names the types that are.
2. **Given** a kind with a limit of 10 MB, **When** a larger file is uploaded, **Then** it is
   refused, and the reason gives the file's size and the limit.
3. **Given** a kind that allows three files for one message, **When** a fourth is chosen,
   **Then** it is refused, and the reason gives the limit.
4. **Given** any accepted file, **When** it has not yet been scanned, **Then** nobody can
   download it, and it reads as "being checked".
5. **Given** a file the scan finds malware in, **When** anyone asks for it, **Then** it is not
   given; it reads as refused; its content is removed; the person who uploaded it finds a
   notification in their bell; and the audit trail has an entry for it under security.
5a. **Given** a file the scanner cannot look inside — a PDF behind a password — **When** it is
   scanned, **Then** it reads as refused because it could not be checked, is not given to
   anyone, and its uploader is told to send it without a password.
6. **Given** several files chosen together, **When** one is refused, **Then** the others are
   not held up by it.
6a. **Given** a person with thirty files uploaded and not yet saved, or sixty uploads in the
   last ten minutes, **When** they upload another, **Then** it is refused with a reason that
   says to try again later, and nothing is kept.
7. **Given** the limits are changed in the browser or bypassed, **When** the file reaches the
   platform, **Then** the platform's own checks decide.

---

### User Story 3 - A file is given only to someone who may see what it belongs to (Priority: P1)

A file has no life of its own: it belongs to a ticket, a patient's record, a consent. Whoever
may see that may download the file; whoever may not, may not — however they came by its
address. A clinic's files are never reachable from another clinic.

**Why this priority**: these files are the most sensitive data the product holds (Constitution
II). A link that works for anyone who has it is a leak waiting for a forwarded email.

**Independent Test**: attach a file to a record one person may see and another may not; the
first downloads it under its original name; the second, and anyone of another clinic, is
refused; and the way in the first one used stops working after a few minutes.

**Acceptance Scenarios**:

1. **Given** a clean file on a record, **When** a person who may see that record asks for it,
   **Then** the download starts, under the name the file was uploaded with.
2. **Given** the same file, **When** a person of the same clinic who may not see the record
   asks for it, **Then** they are refused, exactly as for the record itself.
3. **Given** the same file, **When** a person of another clinic asks for it by its identity,
   **Then** it is as if it did not exist.
4. **Given** a way in that was issued for one download, **When** it is used after it expired,
   **Then** it gives nothing.
5. **Given** nobody signed in, **When** the address of a private file is opened, **Then**
   nothing is given.
6. **Given** a person who could see a record and has since lost that permission, **When** they
   ask for its file again, **Then** they are refused: the question is asked at every download.
7. **Given** a clinic that is read-only, **When** a person who may see a record asks for its
   file, **Then** the download works; uploading is refused as every change is.
8. **Given** a kind of file that is public by nature — a doctor's photo on the booking page —
   **When** anyone opens it, **Then** it is shown without signing in; nothing private is ever
   of such a kind.
9. **Given** a private file, **When** a person downloads it, **Then** the audit trail says who
   downloaded which file of which record and when; a refused attempt adds nothing.
10. **Given** a photo of a public kind taken on a phone, **When** it is opened by a visitor,
    **Then** it carries no trace of where or with what it was taken; a private file downloads
    exactly as it was uploaded.

---

### User Story 4 - Every page offers files the same way (Priority: P2)

Wherever a person attaches a file, the control looks and behaves the same: a zone that says,
before anything is chosen, which types and what size it takes; a highlight when a file is
dragged over it; a row for each file with its progress; and, when a file is refused, a message
that names the file and the reason and leaves the zone ready for another try.

**Why this priority**: it is what a person sees of all the above. Built once, the pages that
follow only place it.

**Independent Test**: on a builders-only page, in Arabic and in English, take the control
through each of its states and lay each beside its design.

**Acceptance Scenarios**:

1. **Given** the control at rest, **When** it is shown, **Then** it says what it takes — the
   types and the size — in the page's language.
2. **Given** a file dragged over the zone, **When** it hovers, **Then** the zone is
   highlighted as the design draws it.
3. **Given** a file being uploaded, **When** its row is shown, **Then** it has the file's
   name, its size and its progress in per cent, and what the page saves with is disabled until
   the upload ends.
4. **Given** a file that is refused, **When** the message is shown, **Then** it names the file
   and gives the reason, and the zone is ready for another file.
5. **Given** an uploaded file not yet saved, **When** its remove control is pressed, **Then**
   it is removed from the list.
6. **Given** the count the kind allows is reached, **When** the control is shown, **Then** it
   takes no more and says the limit.
7. **Given** a person using the keyboard only, **When** they reach the control, **Then** they
   can choose a file, remove one, and hear its state.
8. **Given** no connection, **When** a file is chosen, **Then** it is not lost: it reads as
   not uploaded, with a way to try again.
9. **Given** a page that only shows files, **When** a file's row is shown, **Then** it has its
   name, its size, and a download control — and reads "being checked" or "refused" when it is
   not available.

---

### User Story 5 - A scanner that is down does not stop the clinic's work (Priority: P2)

When the scanner cannot answer, files are still accepted and can be attached and saved. They
read as "being checked" and cannot be downloaded. When the scanner returns, they are scanned
without anyone doing anything.

**Why this priority**: an outage of a checking service must not stop a receptionist attaching
a referral; and it must not become a door either.

**Independent Test**: stop the scanner, upload and save a file, see it listed as being
checked and refused for download; start the scanner and see it become available.

**Acceptance Scenarios**:

1. **Given** the scanner does not answer, **When** a file is uploaded, **Then** it is accepted
   and can be attached.
2. **Given** a file waiting for its scan, **When** anyone asks to download it, **Then** they
   are told it is still being checked.
3. **Given** files waiting for a scan, **When** the scanner returns, **Then** each is scanned
   and becomes available or refused, with nobody asking again.
4. **Given** files that have waited longer than an hour, **When** the platform's health is
   looked at, **Then** the people who run it are alerted.

---

### User Story 6 - A file is not kept longer than it should be (Priority: P2)

A file uploaded and never saved with anything is removed after a day. A file whose owner
removes it stops being reachable at once, can be brought back for thirty days in case it was a
mistake, and is then erased. And each kind of file has a retention period that the platform
applies — once the lawyer has given its value; until then nothing is removed for age.

**Why this priority**: files are personal data. Keeping what nobody asked to keep is a
liability; removing what the law says to keep is another. The engine must be able to do both
correctly before the values arrive.

**Independent Test**: leave an uploaded file unsaved for more than a day and see it gone;
remove an attached file, see it unreachable, restore it, remove it again and see it erased
after thirty days; set a retention period for a kind in a test and see only files past it
removed.

**Acceptance Scenarios**:

1. **Given** a file uploaded and not attached to anything, **When** a day has passed, **Then**
   it is removed, content and all.
2. **Given** an attached file, **When** the page that owns it removes it, **Then** nobody can
   download it from that moment, and it no longer counts towards what its record may hold.
3. **Given** a file removed less than thirty days ago, **When** the page that owns it restores
   it, **Then** it is on its record again as it was, downloadable by whoever may see the
   record — unless the record is full again, in which case the restoring is refused with that
   reason.
4. **Given** a file removed thirty days ago, **When** the daily clean-up runs, **Then** its
   content is erased and it can no longer be restored; what remains says that a file was
   there — its name, size, who uploaded it, who removed it and when — and no content.
5. **Given** a kind with no retention period set, **When** the clean-up runs, **Then** no file
   of that kind is removed for its age.
6. **Given** a kind with a retention period set, **When** the clean-up runs, **Then** files of
   that kind whose period started longer ago than it are removed, and the others are not.
6a. **Given** a file whose owner has not said its period started — a doctor's photo of a clinic
   still at work, an attachment of a ticket still open — **When** the clean-up runs, **Then**
   it is not removed for its age, whatever the period.
7. **Given** a file removed for its age, **When** someone asks for it, **Then** they are told
   it is no longer kept, not that it failed.
7a. **Given** a record whose files are on hold, **When** the clean-up runs, **Then** none of
   them is erased — not for age, not thirty days after its removal — and when the hold is
   lifted the rules apply again from the next clean-up.
8. **Given** a record that must never lose its file — a signed visit — **When** removal of its
   file is asked for, **Then** the owning page's refusal stands: the engine removes only what
   the owner says may go.

---

### User Story 7 - A later spec adds its kind of file without touching the engine (Priority: P3)

The spec that builds tickets says: a ticket attachment is PDF, PNG or JPG, up to 10 MB, three
for a message, and whoever sees the ticket may download it. It adds that as one row and
places the control. Storing, checking, scanning, downloading and removing are already there.

**Why this priority**: it is what makes this a foundation; its users come later.

**Independent Test**: declare a kind in a test, upload, attach, download and remove through
it, with no change to the engine.

**Acceptance Scenarios**:

1. **Given** a new kind in the table, **When** a file of it is uploaded, **Then** its types,
   size and count are enforced, and it is scanned, with no change to the engine.
2. **Given** a kind that is not in the table, **When** something tries to upload under it,
   **Then** it is refused.
3. **Given** the table, **When** the checks run, **Then** every kind has its accepted types,
   its size, its count, whether it is public, and the retention class it falls under; and a
   kind's key, once used, is never renamed or reused.

---

### Edge Cases

- **An empty file.** A file of no bytes is refused: there is nothing to check.
- **A name that is a trap.** A name with a path in it, control characters or five hundred
  letters is kept as a name to show, shortened where it is shown, and is never used as a
  place to store the file.
- **Two files with one name.** Both are kept; a name is not an identity.
- **The same file twice.** Uploaded twice, it is two files.
- **The connection drops mid-upload.** Nothing partial is kept; the row reads as not uploaded,
  with a way to try again.
- **The page is closed mid-upload, or after it.** What was uploaded and not saved is removed
  by the daily clean-up.
- **The record is saved while a file is still uploading.** The page does not allow it: saving
  waits for uploads to end.
- **The record's save fails.** The file stays unattached and unshared, and can be saved with
  the next try.
- **A file attached twice.** A file belongs to one record; attaching it to a second is
  refused.
- **A file found infected after it was attached.** It reads as refused on its record, cannot
  be downloaded, and its content is removed; the record itself is untouched; its uploader is
  told and the trail has its entry.
- **The uploader of an infected file has left.** An account that can no longer sign in is not
  told (spec 008); the trail still has its entry.
- **A removed file whose period runs out while it is removed.** It is erased at the earlier of
  the two: thirty days from its removal, or the end of its retention period.
- **A period that starts and is taken back.** A ticket closed and reopened: its attachments'
  period stops, and starts afresh at the next closing.
- **A removed file that was infected or refused.** There is nothing to restore: its content
  was removed when it was refused.
- **A file attached to a record that is on hold.** The hold covers it from that moment.
- **A removed file on a held record.** It still cannot be downloaded and can still be restored
  only within its thirty days; its content is kept until the hold is lifted.
- **A lab that only sends protected PDFs.** They are refused until sent without a password; the
  reason says so.
- **Many downloads of one file.** Each is its own entry in the trail; nothing is rolled into a
  count.
- **A scan that never answers.** The file stays "being checked" and is tried again; after an
  hour the people who run the platform are alerted.
- **A way in that leaks.** It expires in minutes, gives one file, and cannot be turned into a
  way to another file.
- **A file of a clinic that was cancelled.** It is removed with the clinic's data when that
  period ends (ق-٢٦); the engine can remove every file of a clinic.
- **A patient's record that is "deleted".** Deleting a patient hides the record (ق-١٤); its
  files stay until their retention period.
- **An image that is really a script.** SVG and other files that can carry code are not among
  any kind's accepted types.
- **Storage that is full or unreachable.** The upload is refused with a reason that says to
  try again; nothing is half-kept.

## Requirements *(mandatory)*

### Functional Requirements

**Kinds of file**

- **FR-001**: Every file MUST be of one kind from one table kept with the product. Each kind
  has: a key; the types it accepts; the largest size of one file; how many files one record or
  one message may hold; whether it is private or public; and the retention class it falls
  under (SH-095).
- **FR-002**: The table MUST hold now the kinds whose rules the BRD has settled: a ticket
  attachment (PDF, PNG, JPG; 10 MB; three for a message — ق-٢٠); a patient's file (PDF, JPG,
  PNG; 10 MB; any number — ق-٤٤); the signed copy of a consent (JPG, PNG, PDF; 10 MB; one —
  CL-PL-020); the report of a result (PDF, JPG, PNG; 10 MB; one); a doctor's photo (JPG, PNG;
  2 MB; one; public — CL-DM-034). A kind whose rules are open is added by the spec that
  settles them.
- **FR-003**: A kind's limits are the product's, the same for every clinic and every plan
  (Chapter 08 §8; Chapter 03 §8). There is no limit on how much a clinic stores (SH-015).
- **FR-004**: A key, once used, MUST NOT be renamed or reused; the checks MUST fail when the
  table changes a key that exists. Uploading under a kind that is not in the table MUST be
  refused.

**Taking a file in**

- **FR-005**: A file MUST be uploaded when it is chosen, before the record it is for is
  saved, and MUST show its progress (CL-TK-072).
- **FR-006**: An uploaded file MUST belong to the person who uploaded it and to their clinic,
  and to nobody else, until it is attached: no other person and no other clinic can read it,
  list it or attach it.
- **FR-007**: A file MUST be attached to its record together with the save that names it: if
  the save does not take effect, the file is not attached.
- **FR-008**: A file MUST belong to exactly one record, of the kind it was uploaded for.
  Attaching a file of another kind, another person's unattached file, or a file already
  attached MUST be refused.
- **FR-009**: Who may upload a file of a kind is decided by the page that owns the kind, with
  its own permission; with no permission named the upload is refused (Constitution III).
  Uploading MUST be refused in a read-only clinic.
- **FR-010**: The platform MUST keep, for each file: its kind; its clinic; the name it was
  uploaded with; its size; its type as read from its content; who uploaded it and when; the
  state of its scan; and the record it belongs to once attached (Chapter 08 §3-3).
- **FR-011**: A file's name MUST be kept as it was given, for showing and for downloading,
  and MUST NOT decide where or how the file is stored.

**Checking a file**

- **FR-012**: The platform MUST check, itself and for every upload whatever the browser did:
  that the file is not empty; that its size is within its kind's limit; that the count its
  kind allows is not passed (ق-٢٠, ق-٤٤, CL-TK-082).
- **FR-012a**: One person MUST NOT have more than thirty files uploaded and not yet attached at
  one time, nor upload more than sixty files in any ten minutes; an upload beyond either MUST
  be refused with a reason that says to try again later, and nothing of it is kept
  (clarified).
- **FR-013**: The type of a file MUST be read from its content, not from its name or from what
  the browser says, for every kind (ق-٣٦, clarified). A file whose content is not one of its
  kind's types MUST be refused.
- **FR-014**: Every file of every kind MUST be scanned for malware before it is available
  (ق-٣٦, CL-TK-084, clarified). The state of a file's scan is one of: being checked · clean ·
  refused (Chapter 08 §3-3).
- **FR-014a**: A file the scanner cannot look inside — protected by a password, or damaged —
  MUST be refused, as a file that could not be checked, and is treated from then on as any
  refused file is; its reason is kept apart from "malware was found" so that its uploader can
  be told what to do (clarified).
- **FR-015**: A file MUST NOT be given to anyone unless its scan is clean.
- **FR-016**: A file whose scan is refused MUST keep its row — so that its record says a file
  was refused — and MUST have its content removed.
- **FR-016a**: When a scan refuses a file, the person who uploaded it MUST be told in their
  bell, with the file's name and what it was for and nothing of its content, and an entry MUST
  be written to the audit trail under security, naming the file, its record and its uploader.
  No email is sent for it, and nothing a clinic can set stops either (clarified; specs 007 and
  008).
- **FR-017**: Each refusal MUST carry a reason a page can show in the person's language: the
  type is not accepted, with the types that are; the file is too large, with its size and the
  limit; the count is reached, with the limit; the file is empty; the file did not pass the
  check; the file could not be checked — send it without a password; too many uploads, try
  again later; the upload failed, try again.
- **FR-018**: When the scanner cannot answer, an upload MUST still be accepted and attachable;
  the file stays "being checked" and is scanned when the scanner returns, without anyone
  asking (clarified).
- **FR-019**: Files that have waited for a scan for more than an hour MUST raise an alert to
  the people who run the platform.

**Giving a file out**

- **FR-020**: A private file MUST be given only to a signed-in person whom the page that owns
  the kind says may see the record it belongs to; that question MUST be asked at every download
  (ق-٢٤, CL-TK-081, CL-TK-083). In this spec the people are a clinic's staff, and a file is
  never given to a staff member of another clinic. The support team reading a ticket's
  attachments (OP-B-085) and a patient reading their own documents are given files by the same
  question when their own sign-in exists (specs 010 and 070 onward); until then they are given
  nothing.
- **FR-021**: A kind whose owner gives no answer about who may see MUST be refused for
  everyone (Constitution III).
- **FR-022**: A download MUST go through a way in that is issued after that check, gives that
  one file, and stops working within five minutes; there MUST be no lasting public address for
  a private file (Chapter 08 §3-3, CL-PT-103).
- **FR-023**: A file MUST download under the name it was uploaded with (Chapter 08 §5-11).
- **FR-024**: A file of another clinic, an unattached file of another person, and a file that
  does not exist MUST all answer alike: not found (Constitution II).
- **FR-025**: Downloading MUST work in a read-only clinic (Chapter 08 §5; ق-٢٤).
- **FR-026**: Asking for a file that is being checked, that was refused, or that is no longer
  kept MUST answer with that reason, so the page can say it; none of them is an error of the
  platform.
- **FR-027**: A file of a public kind MUST be shown to anyone, without signing in. A kind is
  public only when what it holds is meant for everyone; nothing about a patient is ever of a
  public kind.
- **FR-028**: Files MUST be given in a way that cannot run in the reader's browser as part of
  the product: a file is downloaded or shown as the type the platform read, never as a page.
- **FR-028c**: A private file is given as a download only; this engine never opens one inside
  the product's own page. A PDF is accepted as it is: scripts or files it may carry are not
  looked for, beyond the malware scan (clarified).
- **FR-028a**: Every download of a private file MUST be written to the audit trail: who, which
  file by its name and kind, the record it belongs to, and when — at the moment the way in is
  given. A refused request writes nothing; a download of a public file writes nothing
  (clarified).
- **FR-028b**: A private file MUST be kept and given exactly as it was uploaded, byte for
  byte. An image of a public kind MUST have its hidden details — where, when and with what it
  was taken — removed before anyone can see it (clarified).

**Not keeping a file**

- **FR-029**: A file not attached within twenty-four hours of its upload MUST be removed,
  content and row (CL-TK-087).
- **FR-030**: The page that owns a file MUST be able to remove it. From that moment it MUST
  NOT be downloadable and MUST NOT count towards what its record may hold. Whether a file may
  be removed is the owning page's rule (CL-PT-105, CL-PT-106), not the engine's.
- **FR-030a**: A removed file MUST be restorable by the page that owns it for thirty days from
  its removal, to the record it was on and as it was; restoring is refused when the record
  already holds as many files as its kind allows. After thirty days its content MUST be erased
  and it can no longer be restored; its row MUST remain, saying a file was there, who removed
  it and when, and no more (clarified).
- **FR-030b**: The engine MUST keep who removed a file and when, and who restored it and when,
  and give the owning page what it needs to record both in the audit trail.
- **FR-031**: Each retention class MUST have a period that is a setting of the platform
  (SH-095). A file's period starts at a moment its owning page gives — the closing of its
  ticket, the cancelling of its clinic, the removal of its patient's record — never at its
  upload by default; until the owner gives that moment the period has not started. With no
  period set, or not started, no file is removed for its age. With one set and started, files
  whose period has run out MUST be erased once a day, removed or not: a retention period is not
  extended by the thirty days of a removal.
- **FR-031a**: The owning page MUST be able to start, and to take back, the retention period of
  all the files of one record in the save that causes it — a ticket closed and reopened.
- **FR-031b**: The owning page MUST be able to put the files of one record on hold and to lift
  the hold, in its own save. While a record is on hold nothing of its files is erased — not
  for age, not after the thirty days of a removal — including files attached while the hold
  lasts; removing, restoring within thirty days and downloading are unchanged. Who may set a
  hold is the owning page's rule; the engine keeps who set it, who lifted it and when
  (clarified).
- **FR-032**: The platform MUST be able to remove every file of one clinic, for when a
  cancelled clinic's data is erased (ق-٢٦).
- **FR-033**: Erasing a file's content MUST leave nothing of it that can be read where files
  are kept, in any earlier version of it there, or in a copy made for showing it. Backups of
  the platform are outside this: what they hold of an erased file goes when the backup itself
  expires, and an erased file is never brought back from one.

**The trail, logs and isolation**

- **FR-034**: Every file, its row and its content, MUST belong to one clinic and MUST NOT be
  reachable from another by any way of reading, listing or downloading; the isolation is
  enforced where the rows are kept, and the content is kept apart by clinic (SH-004, SH-104,
  Constitution II).
- **FR-035**: Attaching a file to a record, removing it and restoring it MUST be traceable to
  a person: the engine keeps who and when, and gives the owning page what it needs to record
  each in the audit trail with its own action (CL-PT-102, spec 007). Downloads and refused
  scans are recorded by the engine itself (FR-016a, FR-028a).
- **FR-036**: A file's name, its content and the ways in MUST NOT appear in operational logs
  (SH-091, SH-094).

**The upload control**

- **FR-037**: There MUST be one upload control in the shared library, as the design's "File
  Upload" draws it: a zone with a title and a hint, and the states Default, Hover, DragActive,
  Uploading, Error and Disabled, in Arabic and in English, in both directions.
- **FR-038**: The hint MUST say, before anything is chosen, the types and the size the kind
  takes; the control reads them from the kind and is not told them twice.
- **FR-039**: A file MUST be choosable by dropping it on the zone, by pressing the zone, and
  by the keyboard alone.
- **FR-040**: Each chosen file MUST have a row: its name, shortened when long; its size as
  people read it; while uploading, its progress in per cent; when refused, the reason; and a
  control to remove it or to try again (Chapter 03 §4-5, CL-TK-074).
- **FR-041**: The control MUST tell the page when uploads are in progress, so the page keeps
  its save disabled until they end (CL-TK-073).
- **FR-042**: A refusal MUST name the file and its reason and MUST leave the zone ready for
  another file; one refused file MUST NOT stop the others (the design's notes; Chapter 03
  §5-17).
- **FR-043**: When the count the kind allows is reached the control MUST take no more and
  MUST say the limit (CL-TK-077).
- **FR-044**: There MUST be one file row for pages that show files: name, size, a download
  control, and the words for "being checked", "refused" and "no longer kept" in place of the
  download when the file is not available. The design's "more" control holds what the owning
  page offers for that file — removing, restoring — and is absent when the page offers
  nothing.
- **FR-045**: Sizes and counts MUST be written with the digits of the language — Arabic-Indic
  in Arabic, Western in English — and the unit in words of the language; a file's name keeps
  its letters as written (SH-098).
- **FR-046**: Until a page uses it, the control and the row MUST be shown on a builders-only
  page that is not part of the product, where each state can be seen and laid beside its
  design.

### Key Entities

- **Kind of file**: an entry of the table — key, accepted types, size, count, private or
  public, retention class. Not data: it ships with the product.
- **File**: one uploaded file — its kind, clinic, name, size, type as read, who uploaded it
  and when, the state of its scan, the record it belongs to once attached, who removed it
  and when, and when its content was erased. Its content is kept apart from this row.
- **Retention period**: for a retention class, how long files of it are kept from the moment
  their owner says the period started; a setting of the platform, empty until the lawyer gives
  its value.
- **Way in**: what a person is given to download one file after the check — short-lived, for
  that file alone. Nothing of it is kept after it expires.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person attaches a 10 MB file and sees it accepted or refused within 15
  seconds on an ordinary office connection, not counting the scan.
- **SC-002**: With the scanner working, a file is available for download within one minute of
  its upload ending, for 95 in 100 files.
- **SC-003**: Of a set of files that are not what their names say — programs, scripts and
  archives renamed as PDF, PNG and JPG — every one is refused.
- **SC-004**: Of a set of files carrying a standard malware test signature, none is ever
  downloadable, by anyone, by any address.
- **SC-004a**: Of a set of PDFs protected by a password, none is ever downloadable, and each
  tells its uploader why.
- **SC-005**: Across every way of asking — by identity, by an expired way in, by another
  clinic's session, signed out — no private file is given to anyone who may not see its
  record: zero in the tests that try each.
- **SC-006**: A download starts within 2 seconds of being asked for.
- **SC-007**: With the scanner stopped, every upload is still accepted, none is downloadable,
  and within five minutes of the scanner returning all of them are scanned.
- **SC-008**: A file never attached is gone within 25 hours of its upload; a removed file is
  restorable on its thirtieth day and its content is gone by the end of its thirty-first.
- **SC-012**: For every download of a private file there is exactly one entry in the audit
  trail, and for every file a scan refused there is one entry and one notification to its
  uploader.
- **SC-013**: A photo of a public kind uploaded with a place and a device recorded in it is
  shown to visitors with neither; a private file downloads identical to what was uploaded.
- **SC-009**: A later spec adds a kind of file — its types, size, count, who may see — and its
  files are stored, checked, scanned, downloadable and removable with no change to the engine.
- **SC-010**: Each of the six states of the upload control, in Arabic and in English, matches
  its design when laid beside it.
- **SC-011**: A person using the keyboard only, or a screen reader, chooses a file, hears
  whether it was accepted, and removes it.

## Assumptions

- **A read-only clinic cannot be shown yet.** FR-009 and FR-025 say what happens in one:
  uploads refused, downloads working. No clinic can be read-only until the subscription specs
  (Phase 1 and Phase 8) build that state; until then neither can be tested, and the spec that
  builds the state adds the two tests. The engine is written so that the download asks nothing
  of the clinic's state and the upload is a changing request like any other.
- **A sixth kind for builders.** Beside the five kinds of FR-002 the table holds one that
  exists on a developer's machine and in tests only, so the feature can be walked for real
  before any page owns a kind. It is not part of the product.
- **No page of its own.** Tickets are spec 082, patient files 032, consents later, a doctor's
  photo 029. This spec gives them the engine and the control; nothing a clinic can open is
  added by it.
- **The same checks for every file** (clarified). The BRD writes the content check and the
  malware scan only for ticket attachments. **Owner item**: say the same in Chapter 03
  (ق-٤٤, CL-PT-097), Chapter 04, Chapter 10 and Chapter 11.
- **Accept and hold when the scanner is down** (clarified). The BRD is silent. **Owner item**:
  add it beside ق-٣٦.
- **The alert for files that wait too long** (FR-019) is, in this spec, a reading of the
  platform's health and a line in its logs; the alarm that reaches a person is wired with the
  platform's monitoring, which spec 001 has not built yet.
- **The support team and patients come later.** Operators sign in with spec 010 and patients
  with the patient-facing specs; each then answers the same question — may this person see the
  record — through the kind's owner. Nothing in the engine is specific to a clinic's staff
  except that a staff member never reaches another clinic's file.
- **Scripts inside a PDF are not looked for** (clarified). Finding them reliably means
  rewriting the file, which a private file must never be. What protects a reader: the malware
  scan, giving the file as a download only, and never opening it inside the product. **Owner
  item**: Chapter 03's "preview the first page of a PDF" is the patient-files spec's to design
  within that limit.
- **Thirty waiting, sixty in ten minutes** (clarified): figures far above ordinary work —
  three files for a message, a handful for a visit.
- **Backups.** An erased file may survive in a backup until that backup expires — thirty days
  for the daily ones and ninety for the weekly ones, as spec 001 sets them. **Owner item**:
  confirm with the lawyer that this is acceptable beside the retention periods.
- **A hold is the owning page's to offer** (clarified): this spec builds the mechanism; where
  a clinic or the support team sets one is designed with the page that owns the record.
- **Five minutes for a way in.** The BRD says "a temporary link" and no figure. Five minutes
  is long enough for a slow connection to start a download and short enough that a forwarded
  address is dead.
- **Twenty-four hours for a file never attached.** CL-TK-087 says "a short period the system
  sets". A day covers a form left open over a break and an evening.
- **A removed file can be brought back for thirty days, then is erased, and its row stays**
  (clarified). The BRD does not say whether removing an attachment hides or erases it.
  Restoring is offered to the owning page; where it puts the way to restore is that page's
  design. **Owner item**: add the thirty days to Chapter 03 beside CL-PT-106.
- **No retention values yet.** SH-095 is conditional on the lawyer and no value is fixed. The
  engine keeps a period per class and applies it; with none set nothing is removed for age.
  The page where an operator sets the periods is the operators' settings (spec 024). **Owner
  item**: the values, from the lawyer.
- **Retention classes** follow SH-095's list: the medical record, conversations, attachments
  of tickets, and a clinic's data after cancellation. A patient's files and a consent's copy
  fall under the medical record; a ticket attachment under attachments of tickets.
- **No storage limit and no storage counter.** SH-015 lists eight counted limits and storage
  is not one; the owner removed the "used of limit" counter (Q18). The total a clinic stores,
  shown to operators on the clinic's page, is that page's to compute.
- **WebP is not accepted** for patient files: the requirement names PDF, JPG and PNG; that an
  earlier build also took WebP is not a requirement.
- **"Image" means PNG and JPG** wherever a chapter says "PDF or an image" without naming
  formats, as the tickets chapter defines it.
- **Files that arrive from WhatsApp or Telegram** are stored by this engine when the channels
  are built (Phase 6); their sizes and types are the channel's own (ق-٤٩), declared as a kind
  by that spec.
- **The platform's own logo** is the operators' (spec 024): one public image with its own
  rules (ق-٩٣), declared as a kind there.
- **Showing a file** — the preview of an image, the first page of a PDF, the X-ray viewer — is
  the owning page's; the engine gives the file. It makes no thumbnails.
- **Every download of a private file is written to the audit trail** (clarified). The BRD
  records uploads (CL-PT-102) and exports and is silent on downloads of attachments; a file
  leaving the system is treated as an export is. Two actions join the audit catalogue the
  way spec 007 lays down: a file downloaded, and a file refused by the scan, both written by
  the engine; attaching, removing and restoring are the owning page's own actions. **Owner
  item**: add it to Chapter 09's list of what the activity log records.
- **A refused scan tells its uploader** (clarified) through a new event of the notifications
  table, in-app only, added the way spec 008 lays down.
- **The file's name is shown** in that notification and in the audit entries of a download and
  of a refused scan (decided by the owner, 11 Oct 2026). A name a person gave a file may hold a
  patient's name; it is clinic data shown to people of that clinic who could already see the
  file, and it never reaches an operational log or an email.
- **Hidden details of a public image are removed; a private file is never changed**
  (clarified). An X-ray, a signed copy and a screenshot are records and may be evidence.
- **A password-protected file is refused, not held** (clarified). **Owner item**: add it
  beside ق-٣٦, with the limit on uploads.
- **The words of the control** are the design's for the zone («إفلات الملف هنا أو النقر
  للاختيار», «جارٍ رفع الملف…», «تعذّر رفع الملف») and the tickets chapter's for the reasons.
  The messages for a file that did not pass the check, a failed download and a file no longer
  kept are not drawn (Chapter 08 §13-ب); this spec writes them and lists them for the page
  review. The two chapters word the same refusal differently on the clinic's and the
  operators' side; one wording per reason is used here.
- **«البرمجيّات الضارّة»** is the word used, as Chapter 08 writes it; Chapter 17 says
  «الخبيثة» for the same thing.
- **The file card** the design's notes mention ("File Attachment Cards") is located in the
  design file at planning; if it is not drawn, the row follows the upload dialog of Chapter 03
  §4-5 and is recorded as undrawn.
- **Where the content is kept** — the hosting of SH-090 — and which scanner is used are
  planning decisions.
