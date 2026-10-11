# Contract: The Two States on Screen

**Spec**: [../spec.md](../spec.md) | **Design**: Figma file `4HLWEqlna9sbn68276LHYt`, page
"✓ Administration › Roles & permissions" (`5803:4`)

## 1. Frames

| State | Arabic (RTL) | English (LTR) |
|---|---|---|
| No permission | "Roles / No Access / RTL" `6675:20818` | "Roles / No Access / LTR" `6675:21209` |
| View only | "Roles / Roles List Read Only / RTL" `6675:19376` | "Roles / Roles List Read Only / LTR" `6675:20097` |

Built from the frames: the content area — breadcrumb, page title, and the card or the notice.
**Not built here**: the top bar and side bar around them (spec 006); the roles list under the
notice (spec 027); the «مقترحة / Proposed» tag beside the title, taken as a note of the design
file. Each is recorded in `validation.md` as not compared.

## 2. Shared parts

| Part | Where | What |
|---|---|---|
| `scs-no-access` | ui library | a card: round mark in the danger tint with the lock icon; title; text. Inputs: the permission's name and the role's name, already in the page's language |
| `scs-state` with `state="view-only"` | ui library | the one «للعرض فقط» notice: `scs-banner` with tone `info` (the information tint and border, the info icon), the title, and under it the line passed as `message`; `role="status"`. The page's content is projected under it |
| `*scsCan="'code'"` | core | renders its content only for a holder; otherwise nothing is in the page — not a disabled control |
| `holds('code')` | core | route guard (`canMatch`) |
| `AccessService` | core | `holds(code)`, `role()`, `nameOf(code)`, `refresh()` |

## 3. Wording

| Key | Arabic | English |
|---|---|---|
| no-access title | هذه الصفحة ليست في دورك | This page is not in your role |
| no-access text | تُفتح لمن يملك «{permission}»، ودورك «{role}» لا يحملها. وما يلزم منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة. | It opens for holders of "{permission}", and your role "{role}" does not carry it. Whatever is needed from it is requested from whoever manages roles & permissions in the clinic. |
| no-access text, no role | تُفتح لمن يملك «{permission}»، وحسابك لا يحمل دورًا بعد. وما يلزم منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة. | It opens for holders of "{permission}", and your account does not hold a role yet. Whatever is needed from it is requested from whoever manages roles & permissions in the clinic. |
| view-only title | للعرض فقط | View only |
| view-only text (roles page) | التعديل يحتاج صلاحية «{permission}». | Changing roles needs "{permission}". |
| breadcrumb | الإدارة › الأدوار والصلاحيات | Administration › Roles & permissions |
| page title | الأدوار والصلاحيات | Roles & permissions |

The first five rows of the first two states are quoted from the frames. The "no role" line is
not drawn: it is this spec's wording for an account without a role, for the owner to confirm.
The view-only text is the page's own: each later destination supplies its sentence.

`{permission}` is the catalogue's name in the page's language. `{role}` is the role's name as
stored, or the name for its ready key when it was never renamed. Both are inside the sentence
as text, so assistive technology reads them.

## 4. The roles destination until spec 027

Address: `/administration/roles`.

| Person | Sees |
|---|---|
| not signed in | the sign-in form (spec 003) |
| temporary password | the change-password page (spec 004) |
| no `admin.roles.view` | breadcrumb, title, `scs-no-access` naming «عرض الأدوار والصلاحيات» and their role. No request for roles is made |
| `admin.roles.view` without `admin.roles.manage` | breadcrumb, title, the «للعرض فقط» notice naming «إدارة الأدوار والصلاحيات», then the "not available yet" panel |
| both | breadcrumb, title, the "not available yet" panel |

For the last two the page calls `GET /v1/roles`. If the server answers `PERMISSION_REQUIRED`
(the role changed a moment ago), the session is read again and the address is matched again:
the person now sees the no-permission state.

## 5. Behaviour

- What a person may do is read at sign-in, after any `PERMISSION_REQUIRED`, and every 10
  minutes while a page is open.
- A page shown under a stale list does no harm: the server refuses, and the refusal corrects
  the list.
- «الرئيسية» (`/`) needs no permission.
- Both states mirror with the language; long names wrap inside the card.
- Test ids: `no-access`, `no-access-title`, `no-access-text`, `view-only`, `roles-not-yet`.

## 6. States to picture (Playwright, both languages)

1. No permission, role named.
2. No permission, account without a role.
3. View only above the panel.
4. Full access (no notice).
5. No permission after a refusal by the server (list was stale).
