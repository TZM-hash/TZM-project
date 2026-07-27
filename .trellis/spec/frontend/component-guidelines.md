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

---

## Accessibility

<!-- A11y requirements and patterns -->

(To be filled by the team)

---

## Common Mistakes

- Building a new workspace shell with visually similar but different tokens. This can leave panels transparent even though the markup appears correct.
- Applying a large fixed or minimum height to a list panel. Let the content define the initial height and keep horizontal overflow inside the table wrapper.
