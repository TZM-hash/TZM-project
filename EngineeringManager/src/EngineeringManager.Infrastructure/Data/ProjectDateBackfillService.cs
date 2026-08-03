using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Data;

public sealed record ProjectDateBackfillItem(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    DateOnly? ExistingStartDate,
    DateOnly? ExistingCompletionDate,
    DateOnly? SuggestedStartDate,
    DateOnly? SuggestedCompletionDate,
    DateOnly? AppliedStartDate,
    DateOnly? AppliedCompletionDate,
    IReadOnlyList<string> Warnings)
{
    public int ChangedFieldCount =>
        (ExistingStartDate != AppliedStartDate ? 1 : 0)
        + (ExistingCompletionDate != AppliedCompletionDate ? 1 : 0);
}

public sealed record ProjectDateBackfillResult(IReadOnlyList<ProjectDateBackfillItem> Items)
{
    public int TotalChanges => Items.Sum(item => item.ChangedFieldCount);

    public int ChangedProjects => Items.Count(item => item.ChangedFieldCount > 0);

    public int ProjectsWithWarnings => Items.Count(item => item.Warnings.Count > 0);
}

public sealed class ProjectDateBackfillService(ApplicationDbContext db)
{
    public async Task<ProjectDateBackfillResult> BackfillAsync(CancellationToken cancellationToken)
    {
        var projects = await db.Projects
            .OrderBy(item => item.ProjectNumber)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var items = new List<ProjectDateBackfillItem>(projects.Count);

        foreach (var project in projects)
        {
            var existingStart = project.ActualStartDate;
            var existingCompletion = project.ActualCompletionDate;
            var parsed = ProjectNoteDateParser.Parse(project.Notes);
            var warnings = parsed.Warnings.ToList();
            var suggestedStart = project.ActualStartDate ?? parsed.StartDate;
            var suggestedCompletion = project.ActualCompletionDate ?? parsed.CompletionDate;
            var appliedStart = project.ActualStartDate;
            var appliedCompletion = project.ActualCompletionDate;

            if (parsed.HasUnsafeOrdering)
            {
                warnings.Add("候选开工和完工日期顺序异常，未自动回填，待人工核实。");
            }
            else
            {
                if (!project.ActualStartDate.HasValue && parsed.StartDate.HasValue)
                    appliedStart = parsed.StartDate;
                if (!project.ActualCompletionDate.HasValue && parsed.CompletionDate.HasValue)
                    appliedCompletion = parsed.CompletionDate;

                if (appliedStart.HasValue && appliedCompletion.HasValue && appliedCompletion.Value < appliedStart.Value)
                {
                    if (!project.ActualStartDate.HasValue)
                        appliedStart = null;
                    if (!project.ActualCompletionDate.HasValue)
                        appliedCompletion = null;
                    warnings.Add("候选日期与已有项目日期冲突，未自动回填冲突字段，待人工核实。");
                }
            }

            if (appliedStart != existingStart)
            {
                project.ActualStartDate = appliedStart;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                project.ConcurrencyStamp = Guid.NewGuid();
            }
            if (appliedCompletion != existingCompletion)
            {
                project.ActualCompletionDate = appliedCompletion;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                project.ConcurrencyStamp = Guid.NewGuid();
            }

            items.Add(new ProjectDateBackfillItem(
                project.Id,
                project.ProjectNumber,
                project.Name,
                existingStart,
                existingCompletion,
                suggestedStart,
                suggestedCompletion,
                appliedStart,
                appliedCompletion,
                warnings.Distinct(StringComparer.Ordinal).ToArray()));
        }

        var result = new ProjectDateBackfillResult(items);
        if (result.TotalChanges == 0)
            return result;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = "system",
            UserName = "系统维护",
            Action = "BackfillProjectDatesFromNotes",
            EntityType = nameof(Project),
            EntityId = Guid.Empty.ToString(),
            Reason = "从项目备注中的机器进退场日期回填实际开工和完工日期",
            AfterJson = JsonSerializer.Serialize(result)
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
