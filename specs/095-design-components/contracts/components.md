# Contract: The Components (spec 095)

For each component: the design nodes it is built from (operators' file, English twin / Arabic
twin; the clinic file's set when shared), where it lives, the existing part it becomes (FR-006),
its options as a screen sets them, and its keys. Every component also follows
`conventions.md`. Node ids were read on 16 Oct 2026.

`ui` = `src/web/projects/ui/src/lib/`; `ops` = `src/web/projects/ops/src/app/components/`.

## Actions and inputs

| Component | Design (ops EN / AR · clinic) | Where | Becomes | Options | Keys |
|---|---|---|---|---|---|
| Button | `31:302` / `316:470`; icon parts `32:3`, `32:9`, `32:14`, `32:19` · `4252:2227` | `ui/controls/` | `button[scsButton]`, `[scsLink]` | `scsButton`: `primary` · `secondary` · `tertiary` · `ghost` · `link` · `destructive`; `size`: `sm` · `md` · `lg`; `busy`; `disabled`; leading or trailing `scs-icon`, or icon only with `label` | Enter, Space |
| Checkbox | `43:36` / `297:34` · `4252:58206` | `ui/controls/form-controls.ts` | `scs-checkbox` | `checked`; `indeterminate`; `disabled` with `reason`; `error` | Space |
| Radio | `43:66` / `298:29` · `4252:58465` | `ui/controls/radio.component.ts` | new: `scs-radio-group`, `scs-radio` | `value`; options; `disabled` with `reason`; `error` | arrows move and choose; Tab leaves the group |
| Switch | `43:102` / `297:96` · `4252:58656` | `ui/controls/form-controls.ts` | `scs-switch` | `on`; `disabled` with `reason`; `busy` (Loading) | Space, Enter |
| Slider | `55:58` / `300:56` · `4260:809` | `ui/controls/slider.component.ts` | new: `scs-slider` | `min`, `max`, `step`; `value` or `range` (`[start, end]`); `disabled` | arrows, Page Up/Down, Home, End per thumb |
| Input | `54:257` / `304:257` · `4252:56552` | `ui/field/` | `scs-text-field` | `size`: `sm` · `md` · `lg`; `state`: `warning` · `success` with `message`; `error`; `readonly`; `disabled`; `busy`; leading icon | as a text box |
| Textarea | `146:95` / `304:369` · `4272:885` | `ui/controls/form-controls.ts` | `scs-textarea` | `size`; `max` with count; `error`; `readonly`; `disabled` | as a text box |
| File Upload | `157:72` / `304:440` · `4260:938` | `ui/files/` | `scs-file-upload`, `scs-file-card`, `scs-file-field` | the drawn states: default, hover, drag over, uploading (progress), success, error | Enter, Space open the picker |
| Select and its list | `57:43` / `303:40`; list `58:3`, multi `58:14`, search empty `58:30`, results `2224:70` · `4272:1534`, `4973:2602` | `ui/controls/select.component.ts` | `scs-select` (spec 011) | `options` with `disabled` and `reason`; `multiple` with chips; `searchable`; `error`; `disabled`; `placeholder` | combobox: Down/Up open and move, Home, End, Enter, Space, Escape, typed letters |
| Segmented control | `385:11` / `386:19`; track `385:12`, `386:20` · `4260:1432`, `4260:1448` | `ui/controls/segmented.component.ts` | new: `scs-segmented` | options; `value`; `disabled` per option | arrows move and choose |
| Date field and calendar | `74:53` / `308:52`; calendar `75:4`, range `76:3` · `4256:824`, `4256:1424`, `4256:885` | `ui/date/` | new: `scs-date-field`, `scs-calendar` | `value` (a day) or `range`; `min`, `max`; `error`; `disabled` | field: Down or Enter opens; calendar: arrows by day and week, Page Up/Down by month, Home/End to the week's edges, Enter chooses, Escape closes |
| Date range field | `392:53` / `393:112`; calendar `393:2` | `ops/date-range/` | new: `ops-date-range-field` (on `scs-calendar`) | `range`; presets as drawn | as the date field |
| Time field and list | `77:53` / `308:103`; list `78:4`, wheel `78:16` · `4256:1542`, `4256:1480` | `ui/date/` | new: `scs-time-field` | `value`; `step` (minutes); `mode`: `list` · `wheel` | Down opens; arrows, Home, End, Enter, Escape |
| Date & time field | `79:71` / `308:172`; panel `80:4` | `ops/date-time/` | new: `ops-date-time-field` | `value` | as both fields, Tab between the two sides |

## Display

| Component | Design (ops EN / AR · clinic) | Where | Becomes | Options |
|---|---|---|---|---|
| Avatar | `294:131` / `8:111` · Clinic `Avatar` (24–56) | `ui/avatar/` | new: `scs-avatar` | `size`: `sm` · `md` · `lg`; `name` (initials) or `src`; a group (`ops/media/`) |
| Badge | `294:147` / `8:86` · `4253:53652` | `ui/badge/` | `scs-status-mark` becomes `scs-badge` (the old selector kept as an alias until the clinic moves) | `intent`: `success` · `warning` · `danger` · `info` · `neutral` (+ `brand`, `solid`, `size` for the clinic) |
| Tag | `281:64` / `313:63` · `4253:53827` | `ui/tag/` | `scs-applied-chips` items use it | `intent`: `default` · `success` · `warning` · `neutral`; `dismissible` with its label |
| Card | `281:27` / `313:26` · `4252:61428` | `ui/list/page-parts.ts` | `scs-card` (owner: its look stays) | `state`: `default` · `hover` · `selected`; `interactive` |
| Accordion | `281:77` / `313:76` · `4260:1383` | `ui/accordion/` | new: `scs-accordion`, `scs-accordion-item` | `expanded` | Enter, Space toggle |
| Divider | `281:88` / `313:87` | `ops/divider/` | new: `ops-divider` | `label` |
| Stat | `281:107` / `313:106` | `ops/stat/` | new: `ops-stat` | `trend`: `positive` · `negative` · `neutral`; value; note |
| KPI Card | `294:315` / `8:135` · `4267:1175` | `ui/list/page-parts.ts` | `scs-kpi-card` | `trend`: `positive` · `negative` · `neutral` (+ `none` for the clinic) |
| Usage meter | `387:24` / `388:23` · Clinic `Plan Usage Meter` | `ui/usage-meter/` | new: `scs-usage-meter` | `level`: `normal` · `warning` · `critical` (clinic: `exceeded`); used, limit |
| Table | row `273:52` / `309:166`; example `273:55` · `4267:903`, `4267:1004`, `4267:744` | `ui/list/` | the list's table classes and `scs-table-footer` | row `state`: `default` · `hover` · `selected`; header; footer (CL-SH-051) |
| Empty state | `374:35` / `374:68` · `4252:60047` | `ui/state/` | `scs-empty-state`, `scs-state` | `type`: `empty` · `error` · `loading` · `success`; action |
| Image | `82:20` / `314:19` | `ops/media/` | new: `ops-image` | `state`: `default` · `loading` · `error` |
| Video player, carousel, gallery tile, avatar group | `84:15`, `84:26`, `84:39`…`84:67`, `84:3` | `ops/media/` | new | as drawn |
| Sparkline | `394:15` / `395:14` | `ops/chart/` | new: `ops-sparkline` | `trend`: `up` · `down` · `flat`; values |
| Bar chart | `394:16` / `395:15` · `5601:4517` | `ui/chart/` | new: `scs-bar-chart` | values with labels; hover shows the value (Default, Hover) |
| AI badge | `399:13` / `408:21` | `ops/ai/` | new: `ops-ai-badge` | `size`: `sm` · `md` |
| AI insight card | `401:19` / `408:39` · Clinic `Insight Card` | `ui/ai/` | new: `scs-insight-card` | `type`: `insight` · `suggestion`; the suggestion card's loading, none, AI off, error |
| Message bubble | `404:10` / `409:29` · Clinic `Chat Bubble` | `ui/ai/` | new: `scs-message-bubble` | `sender`: `bot` · `user`; media |
| Ask-AI button and field, chat widget | `402:3`, `402:6`, `405:2` / `409:12`, `409:15`, `411:11` | `ops/ai/` | new | as drawn; example data only |

## Feedback and overlays

| Component | Design (ops EN / AR · clinic) | Where | Becomes | Options | Keys |
|---|---|---|---|---|---|
| Spinner | `289:27` / `314:54` · `4252:59832` | `ui/loading/` | new: `scs-spinner` | `size`: `sm` · `md` · `lg` · `xl`; `label` | — |
| Progress bar | `289:38` / `314:65` · `4252:59743` | `ui/loading/` | new: `scs-progress` | `value` or indeterminate | — |
| Skeleton | `289:48` / `314:75` · `4252:59877` | `ui/list/` | `scs-skeleton` | line widths 280 · 220 · 140; composed | — |
| Alert | `60:46` / `257:71` · `4253:54128` | `ui/controls/controls.ts`, `ui/overlay/` | `scs-banner`, the toast of `scs-toast-outlet` | `style`: `banner` · `toast`; `intent`: `info` · `success` · `warning` · `error`; closable | Escape closes a toast that has focus |
| Modal | `62:57` / `309:116` · `4252:61695` | `ui/dialog/` | `scs-dialog`, `scs-confirm` (owner: its look stays) | `size`: `sm` · `md` · `lg` · `full` | Tab trapped; Escape closes |
| Tooltip | `383:23` / `384:22` · `4252:59606` | `ui/overlay/` | `[scsTooltip]`, `scs-tooltip-bubble` | `placement`: `top` · `bottom` · `start` · `end` | shown on focus and hover; Escape hides |
| Menu | item `368:23` / `370:38`; panel `368:26`; row menus `2201:86`; icons `2200:86` · `4252:61814`, `4252:61893` | `ui/overlay/` | `[scsMenu]`, `[scsMenuItem]` | item: `danger`; icon; disabled with reason | arrows, Home, End, Enter, Escape returns focus |
| Notifications | bell `362:23` / `365:59`; item `362:43` / `365:77`; panel `364:32` · Clinic `Notification Panel` `4262:1056`, `Notification Item` | `ui/notifications/` | new: `scs-notification-bell`, `scs-notification-item`, `scs-notification-panel` | bell: unread count; item: `unread` · `read` | bell: Enter opens; panel: as a menu |

## Navigation

| Component | Design (ops EN / AR · clinic) | Where | Becomes | Options | Keys |
|---|---|---|---|---|---|
| Navbar | `278:3` / `311:16`; unread `2403:6546`; logo `2467:133` · `4252:60501` | `ui/shell/navbar.component.ts` | new: `scs-navbar` — the operators' frame moves onto it; the clinic's shell keeps its own bar until its spec | brand; logo; bell with count; account menu | as its parts |
| Sidebar nav item | `294:323` / `8:119` · `4252:60310` | `ui/shell/nav-item.component.ts` | new: `scs-nav-item` — the operators' side bar moves onto it | `state`: `active` · `disabled`; icon | Enter |
| Tabs | `284:12` / `312:62` · `4252:60720`, `4252:60835` | `ui/list/page-parts.ts` | `scs-tabs` | items; active | arrows, Home, End |
| Breadcrumb | `284:21` / `312:71` · `4252:60901`, `4252:60932` | `ui/list/page-parts.ts` | `scs-breadcrumb` | crumbs; current | — |
| Pagination | `284:34` / `312:84`; menu `2104:140` / `2104:160`; folded `2111:195` / `2111:285` · `4252:60984`, `4252:61024` | `ui/list/` | the pager of `scs-table-footer` | pages; folded with its menu | Enter on a page; the folded menu as a menu |
| Stepper | `284:53` / `312:103` · `4252:61106`, `4252:61157` | `ui/stepper/` | new: `scs-stepper` | steps: `complete` · `active` · `upcoming` | — |

## Kept as they are (not on a component page of the operators' file)

`scs-code-input`, `scs-password-rules`, `scs-language-toggle`, `scs-env-strip`, `scs-auth-frame`,
`scs-no-access`, `scs-page-header`, `scs-list-toolbar`, `scs-filter-popover`,
`scs-placeholder-panel`: they keep their looks and take the new parts inside them (the toolbar's
buttons, the filter panel's drop-downs). Each is shown in the gallery under "Other parts" with
its states, so nothing on a screen is missing from the gallery.

## The patterns (gallery only, composed)

| Pattern | Design | Rules it shows |
|---|---|---|
| Search & Filter | `111:2` | CL-SH-046 … CL-SH-050 |
| Page States | `117:2` | SH-097 (loading, empty, error, offline, read only, not in the plan) |
| Confirmation | `120:2` | CL-SH-060 |
| Multi-step Form | `123:2` | CL-SH-061 |
| Onboarding | `253:2` | — (drawn; example data) |
