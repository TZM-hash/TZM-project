# State Management

> How state is managed in this project.

---

## Overview

<!--
Document your project's state management conventions here.

Questions to answer:
- What state management solution do you use?
- How is local vs global state decided?
- How do you handle server state?
- What are the patterns for derived state?
-->

(To be filled by the team)

---

## State Categories

<!-- Local state, global state, server state, URL state -->

(To be filled by the team)

---

## When to Use Global State

<!-- Criteria for promoting state to global -->

(To be filled by the team)

---

## Server State

- Index-page filters are URL query state rendered by Razor Pages and submitted through `_DataWorkbench`.
- Table column visibility/order and export column selection are separate state domains. Export selectors must use feature-specific form fields and data attributes, must not participate in shared table-column synchronization, and should persist through a dedicated server-side saved-view page key when the preference must follow the authenticated user across devices.
- When a page shows both a filtered list and summary metrics, derive both from one final server-side result set after all query filters have been applied. Do not retain an unfiltered overview for the summary while filtering only the table collection.
- Quick-filter links in a summary rail must preserve the other active query parameters so that search, scope, and status filters compose predictably.

---

## Common Mistakes

- Filtering the table after an overview DTO has already been calculated, which makes the summary disagree with the visible rows.
- Omitting a newly added query parameter from create/edit return links or summary quick-filter links.
- Reusing table-column synchronization attributes for an export selector, which makes changing visible table columns silently alter the exported workbook.
