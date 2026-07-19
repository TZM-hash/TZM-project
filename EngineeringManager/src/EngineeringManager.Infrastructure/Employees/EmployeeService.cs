using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Infrastructure.Employees;

public sealed class EmployeeService(ApplicationDbContext db) : IEmployeeService
{
    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var number = NormalizeRequired(request.EmployeeNumber, nameof(request.EmployeeNumber));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        if (await db.Employees.AnyAsync(item => item.EmployeeNumber == number, cancellationToken))
        {
            throw new InvalidOperationException($"员工编号已存在：{number}");
        }

        ValidateRates(request.DefaultMonthlySalary, request.DefaultDailyRate, request.DefaultHourlyRate, request.DefaultPieceworkRate);
        await ValidateLegalEntityAsync(request.DefaultLegalEntityId, cancellationToken);
        var employee = new Employee
        {
            EmployeeNumber = number,
            Name = name,
            EmployeeType = request.EmployeeType,
            Phone = NormalizeOptional(request.Phone),
            IdentityNumber = NormalizeOptional(request.IdentityNumber),
            BankAccountNumber = NormalizeOptional(request.BankAccountNumber),
            BankName = NormalizeOptional(request.BankName),
            HireDate = request.HireDate,
            LeaveDate = request.LeaveDate,
            PositionTitle = NormalizeOptional(request.PositionTitle),
            DefaultLegalEntityId = request.DefaultLegalEntityId,
            DefaultMonthlySalary = request.DefaultMonthlySalary,
            DefaultDailyRate = request.DefaultDailyRate,
            DefaultHourlyRate = request.DefaultHourlyRate,
            DefaultPieceworkRate = request.DefaultPieceworkRate,
            Notes = NormalizeOptional(request.Notes),
            IsActive = request.IsActive
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(employee);
    }

    public async Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken cancellationToken)
    {
        var source = await db.Employees.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.SourceEmployeeId, cancellationToken)
            ?? throw new InvalidOperationException("源员工不存在。");
        return await CreateAsync(
            new CreateEmployeeRequest(
                request.NewEmployeeNumber,
                request.NewName,
                source.EmployeeType,
                PositionTitle: source.PositionTitle,
                DefaultLegalEntityId: source.DefaultLegalEntityId,
                DefaultDailyRate: source.DefaultDailyRate,
                DefaultPieceworkRate: source.DefaultPieceworkRate,
                DefaultMonthlySalary: source.DefaultMonthlySalary,
                DefaultHourlyRate: source.DefaultHourlyRate,
                Notes: source.Notes),
            cancellationToken);
    }

    public async Task<EmployeeDto> UpdateAsync(string userId, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.Include(item => item.AffiliationHistory).SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("员工不存在。");
        if (employee.ConcurrencyStamp != request.ConcurrencyStamp) throw new DbUpdateConcurrencyException("员工资料已被其他用户修改，请刷新后重试。");
        var number = NormalizeRequired(request.EmployeeNumber, nameof(request.EmployeeNumber));
        if (await db.Employees.AnyAsync(item => item.Id != request.Id && item.EmployeeNumber == number, cancellationToken))
            throw new InvalidOperationException($"员工编号已存在：{number}");
        ValidateRates(request.DefaultMonthlySalary, request.DefaultDailyRate, request.DefaultHourlyRate, request.DefaultPieceworkRate);
        await ValidateLegalEntityAsync(request.DefaultLegalEntityId, cancellationToken);
        var reason = NormalizeRequired(request.Reason, nameof(request.Reason));
        var before = Snapshot(employee);
        employee.EmployeeNumber = number;
        employee.Name = NormalizeRequired(request.Name, nameof(request.Name));
        employee.EmployeeType = request.EmployeeType;
        employee.Phone = NormalizeOptional(request.Phone);
        employee.IdentityNumber = NormalizeOptional(request.IdentityNumber);
        employee.BankAccountNumber = NormalizeOptional(request.BankAccountNumber);
        employee.BankName = NormalizeOptional(request.BankName);
        employee.HireDate = request.HireDate;
        employee.LeaveDate = request.LeaveDate;
        employee.PositionTitle = NormalizeOptional(request.PositionTitle);
        employee.DefaultLegalEntityId = request.DefaultLegalEntityId;
        employee.DefaultMonthlySalary = request.DefaultMonthlySalary;
        employee.DefaultDailyRate = request.DefaultDailyRate;
        employee.DefaultHourlyRate = request.DefaultHourlyRate;
        employee.DefaultPieceworkRate = request.DefaultPieceworkRate;
        employee.Notes = NormalizeOptional(request.Notes);
        employee.IsActive = request.IsActive;
        employee.UpdatedAt = DateTimeOffset.UtcNow;
        db.Entry(employee).Property(item => item.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;
        employee.ConcurrencyStamp = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "UpdateEmployee", EntityType = nameof(Employee), EntityId = employee.Id.ToString(), Reason = reason, BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(Snapshot(employee)) });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(employee);
    }

    public async Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken)
    {
        if (request.EndDate.HasValue && request.EndDate < request.StartDate)
        {
            throw new ArgumentException("归属结束日期不能早于开始日期。", nameof(request));
        }

        var employee = await db.Employees.Include(item => item.AffiliationHistory).SingleOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new InvalidOperationException("员工不存在。");
        await ValidateAffiliationReferencesAsync(request, cancellationToken);
        if (request.IsPrimary && employee.AffiliationHistory.Where(item => item.IsPrimary).Any(item => PeriodsOverlap(item.StartDate, item.EndDate, request.StartDate, request.EndDate)))
        {
            throw new InvalidOperationException("员工主归属时间区间不能重叠。");
        }

        var affiliation = new EmployeeAffiliationHistory
        {
            EmployeeId = request.EmployeeId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DepartmentId = request.DepartmentId,
            ProjectId = request.ProjectId,
            CrewBusinessPartnerId = request.CrewBusinessPartnerId,
            LegalEntityId = request.LegalEntityId,
            PositionTitle = NormalizeOptional(request.PositionTitle),
            IsPrimary = request.IsPrimary,
            Notes = NormalizeOptional(request.Notes)
        };
        db.EmployeeAffiliationHistories.Add(affiliation);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(affiliation);
    }

    public Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, CancellationToken cancellationToken) =>
        ListAsync(search, false, cancellationToken);

    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, bool canViewSensitiveData, CancellationToken cancellationToken)
    {
        var query = EmployeeQuery().AsQueryable();
        foreach (var term in SearchTerms.Parse(search))
        {
            var employeeType = ParseEmployeeType(term);
            var active = ParseActive(term);
            var hasDate = SearchTerms.TryParseDate(term, out var date);
            var hasAmount = SearchTerms.TryParseDecimal(term, out var amount);
            if (canViewSensitiveData)
            {
                query = query.Where(item =>
                    item.EmployeeNumber.Contains(term)
                    || item.Name.Contains(term)
                    || (item.Phone != null && item.Phone.Contains(term))
                    || (item.PositionTitle != null && item.PositionTitle.Contains(term))
                    || (item.Notes != null && item.Notes.Contains(term))
                    || (item.IdentityNumber != null && item.IdentityNumber.Contains(term))
                    || (item.BankAccountNumber != null && item.BankAccountNumber.Contains(term))
                    || (item.BankName != null && item.BankName.Contains(term))
                    || (item.DefaultLegalEntity != null && (item.DefaultLegalEntity.Code.Contains(term) || item.DefaultLegalEntity.Name.Contains(term) || item.DefaultLegalEntity.ShortName.Contains(term)))
                    || item.AffiliationHistory.Any(affiliation =>
                        (affiliation.PositionTitle != null && affiliation.PositionTitle.Contains(term))
                        || (affiliation.Notes != null && affiliation.Notes.Contains(term))
                        || (affiliation.Department != null && (affiliation.Department.Code.Contains(term) || affiliation.Department.Name.Contains(term)))
                        || (affiliation.Project != null && (affiliation.Project.ProjectNumber.Contains(term) || affiliation.Project.Name.Contains(term)))
                        || (affiliation.CrewBusinessPartner != null && (affiliation.CrewBusinessPartner.PartnerNumber.Contains(term) || affiliation.CrewBusinessPartner.Name.Contains(term) || affiliation.CrewBusinessPartner.ShortName.Contains(term)))
                        || (affiliation.LegalEntity != null && (affiliation.LegalEntity.Code.Contains(term) || affiliation.LegalEntity.Name.Contains(term) || affiliation.LegalEntity.ShortName.Contains(term)))
                        || (hasDate && (affiliation.StartDate == date || affiliation.EndDate == date)))
                    || item.Certificates.Any(certificate => !certificate.IsDeleted && (
                        certificate.CertificateType.Contains(term)
                        || (certificate.CertificateNumber != null && certificate.CertificateNumber.Contains(term))
                        || (certificate.SpecialtyLevelScope != null && certificate.SpecialtyLevelScope.Contains(term))
                        || (certificate.IssuingAuthority != null && certificate.IssuingAuthority.Contains(term))
                        || (certificate.Attachment != null && certificate.Attachment.OriginalFileName.Contains(term))
                        || (certificate.Notes != null && certificate.Notes.Contains(term))
                        || (hasDate && (certificate.IssuedOn == date || certificate.ExpiresOn == date))))
                    || (employeeType.HasValue && item.EmployeeType == employeeType.Value)
                    || (active.HasValue && item.IsActive == active.Value)
                    || (hasDate && (item.HireDate == date || item.LeaveDate == date))
                    || (hasAmount && (item.DefaultMonthlySalary == amount || item.DefaultDailyRate == amount || item.DefaultHourlyRate == amount || item.DefaultPieceworkRate == amount)));
            }
            else
            {
                query = query.Where(item =>
                    item.EmployeeNumber.Contains(term)
                    || item.Name.Contains(term)
                    || (item.Phone != null && item.Phone.Contains(term))
                    || (item.PositionTitle != null && item.PositionTitle.Contains(term))
                    || (item.Notes != null && item.Notes.Contains(term))
                    || (item.BankName != null && item.BankName.Contains(term))
                    || (item.DefaultLegalEntity != null && (item.DefaultLegalEntity.Code.Contains(term) || item.DefaultLegalEntity.Name.Contains(term) || item.DefaultLegalEntity.ShortName.Contains(term)))
                    || item.AffiliationHistory.Any(affiliation =>
                        (affiliation.PositionTitle != null && affiliation.PositionTitle.Contains(term))
                        || (affiliation.Notes != null && affiliation.Notes.Contains(term))
                        || (affiliation.Department != null && (affiliation.Department.Code.Contains(term) || affiliation.Department.Name.Contains(term)))
                        || (affiliation.Project != null && (affiliation.Project.ProjectNumber.Contains(term) || affiliation.Project.Name.Contains(term)))
                        || (affiliation.CrewBusinessPartner != null && (affiliation.CrewBusinessPartner.PartnerNumber.Contains(term) || affiliation.CrewBusinessPartner.Name.Contains(term) || affiliation.CrewBusinessPartner.ShortName.Contains(term)))
                        || (affiliation.LegalEntity != null && (affiliation.LegalEntity.Code.Contains(term) || affiliation.LegalEntity.Name.Contains(term) || affiliation.LegalEntity.ShortName.Contains(term)))
                        || (hasDate && (affiliation.StartDate == date || affiliation.EndDate == date)))
                    || item.Certificates.Any(certificate => !certificate.IsDeleted && (
                        certificate.CertificateType.Contains(term)
                        || (certificate.CertificateNumber != null && certificate.CertificateNumber.Contains(term))
                        || (certificate.SpecialtyLevelScope != null && certificate.SpecialtyLevelScope.Contains(term))
                        || (certificate.IssuingAuthority != null && certificate.IssuingAuthority.Contains(term))
                        || (certificate.Attachment != null && certificate.Attachment.OriginalFileName.Contains(term))
                        || (certificate.Notes != null && certificate.Notes.Contains(term))
                        || (hasDate && (certificate.IssuedOn == date || certificate.ExpiresOn == date))))
                    || (employeeType.HasValue && item.EmployeeType == employeeType.Value)
                    || (active.HasValue && item.IsActive == active.Value)
                    || (hasDate && (item.HireDate == date || item.LeaveDate == date)));
            }
        }

        var employees = await query.OrderBy(item => item.EmployeeNumber).ToListAsync(cancellationToken);
        return employees.Select(ToDto).ToArray();
    }

    public async Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await EmployeeQuery().SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        return employee is null ? null : ToDto(employee);
    }

    private async Task ValidateAffiliationReferencesAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken)
    {
        if (request.DepartmentId.HasValue && !await db.OrganizationUnits.AnyAsync(item => item.Id == request.DepartmentId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("部门不存在或已停用。");
        }

        if (request.ProjectId.HasValue && !await db.Projects.AnyAsync(item => item.Id == request.ProjectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在或已停用。");
        }

        if (request.CrewBusinessPartnerId.HasValue && !await db.BusinessPartnerRoles.AnyAsync(item =>
                item.BusinessPartnerId == request.CrewBusinessPartnerId &&
                item.RoleType == BusinessPartnerRoleType.ConstructionCrew &&
                item.Partner.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException("施工班组不存在、已停用或没有施工班组角色。");
        }

        await ValidateLegalEntityAsync(request.LegalEntityId, cancellationToken);
    }

    private async Task ValidateLegalEntityAsync(Guid? legalEntityId, CancellationToken cancellationToken)
    {
        if (legalEntityId.HasValue && !await db.LegalEntities.AnyAsync(item => item.Id == legalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("签约公司不存在或已停用。");
        }
    }

    private static bool PeriodsOverlap(DateOnly firstStart, DateOnly? firstEnd, DateOnly secondStart, DateOnly? secondEnd) =>
        firstStart <= (secondEnd ?? DateOnly.MaxValue) && secondStart <= (firstEnd ?? DateOnly.MaxValue);

    private static void ValidateRates(params decimal?[] rates)
    {
        if (rates.Any(rate => rate < 0m))
        {
            throw new ArgumentException("默认工资单价不能为负数。", nameof(rates));
        }
    }

    private static EmployeeDto ToDto(Employee employee) =>
        new(
            employee.Id,
            employee.EmployeeNumber,
            employee.Name,
            employee.EmployeeType,
            employee.Phone,
            employee.PositionTitle,
            employee.DefaultLegalEntityId,
            employee.DefaultMonthlySalary,
            employee.DefaultDailyRate,
            employee.DefaultHourlyRate,
            employee.DefaultPieceworkRate,
            employee.IsActive,
            employee.AffiliationHistory.OrderByDescending(item => item.StartDate).Select(ToDto).ToArray(),
            employee.IdentityNumber,
            employee.BankAccountNumber,
            employee.BankName,
            employee.HireDate,
            employee.LeaveDate,
            employee.ConcurrencyStamp,
            employee.Notes);

    private static object Snapshot(Employee employee) => new { employee.EmployeeNumber, employee.Name, employee.EmployeeType, employee.Phone, employee.IdentityNumber, employee.BankAccountNumber, employee.BankName, employee.HireDate, employee.LeaveDate, employee.PositionTitle, employee.DefaultLegalEntityId, employee.DefaultMonthlySalary, employee.DefaultDailyRate, employee.DefaultHourlyRate, employee.DefaultPieceworkRate, employee.Notes, employee.IsActive };

    private static EmployeeAffiliationDto ToDto(EmployeeAffiliationHistory affiliation) =>
        new(
            affiliation.Id,
            affiliation.StartDate,
            affiliation.EndDate,
            affiliation.DepartmentId,
            affiliation.ProjectId,
            affiliation.CrewBusinessPartnerId,
            affiliation.LegalEntityId,
            affiliation.PositionTitle,
            affiliation.IsPrimary,
            affiliation.Notes,
            affiliation.Department?.Name,
            affiliation.Project?.Name,
            affiliation.CrewBusinessPartner?.Name,
            affiliation.LegalEntity?.ShortName);

    private IQueryable<Employee> EmployeeQuery() =>
        db.Employees
            .AsNoTracking()
            .Include(item => item.AffiliationHistory).ThenInclude(item => item.Department)
            .Include(item => item.AffiliationHistory).ThenInclude(item => item.Project)
            .Include(item => item.AffiliationHistory).ThenInclude(item => item.CrewBusinessPartner)
            .Include(item => item.AffiliationHistory).ThenInclude(item => item.LegalEntity);

    private static EmployeeType? ParseEmployeeType(string term) => term switch
    {
        "正式" or "正式员工" => EmployeeType.Formal,
        "劳务" or "劳务员工" => EmployeeType.Labor,
        "临时" or "特殊临时人员" => EmployeeType.Temporary,
        _ => Enum.TryParse<EmployeeType>(term, true, out var value) ? value : null
    };

    private static bool? ParseActive(string term) => term switch
    {
        "启用" or "在用" or "在职" => true,
        "停用" or "离职" => false,
        _ => null
    };

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
