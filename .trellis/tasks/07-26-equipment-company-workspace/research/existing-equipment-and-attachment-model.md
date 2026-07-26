# Existing Equipment and Attachment Model

## Equipment domain

- `Infrastructure/Data/Equipment.cs` is the canonical equipment entity used by equipment management and project construction.
- It already stores equipment number, name, model, category, ownership type, status, owner legal entity, lessor business partner, purchase date, purchase amount, internal daily rate, notes, activation state, concurrency stamp, and timestamps.
- `SaveEquipmentRequest` and `EquipmentDetailsDto` currently omit `PurchaseDate` and `PurchaseAmount`, so the existing edit page cannot maintain all persisted fields.
- Rented equipment currently clears `OwnerLegalEntityId`. Therefore `OwnerLegalEntityId` cannot correctly represent the self-owned company responsible for both self-owned and rented equipment.
- `EquipmentFilter.CompanyId` exists, but dashboard filtering currently combines owner company and project usage company. That does not provide stable company classification independent of project history.

## Project reuse

- `ProjectConstructionService.CreateEquipmentAsync` calls `IEquipmentService.SaveEquipmentAsync`; project construction already shares the canonical equipment domain.
- Project construction records reference `EquipmentId`, so extending the canonical entity makes the new data available without duplicating equipment records.
- The project-side create request and option/list projections must be updated so newly created equipment always has a managing company and project selectors can expose company/ownership context.

## Attachments

- The project has a shared `Attachment` entity and `IFileStore` implementation.
- Company and employee certificate services already support upload, replacement, removal, authorized download, file-name normalization, audit logging, and soft deletion through `CertificateServiceSupport`.
- Equipment qualification files can reuse the same infrastructure and security pattern. No new physical file store is needed.

## Web UI

- The equipment index is currently a metric grid plus a wide table. Add, detail, edit, copy, and usage operations navigate to separate pages.
- The equipment edit page uses raw GUID inputs for owner company and lessor and omits purchase fields and qualification data.
- The company details workspace provides the target layout and native `<dialog>` interaction pattern already used elsewhere in the application.

## Recommended technical direction

- Add a required `ManagingLegalEntityId` relationship to `Equipment`, separate from the legal owner.
- Backfill existing rows from `OwnerLegalEntityId`; for legacy rented rows without an owner, leave the new column nullable during migration and require it for all newly saved or edited equipment. A later cleanup can make it non-null after legacy data is assigned.
- Add one set of qualification fields directly to `Equipment`: number, issued date, expiry date, and optional attachment FK. This matches the confirmed one-current-certificate requirement and avoids a needless certificate-history table.
- Reuse the shared attachment store and certificate file validation/download helpers.
