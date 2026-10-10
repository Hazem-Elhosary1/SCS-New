# Contract: password screens

Clinic app. All screens use `scs-auth-frame` of spec 003 (panel, language switch, footer with the
help link). Arabic wording is the BRD's; English is its translation. Wording marked † is not
drawn and is fixed at the page review (B-O19).

## Routes

| Route | Guard | Shows |
|---|---|---|
| `/forgot-password` | none | Request form → result |
| `/set-password#t=<token>` | none | Loading → reset form / new-account form / link no longer works |
| `/change-password` | signed in **and** must change | Temporary-password form |
| `/sign-in` | as spec 003 | Gains two success banners, handed over in memory (never in the address) |

`sessionGuard` sends a signed-in person who must change to `/change-password` from any other
page; a person who need not is sent away from `/change-password` to `/`. `/set-password` ignores
any existing session and does not end it. Without a token it shows the dead reset-link screen.

## States

| # | Screen | State | What shows |
|---|---|---|---|
| 1 | Request | form | «إعادة تعيين كلمة المرور» · «أدخل بريدك الإلكتروني وسنرسل لك رابط إعادة تعيين كلمة المرور» · email · «إرسال رابط إعادة التعيين» · «الرجوع لتسجيل الدخول» |
| 2 | Request | field error | «البريد مطلوب» / «البريد غير صحيح — مثل name@clinic.com» under the field; no banner; no call |
| 3 | Request | loading | Button busy, field locked |
| 4 | Request | sent | «أُرسل الطلب» · «إن كان ‹البريد› مسجَّلًا في العيادة فسيصله خلال دقائق رابطٌ لإعادة تعيين كلمة المرور.» · «الرجوع لتسجيل الدخول» |
| 5 | Request | too many requests | Warning banner with the wait, as on sign-in † |
| 6 | Request | unreachable | Danger banner, as on sign-in |
| 7 | Set | loading | Frame with a busy indicator while the link is inspected |
| 8 | Set | reset form | «تعيين كلمة مرور جديدة» · «لحساب ‹البريد›» · «كلمة المرور الجديدة» with the eye · four rules · «تعيين كلمة المرور الجديدة» |
| 9 | Set | new-account form | «مرحبًا بك في ‹اسم العيادة›» · «اختر كلمة مرور لحسابك ‹البريد›» · field · four rules · «الرابط صالح ٧٢ ساعة من إرساله.» · «تفعيل الحساب» |
| 10 | Set | rule refused | The unmet rules red; no error under the field; button enabled |
| 11 | Set (reset) | same password | Message under the field † |
| 12 | Set | submitting / too many / unreachable | As 3, 5, 6 |
| 13 | Set | reset link dead | «انتهت صلاحية الرابط أو استُعمل» · «رابط إعادة تعيين كلمة المرور يعمل مرة واحدة ولمدة محدودة.» · «طلب رابط جديد» → `/forgot-password` · «الرجوع لتسجيل الدخول» |
| 14 | Set | new-account link dead | Same title · «رابط الحساب الجديد يعمل مرة واحدة ولمدة محدودة. إرسال رابط آخر بيد من أرسل الأول.» · no button · «الرجوع لتسجيل الدخول» |
| 15 | Change | form | Title saying the password is temporary † · «لحساب ‹البريد›» · field · four rules · one button † · sign-out link |
| 16 | Change | rule refused / same password / submitting / unreachable | As 10, 11, 3, 6 |
| 17 | Sign-in | reset done | Success banner «غُيّرت كلمة المرور» / «الدخول الآن بكلمة المرور الجديدة.» |
| 18 | Sign-in | account activated | Success banner «فُعِّل الحساب» / «الدخول الآن بكلمة المرور الجديدة.» |

The four rules, always in this order: «١٠ أحرف على الأقل» · «حرف كبير وحرف صغير (A-z)» · «رقم
واحد على الأقل (0-9)» · «رمز خاص واحد على الأقل (!@#$%)».

## Behaviour

- **Rules** (`scs-password-rules`): neutral before typing; a rule turns green as soon as the
  text meets it; unmet rules turn red on submit and return to neutral/green as the person types
  again. State is announced to assistive technology, not carried by colour alone.
- **Submit with an unmet rule**: no call is made. The server's own refusal (rules or
  `SAME_AS_CURRENT`) is shown the same way if it ever arrives.
- **One field** with the eye; never a confirm field.
- **Token**: read from the fragment once, removed from the address immediately, held in memory;
  reloading the page therefore shows state 13. It is never put in storage, a query string or a
  log.
- **`410 LINK_INVALID`** at inspection or on submit: state 13 or 14 by the `kind` in the answer.
- **After success** on `/set-password`: navigate to `/sign-in`; the banner (17 or 18) and the
  account's email are handed to the sign-in form in memory, once, so a reload shows the plain
  form. The email field is prefilled.
- **The platform cannot be asked about a link** (outage, or too many requests): a banner and a
  "try again" button. The link is kept in memory and is not shown as dead.
- **After success** on `/change-password`: refresh the session and go to `/`.
- **`403 PASSWORD_CHANGE_REQUIRED`** from any call: go to `/change-password`.
- **Clinic name**: only in state 9, in the current language.
- **Language switch, help window, footer links**: as spec 003, on every state.
- **Focus**: on the first field when a form appears; on the title when a result or dead-link
  screen appears.
