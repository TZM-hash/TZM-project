# Equipment Aggregate Contract

## 1. Scope / Trigger

Use this contract whenever equipment master data is created, edited, copied, filtered by company, exposed to projects, or migrated. `Equipment` is the single shared master record; pages and project workflows must not create private equipment copies.

## 2. Signatures

The application boundary is owned by `IEquipmentService`:

```csharp
Task<EquipmentDetailsDto> SaveEquipmentAsync(
    EquipmentActor actor,
    SaveEquipmentRequest request,
    CancellationToken token);

Task<EquipmentDetailsDto> GetEquipmentAsync(
    EquipmentActor actor,
    Guid id,
    CancellationToken token);

Task<CertificateFileDto> DownloadQualificationAttachmentAsync(
    EquipmentActor actor,
    Guid equipmentId,
    CancellationToken token);
```

The database contract adds nullable compatibility columns on `Equipment`:

```text
ManagingLegalEntityId uniqueidentifier NULL -> LegalEntities(Id), Restrict
QualificationAttachmentId uniqueidentifier NULL -> Attachments(Id), Restrict
QualificationCertificateNumber nvarchar(100) NULL
QualificationIssuedOn date NULL
QualificationExpiresOn date NULL
```

## 3. Contracts

- `ManagingLegalEntityId` is the only company-classification field for the equipment workspace. It identifies the accessible self-owned company that manages or uses the device.
- `OwnerLegalEntityId` is separate ownership data. It is required for `SelfOwned` equipment and cleared for `Rented` equipment.
- `LessorBusinessPartnerId` identifies the lessor. It is required for `Rented` equipment and cleared for `SelfOwned` equipment.
- New and edited equipment requires a management company. The database column remains nullable only for migrated historical rows, which appear under the unassigned filter.
- Each device has at most one current qualification attachment. Replacing it changes the current attachment; no certificate history record is created.
- `RemoveQualificationAttachment` removes only the attachment. Certificate number and dates remain unless explicitly edited.
- Project equipment creation calls the same `SaveEquipmentAsync` boundary and persists the project-selected management company into the shared `Equipment` row.
- Migration backfill order is deterministic: owner company first, then the latest project usage company ordered by `EntryDate DESC, Id DESC`, otherwise null.

## 4. Validation & Error Matrix

| Condition | Required result |
| --- | --- |
| Actor cannot manage equipment | Reject with `UnauthorizedAccessException` |
| Management company is missing | Reject with `ArgumentException` |
| Management/owner company is inaccessible | Reject with `InvalidOperationException` |
| Self-owned equipment has no owner | Reject with `ArgumentException` |
| Rented equipment has no active lessor | Reject with `ArgumentException` or `InvalidOperationException` |
| Qualification expiry precedes issue date | Reject with `ArgumentException` |
| Attachment is empty, over 20 MB, has a path component, or uses an unsupported extension | Reject before committing equipment changes |
| Concurrency stamp is missing or stale on edit | Reject with `DbUpdateConcurrencyException` |
| Attachment download targets an inaccessible device | Return the existing not-found/authorization behavior without exposing a storage path |

## 5. Good / Base / Bad Cases

- Good: a rented crane selects a management company and active lessor, uploads one PDF qualification attachment, and is immediately available to project equipment options.
- Base: a migrated device with no resolvable company remains readable under the unassigned filter; its first successful edit assigns a management company.
- Bad: filtering company scope through owner or project usage instead of `ManagingLegalEntityId` mixes ownership and operational responsibility and produces inconsistent counts.

## 6. Tests Required

- Model test: relationships, precision, lengths, and nullable compatibility columns persist and load.
- Service tests: company authorization, ownership-field exclusivity, purchase/qualification round trip, company and unassigned filters, copy projection, and project option labels.
- Attachment tests: upload/download, replacement, removal without clearing metadata, unsupported input, and inaccessible download.
- Migration test/review: only the listed columns, indexes, foreign keys, and ordered backfill SQL are introduced; `Down` removes only this schema.
- Web tests: create/view/edit/copy/usage use dialogs, company values come from `LegalEntity`, and project creation passes the shared management-company field.

## 7. Wrong vs Correct

### Wrong

```csharp
// Ownership is not the workspace classification contract.
query = query.Where(item => item.OwnerLegalEntityId == filter.CompanyId);
```

### Correct

```csharp
query = query.Where(item => item.ManagingLegalEntityId == filter.CompanyId);
```

Keep ownership and management independent even when a self-owned device defaults both fields to the same company.
