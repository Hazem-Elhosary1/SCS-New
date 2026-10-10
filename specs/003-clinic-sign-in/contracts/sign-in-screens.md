# Contract: the sign-in screens

Spec 003. Routes of the clinic app, their states, and which answer of the platform leads to
which state. Arabic wording is the BRD's (Volume 1, Chapter 01, Part B); English wording is
written at implementation and reviewed with it. Layout follows the design file "Smart Clinic OS
— Design System & Screens", sign-in section.

## Routes

| Route | Screen | Guard |
|---|---|---|
| `/sign-in` | Sign-in form | With a live session → `/` |
| `/sign-in/code` | Code step | Without a pending sign-in → `/sign-in` |
| `/` and every other route | The console (for now, the landing page) | Without a live session → `/sign-in` |

`/sign-in?ended=1` shows the "session ended" notice; the parameter is removed from the address
once read. Nothing else about the person is ever in the address.

## The frame (every screen)

Introduction panel on the end side: logo · «نظام تشغيل العيادة الذكي» · «بنية تحتية طبية رقمية
متكاملة» · «مواعيد عيادتك ومرضاها وسجلاتها الطبية وفواتيرها في مكان واحد، ومساعد حجز يردّ على
مرضاك عبر واتساب وتيليجرام.» No buttons. Form panel with a 400-wide form in its middle.
Language switch in the outer top corner. Footer: «تحتاج مساعدة؟ تواصل مع الدعم الفني» ·
«سياسة الخصوصية» · «شروط الاستخدام». On narrow screens the introduction panel is hidden.

No clinic name anywhere.

## Sign-in form — states

Title «تسجيل دخول فريق العيادة»; under it «أدخل البريد الإلكتروني وكلمة المرور المسجلة لعيادتك».
Fields: email (placeholder `clinic@example.com`, always left-to-right), password (with the eye).
Button «تسجيل الدخول». Link «نسيت كلمة المرور؟» (leads to `/forgot-password`, which shows the standard "not
available yet" state until spec 004). Fixed line «حساب
جديد؟ افتح الرابط في بريدك لتعيين كلمة المرور.»

| State | Shown when | Banner (tone) |
|---|---|---|
| Default | Opened | — |
| Session ended | `?ended=1`, or a request answered `SESSION_ENDED` | «انتهت الجلسة. الدخول من جديد للمتابعة.» (neutral) |
| Filled / Password shown | Typing; eye pressed | — |
| Validation | Submit with a field problem; no call | Under the field: «البريد مطلوب» · «البريد غير صحيح — مثل name@clinic.com» · «كلمة المرور مطلوبة» |
| Loading | Call in flight | Button «جارٍ تسجيل الدخول…», locked |
| Error | `INVALID_CREDENTIALS` | «البريد الإلكتروني أو كلمة المرور غير صحيحة.» (danger) |
| Request failed | No answer from the platform | «تعذّر الاتصال بالخادم. حاول مرة أخرى بعد قليل.» (danger) |
| Code not sent | `SIGN_IN_CODE_NOT_SENT` | «لم نستطع إرسال رمز التحقق إلى بريدك. حاول مرة أخرى بعد قليل.» (danger) |
| Account locked | `ACCOUNT_LOCKED` | «أُقفل الحساب بعد محاولات دخول فاشلة. يُفتح بعد الساعة ‹وقت الفتح›، أو فور إعادة تعيين كلمة المرور من «نسيت كلمة المرور؟».» (warning). The time is local, and does not break from its AM/PM mark |
| Account disabled | `ACCOUNT_DISABLED` | Says the account is disabled and that whoever manages users in the clinic can re-enable it (danger). Wording fixed at page review (B-O18) |
| Clinic suspended | `CLINIC_SUSPENDED` | Says the clinic is suspended and that contacting support is the only way; action «التواصل مع الدعم» opens the help window (danger). No sentence about an invoice |
| Network blocked | `NETWORK_BLOCKED` | «الدخول من هذه الشبكة محظور الآن. راجِع فريق الدعم لمعرفة السبب ورفع الحظر.» (warning) |
| Too many requests | `RATE_LIMITED` | Says when to try again, from `Retry-After` (warning). Not drawn; reuses the warning banner |

Rules: one banner at most; a banner and a field error never together; a banner clears when the
form is submitted again; both fields keep their content; the button is enabled except while
loading.

## Code step — states

Title «التحقق بخطوتين»; under it «أدخل الرمز المكوّن من ٦ أرقام المرسل إلى بريدك الإلكتروني
‹البريد›» (the email as typed). Six boxes, left-to-right, Western digits, in both languages;
typing moves on, Backspace moves back, pasting six digits fills all. Button «تأكيد الرمز». Link
«الرجوع لتسجيل الدخول» with a direction-following arrow.

| State | Shown when | What is shown |
|---|---|---|
| Code entry | Arrived from step one | Countdown «إعادة الإرسال خلال ‹د:ث›»; when it ends, «لم يصلك الرمز؟ إعادة الإرسال» |
| Code error | `CODE_INVALID` | Boxes red; «الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.» (danger); button enabled |
| Resend failed | `SIGN_IN_CODE_NOT_RESENT` | «لم نستطع إرسال رمز جديد. الرمز السابق يبقى صالحًا حتى تنتهي مدّته.» (danger); secondary button «إعادة إرسال الرمز» |
| Code accepted | 200 | Boxes green and locked; «الرمز صحيح، وسُجّل الدخول.» (success); button «جارٍ فتح الرئيسية…»; no back link; then `/` |
| Returned | `ACCOUNT_DISABLED` or `CLINIC_SUSPENDED` | Back to the sign-in form in that state |
| Too many requests | `RATE_LIMITED` | Warning banner with the time, on this screen |

| No more resends | `resendsLeft` is 0, or `RESEND_LIMIT_REACHED` | The countdown and the resend link are replaced by a note saying to go back and sign in again (neutral; wording at page review). «تأكيد الرمز» stays enabled: the last code still works |

`RESEND_TOO_EARLY` is not a state: the screen corrects its countdown from the answer.

The email shown lives in memory only. Reloading the code page returns to the sign-in form.

## Help window

Opened from the footer link on every screen and from the suspended banner. Title «تحتاج
مساعدة؟»; the hours line; three rows — «الخط الساخن», «واتساب», «البريد الإلكتروني» — each a
link (`tel:`, `https://wa.me/…`, `mailto:`) that leaves the application. × closes it and returns
focus to what opened it. Escape closes it. Values come from `GET /v1/meta/support`; if that
fails the window says so with the standard error state.

## Landing page (until spec 006)

Shows that the person is signed in (their email, the clinic's name in the current language) and
a neutral «تسجيل الخروج». Signing out goes to `/sign-in` in its default state.

## Session handling in the app

- At start the app asks `GET /v1/auth/session` once; the guard waits for the answer.
- `ACCESS_EXPIRED` on any call → one renewal (shared by all waiting calls) → the calls are
  repeated. Renewal refused → the app clears what is on screen and goes to `/sign-in?ended=1`.
- `SESSION_ENDED` on any call → the same.
- `UNAUTHENTICATED` → `/sign-in`, no notice.
- A second tab learns of sign-out or sign-in through a broadcast between tabs, so no tab keeps
  showing data after another signed out.

## Accessibility and languages

Every state exists in Arabic and English with mirrored layout. Banners are announced
(`role="alert"` for danger and warning, `role="status"` for neutral and success). Field errors
are tied to their fields. The code boxes are one labelled group. Everything is reachable and
operable by keyboard; focus starts on the email field, and on the first box of the code step.
