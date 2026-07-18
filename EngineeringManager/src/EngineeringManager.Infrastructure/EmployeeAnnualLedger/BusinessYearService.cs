using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.EmployeeAnnualLedger;

public sealed class BusinessYearService(ApplicationDbContext db) : IBusinessYearService
{
    public async Task<BusinessYearDto> CreateAsync(CreateBusinessYearRequest request, CancellationToken cancellationToken)
    {
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var periods = await db.BusinessYears.AsNoTracking()
            .Select(item => new BusinessYearPeriod(item.StartDate, item.EndDate))
            .ToListAsync(cancellationToken);
        BusinessYearRules.EnsureNoOverlap(request.StartDate, request.EndDate, periods);
        var businessYear = new BusinessYear
        {
            Name = name,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };
        db.BusinessYears.Add(businessYear);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(businessYear);
    }

    public async Task<IReadOnlyList<BusinessYearDto>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await db.BusinessYears.AsNoTracking().OrderByDescending(item => item.StartDate).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToArray();
    }

    public async Task<BusinessYearDto?> GetByDateAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        var item = await db.BusinessYears.AsNoTracking()
            .SingleOrDefaultAsync(year => year.StartDate <= businessDate && year.EndDate >= businessDate, cancellationToken);
        return item is null ? null : ToDto(item);
    }

    private static BusinessYearDto ToDto(BusinessYear item) =>
        new(item.Id, item.Name, item.StartDate, item.EndDate, item.ConcurrencyStamp);

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }
}
