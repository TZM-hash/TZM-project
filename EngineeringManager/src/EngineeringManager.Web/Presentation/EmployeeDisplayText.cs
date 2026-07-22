using EngineeringManager.Domain.Employees;

namespace EngineeringManager.Web.Presentation;

public static class EmployeeDisplayText
{
    public static string WageEntryType(EmployeeWageEntryType value) => value switch
    {
        EmployeeWageEntryType.Attendance => "考勤工资",
        EmployeeWageEntryType.Overtime => "加班工资",
        EmployeeWageEntryType.Bonus => "奖金",
        EmployeeWageEntryType.Penalty => "罚款",
        EmployeeWageEntryType.Other => "其他",
        _ => value.ToString()
    };

    public static string DisbursementType(PayrollDisbursementType value) => value switch
    {
        PayrollDisbursementType.Wage => "工资",
        PayrollDisbursementType.Other => "其他",
        _ => value.ToString()
    };

    public static string PaymentCategory(PayrollPaymentCategory value) => value switch
    {
        PayrollPaymentCategory.Wage => "工资",
        PayrollPaymentCategory.Other => "其他",
        _ => value.ToString()
    };

    public static string FundingSource(PayrollFundingSource value) => value switch
    {
        PayrollFundingSource.CompanyAccount => "公司账户",
        PayrollFundingSource.PersonalAdvance => "私人转账",
        _ => value.ToString()
    };

    public static string WageCategory(EmployeeWageCategory value) => value switch
    {
        EmployeeWageCategory.SocialSecurityWage => "社保工资",
        EmployeeWageCategory.MigrantWorkerWage => "民工工资",
        EmployeeWageCategory.OtherWage => "其他工资",
        _ => value.ToString()
    };
}
