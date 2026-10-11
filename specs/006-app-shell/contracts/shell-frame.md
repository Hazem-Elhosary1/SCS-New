# Contract: The Console Frame

**Spec**: [../spec.md](../spec.md) | **Design**: Figma `4HLWEqlna9sbn68276LHYt`, page "✓ Home"
(`4160:3`) and the components pages. Frame ids are in the spec's "Design source" table.

## 1. Layout, top to bottom

| Part | Size | Shown |
|---|---|---|
| Environment strip | full width × 32 | every screen, when the environment is not production |
| Top bar | full width × 64 | after sign-in |
| Subscription banner | full width × 48 | when the subscription source gives a state |
| Side bar | 240 (72 folded) × the rest, on the reading side | after sign-in |
| Content | the rest; 32 side padding, 24 top | the page |

Minimum width of the frame 1024; under it the page scrolls sideways.

## 2. Top bar

Reading start → end: logo (36) · clinic name (page language) · divider · branch button (only
with more than one branch) · — · language pill · bell · divider · account chip.

| Control | Opens | Closed by |
|---|---|---|
| Branch button | menu 240 wide under it, start-aligned: «كل الفروع», the person's branches; the active one marked | choosing, outside press, Escape |
| Language pill «عربي · EN» | nothing; the inactive half switches | — |
| Bell | panel 360 wide under it at the far edge | outside press, Escape, opening an item |
| Account chip (mark, name or email, role) | menu 240 wide under it, far-aligned | choosing, outside press, Escape |

Opening one closes any other. The opener is drawn pressed and carries `aria-expanded`. Escape
returns focus to the opener.

## 3. Side bar

Groups and items in this order; an item is present only for a person who may open it:

| Group | Item | Children |
|---|---|---|
| القائمة · Main | الرئيسية · Home | |
| | الحجوزات · Bookings | |
| | المرضى · Patients | |
| | الفواتير والتحصيل · Invoices & collection | |
| | الصندوق الوارد · Inbox (count) | |
| | التذاكر · Tickets | |
| | التقارير · Reports | التقارير المالية · التقارير الطبية · سجل الأنشطة والأمان · الملخص الأسبوعي · تقييمات المرضى |
| إدارة العيادة · Clinic management | الإدارة · Administration | المستخدمون · الأدوار والصلاحيات · الرخص والتغطية |
| | إدارة البيانات · Data management | الأطباء · الفروع · الخدمات والأسعار · نصوص الموافقة |
| | الإعدادات · Settings | |
| | معمل الأسنان · Dental lab (dental clinics) | |

Addresses and permissions: [research.md](../research.md) R2.

- Active item marked; its parent open. A parent toggles on press; one parent is open at a
  time; a parent closed over the open page carries the mark.
- Fold button at the top, beside the first group's name, with a hairline (owner decision; the
  frames draw it at the foot). Folded: icons with tooltips; a parent's icon unfolds with it open;
  other icons navigate. The inbox count is not shown folded.
- Folded state: the saved choice, else folded under 1280 wide.

## 4. Account menu

«الملف والاشتراك» · «ملف الطبيب» (linked accounts only) · separator · «تسجيل الخروج» (ordinary
item, no confirmation; ends this session, shows the plain sign-in form).

## 5. Notifications panel

Heading «الإشعارات». States:

| State | Shows |
|---|---|
| Empty | bell mark, «لا إشعارات جديدة», «سيظهر هنا ما يخصّك من تذاكر وفواتير واشتراك.»; no «تعليم الكل كمقروء»; no dot on the bell |
| Items | «تعليم الكل كمقروء» in the heading; newest first; unread tinted; kind icon, title, text (two lines at most), relative time; scrolls inside the panel; dot on the bell while one is unread |
| Loading | skeleton rows |
| Failed | an error line and «إعادة المحاولة» inside the panel |

## 6. Banners

| Banner | Tone | Arabic | English | Action | Closes |
|---|---|---|---|---|---|
| Trial | info | تبقّى ‹ن› أيام على انتهاء الفترة التجريبية | ‹n› days left in your trial | اختيار باقة · Choose a plan | until tomorrow |
| Expiring | warning | ينتهي اشتراك العيادة خلال ‹ن› أيام | Your clinic subscription ends in ‹n› days | تجديد الاشتراك · Renew subscription | until tomorrow |
| Grace | danger | انتهى اشتراكك — أمامك ‹ن› أيام قبل إقفال التعديل | Your subscription has ended — ‹n› days before editing locks | تجديد الاشتراك · Renew subscription | until tomorrow |
| Read-only | neutral | الحساب للقراءة فقط حتى يُجدَّد الاشتراك | The account is read-only until the subscription is renewed | تجديد الاشتراك · Renew subscription | never |
| Offline (inside the content, under the page header) | warning | انقطع الاتصال بالإنترنت — ما تراه آخر نسخة محمَّلة، وما يغيّر البيانات معطَّل حتى يعود الاتصال. | You are offline — this is the last loaded copy, and anything that changes data is disabled until the connection returns. | — | by itself |
| Environment strip | — | بيئة اختبار — البيانات ليست حقيقية | Test environment — data is not real | — | never |

The action is absent for a person without `subscription.view`. One subscription banner at a
time; it sits above the offline alert.

## 7. Pages the frame supplies

| Page | When | Shows |
|---|---|---|
| Not available yet | a destination with no page, opened by a person who may open it | page header with the destination's name; a card saying the page is not available yet |
| Not in your role | spec 005 | as spec 005, now inside the frame |
| Not in your plan | the plan source excludes the destination | page header; a card naming the plan as the reason with a link to «الملف والاشتراك» |
| Not found | an address that is no destination | a card with a link to «الرئيسية» |
| Platform status (/dev/platform) | outside production only | the panel of spec 001, moved from the landing page |

## 8. What a later spec does

| To | Do |
|---|---|
| add a destination's page | register its component for the destination's key; nothing in the frame changes |
| fill the bell, branches, subscription state, plan, inbox count, clinic traits | provide the source ([data-model.md](../data-model.md) §3) |
| make a control obey "offline" (and later "read-only") | put `scsWrite` on it |
| tell the person something happened | `ToastService` |

## 9. States to picture (Playwright, both languages, 1440×900)

Default (owner) · limited permissions · folded · folded with a group opened · account menu ·
language switched · bell empty · bell with items · bell failed · branch menu · four banners ·
offline · environment strip · not available yet · not found · not in plan · roles list (owner,
view only, loading, failed, no custom roles) · sample list (default, filter open, filtered, no
results, empty, error, row menu, toast, confirmation).
