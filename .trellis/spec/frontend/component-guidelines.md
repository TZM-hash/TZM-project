# Component Guidelines

> How components are built in this project.

---

## Overview

<!--
Document your project's component conventions here.

Questions to answer:
- What component patterns do you use?
- How are props defined?
- How do you handle composition?
- What accessibility standards apply?
-->

(To be filled by the team)

---

## Component Structure

<!-- Standard structure of a component file -->

(To be filled by the team)

---

## Props Conventions

<!-- How props should be defined and typed -->

(To be filled by the team)

---

## Styling Patterns

- Shared desktop workspaces use the established equipment/crew workspace shell: a compact two-column grid, a sticky summary rail, an integrated toolbar, and a scrollable table surface.
- Use the active application design tokens (`--app-border`, `--app-surface`, `--app-surface-raised`, `--app-text`, `--app-muted`, and `--app-shadow-soft`) for workspace panels. Do not introduce legacy or undefined aliases such as `--border-color` or `--surface`; unresolved tokens silently remove the intended panel boundary.
- Keep page-specific selectors for domain sizing and table columns, but align panel radius, border, background, shadow, and spacing with an existing workspace before adding new visual rules.
- Core data-table cells default to `white-space: nowrap`. For long free-text columns such as issuing authorities, use a block-level cell wrapper with `white-space: normal` and `overflow-wrap: anywhere`, and keep horizontal overflow on the table wrapper instead of allowing text to overlap the next column.
- Do not apply hover `transform` translation or scaling to `.panel` or another container that carries dense text/data tables; transformed text can be rasterized into a soft-looking composited layer. Use shadow, border, or background-color feedback instead. Motion transforms remain acceptable for decorative metric cards that do not carry dense tabular content.

---

## Accessibility

<!-- A11y requirements and patterns -->

(To be filled by the team)

---

## Common Mistakes

- Building a new workspace shell with visually similar but different tokens. This can leave panels transparent even though the markup appears correct.
- Applying a large fixed or minimum height to a list panel. Let the content define the initial height and keep horizontal overflow inside the table wrapper.
- Adding only `overflow-wrap` to a long data-table value. It does not override the global `white-space: nowrap`, so the value can still collide visually with the following column.
- Adding a hover transform to a data-bearing panel. Browser compositing can make all text inside the panel appear blurry while the pointer is over it; keep the panel stationary and animate only non-text decoration.
