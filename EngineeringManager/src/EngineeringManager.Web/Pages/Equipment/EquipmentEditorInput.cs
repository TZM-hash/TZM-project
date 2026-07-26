using System.ComponentModel.DataAnnotations;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;

namespace EngineeringManager.Web.Pages.Equipment;

public sealed class EquipmentEditorInput
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "请填写设备编号。")]
    public string EquipmentNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "请填写设备名称。")]
    public string Name { get; set; } = string.Empty;

    public string? Model { get; set; }
    public string? Category { get; set; }
    public EquipmentOwnershipType OwnershipType { get; set; } = EquipmentOwnershipType.SelfOwned;

    [Required(ErrorMessage = "请选择管理公司。")]
    public Guid? ManagingLegalEntityId { get; set; }

    public Guid? OwnerLegalEntityId { get; set; }
    public Guid? LessorBusinessPartnerId { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public decimal? InternalDailyRate { get; set; }
    public string? QualificationCertificateNumber { get; set; }
    public DateOnly? QualificationIssuedOn { get; set; }
    public DateOnly? QualificationExpiresOn { get; set; }
    public Guid? QualificationAttachmentId { get; set; }
    public string? QualificationAttachmentFileName { get; set; }
    public bool RemoveQualificationAttachment { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public Guid? ConcurrencyStamp { get; set; }

    [Required(ErrorMessage = "请填写修改原因。")]
    public string Reason { get; set; } = "维护设备档案";

    public SaveEquipmentRequest ToRequest(CertificateAttachmentUpload? upload) => new(
        Id,
        EquipmentNumber,
        Name,
        Model,
        Category,
        OwnershipType,
        OwnerLegalEntityId,
        LessorBusinessPartnerId,
        InternalDailyRate,
        ConcurrencyStamp,
        Reason,
        Notes,
        ManagingLegalEntityId,
        PurchaseDate,
        PurchaseAmount,
        QualificationCertificateNumber,
        QualificationIssuedOn,
        QualificationExpiresOn,
        upload,
        RemoveQualificationAttachment,
        IsActive);

    public static EquipmentEditorInput From(EquipmentDetailsDto item, bool copy = false) => new()
    {
        Id = copy || item.Id == Guid.Empty ? null : item.Id,
        EquipmentNumber = copy ? string.Empty : item.EquipmentNumber,
        Name = copy ? $"{item.Name} - 副本" : item.Name,
        Model = item.Model,
        Category = item.Category,
        OwnershipType = item.OwnershipType,
        ManagingLegalEntityId = item.ManagingLegalEntityId,
        OwnerLegalEntityId = item.OwnerLegalEntityId,
        LessorBusinessPartnerId = item.LessorBusinessPartnerId,
        PurchaseDate = item.PurchaseDate,
        PurchaseAmount = item.PurchaseAmount,
        InternalDailyRate = item.InternalDailyRate,
        QualificationCertificateNumber = item.QualificationCertificateNumber,
        QualificationIssuedOn = item.QualificationIssuedOn,
        QualificationExpiresOn = item.QualificationExpiresOn,
        QualificationAttachmentId = copy ? null : item.QualificationAttachmentId,
        QualificationAttachmentFileName = copy ? null : item.QualificationAttachmentFileName,
        IsActive = item.IsActive,
        Notes = item.Notes,
        ConcurrencyStamp = copy ? null : item.ConcurrencyStamp,
        Reason = copy ? "复制设备档案" : "维护设备档案"
    };
}
