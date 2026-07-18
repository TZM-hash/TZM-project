using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.TemporaryWorkers;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class DetailsModel(ApplicationDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var employeeId = await db.PersonnelMigrationMaps
            .AsNoTracking()
            .Where(item => item.LegacyTemporaryWorkerId == id)
            .Select(item => (Guid?)item.EmployeeId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!employeeId.HasValue)
        {
            return NotFound($"未找到旧临时人员 {id} 的迁移映射，无法定位合并后的员工档案。");
        }

        return RedirectToPage("/Employees/Details", new { id = employeeId.Value });
    }
}
