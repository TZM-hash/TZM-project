using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class EmployeeWorkspacePersistenceTests
{
    [Fact]
    public void EmployeePayableEntitiesExposeWorkspacePersistenceFields()
    {
        Property<EmployeeWageEntry>("EntryType").PropertyType.Should().Be<EmployeeWageEntryType>();
        Property<EmployeeWageEntry>("AttachmentId").PropertyType.Should().Be<Guid?>();
        Property<EmployeeWageEntry>("SourcePersonalAdvanceBatchId").PropertyType.Should().Be<Guid?>();
        Property<EmployeeWageEntry>("IsSystemGenerated").PropertyType.Should().Be<bool>();
        Property<EmployeeWageEntry>("ExcludeFromWageCost").PropertyType.Should().Be<bool>();
        Property<EmployeeOtherPayment>("AttachmentId").PropertyType.Should().Be<Guid?>();
    }

    [Fact]
    public void PayrollAndAccountEntitiesExposeTraceableDisbursementFields()
    {
        Property<FinancialAccount>("OwnerEmployeeId").PropertyType.Should().Be<Guid?>();
        Property<FinancialAccount>("OwnerName").PropertyType.Should().Be<string>();
        Property<PayrollBatch>("DisbursementType").PropertyType.Should().Be<PayrollDisbursementType>();
        Property<PayrollBatch>("FundingSource").PropertyType.Should().Be<PayrollFundingSource>();
        Property<PayrollBatch>("RepaysPersonalAdvanceAccountId").PropertyType.Should().Be<Guid?>();
        Property<PayrollPayment>("PaymentCategory").PropertyType.Should().Be<PayrollPaymentCategory>();
        Property<PayrollPayment>("WageCategory").PropertyType.Should().Be<EmployeeWageCategory?>();
        Property<PayrollPayment>("LaborBusinessPartnerId").PropertyType.Should().Be<Guid?>();
        Property<PayrollPayment>("ProjectId").PropertyType.Should().Be<Guid?>();
    }

    private static System.Reflection.PropertyInfo Property<T>(string name)
    {
        var property = typeof(T).GetProperty(name);
        property.Should().NotBeNull($"{typeof(T).Name}.{name} is required by the employee workspace");
        return property!;
    }
}
