using EngineeringManager.Application.StageResults;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.StageResults;

public sealed class StageResultService(ApplicationDbContext db) : IStageResultService
{
    public async Task<StageResultDto> CreateAsync(
        CreateStageResultRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(item => item.Id == request.ProjectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("项目不存在或已停用。");
        }

        if (request.ContractId.HasValue && !await db.Contracts.AnyAsync(
                item => item.Id == request.ContractId && item.ProjectId == request.ProjectId && item.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException("合同不存在或不属于所选项目。");
        }

        if (request.SubmittedByUserId is not null && !await db.Users.AnyAsync(
                item => item.Id == request.SubmittedByUserId && item.IsEnabled,
                cancellationToken))
        {
            throw new InvalidOperationException("提交人不存在或已停用。");
        }

        var lineRequests = request.Lines.ToArray();
        if (lineRequests.Select(item => item.ContractLineItemId).Distinct().Count() != lineRequests.Length)
        {
            throw new ArgumentException("同一阶段成果不能重复填写同一清单项。", nameof(request));
        }

        var lineItemIds = lineRequests.Select(item => item.ContractLineItemId).ToArray();
        var lineItems = await db.ContractLineItems
            .Include(item => item.Contract)
            .Where(item => lineItemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (lineItems.Count != lineItemIds.Length || lineItems.Values.Any(item => item.Contract.ProjectId != request.ProjectId))
        {
            throw new InvalidOperationException("阶段成果清单项不存在或不属于所选项目。");
        }

        var previousQuantities = await db.StageResultLines
            .Where(item => lineItemIds.Contains(item.ContractLineItemId) && item.StageResult.Status == StageResultStatus.Recorded)
            .GroupBy(item => item.ContractLineItemId)
            .Select(group => new { ContractLineItemId = group.Key, Quantity = group.Sum(item => item.PeriodQuantity) })
            .ToDictionaryAsync(item => item.ContractLineItemId, item => item.Quantity, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stageResult = new StageResult
        {
            ProjectId = request.ProjectId,
            ContractId = request.ContractId,
            Title = NormalizeRequired(request.Title, nameof(request.Title)),
            ResultType = request.ResultType,
            Status = request.Status,
            ResultDate = request.ResultDate,
            Description = NormalizeOptional(request.Description),
            QualityResult = request.QualityResult,
            SubmittedByUserId = request.SubmittedByUserId,
            SubmittedAt = request.Status == StageResultStatus.Recorded ? DateTimeOffset.UtcNow : null,
            IsOfflineDraft = request.IsOfflineDraft
        };
        foreach (var lineRequest in lineRequests)
        {
            var lineItem = lineItems[lineRequest.ContractLineItemId];
            var targetQuantity = lineItem.IsSettlementConfirmed && lineItem.SettledQuantity.HasValue
                ? lineItem.SettledQuantity.Value
                : lineItem.EstimatedQuantity ?? 0m;
            var previousQuantity = previousQuantities.GetValueOrDefault(lineRequest.ContractLineItemId);
            var quantity = StageQuantityCalculator.Calculate(targetQuantity, previousQuantity, lineRequest.PeriodQuantity);
            stageResult.Lines.Add(new StageResultLine
            {
                StageResult = stageResult,
                ContractLineItemId = lineRequest.ContractLineItemId,
                PeriodQuantity = lineRequest.PeriodQuantity,
                CumulativeQuantity = quantity.CumulativeQuantity,
                RemainingQuantity = quantity.RemainingQuantity,
                CompletionPercentage = quantity.CompletionPercentage,
                ExceedsTarget = quantity.ExceedsTarget,
                Notes = NormalizeOptional(lineRequest.Notes)
            });
        }

        foreach (var attachmentRequest in request.Attachments)
        {
            ValidateStoredName(attachmentRequest.StoredName);
            stageResult.Attachments.Add(new Attachment
            {
                StageResult = stageResult,
                ProjectId = request.ProjectId,
                ContractId = request.ContractId,
                StoredName = attachmentRequest.StoredName,
                OriginalFileName = NormalizeRequired(attachmentRequest.OriginalFileName, nameof(attachmentRequest.OriginalFileName)),
                ContentType = NormalizeRequired(attachmentRequest.ContentType, nameof(attachmentRequest.ContentType)),
                SizeBytes = attachmentRequest.SizeBytes,
                Category = attachmentRequest.Category,
                Description = NormalizeOptional(attachmentRequest.Description),
                UploadedByUserId = request.SubmittedByUserId
            });
        }

        db.StageResults.Add(stageResult);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(stageResult.Id, cancellationToken)
            ?? throw new InvalidOperationException("阶段成果保存后无法读取。");
    }

    public async Task<IReadOnlyList<StageResultDto>> ListByProjectAsync(
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var query = db.StageResults.AsNoTracking()
            .Include(item => item.Lines).ThenInclude(line => line.ContractLineItem)
            .Include(item => item.Attachments)
            .Where(item => item.Status != StageResultStatus.Voided);
        if (projectId.HasValue)
        {
            query = query.Where(item => item.ProjectId == projectId.Value);
        }

        var results = await query.OrderByDescending(item => item.ResultDate).ThenByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return results.Select(ToDto).ToArray();
    }

    public async Task<StageResultDto?> GetAsync(Guid stageResultId, CancellationToken cancellationToken)
    {
        var result = await db.StageResults.AsNoTracking()
            .Include(item => item.Lines).ThenInclude(line => line.ContractLineItem)
            .Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.Id == stageResultId, cancellationToken);
        return result is null ? null : ToDto(result);
    }

    private static StageResultDto ToDto(StageResult result) =>
        new(
            result.Id,
            result.ProjectId,
            result.ContractId,
            result.Title,
            result.ResultType,
            result.Status,
            result.ResultDate,
            result.Description,
            result.QualityResult,
            result.IsOfflineDraft,
            result.Lines.OrderBy(line => line.ContractLineItem.Code).Select(line => new StageResultLineDto(
                line.ContractLineItemId,
                line.ContractLineItem.Code,
                line.ContractLineItem.Name,
                line.ContractLineItem.Unit,
                line.PeriodQuantity,
                line.CumulativeQuantity,
                line.RemainingQuantity,
                line.CompletionPercentage,
                line.ExceedsTarget,
                line.Notes)).ToArray(),
            result.Attachments.Where(item => !item.IsDeleted).Select(item => new StageAttachmentDto(
                item.Id,
                item.OriginalFileName,
                item.ContentType,
                item.SizeBytes,
                item.Category,
                item.Description)).ToArray());

    private static void ValidateStoredName(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName)
            || Path.IsPathRooted(storedName)
            || storedName.Contains("..", StringComparison.Ordinal)
            || storedName.Contains(Path.DirectorySeparatorChar)
            || storedName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("附件存储名必须是不包含路径的安全文件名。", nameof(storedName));
        }
    }

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
