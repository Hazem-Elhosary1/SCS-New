# Contract: Lists, Tables, Dialogs and Passing Notices

**Spec**: [../spec.md](../spec.md) FR-030 to FR-038, FR-042 | **BRD**: Chapter 01 §A-7

What every list in the console is built from, and the rules a page keeps when it uses them.
Decided once here; a later spec does not decide them again.

## 1. Anatomy

```text
page header        breadcrumb · title · one line · header buttons (end)      [toast appears beside these]
tabs               only for an exclusive split with counts
card
  heading          the list's name
  toolbar          search (start, fixed width) ················ «تصفية» (end)
  applied chips    «‹الوجه›: ‹القيمة› ×» … ················· «مسح الكل» (end)   — only when something is applied
  rows             header row · rows
  footer           count (start) ················ paging (end)
```

A short list that is read and not browsed has no toolbar.

## 2. Parts (shared library)

| Part | Use | Design |
|---|---|---|
| `scs-page-header` | breadcrumb, title, line under it, slot for header buttons | Page Header `4252:61267`, Breadcrumb `4252:60932` |
| `scs-card` | a list's container with its heading | Card `4252:61428` |
| `scs-tabs` | exclusive split with counts | Tab Bar `4252:60835` |
| `scs-list-toolbar` | search + the filter button; nothing else goes in it | Users list frames, page `4160:7` |
| `scs-filter-popover` | the filter's panel, with «تطبيق» and «مسح الكل» | Filter Popover `4274:54643` |
| `scs-applied-chips` | what is applied, and «مسح الكل» | "Shell / Table Filtered" `4481:16929` |
| table styles | header row, row, cell | Table Header Row `4267:1004`, Table Row `4267:903` |
| `scs-row-menu` | the «⋯» of a row | Menu `4252:61893` |
| `scs-table-footer` | count and paging | Pagination `4252:61024` |
| `scs-skeleton` | loading | Skeleton `4252:59877` |
| `scs-empty-state` | no data · no results · error | Empty State `4252:60047` |
| `scs-toast` + `ToastService` | the passing notice | Alert, `Style=Toast` `4253:54128` |
| `scs-confirm` | confirmation | Modal `4252:61695` |

## 3. Rules

**Toolbar and filters**

1. Search is at the reading start with a fixed width; its placeholder names what is searched,
   with no verb and no dots («باسم المريض أو رقم الهاتف»).
2. «تصفية» is at the other end, with its word and icon, and no count badge.
3. The filter panel opens downward over the rows it filters; it covers no notice and no page
   numbers; its button is drawn pressed while it is open.
4. What is applied is shown once: as chips under the toolbar, starting under the search. The
   row of chips is absent when nothing is applied.
5. No filter without a visible column for it. The branch is never a filter or a chip.
6. An action does not change a filter nobody asked to change.

**Rows**

7. A row opens its record on press. "Open" is never an item of «⋯».
8. «⋯» exists only when there are actions other than opening; its items follow the record's
   state and the person's permissions; a dangerous one is red wherever it sits.
9. With the view alone: no «⋯», no control that writes; the row still opens its record.

**Footer**

10. One row: the count at the start («١–٥ من ٥٢ …»), the paging at the end.
11. «السابق» is disabled on the first page, «التالي» on the last. One page shows the count alone.

**States**

| State | The list shows |
|---|---|
| Loading | grey skeletons in place of cards and rows |
| Empty | no toolbar, no export; a title, a message, and the create action for a person who holds it |
| No results | the toolbar stays; the way out is the × of the search or «مسح الكل» |
| Error | the page and toolbar stay; the table area alone shows the error with «إعادة المحاولة»; numbers that came from the failed request show a dash — never the last loaded values |

12. A control with nothing to act on is absent («تصدير» in empty, loading and error).

**Dialogs, drawers, menus**

13. Whatever opened one stays marked while it is open: the button pressed, or the row marked.
14. A confirmation is for what cannot be undone or touches someone else. Its button names the
    action and is disabled until what it needs is chosen. The way back is «تراجع» when the
    action itself is "cancel …".
15. A form's main button is enabled; checking happens on submit; a field's error replaces its
    hint and names the fault. A banner and a field error are not shown together for two causes.
16. What is known before submitting is drawn on the control, disabled with its reason under it.
17. One form for one action, wherever it is opened from.

**After an action**

18. Every action that changes something is followed by a toast and a visible change.
19. The toast is in the corner at the reading end, beside the page's header buttons — never
    over them, and never over the control that caused it.
20. After saving: an edit returns to the record's detail; a create or delete returns to the list.
21. «تصدير» downloads at once, then a toast; no dialog asks for a format.

**Writing controls**

22. Every control that changes data carries `scsWrite`: it is disabled, with the reason,
    while there is no connection.
23. A control guarded by a permission is absent for a person without it (`*scsCan`, spec 005) —
    absent, not disabled. Disabled is for the connection, the record's state, the plan.

## 4. Where it is proven

- The roles list (`/administration/roles`): cards, short tables, loading, failure, empty.
- The sample list (`/dev/sample-list`, outside production only): everything else.
