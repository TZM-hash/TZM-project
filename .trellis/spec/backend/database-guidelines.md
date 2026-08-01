# Database Guidelines

> Database patterns and conventions for this project.

---

## Overview

<!--
Document your project's database conventions here.

Questions to answer:
- What ORM/query library do you use?
- How are migrations managed?
- What are the naming conventions for tables/columns?
- How do you handle transactions?
-->

(To be filled by the team)

---

## Query Patterns

<!-- How should queries be written? Batch operations? -->

(To be filled by the team)

---

## Migrations

<!-- How to create and run migrations -->

(To be filled by the team)

---

## Naming Conventions

- Auto-generated business identifiers must use short, stable domain prefixes and a bounded numeric sequence. Do not expose GUIDs, timestamps, or source-system identifiers as the primary business number.
- Import placeholders that appear in user-facing `Name` fields must be concise Chinese labels such as `主合同（待确认）`, `待确认工程量1`, `待确认单位0001`, or `待确认账户`. Never concatenate a project name, payment source text, identity/bank numbers, legacy number, GUID, or implementation note such as `旧资料补导` into the display name; keep traceability in notes and import/repair reports instead.
- Automatically created records must use a short semantic name rather than copying a potentially long parent name. For example, a new project's default contract is named `主合同`.
- New/copy forms that suggest a business identifier must allocate the first available standard short number from the complete entity set, including inactive records. Never append technical suffixes such as `-COPY`; retain the database uniqueness check as the concurrency fallback.
- Copy labels use the compact Chinese suffix `（副本）`. Do not use spaced technical labels such as ` - 副本`, and avoid repeatedly extending an already generated display name.

---

## Common Mistakes

<!-- Database-related mistakes your team has made -->

- SQLite does not translate `DateTimeOffset` expressions in `ORDER BY` clauses. When a query must run against the SQLite test provider, filter in SQL first, materialize the bounded candidate set, and then sort/take by `DateTimeOffset` in memory. Keep the pre-materialization filter narrow so this compatibility fallback does not become an unbounded table scan.
- Spreadsheet importers must distinguish business rows from template structure. Section captions such as `应付款` / `已付款`, totals, and zero-valued placeholder rows are not employees or financial details. Skip structural labels and rows whose relevant business amounts are all zero; if a non-zero detail cannot be assigned to a real participant, return a row error instead of creating a placeholder master record.
