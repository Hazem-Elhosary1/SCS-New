# Reference documents

Documents carried over from the project that came before this repository. They are here to be
read while specs are written and built. **They are a copy, taken on 11 Oct 2026**: nothing in
this folder is edited here.

| Folder | What it holds | How it is used |
|---|---|---|
| [`brd/source/`](brd/source/) | The Business Requirements Document as it is written: the shared volume `BRD-0-shared.md`, the seventeen chapters in `modules/`, and the logs of what was decided (`BRD-assumptions-closed`, `BRD-round3-decisions`, `BRD-triage-decisions`, `BRD_PRE_REVIEW`). Arabic. | **The source of every spec**, together with the design file (constitution, Principle I). Rule and screen codes in specs — `SH-050`, `CL-SH-027`, `ق-٥٧` — are found here. |
| [`brd/SCS-SpecKit-Phases.md`](brd/SCS-SpecKit-Phases.md) | The 92 specs in 10 phases, in the order they are built. | `/speckit-specify` with no description takes the next row. |
| [`bpmn/`](bpmn/) | Four process maps: sign-in, a clinic visit, the operators' lifecycle, and the landscape. | Open with any BPMN viewer. |
| [`plans/`](plans/) | Earlier plans: the clinic's development plan, integrations, remote visits, the operators' console and top-ups. | Background for the specs that reach those areas. |
| [`design-audit/`](design-audit/) | The audit of the design file: its plan, its change log, and the plan for making the Figma file ready. | Read beside the design file when a screen and its frame disagree. |

## When two documents disagree

1. The BRD in `brd/source/` and the design file win over everything in `plans/` and
   `design-audit/`.
2. A spec's own clarifications, once answered by the product owner, are recorded in that spec
   and are taken to the BRD by the owner.

## Where the originals are

The BRD is maintained by the product owner outside this repository; the constitution's paths
`docs/brd/…` are these files under `docs/reference/brd/`. When the owner changes the BRD, this
copy is replaced whole, in a change of its own — never corrected line by line here.

## What was left out

- The BRD as Word and PDF, and the three compiled volumes: the same text as `brd/source/`.
- The screenshots of the design audit (714 pictures, 78 MB): the design file itself is the
  reference.
- The September 2026 assessments that the BRD replaced, the notes on running the old projects,
  and the list of permission gaps in the old system's API: none of them is built on.
