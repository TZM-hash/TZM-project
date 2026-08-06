using System.Text.Json;
using EngineeringManager.Application.Employees;
using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Personnel;

public sealed class PersonnelService(ApplicationDbContext db) : IPersonnelService
{
    public async Task<PersonnelDetailsDto> CreateAsync(string userId, CreatePersonRequest request, CancellationToken cancellationToken)
    {
        var personNumber = Required(request.PersonNumber, nameof(request.PersonNumber));
        if (await db.People.AnyAsync(item => item.PersonNumber == personNumber, cancellationToken))
        {
            throw new InvalidOperationException($"人员编号已存在：{personNumber}");
        }

        await ValidateIdentityNumberAsync(null, request.IdentityNumber, cancellationToken);
        ValidateScope(request.Scope, request.InternalType, request.ExternalType, request.BusinessPartnerId, request.CrewBusinessPartnerId);
        await ValidateAffiliationReferencesAsync(
            request.Scope,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.OrganizationUnitId,
            request.ProjectId,
            request.CrewBusinessPartnerId,
            cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var person = new Person { PersonNumber = personNumber };
        PersonPublicDataSynchronizer.Apply(
            person,
            request.Name,
            request.Phone,
            request.IdentityNumber,
            request.BankAccountNumber,
            request.BankName,
            request.Notes,
            true);
        db.People.Add(person);

        if (request.Scope == PersonnelScope.Internal)
        {
            var employee = CreateEmployee(person, request.InternalType!.Value, request.PositionTitle, request.LegalEntityId);
            db.Employees.Add(employee);
        }
        else if (request.ExternalType == ExternalPersonnelType.ConstructionCrew)
        {
            var worker = CreateConstructionWorker(person, request.PositionTitle);
            worker.Memberships.Add(new ConstructionCrewMembership
            {
                Worker = worker,
                CrewBusinessPartnerId = request.CrewBusinessPartnerId!.Value,
                StartDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.Today),
                IsPrimary = true,
                Notes = Optional(request.Reason)
            });
            db.ConstructionWorkers.Add(worker);
        }

        var engagement = BuildEngagement(
            person.Id,
            request.Scope,
            request.InternalType,
            request.ExternalType,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.OrganizationUnitId,
            request.ProjectId,
            request.CrewBusinessPartnerId,
            request.PositionTitle,
            request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.Today),
            request.Notes,
            request.Reason);
        db.PersonnelEngagementHistories.Add(engagement);
        AddAudit(userId, "CreatePerson", person.Id, request.Reason, null, Snapshot(engagement));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(person.Id, request.EffectiveDate, true, cancellationToken))!;
    }

    public async Task<PersonnelDetailsDto?> GetAsync(Guid personId, DateOnly? asOf, bool canViewSensitiveData, CancellationToken cancellationToken)
    {
        var person = await PersonQuery().SingleOrDefaultAsync(item => item.Id == personId, cancellationToken);
        return person is null ? null : ToDetails(person, asOf ?? DateOnly.FromDateTime(DateTime.Today), canViewSensitiveData);
    }

    public async Task<IReadOnlyList<PersonnelListItemDto>> ListAsync(PersonnelListQuery query, bool canViewSensitiveData, CancellationToken cancellationToken)
    {
        var people = await PersonQuery().OrderBy(item => item.PersonNumber).ToListAsync(cancellationToken);
        var asOf = query.AsOf ?? DateOnly.FromDateTime(DateTime.Today);
        IEnumerable<PersonnelDetailsDto> items = people.Select(item => ToDetails(item, asOf, canViewSensitiveData));
        items = items.Where(item => item.CurrentAffiliation?.Scope == query.Scope);
        if (query.IsActive.HasValue) items = items.Where(item => item.IsActive == query.IsActive.Value);
        if (query.LegalEntityId.HasValue) items = items.Where(item => item.CurrentAffiliation?.LegalEntityId == query.LegalEntityId.Value);
        if (query.BusinessPartnerId.HasValue) items = items.Where(item => item.CurrentAffiliation?.BusinessPartnerId == query.BusinessPartnerId.Value);
        if (query.OrganizationUnitId.HasValue) items = items.Where(item => item.CurrentAffiliation?.OrganizationUnitId == query.OrganizationUnitId.Value);
        if (query.InternalType.HasValue) items = items.Where(item => item.CurrentAffiliation?.InternalType == query.InternalType.Value);
        if (query.ExternalType.HasValue) items = items.Where(item => item.CurrentAffiliation?.ExternalType == query.ExternalType.Value);
        foreach (var term in SearchTerms.Parse(query.Search))
        {
            items = items.Where(item => Matches(item, term, canViewSensitiveData));
        }

        return items.Select(item => new PersonnelListItemDto(
            item.Id,
            item.PersonNumber,
            item.Name,
            item.Phone,
            item.IsActive,
            item.CurrentAffiliation!.Scope,
            item.CurrentAffiliation.InternalType,
            item.CurrentAffiliation.ExternalType,
            item.EmployeeId,
            item.ConstructionWorkerId,
            item.CurrentAffiliation)).ToArray();
    }

    public async Task<PersonnelDetailsDto> SavePublicDataAsync(string userId, SavePersonRequest request, CancellationToken cancellationToken)
    {
        var person = await db.People
            .Include(item => item.Employee)
            .Include(item => item.ConstructionWorker)
            .SingleOrDefaultAsync(item => item.Id == request.PersonId, cancellationToken)
            ?? throw new InvalidOperationException("人员不存在。");
        if (person.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw new DbUpdateConcurrencyException("人员资料已被其他用户修改，请刷新后重试。");
        }

        await ValidateIdentityNumberAsync(person.Id, request.IdentityNumber, cancellationToken);
        var before = PublicSnapshot(person);
        db.Entry(person).Property(item => item.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;
        PersonPublicDataSynchronizer.Apply(
            person,
            request.Name,
            request.Phone,
            request.IdentityNumber,
            request.BankAccountNumber,
            request.BankName,
            request.Notes,
            request.IsActive);
        AddAudit(userId, "UpdatePerson", person.Id, request.Reason, before, PublicSnapshot(person));
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(person.Id, null, true, cancellationToken))!;
    }

    public async Task<PersonnelAffiliationDto> SaveAffiliationAsync(string userId, SavePersonnelAffiliationRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var affiliation = await SaveAffiliationCoreAsync(userId, request, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAffiliation(affiliation);
    }

    public async Task<PersonnelDetailsDto> SwitchScopeAsync(string userId, SwitchPersonnelScopeRequest request, CancellationToken cancellationToken)
    {
        ValidateScope(request.Scope, request.InternalType, request.ExternalType, request.BusinessPartnerId, request.CrewBusinessPartnerId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var person = await db.People
            .Include(item => item.Employee)
            .Include(item => item.ConstructionWorker).ThenInclude(item => item!.Memberships)
            .SingleOrDefaultAsync(item => item.Id == request.PersonId, cancellationToken)
            ?? throw new InvalidOperationException("人员不存在。");

        if (request.Scope == PersonnelScope.Internal)
        {
            if (person.Employee is null)
            {
                person.Employee = CreateEmployee(person, request.InternalType!.Value, request.PositionTitle, request.LegalEntityId);
                db.Employees.Add(person.Employee);
            }
            else
            {
                person.Employee.EmployeeType = request.InternalType!.Value;
                person.Employee.PositionTitle = Optional(request.PositionTitle);
                person.Employee.DefaultLegalEntityId = request.LegalEntityId;
                person.Employee.IsActive = true;
                person.Employee.LeaveDate = null;
            }
        }
        else if (request.ExternalType == ExternalPersonnelType.ConstructionCrew)
        {
            if (person.ConstructionWorker is null)
            {
                person.ConstructionWorker = CreateConstructionWorker(person, request.PositionTitle);
                db.ConstructionWorkers.Add(person.ConstructionWorker);
            }

            var currentMembership = person.ConstructionWorker.Memberships
                .Where(item => item.IsPrimary && item.StartDate <= request.EffectiveDate && (item.EndDate is null || item.EndDate >= request.EffectiveDate))
                .OrderByDescending(item => item.StartDate)
                .FirstOrDefault();
            if (currentMembership is not null && currentMembership.StartDate < request.EffectiveDate)
            {
                currentMembership.EndDate = request.EffectiveDate.AddDays(-1);
            }
            if (currentMembership is null || currentMembership.CrewBusinessPartnerId != request.CrewBusinessPartnerId)
            {
                person.ConstructionWorker.Memberships.Add(new ConstructionCrewMembership
                {
                    Worker = person.ConstructionWorker,
                    CrewBusinessPartnerId = request.CrewBusinessPartnerId!.Value,
                    StartDate = request.EffectiveDate,
                    IsPrimary = true,
                    Notes = Required(request.Reason, nameof(request.Reason))
                });
            }
        }

        await SaveAffiliationCoreAsync(userId, new SavePersonnelAffiliationRequest(
            request.PersonId,
            request.Scope,
            request.InternalType,
            request.ExternalType,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.OrganizationUnitId,
            request.ProjectId,
            request.CrewBusinessPartnerId,
            request.PositionTitle,
            request.EffectiveDate,
            request.Reason), cancellationToken);
        AddAudit(userId, "SwitchPersonnelScope", person.Id, request.Reason, null, new { request.Scope, request.InternalType, request.ExternalType, request.EffectiveDate });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(person.Id, request.EffectiveDate, true, cancellationToken))!;
    }

    public async Task<PersonnelOptionSetDto> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var legalEntities = await db.LegalEntities.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ShortName)
            .Select(item => new PersonnelOrganizationOptionDto(item.Id, item.ShortName, false)).ToArrayAsync(cancellationToken);
        var partnerRows = await db.BusinessPartners.AsNoTracking().Where(item => item.IsActive).Include(item => item.Roles).OrderBy(item => item.ShortName).ToArrayAsync(cancellationToken);
        var partners = partnerRows.Select(item => new PersonnelOrganizationOptionDto(item.Id, item.ShortName, item.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew))).ToArray();
        var departments = await db.OrganizationUnits.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name)
            .Select(item => new PersonnelDepartmentOptionDto(item.Id, item.Code, item.Name, item.LegalEntityId, item.BusinessPartnerId)).ToArrayAsync(cancellationToken);
        var projectRows = await db.Projects.AsNoTracking().Where(item => item.IsActive)
            .Include(item => item.LegalEntities)
            .Include(item => item.Partners)
            .Include(item => item.ConstructionRecords)
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var projects = projectRows.Select(item => new PersonnelProjectOptionDto(
            item.Id,
            item.Name,
            item.LegalEntities.Select(link => link.LegalEntityId).Distinct().ToArray(),
            item.Partners.Where(link => link.IsActive).Select(link => link.BusinessPartnerId)
                .Concat(item.ConstructionRecords.Where(record => record.CrewBusinessPartnerId.HasValue).Select(record => record.CrewBusinessPartnerId!.Value))
                .Distinct().ToArray())).ToArray();
        return new PersonnelOptionSetDto(legalEntities, partners, departments, projects, partners.Where(item => item.IsCrew).ToArray());
    }

    public Task<Guid?> ResolvePersonIdForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        db.Employees.AsNoTracking().Where(item => item.Id == employeeId).Select(item => item.PersonId).SingleOrDefaultAsync(cancellationToken);

    private async Task<PersonnelEngagementHistory> SaveAffiliationCoreAsync(string userId, SavePersonnelAffiliationRequest request, CancellationToken cancellationToken)
    {
        ValidateScope(request.Scope, request.InternalType, request.ExternalType, request.BusinessPartnerId, request.CrewBusinessPartnerId);
        await ValidateAffiliationReferencesAsync(
            request.Scope,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.OrganizationUnitId,
            request.ProjectId,
            request.CrewBusinessPartnerId,
            cancellationToken);
        var person = await db.People.Include(item => item.EngagementHistory).Include(item => item.Employee)
            .SingleOrDefaultAsync(item => item.Id == request.PersonId, cancellationToken)
            ?? throw new InvalidOperationException("人员不存在。");
        var current = person.EngagementHistory
            .Where(item => item.IsPrimary && item.StartDate <= request.EffectiveDate && (item.EndDate is null || item.EndDate >= request.EffectiveDate))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();
        if (current is not null)
        {
            if (request.ConcurrencyStamp.HasValue && current.ConcurrencyStamp != request.ConcurrencyStamp.Value)
            {
                throw new DbUpdateConcurrencyException("人员归属已被其他用户修改，请刷新后重试。");
            }
            if (current.StartDate >= request.EffectiveDate)
            {
                throw new InvalidOperationException("新归属生效日期必须晚于当前归属开始日期。");
            }
            current.EndDate = request.EffectiveDate.AddDays(-1);
            current.ConcurrencyStamp = Guid.NewGuid();
        }

        var engagement = BuildEngagement(
            request.PersonId,
            request.Scope,
            request.InternalType,
            request.ExternalType,
            request.LegalEntityId,
            request.BusinessPartnerId,
            request.OrganizationUnitId,
            request.ProjectId,
            request.CrewBusinessPartnerId,
            request.PositionTitle,
            request.EffectiveDate,
            null,
            request.Reason);
        person.EngagementHistory.Add(engagement);
        db.PersonnelEngagementHistories.Add(engagement);
        PersonnelEngagementRules.ValidatePrimaryPeriods(person.EngagementHistory.Select(item => new EngagementPeriod(item.StartDate, item.EndDate, item.IsPrimary)));
        SyncEmployeeAffiliation(person.Employee, engagement);
        AddAudit(userId, "UpdatePersonnelAffiliation", person.Id, request.Reason, current is null ? null : Snapshot(current), Snapshot(engagement));
        return engagement;
    }

    private async Task ValidateAffiliationReferencesAsync(
        PersonnelScope scope,
        Guid? legalEntityId,
        Guid? businessPartnerId,
        Guid? organizationUnitId,
        Guid? projectId,
        Guid? crewBusinessPartnerId,
        CancellationToken cancellationToken)
    {
        if (scope == PersonnelScope.Internal && !legalEntityId.HasValue)
        {
            throw new InvalidOperationException("内部人员必须选择自有公司。");
        }
        if (scope == PersonnelScope.External && businessPartnerId.HasValue && !await db.BusinessPartners.AnyAsync(item => item.Id == businessPartnerId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("所属合作单位不存在或已停用。");
        }
        if (legalEntityId.HasValue && !await db.LegalEntities.AnyAsync(item => item.Id == legalEntityId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("自有公司不存在或已停用。");
        }
        if (organizationUnitId.HasValue)
        {
            var department = await db.OrganizationUnits.AsNoTracking().SingleOrDefaultAsync(item => item.Id == organizationUnitId && item.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("部门不存在或已停用。");
            PersonnelEngagementRules.ValidateDepartmentOwner(department.LegalEntityId, department.BusinessPartnerId, legalEntityId, businessPartnerId);
        }
        if (crewBusinessPartnerId.HasValue && !await db.BusinessPartnerRoles.AnyAsync(item => item.BusinessPartnerId == crewBusinessPartnerId && item.RoleType == BusinessPartnerRoleType.ConstructionCrew && item.Partner.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("施工班组不存在、已停用或没有施工班组角色。");
        }
        if (projectId.HasValue)
        {
            var projectMatches = legalEntityId.HasValue
                ? await db.Projects.AnyAsync(item => item.Id == projectId && item.IsActive && item.LegalEntities.Any(link => link.LegalEntityId == legalEntityId), cancellationToken)
                : businessPartnerId.HasValue && await db.Projects.AnyAsync(item => item.Id == projectId && item.IsActive &&
                    (item.Partners.Any(link => link.BusinessPartnerId == businessPartnerId && link.IsActive)
                     || item.ConstructionRecords.Any(record => record.CrewBusinessPartnerId == businessPartnerId)), cancellationToken);
            if (!projectMatches)
            {
                throw new InvalidOperationException("项目与当前选择的公司或合作单位没有有效关联。");
            }
        }
    }

    private async Task ValidateIdentityNumberAsync(Guid? personId, string? identityNumber, CancellationToken cancellationToken)
    {
        var normalized = PersonPublicDataSynchronizer.NormalizeIdentityNumber(identityNumber);
        if (normalized is not null && await db.People.AnyAsync(item => item.Id != personId && item.IdentityNumberNormalized == normalized, cancellationToken))
        {
            throw new InvalidOperationException("身份证号已被其他人员档案使用。");
        }
    }

    private static void ValidateScope(PersonnelScope scope, EmployeeType? internalType, ExternalPersonnelType? externalType, Guid? businessPartnerId, Guid? crewBusinessPartnerId)
    {
        if (scope == PersonnelScope.Internal && (!internalType.HasValue || externalType.HasValue))
        {
            throw new InvalidOperationException("内部人员必须选择内部人员类型，且不能保存外部人员类型。");
        }
        if (scope == PersonnelScope.External && (!externalType.HasValue || internalType.HasValue))
        {
            throw new InvalidOperationException("外部人员必须选择外部人员类型，且不能保存内部人员类型。");
        }
        if (scope == PersonnelScope.External && externalType == ExternalPersonnelType.ConstructionCrew &&
            (!businessPartnerId.HasValue || !crewBusinessPartnerId.HasValue || businessPartnerId != crewBusinessPartnerId))
        {
            throw new InvalidOperationException("外部施工班组人员必须选择同一个所属班组和当前班组。");
        }
    }

    private static Employee CreateEmployee(Person person, EmployeeType employeeType, string? positionTitle, Guid? legalEntityId) => new()
    {
        Person = person,
        EmployeeNumber = $"E-{person.Id:N}",
        Name = person.Name,
        EmployeeType = employeeType,
        Phone = person.Phone,
        IdentityNumber = person.IdentityNumber,
        BankAccountNumber = person.BankAccountNumber,
        BankName = person.BankName,
        PositionTitle = Optional(positionTitle),
        DefaultLegalEntityId = legalEntityId,
        Notes = person.Notes,
        IsActive = true
    };

    private static ConstructionWorker CreateConstructionWorker(Person person, string? trade) => new()
    {
        Person = person,
        Name = person.Name,
        IdentityNumber = person.IdentityNumber,
        Phone = person.Phone,
        BankAccountNumber = person.BankAccountNumber,
        BankName = person.BankName,
        Trade = Optional(trade),
        Notes = person.Notes,
        IsActive = true
    };

    private static PersonnelEngagementHistory BuildEngagement(
        Guid personId,
        PersonnelScope scope,
        EmployeeType? internalType,
        ExternalPersonnelType? externalType,
        Guid? legalEntityId,
        Guid? businessPartnerId,
        Guid? organizationUnitId,
        Guid? projectId,
        Guid? crewBusinessPartnerId,
        string? positionTitle,
        DateOnly effectiveDate,
        string? notes,
        string reason) => new()
    {
        PersonId = personId,
        Scope = scope,
        InternalType = internalType,
        ExternalType = externalType,
        LegalEntityId = legalEntityId,
        BusinessPartnerId = businessPartnerId,
        OrganizationUnitId = organizationUnitId,
        ProjectId = projectId,
        CrewBusinessPartnerId = crewBusinessPartnerId,
        PositionTitle = Optional(positionTitle),
        StartDate = effectiveDate,
        IsPrimary = true,
        Notes = Optional(notes),
        Reason = Required(reason, nameof(reason))
    };

    private void SyncEmployeeAffiliation(Employee? employee, PersonnelEngagementHistory engagement)
    {
        if (employee is null) return;
        var current = employee.AffiliationHistory
            .Where(item => item.IsPrimary && item.StartDate <= engagement.StartDate && (item.EndDate is null || item.EndDate >= engagement.StartDate))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();
        if (current is not null && current.StartDate < engagement.StartDate)
        {
            current.EndDate = engagement.StartDate.AddDays(-1);
        }
        var affiliation = new EmployeeAffiliationHistory
        {
            Employee = employee,
            StartDate = engagement.StartDate,
            DepartmentId = engagement.OrganizationUnitId,
            ProjectId = engagement.ProjectId,
            CrewBusinessPartnerId = engagement.CrewBusinessPartnerId,
            LegalEntityId = engagement.LegalEntityId,
            PositionTitle = engagement.PositionTitle,
            IsPrimary = true,
            Notes = engagement.Reason
        };
        employee.AffiliationHistory.Add(affiliation);
        db.EmployeeAffiliationHistories.Add(affiliation);
    }

    private IQueryable<Person> PersonQuery() => db.People.AsNoTracking()
        .Include(item => item.Employee)
        .Include(item => item.ConstructionWorker)
        .Include(item => item.EngagementHistory).ThenInclude(item => item.LegalEntity)
        .Include(item => item.EngagementHistory).ThenInclude(item => item.BusinessPartner)
        .Include(item => item.EngagementHistory).ThenInclude(item => item.OrganizationUnit)
        .Include(item => item.EngagementHistory).ThenInclude(item => item.Project)
        .Include(item => item.EngagementHistory).ThenInclude(item => item.CrewBusinessPartner);

    private static PersonnelDetailsDto ToDetails(Person person, DateOnly asOf, bool canViewSensitiveData)
    {
        var history = person.EngagementHistory.OrderByDescending(item => item.StartDate).Select(ToAffiliation).ToArray();
        var current = history.FirstOrDefault(item => item.IsPrimary && item.StartDate <= asOf && (item.EndDate is null || item.EndDate >= asOf));
        return new PersonnelDetailsDto(
            person.Id,
            person.PersonNumber,
            person.Name,
            person.Phone,
            canViewSensitiveData ? person.IdentityNumber : EmployeeSensitiveDataMasker.MaskIdentityNumber(person.IdentityNumber),
            canViewSensitiveData ? person.BankAccountNumber : EmployeeSensitiveDataMasker.MaskBankAccountNumber(person.BankAccountNumber),
            person.BankName,
            person.Notes,
            person.IsActive,
            person.ConcurrencyStamp,
            person.Employee?.Id,
            person.ConstructionWorker?.Id,
            current,
            history);
    }

    private static PersonnelAffiliationDto ToAffiliation(PersonnelEngagementHistory item) => new(
        item.Id,
        item.Scope,
        item.InternalType,
        item.ExternalType,
        item.LegalEntityId,
        item.LegalEntity?.ShortName,
        item.BusinessPartnerId,
        item.BusinessPartner?.ShortName,
        item.OrganizationUnitId,
        item.OrganizationUnit?.Name,
        item.ProjectId,
        item.Project?.Name,
        item.CrewBusinessPartnerId,
        item.CrewBusinessPartner?.ShortName,
        item.PositionTitle,
        item.StartDate,
        item.EndDate,
        item.IsPrimary,
        item.Notes,
        item.ConcurrencyStamp);

    private static bool Matches(PersonnelDetailsDto item, string term, bool canViewSensitiveData) =>
        item.PersonNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
        || item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (item.Phone?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.CurrentAffiliation?.LegalEntityName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.CurrentAffiliation?.BusinessPartnerName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.CurrentAffiliation?.OrganizationUnitName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.CurrentAffiliation?.ProjectName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.CurrentAffiliation?.CrewBusinessPartnerName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (canViewSensitiveData && (item.IdentityNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));

    private void AddAudit(string userId, string action, Guid personId, string reason, object? before, object after) => db.AuditLogs.Add(new AuditLog
    {
        UserId = userId,
        Action = action,
        EntityType = nameof(Person),
        EntityId = personId.ToString(),
        Reason = Required(reason, nameof(reason)),
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
        AfterJson = JsonSerializer.Serialize(after)
    });

    private static object Snapshot(PersonnelEngagementHistory item) => new
    {
        item.Scope,
        item.InternalType,
        item.ExternalType,
        item.LegalEntityId,
        item.BusinessPartnerId,
        item.OrganizationUnitId,
        item.ProjectId,
        item.CrewBusinessPartnerId,
        item.PositionTitle,
        item.StartDate,
        item.EndDate
    };

    private static object PublicSnapshot(Person person) => new { person.Name, person.Phone, person.IsActive, person.Notes };

    private static string Required(string value, string parameterName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("值不能为空。", parameterName)
        : value.Trim();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
