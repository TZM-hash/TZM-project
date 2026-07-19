using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ImportMappingTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ExportDataset Dataset { get; set; }
    public ExportTemplateScope Scope { get; set; }
    public string DatasetVersion { get; set; } = "1";
    public string MappingJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
