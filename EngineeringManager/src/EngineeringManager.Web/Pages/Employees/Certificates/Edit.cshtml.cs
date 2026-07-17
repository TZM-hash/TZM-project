using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees.Certificates;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class EditModel(IEmployeeCertificateService certificateService, IEmployeeService employeeService) : PageModel
{
    public IReadOnlyList<EmployeeDto> Employees { get; private set; } = [];
    [BindProperty] public InputModel Input { get; set; } = new();

    public async Task OnGetAsync(Guid? id, Guid? employeeId, CancellationToken cancellationToken)
    {
        Employees = await employeeService.ListAsync(null, cancellationToken);
        if (id.HasValue) Input = InputModel.From(await certificateService.GetAsync(id.Value, DateOnly.FromDateTime(DateTime.Today), cancellationToken));
        else if (employeeId.HasValue) Input.EmployeeId = employeeId.Value;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Employees = await employeeService.ListAsync(null, cancellationToken);
        if (!ModelState.IsValid) return Page();
        CertificateAttachmentUpload? upload = null;
        if (Input.Attachment is { Length: > 0 })
        {
            await using var memory = new MemoryStream();
            await Input.Attachment.CopyToAsync(memory, cancellationToken);
            upload = new CertificateAttachmentUpload(Input.Attachment.FileName, Input.Attachment.ContentType, memory.ToArray());
        }
        await certificateService.SaveAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", true, Input.ToRequest(upload), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        return RedirectToPage("Index");
    }

    public sealed class InputModel
    {
        public Guid? Id { get; set; }
        [Required] public Guid EmployeeId { get; set; }
        [Required, StringLength(100)] public string CertificateType { get; set; } = string.Empty;
        [StringLength(100)] public string? CertificateNumber { get; set; }
        [StringLength(500)] public string? SpecialtyLevelScope { get; set; }
        [StringLength(200)] public string? IssuingAuthority { get; set; }
        public DateOnly? IssuedOn { get; set; }
        public DateOnly? ExpiresOn { get; set; }
        public IFormFile? Attachment { get; set; }
        public string? ExistingAttachmentFileName { get; set; }
        public bool RemoveAttachment { get; set; }
        [StringLength(1000)] public string? Notes { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required] public string Reason { get; set; } = "维护员工证书";
        public SaveEmployeeCertificateRequest ToRequest(CertificateAttachmentUpload? upload) => new(Id, EmployeeId, CertificateType, CertificateNumber, SpecialtyLevelScope, IssuingAuthority, IssuedOn, ExpiresOn, upload, RemoveAttachment, Notes, ConcurrencyStamp, Reason);
        public static InputModel From(EmployeeCertificateDto item) => new() { Id = item.Id, EmployeeId = item.EmployeeId, CertificateType = item.CertificateType, CertificateNumber = item.CertificateNumber, SpecialtyLevelScope = item.SpecialtyLevelScope, IssuingAuthority = item.IssuingAuthority, IssuedOn = item.IssuedOn, ExpiresOn = item.ExpiresOn, ExistingAttachmentFileName = item.AttachmentFileName, Notes = item.Notes, ConcurrencyStamp = item.ConcurrencyStamp, Reason = "修改员工证书" };
    }
}
