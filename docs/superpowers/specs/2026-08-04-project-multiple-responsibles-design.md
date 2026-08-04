# Project Multiple Responsible Employees Design

## Goal

Support one project having multiple employee project responsible persons while preserving the current single-responsible data and workbook contracts. Backfill the historical project manager data from `old-data/旧资料项目导入模板_20260719.xlsx` into employee records and project relationships.

## Architecture

`ProjectResponsibleEmployee` is the normalized source of truth for employee responsibility. It contains one row per project/employee pair, a stable display order, and a primary flag. The existing `Projects.ResponsibleEmployeeId` column remains as the legacy/primary projection so existing exports, filters, and integrations continue to work during the transition; it always mirrors the first responsible employee.

Application DTOs expose both the existing singular primary fields and a new ordered collection of responsible employee IDs/names. Project create/update validation accepts one or more active employees marked `IsProjectResponsible`. The web edit and quick-edit controls use a multi-select; list, detail, and export views join names with `、`.

## Historical Import Rules

- Read `项目导入!原始_项目经理` and match projects by exact normalized project name because the current system uses regenerated `XMxxxx` project numbers.
- Reuse existing employee records by exact name first.
- Create missing employee records with the next available `YG####` employee number, `Formal` employee type, active status, and `IsProjectResponsible = true`; only the explicit phone in `赵鸿辉：18968023336` is copied.
- Normalize source annotations: `裘华忠班组` becomes employee `裘华忠`, `张恒挂靠` becomes `张恒`, and `赵鸿辉：18968023336` becomes employee `赵鸿辉` with the phone stored.
- Split composite values into all employees: `沈健马罗杰` becomes `沈健` plus `马罗杰`; `马罗杰， 张冬冬` becomes `马罗杰` plus `张冬冬`.
- Existing 38 single-responsible relationships are migrated into the join table. The two composite values create three additional responsible links across two projects.
- The write runs after a SQL Server full backup, in one transaction, updates only currently empty responsibility relationships, and writes JSON before/after audit records with a batch identifier. Re-running the maintenance command is idempotent.

## Error Handling

The maintenance step aborts before writing when a source project cannot be matched uniquely, an employee name maps to multiple employee records, or a requested employee is inactive. Ambiguous or structurally invalid source manager text is reported instead of silently selecting one person.

## Testing

- Infrastructure tests cover the join entity mapping and unique project/employee relationship.
- Application tests cover responsible employee option filtering, multiple-ID validation, primary projection, and round-trip workbook compatibility.
- Web tests cover the multi-select markup and joined-name display.
- A post-migration SQL verification checks employee counts, relationship counts, project displays, and the audit batch.
