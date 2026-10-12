# Contract: the operators console's screens (spec 010)

Each screen, its frame in the design file `SetO9EhDivNcmurAMfcflY`, its words and its states.
Arabic is quoted from the frames; English is the twin frame's. Where the design draws nothing
the words are marked **(owner)**: taken from the BRD or the clinic's sign-in, to be confirmed.

## 1. Before sign-in — the split screen

Every screen before sign-in is 1440 × 900 in the design: the form in one half, a brand panel
in the other with the mark, a title and a line. In Arabic the form is on the right; in
English on the left. Below 900 wide the panel is dropped and the form is centred. The footer
of the form's half holds «سياسة الخصوصية» and the language control **(owner)**.

| Screen | Route | Frames (AR · EN) | Panel |
|---|---|---|---|
| Sign in | `/sign-in` | `128:85` · `128:54`; error `134:54` · `134:15` | «لوحة تشغيل SCS» / «إدارة كل عيادات المنصة، الباقات، التذاكر، والفريق من مكان واحد.» |
| Code | `/sign-in/code` | `135:61` · `135:38` | «التحقق بخطوتين» / «طبقة حماية إضافية لحساب التشغيل الخاص بك.» |
| Forgot password | `/forgot-password` | `141:53` · `141:38`; sent `158:52` · `158:38` | as sign in |
| New password from a reset | `/password/reset` | after `167:66` · `167:38` | as sign in |
| First password | `/password/first` | `167:66` · `167:38` | as sign in |

### 1.1 Sign in

- Title «تسجيل الدخول», line «ادخل ببيانات حساب التشغيل الخاص بك».
- «البريد الإلكتروني», «كلمة المرور» (with show/hide), «تذكرني», «نسيت كلمة المرور؟», button
  «تسجيل الدخول».
- Under a field: «البريد مطلوب» · «البريد غير صحيح — مثل name@company.com» · «كلمة المرور
  مطلوبة» **(owner)**.
- Banner between the line and the fields, danger, closable: «بيانات الدخول غير صحيحة» / «تأكد
  من البريد الإلكتروني وكلمة المرور وحاول مرة أخرى.»
- Other banners: suspended «هذا الحساب موقوف.» (danger); locked — the account is locked until
  ‹time›, and resetting the password opens it now (warning) **(owner)**; no connection «تعذّر
  الاتصال بالخادم. حاول مرة أخرى بعد قليل.»; code not sent **(owner)**; too many requests
  **(owner)**.
- A line above the form when the session was ended for a reason (`operator-sessions.md` §3),
  and after a password was saved: the password was changed / the account is ready **(owner)**.
- Working: the button is locked and reads «جارٍ تسجيل الدخول…».

### 1.2 Code

- Title «التحقق بخطوتين», line «أدخل الكود المكوّن من 6 أرقام المُرسل إلى ‹البريد›».
- Six boxes, digits only, left to right in both languages; paste fills them; the sixth digit
  sends.
- «يمكنك إعادة إرسال الكود خلال ‹د:ث›», then the control to send again **(owner: its words)**.
- Button «تحقق»; link «الرجوع لتسجيل الدخول».
- Errors under the boxes: wrong or expired, a new one can be asked for; tries spent — back to
  the sign-in form with a line; not sent again, the earlier code stays good **(owner)**.

### 1.3 Forgot password

- «نسيت كلمة المرور» / «أدخل بريدك الإلكتروني وسنرسل لك رابط إعادة التعيين»; «البريد
  الإلكتروني»; «إرسال رابط إعادة التعيين»; «→ الرجوع لتسجيل الدخول».
- Sent: «تحقق من بريدك الإلكتروني» / «أرسلنا رابط إعادة تعيين كلمة المرور إلى ‹البريد›» /
  «لم يصلك البريد الإلكتروني؟ إعادة الإرسال» / the way back. "Send again" is quiet for a
  minute after each sending.

### 1.4 Choosing a password — first, and from a reset

- First: «تعيين كلمة المرور»; a line that the operator was invited to «لوحة تشغيل SCS»,
  without a role until spec 011 **(owner)**; the email, shown and locked; «كلمة المرور
  الجديدة»; «تأكيد كلمة المرور»; the rules, each turning when met — «10 أحرف على الأقل» · «حرف
  كبير وحرف صغير» · «رقم واحد على الأقل» · «رمز خاص واحد على الأقل»; button «تفعيل الحساب».
- Reset: the same layout; title, line and button reworded for a new password **(owner)**.
- A link that no longer works: a state in place of the form. For a reset it offers a new
  link; for a first password it says to ask whoever sent it **(owner)**.
- The design's frame lists eight characters; ten is the rule (spec FR-029).

## 2. Inside — the frame

Design: "Navbar" (`278:2`), "Sidebar Nav Item" (`8:40`), and the frame around every screen of
"Template — Profile" (`1844:70`).

- **Top bar**: the mark and «SCS — لوحة التشغيل»; at the far side the operator's name with the
  role's name under it and a round mark with their initials that opens the account menu. No
  bell until its spec.
- **Account menu** (`1844:2699` · `1844:2875`): «الملف الشخصي», «تسجيل الخروج». Closes on
  Escape and on a press elsewhere; arrow keys move, Enter chooses.
- **Side bar**: «الرئيسية» only. The item being shown is marked. On the right in Arabic, the
  left in English. Under 1024 wide it is behind a control in the top bar.
- **Home** `/`: a greeting by name and one line that the console's sections appear here as
  they are built **(owner)**.
- **Not found**: inside the frame, with a way home **(owner)**.
- States of any page in the frame: loading (skeleton), error with "try again", no connection.

## 3. My profile — `/profile`

Frames `1849:2791` · `1850:2824`; two-factor required `2185:567` · `2185:460`.

- «الملف الشخصي» / «إدارة معلومات حسابك الشخصي وإعدادات الأمان.»
- Card «معلومات الحساب»: initials, name, role's name; the email over «البريد الإلكتروني»; the
  mobile over «رقم الهاتف» («—» when there is none); button «تغيير كلمة المرور».
- Card «الأمان والجلسات»: the switch «تفعيل التحقق بخطوتين (2FA) لحسابي», on and locked, with
  «مطلوب لكل المشغلين من الإعدادات ← الأمان، لذا لا يمكن إيقافه من هنا.»
- «الجلسات الأخيرة» with «جلسة واحدة فقط في كل مرة — تسجيل الدخول من جهاز آخر يُنهي هذه
  الجلسة.»; columns «الجهاز وعنوان IP», «الحالة», «الإجراء»; a row reads «Chrome على Windows»
  over the address; «نشط الآن» (a success mark) and «الجلسة الحالية», or «انتهت منذ ‹مدة›» and
  «—».
- Button «تسجيل الخروج من كل الأجهزة» (danger), centred under the list; a confirmation before
  it acts **(owner: its words)**.

### 3.1 Change password — a dialog

Frames open `1851:2003` · `1853:2115`, error `1852:2042` · `1853:2207`, success `1852:2169` ·
`1853:2334`; note `2185:674`.

- «تأكد من استخدام كلمة مرور قوية لحماية حسابك.»; «كلمة المرور الحالية»; «كلمة المرور
  الجديدة» with the hint «10 أحرف على الأقل، تتضمن حروفًا كبيرة وصغيرة ورقمًا ورمزًا.»;
  «تأكيد كلمة المرور الجديدة»; «إلغاء»; «حفظ كلمة المرور».
- Under the field: «كلمة المرور الحالية غير صحيحة.» · «يجب أن تختلف كلمة المرور الجديدة عن
  الحالية.» · «كلمتا المرور غير متطابقتين، يرجى المحاولة مرة أخرى.»; rules not met — the hint
  turns red.
- After «حفظ كلمة المرور» with a right current password and a valid new one, the dialog asks
  for the code sent by email: a line naming the address, the six boxes, the countdown and
  "send again", a button to confirm and «إلغاء» **(owner: its words)**. Not drawn; it reuses
  the parts of the code screen (§1.2). Closing the dialog abandons the change.
- Success, after the right code: «تم تغيير كلمة المرور بنجاح» / «يمكنك الآن استخدام كلمة المرور الجديدة لتسجيل
  الدخول.» / «تم».

## 4. The four emails

In the account's language only, in the look of the design's "Email Template" page (`447:2`),
with the platform's name in the subject. Their exact words are **(owner)**.

| Email | Holds |
|---|---|
| The code | the six digits, ten minutes, used once, "nobody from the platform will ask for it"; no link |
| Reset link | the link, thirty minutes, "ignore this if it was not you" |
| First password | who the platform is, the link, seventy-two hours |
| New browser | browser and system, address, time in Cairo time, "if this was not you, reset your password" |

## 5. Colour, direction, digits

- The operators console uses its own brand colour, as its design file does; the shared
  components take it from the tokens the application sets.
- Arabic reads right to left with Arabic-Indic digits; English left to right with Western
  digits. Emails, addresses, codes and device names keep their own direction.
- Every control is reachable and workable with the keyboard; errors are announced; the code
  boxes are one group named as a six-digit code.
