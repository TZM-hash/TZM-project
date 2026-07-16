using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Projects;

public sealed class ProjectService(ApplicationDbContext db) : IProjectService
{
    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var projectNumber = NormalizeRequired(request.ProjectNumber, nameof(request.ProjectNumber));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        if (await db.Projects.AnyAsync(project => project.ProjectNumber == projectNumber, cancellationToken))
        {
            throw new InvalidOperationException($"项目编号已存在：{projectNumber}");
        }

        await ValidateProjectReferencesAsync(request, cancellationToken);
        var project = new Project
        {
            ProjectNumber = projectNumber,
            Name = name,
            GeneralContractorName = NormalizeOptional(request.GeneralContractorName),
            ResponsibleUserId = request.ResponsibleUserId,
            DepartmentId = request.DepartmentId,
            BranchId = request.BranchId,
            Stage = request.Stage,
            ArchiveStatus = request.ArchiveStatus
        };
        foreach (var legalEntityId in request.LegalEntityIds.Distinct())
        {
            project.LegalEntities.Add(new ProjectLegalEntity
            {
                Project = project,
                LegalEntityId = legalEntityId,
                IsPrimary = project.LegalEntities.Count == 0
            });
        }

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return ToProjectDto(project);
    }

    public async Task<ContractDto> AddContractAsync(CreateContractRequest request, CancellationToken cancellationToken)
    {
        var contractNumber = NormalizeRequired(request.ContractNumber, nameof(request.ContractNumber));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        ContractAllocationValidator.Validate(
            request.AllocationMode,
            request.TotalAmount,
            request.Allocations.Select(item => new ContractAllocationInput(item.Amount, item.Percentage)));

        var project = await db.Projects
            .Include(item => item.LegalEntities)
            .SingleOrDefaultAsync(item => item.Id == request.ProjectId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("项目不存在或已停用。");
        if (await db.Contracts.AnyAsync(
                contract => contract.ProjectId == request.ProjectId && contract.ContractNumber == contractNumber,
                cancellationToken))
        {
            throw new InvalidOperationException($"项目下的合同编号已存在：{contractNumber}");
        }

        var projectLegalEntityIds = project.LegalEntities.Select(item => item.LegalEntityId).ToHashSet();
        if (request.Allocations.Any(item => !projectLegalEntityIds.Contains(item.LegalEntityId)))
        {
            throw new InvalidOperationException("合同分摊公司必须先关联到项目。");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var contract = new Contract
        {
            ProjectId = project.Id,
            ContractNumber = contractNumber,
            Name = name,
            ContractType = request.ContractType,
            AllocationMode = request.AllocationMode,
            CounterpartyName = NormalizeOptional(request.CounterpartyName),
            TotalAmount = request.TotalAmount
        };
        foreach (var allocation in request.Allocations)
        {
            contract.LegalEntityAllocations.Add(new ContractLegalEntityAllocation
            {
                Contract = contract,
                LegalEntityId = allocation.LegalEntityId,
                Amount = allocation.Amount,
                Percentage = allocation.Percentage
            });
        }

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToContractDto(contract);
    }

    public async Task<ContractLineItemDto> AddLineItemAsync(
        CreateContractLineItemRequest request,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequired(request.Code, nameof(request.Code));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var unit = NormalizeRequired(request.Unit, nameof(request.Unit));
        if (request.IsSettlementConfirmed && (!request.SettledQuantity.HasValue || !request.SettledUnitPrice.HasValue))
        {
            throw new ArgumentException("确认结算时必须填写结算工程量和结算单价。", nameof(request));
        }

        if (!await db.Contracts.AnyAsync(contract => contract.Id == request.ContractId && contract.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("合同不存在或已停用。");
        }

        if (await db.ContractLineItems.AnyAsync(
                item => item.ContractId == request.ContractId && item.Code == code,
                cancellationToken))
        {
            throw new InvalidOperationException($"合同下的清单编码已存在：{code}");
        }

        var lineItem = new ContractLineItem
        {
            ContractId = request.ContractId,
            Code = code,
            Name = name,
            Unit = unit,
            EstimatedQuantity = request.EstimatedQuantity,
            EstimatedUnitPrice = request.EstimatedUnitPrice,
            SettledQuantity = request.SettledQuantity,
            SettledUnitPrice = request.SettledUnitPrice,
            IsSettlementConfirmed = request.IsSettlementConfirmed
        };
        db.ContractLineItems.Add(lineItem);
        await db.SaveChangesAsync(cancellationToken);
        return ToLineItemDto(lineItem);
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(
        string? search,
        ProjectStage? stage,
        CancellationToken cancellationToken)
    {
        var query = db.Projects
            .AsNoTracking()
            .Include(project => project.Contracts)
                .ThenInclude(contract => contract.LineItems)
            .Where(project => project.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(project => project.ProjectNumber.Contains(term) || project.Name.Contains(term));
        }

        if (stage.HasValue)
        {
            query = query.Where(project => project.Stage == stage.Value);
        }

        var projects = await query.OrderByDescending(project => project.CreatedAt).ToListAsync(cancellationToken);
        return projects.Select(project => new ProjectListItemDto(ToProjectDto(project), ProjectSummaryService.Calculate(project))).ToArray();
    }

    public async Task<ProjectDetailsDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.LineItems)
            .SingleOrDefaultAsync(item => item.Id == projectId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return null;
        }

        return new ProjectDetailsDto(
            ToProjectDto(project),
            ProjectSummaryService.Calculate(project),
            project.Contracts.Where(contract => contract.IsActive).Select(ToContractDto).ToArray());
    }

    private async Task ValidateProjectReferencesAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (request.ResponsibleUserId is not null && !await db.Users.AnyAsync(user => user.Id == request.ResponsibleUserId && user.IsEnabled, cancellationToken))
        {
            throw new InvalidOperationException("项目负责人不存在或已停用。");
        }

        var organizationIds = new[] { request.DepartmentId, request.BranchId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (organizationIds.Length > 0 && await db.OrganizationUnits.CountAsync(unit => organizationIds.Contains(unit.Id) && unit.IsActive, cancellationToken) != organizationIds.Length)
        {
            throw new InvalidOperationException("项目部门或分支机构不存在或已停用。");
        }

        var legalEntityIds = request.LegalEntityIds.Distinct().ToArray();
        if (legalEntityIds.Length > 0 && await db.LegalEntities.CountAsync(item => legalEntityIds.Contains(item.Id) && item.IsActive, cancellationToken) != legalEntityIds.Length)
        {
            throw new InvalidOperationException("项目关联的签约公司不存在或已停用。");
        }
    }

    private static ProjectDto ToProjectDto(Project project) =>
        new(project.Id, project.ProjectNumber, project.Name, project.GeneralContractorName, project.Stage, project.ArchiveStatus);

    private static ContractDto ToContractDto(Contract contract) =>
        new(
            contract.Id,
            contract.ContractNumber,
            contract.Name,
            contract.ContractType,
            contract.AllocationMode,
            contract.TotalAmount,
            contract.LineItems.OrderBy(item => item.SortOrder).ThenBy(item => item.Code).Select(ToLineItemDto).ToArray());

    private static ContractLineItemDto ToLineItemDto(ContractLineItem item)
    {
        var estimatedAmount = (item.EstimatedQuantity ?? 0m) * (item.EstimatedUnitPrice ?? 0m);
        var settledAmount = item.IsSettlementConfirmed
            ? (item.SettledQuantity ?? 0m) * (item.SettledUnitPrice ?? 0m)
            : 0m;
        return new ContractLineItemDto(
            item.Id,
            item.Code,
            item.Name,
            item.Unit,
            item.EstimatedQuantity,
            item.EstimatedUnitPrice,
            estimatedAmount,
            item.SettledQuantity,
            item.SettledUnitPrice,
            settledAmount,
            item.IsSettlementConfirmed);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
