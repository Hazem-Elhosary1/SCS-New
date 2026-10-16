# Contract: the emails

**Feature**: 094-email-template

Every email the platform sends after this spec, with its words in both languages. `‹…›` is a
value. **(design)** marks words taken from the frame named; **(kept)** marks words the email
has today, reset in the template's parts; **(owner)** marks words drafted here for the owner
to confirm — every subject, and the short lines and titles the design does not draw.

When an email is sent, to whom, and what its code or link does are unchanged and are defined
by the spec that built it. `‹المنصّة›` / `‹platform›` is the platform's name from configuration.

The default reason line, used unless another is given: «وصلتك هذه الرسالة بسبب نشاط على حسابك
في ‹المنصّة›.» / "You're receiving this email because of activity on your ‹platform› account."
**(design)**

## A. The clinic's team (specs 003, 004)

### A1. `SignInCode` — the code after a correct password

Frames `2227:91` · `2227:71` **(design)**.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | رمز التحقق | Your verification code |
| Short line | رمز التحقق | VERIFICATION CODE |
| Title | تأكيد هويتك | Confirm it's you |
| Body | استخدم هذا الرمز لإكمال تسجيل الدخول إلى ‹المنصّة›. | Use this code to finish signing in to ‹platform›. |
| Code | ‹6 digits› | |
| Note | ينتهي هذا الرمز خلال ‹n› دقائق. | This code expires in ‹n› minutes. |
| Note | لم تحاول تسجيل الدخول؟ قد يكون شخص ما يعرف كلمة مرورك — غيّرها الآن. | Didn't try to sign in? Someone may know your password — change it now. |
| Note **(required by spec 010; added for the clinic)** | لن يطلب منك أحد من ‹المنصّة› هذا الرمز. | Nobody from ‹platform› will ask you for this code. |

No button, no link.

### A2. `PasswordReset` — a reset was asked for

Frames `2227:132` · `2227:111` **(design)**.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | إعادة تعيين كلمة المرور | Reset your password |
| Short line | إعادة تعيين كلمة المرور | PASSWORD RESET |
| Title | اختر كلمة مرور جديدة | Choose a new password |
| Body | وصلنا طلب لإعادة تعيين كلمة مرور حسابك في ‹المنصّة›. يعمل الرابط مرة واحدة وينتهي خلال ‹n› دقيقة. | We received a request to reset the password for your ‹platform› account. The link works once and expires in ‹n› minutes. |
| Button | إعادة تعيين كلمة المرور → ‹link› | Reset password → ‹link› |
| Note | لم تطلب ذلك؟ تجاهل هذه الرسالة — ستبقى كلمة مرورك كما هي. | Didn't ask for this? Ignore this email — your password stays the same. |

### A3. `PasswordChanged` — the password was changed

Not drawn.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | تم تغيير كلمة مرورك | Your password was changed |
| Short line **(owner)** | أمان الحساب | ACCOUNT SECURITY |
| Title **(owner)** | تم تغيير كلمة مرورك | Your password was changed |
| Body **(kept)** | غُيّرت كلمة مرور حسابك في ‹المنصّة›. | The password of your ‹platform› account was changed. |
| Facts | «الوقت»: ‹التاريخ والوقت، القاهرة› | "When": ‹date and time, Cairo› |
| Note **(kept)** | إن لم تكن أنت من غيّرها، تواصل مع فريق الدعم فورًا. | If you did not make this change, contact support at once. |

No button. The time is now written in Cairo time, as the consoles write it; today it is UTC.

### A4. `Invitation` — a team member is added with a link

Laid out as frames `2227:174` · `2227:153`; the body names the clinic, not its creation.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | دعوة إلى ‹العيادة› | Invitation to ‹clinic› |
| Short line **(design)** | دعوة | INVITATION |
| Title **(design)** | مرحبًا بك في ‹المنصّة› | Welcome to ‹platform› |
| Body **(owner, after the frame)** | أُنشئ لك حساب في ‹العيادة› على ‹المنصّة›. عيّن كلمة المرور لتسجيل الدخول — ينتهي الرابط خلال ‹n› ساعة. | An account was created for you at ‹clinic› on ‹platform›. Set your password to sign in — the link expires in ‹n› hours. |
| Button **(design)** | تعيين كلمة المرور → ‹link› | Set password → ‹link› |
| Note **(design)** | لم تكن تتوقع هذه الرسالة؟ يمكنك تجاهلها. | Weren't expecting this? You can ignore this email. |
| Reason **(owner, after the frame)** | وصلتك هذه الرسالة لأنه أُنشئ لك حساب في ‹العيادة› على ‹المنصّة›. | You're receiving this email because an account was created for you at ‹clinic› on ‹platform›. |

The clinic's name is its Arabic name in an Arabic email and its English name in an English one.

### A5. `AccountCreated` — an account whose password is handed over in person

Not drawn.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | أُنشئ حساب لك في ‹العيادة› | An account was created for you at ‹clinic› |
| Short line **(owner)** | حساب جديد | NEW ACCOUNT |
| Title **(owner)** | مرحبًا بك في ‹المنصّة› | Welcome to ‹platform› |
| Body **(kept)** | أُنشئ لك حساب في ‹العيادة› على ‹المنصّة›. | An account was created for you at ‹clinic› on ‹platform›. |
| Body **(kept)** | كلمة المرور يسلّمها لك من أنشأ الحساب، ولا تُرسل بالبريد. عند أول دخول يُطلب منك اختيار كلمة مرور جديدة. | The password is handed to you by whoever created the account; it is never sent by email. At first sign-in you choose a new one. |
| Button **(owner)** | تسجيل الدخول → ‹sign-in address› | Sign in → ‹sign-in address› |
| Reason | as A4 | as A4 |

### A6. `AccessReset` — a team member's access was reset

Not drawn.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | أُعيد ضبط الدخول إلى حسابك | Access to your account was reset |
| Short line **(owner)** | أمان الحساب | ACCOUNT SECURITY |
| Title **(owner)** | أُعيد ضبط الدخول إلى حسابك | Access to your account was reset |
| Body **(kept)** | أعاد أحد أعضاء فريق العيادة ضبط الدخول إلى حسابك في ‹المنصّة›، وكلمة المرور السابقة لم تعد تعمل. | Someone on the clinic's team reset access to your ‹platform› account; the earlier password no longer works. |
| Body **(kept)** | كلمة المرور المؤقتة يسلّمها لك من أعاد الضبط، ولا تُرسل بالبريد. عند أول دخول يُطلب منك اختيار كلمة مرور جديدة. | The temporary password is handed to you by whoever reset it; it is never sent by email. At first sign-in you choose a new one. |
| Button **(owner)** | تسجيل الدخول → ‹sign-in address› | Sign in → ‹sign-in address› |
| Note **(kept)** | إن لم يكن هذا متوقَّعًا، تواصل مع مسؤول العيادة أو فريق الدعم. | If this was not expected, contact the clinic's manager or support. |

## B. Notifications (spec 008)

### B1. `Notification` — a notification that is also sent by email

Laid out after frame `449:2` without its tinted box.

| | Arabic | English |
|---|---|---|
| Subject | ‹the notification's title›, cut at a word at 120 | the same |
| Short line **(owner)** | إشعار | NOTIFICATION |
| Title | ‹the notification's title› | |
| Body | ‹the notification's line› | |
| Facts, when the time is known | «الوقت»: ‹when› | "When": ‹when› |
| Button **(kept)** | فتح في ‹المنصّة› → ‹address› | Open in ‹platform› → ‹address› |
| Note **(kept)** | أُرسلت هذه الرسالة تلقائيًّا، ولا تُقرأ الردود عليها. | This message was sent automatically; replies to it are not read. |
| Reason **(owner)** | وصلتك هذه الرسالة لأن تنبيهات البريد مفعّلة لحسابك في ‹المنصّة›. | You're receiving this email because email notifications are on for your ‹platform› account. |

The title and the line are written by spec 008 for each event and are not changed here.

## C. Operators (spec 010)

### C1. `Code` — the code after a correct password, or to confirm a change of password

Frames as A1 **(design)**. For a sign-in: exactly A1, with "‹platform›" followed by nothing
more. For a change of password, the body and the second note differ:

| | Arabic | English |
|---|---|---|
| Subject, sign-in **(owner)** | رمز التحقق | Your verification code |
| Subject, change **(owner)** | رمز تأكيد تغيير كلمة المرور | Code to confirm your password change |
| Body, change **(owner)** | استخدم هذا الرمز لتأكيد تغيير كلمة مرورك في لوحة التشغيل. | Use this code to confirm the change of your Ops Console password. |
| Second note, change **(owner)** | لم تطلب تغيير كلمة المرور؟ لا تُدخل الرمز، وغيّر كلمة مرورك الآن. | Didn't ask to change your password? Don't enter the code, and change your password now. |

### C2. `ResetLink`

Exactly A2 **(design)**; the link opens the operators console.

### C3. `FirstPassword` — the first operator, invited by the deployment command

Laid out as frames `2227:216` · `2227:195`. The frame's body names who invited and in what
role; this account has neither until spec 011.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | دعوة إلى لوحة التشغيل | Invitation to the Ops Console |
| Short line **(design)** | دعوة | INVITATION |
| Title **(design)** | انضم إلى لوحة التشغيل | Join the Ops Console |
| Body **(owner, after the frame)** | أُضفت إلى لوحة التشغيل الخاصة بـ‹المنصّة›. عيّن كلمة المرور لتسجيل الدخول — ينتهي الرابط خلال ‹n› ساعة. | You were added to the ‹platform› Ops Console. Set your password to sign in — the link expires in ‹n› hours. |
| Button **(design)** | تعيين كلمة المرور → ‹link› | Set password → ‹link› |
| Note **(design)** | لم تكن تتوقع هذه الرسالة؟ يمكنك تجاهلها. | Weren't expecting this? You can ignore this email. |
| Reason **(design)** | وصلتك هذه الرسالة لأن أحد المسؤولين أضافك إلى لوحة التشغيل. | You're receiving this email because an administrator added you to the Ops Console. |

**Invited by an operator (spec 011, FR-032)** — the same email with the frame's own body, when the
account has an inviter and a role; everything else is as above.

| | Arabic | English |
|---|---|---|
| Body **(design)** | أضافك ‹الداعي› بدور ‹الدور› في لوحة التشغيل الخاصة بـ‹المنصّة›. عيّن كلمة المرور لتسجيل الدخول — ينتهي الرابط خلال ‹n› ساعة. | ‹inviter› added you as a ‹role› on the ‹platform› Ops Console. Set your password to sign in — the link expires in ‹n› hours. |

The role is named in the invited person's language (a system role) or as written (a custom
one). The frame's "30 minutes" is a sample value: an invitation lives 72 hours.

### C4. `NewBrowser` — a sign-in from a browser the account has not used

Not drawn.

| | Arabic | English |
|---|---|---|
| Subject **(owner)** | تسجيل دخول من متصفح جديد | New sign-in to your account |
| Short line **(owner)** | أمان الحساب | ACCOUNT SECURITY |
| Title **(owner)** | تسجيل دخول من متصفح جديد | New sign-in to your account |
| Body **(kept)** | سُجّل الدخول إلى حسابك في لوحة التشغيل من متصفح لم نره من قبل. | Your Ops Console account was signed in to from a browser we have not seen before. |
| Facts | «المتصفح»: ‹متصفح على نظام› · «العنوان»: ‹IP› · «الوقت»: ‹التاريخ والوقت، القاهرة› | "Browser": ‹browser on system› · "Address": ‹IP› · "When": ‹date and time, Cairo› |
| Note **(kept)** | إن كنت أنت، فلا شيء مطلوب. وإن لم تكن، أعد تعيين كلمة مرورك الآن من شاشة تسجيل الدخول: ذلك يُنهي كل الجلسات. | If this was you, nothing needs doing. If it was not, reset your password now from the sign-in screen: that ends every session. |

No button, no link — as today.

## D. Samples of kinds not sent yet

Built from the parts with the frames' own sample words, in the catalogue only. They prove the
parts are enough; the specs that send these emails write their real words.

| Sample | Frames | Parts used |
|---|---|---|
| `TicketNotification` | `449:2` · `455:2` | paragraph, attention (mark "Overdue", a line, a second line), button |
| `Invoice` | `2227:237` · `2227:268` | paragraph, facts (three rows, the last standing out), button, note |
| `SubscriptionNotice` | `2593:71` · `2593:183` | paragraph, facts (two rows, a rule, a total row, a status mark in the "bad" tone), note |

## E. What changes for someone who receives these today

- The clinic's emails arrive in one language, not two.
- Subjects are shorter: no second language and no "— Smart Clinic OS".
- The sender's name is the platform's name in the email's language.
- "Password changed" and the new-browser notice give their time in Cairo time.
- "Account created" and "access reset" gain a "sign in" button in place of an address written
  in a sentence.
- The clinic's sign-in code email gains the line that nobody from the platform will ask for
  the code, which the operators' already had.
- Everything else — who, when, the code, the link, how long it lives — is as it was.
