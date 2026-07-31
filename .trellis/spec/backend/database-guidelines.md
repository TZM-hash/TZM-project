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

<!-- Table names, column names, index names -->

(To be filled by the team)

---

## Common Mistakes

<!-- Database-related mistakes your team has made -->

- SQLite does not translate `DateTimeOffset` expressions in `ORDER BY` clauses. When a query must run against the SQLite test provider, filter in SQL first, materialize the bounded candidate set, and then sort/take by `DateTimeOffset` in memory. Keep the pre-materialization filter narrow so this compatibility fallback does not become an unbounded table scan.
