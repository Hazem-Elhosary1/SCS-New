# Contract: the emails of clinic onboarding

**Feature**: [../spec.md](../spec.md) · Built on spec 094's one template (`docs/adding-an-email.md`).

## The owner's invitation (exists, 004; unchanged)

- **Built by**: `AccountEmails.Invitation(to, language, address, clinicAr, clinicEn, validHours)`.
  It carries the button to the 72-hour link and names the sign-in address `app.scs-heta.com`.
- **Language**: the owner's preference. A new owner has none yet, so the account's preference is
  set to the clinic's `DefaultLanguage` when the owner is created (Arabic at creation, FR-005).
- **Sending**:
  - after the add's activation;
  - from «إعادة إرسال الدعوة»;
  - after an invited owner's email is changed (FR-021a). The new address gets a new link only
    when «إعادة إرسال الدعوة» is pressed: the change ends the old link, and the row offers the
    resend.
- **What the operator sees**: never the link (OP-A-027). The token is never logged or answered.

## The notice to the old address when a signed-in owner's email changes (new)

`AccountEmails.EmailChanged(to: old address, language, newAddressMasked, clinicAr, clinicEn)`.
The notice is sent once, after Identity's transaction commits. A failure is logged with the
exception type only, and the change stands (FR-021a).

- **The masked address** shows the first character of the local part and the whole domain, for
  example `m•••@example.com`. It is given left to right with Western digits.
- **The words are proposed**. The BRD does not word this email. They follow SH-098 (no gendered
  address, the passive form) and are raised with the owner.

| Part | Arabic | English |
|---|---|---|
| Subject | «تغيّر بريد الدخول لحساب مالك {clinicAr}» | "The sign-in email of the {clinicEn} owner account changed" |
| Eyebrow | «تنبيه أمني» | "Security notice" |
| Title | «تغيّر بريد الدخول» | "Your sign-in email changed" |
| Paragraph | «غيّر فريق {platform} بريد الدخول لحساب مالك {clinicAr} إلى {newAddressMasked}. لم يعد هذا البريد يصلح للدخول، وأُنهيت الجلسات المفتوحة.» | "The {platform} team changed the sign-in email of the {clinicEn} owner account to {newAddressMasked}. This address no longer signs in, and open sessions were ended." |
| Attention | «إن لم يكن التغيير مطلوبًا، يُتواصل مع فريق الدعم فورًا.» | "If this change was not requested, contact the support team right away." |
| Reason (footer) | «أُرسلت هذه الرسالة لأن بريد الدخول لهذا الحساب تغيّر.» | "You received this because this account's sign-in email changed." |

- **No button**: the old address can no longer sign in.
- **Samples**: Arabic and English are added to `IdentityEmailSamples` (`IEmailSamples`). The
  architecture test requires them. `npm run emails` and the Playwright `emails` tests include it.
