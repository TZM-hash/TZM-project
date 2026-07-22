using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Generated migration arrays

namespace EngineeringManager.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class EmployeeManagementWorkspace : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AttachmentId",
            table: "EmployeeOtherPayments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "AttachmentId",
            table: "EmployeeWageEntries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "EntryType",
            table: "EmployeeWageEntries",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "ExcludeFromWageCost",
            table: "EmployeeWageEntries",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsSystemGenerated",
            table: "EmployeeWageEntries",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "SourcePersonalAdvanceBatchId",
            table: "EmployeeWageEntries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "OwnerEmployeeId",
            table: "FinancialAccounts",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OwnerName",
            table: "FinancialAccounts",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DisbursementType",
            table: "PayrollBatches",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "FundingSource",
            table: "PayrollBatches",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<Guid>(
            name: "RepaysPersonalAdvanceAccountId",
            table: "PayrollBatches",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LaborBusinessPartnerId",
            table: "PayrollPayments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PaymentCategory",
            table: "PayrollPayments",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectId",
            table: "PayrollPayments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "WageCategory",
            table: "PayrollPayments",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeOtherPayments_AttachmentId",
            table: "EmployeeOtherPayments",
            column: "AttachmentId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeWageEntries_AttachmentId",
            table: "EmployeeWageEntries",
            column: "AttachmentId");

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeWageEntries_EmployeeId_BusinessYearId_EntryType",
            table: "EmployeeWageEntries",
            columns: new[] { "EmployeeId", "BusinessYearId", "EntryType" });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeWageEntries_SourcePersonalAdvanceBatchId",
            table: "EmployeeWageEntries",
            column: "SourcePersonalAdvanceBatchId");

        migrationBuilder.CreateIndex(
            name: "IX_FinancialAccounts_OwnerEmployeeId",
            table: "FinancialAccounts",
            column: "OwnerEmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollBatches_RepaysPersonalAdvanceAccountId",
            table: "PayrollBatches",
            column: "RepaysPersonalAdvanceAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollPayments_EmployeeId_PaymentCategory",
            table: "PayrollPayments",
            columns: new[] { "EmployeeId", "PaymentCategory" });

        migrationBuilder.CreateIndex(
            name: "IX_PayrollPayments_LaborBusinessPartnerId",
            table: "PayrollPayments",
            column: "LaborBusinessPartnerId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollPayments_ProjectId",
            table: "PayrollPayments",
            column: "ProjectId");

        migrationBuilder.AddForeignKey(
            name: "FK_EmployeeOtherPayments_Attachments_AttachmentId",
            table: "EmployeeOtherPayments",
            column: "AttachmentId",
            principalTable: "Attachments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_EmployeeWageEntries_Attachments_AttachmentId",
            table: "EmployeeWageEntries",
            column: "AttachmentId",
            principalTable: "Attachments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_EmployeeWageEntries_PayrollBatches_SourcePersonalAdvanceBatchId",
            table: "EmployeeWageEntries",
            column: "SourcePersonalAdvanceBatchId",
            principalTable: "PayrollBatches",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FinancialAccounts_Employees_OwnerEmployeeId",
            table: "FinancialAccounts",
            column: "OwnerEmployeeId",
            principalTable: "Employees",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PayrollBatches_FinancialAccounts_RepaysPersonalAdvanceAccountId",
            table: "PayrollBatches",
            column: "RepaysPersonalAdvanceAccountId",
            principalTable: "FinancialAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PayrollPayments_BusinessPartners_LaborBusinessPartnerId",
            table: "PayrollPayments",
            column: "LaborBusinessPartnerId",
            principalTable: "BusinessPartners",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PayrollPayments_Projects_ProjectId",
            table: "PayrollPayments",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_EmployeeOtherPayments_Attachments_AttachmentId", "EmployeeOtherPayments");
        migrationBuilder.DropForeignKey("FK_EmployeeWageEntries_Attachments_AttachmentId", "EmployeeWageEntries");
        migrationBuilder.DropForeignKey("FK_EmployeeWageEntries_PayrollBatches_SourcePersonalAdvanceBatchId", "EmployeeWageEntries");
        migrationBuilder.DropForeignKey("FK_FinancialAccounts_Employees_OwnerEmployeeId", "FinancialAccounts");
        migrationBuilder.DropForeignKey("FK_PayrollBatches_FinancialAccounts_RepaysPersonalAdvanceAccountId", "PayrollBatches");
        migrationBuilder.DropForeignKey("FK_PayrollPayments_BusinessPartners_LaborBusinessPartnerId", "PayrollPayments");
        migrationBuilder.DropForeignKey("FK_PayrollPayments_Projects_ProjectId", "PayrollPayments");

        migrationBuilder.DropIndex("IX_EmployeeOtherPayments_AttachmentId", "EmployeeOtherPayments");
        migrationBuilder.DropIndex("IX_EmployeeWageEntries_AttachmentId", "EmployeeWageEntries");
        migrationBuilder.DropIndex("IX_EmployeeWageEntries_EmployeeId_BusinessYearId_EntryType", "EmployeeWageEntries");
        migrationBuilder.DropIndex("IX_EmployeeWageEntries_SourcePersonalAdvanceBatchId", "EmployeeWageEntries");
        migrationBuilder.DropIndex("IX_FinancialAccounts_OwnerEmployeeId", "FinancialAccounts");
        migrationBuilder.DropIndex("IX_PayrollBatches_RepaysPersonalAdvanceAccountId", "PayrollBatches");
        migrationBuilder.DropIndex("IX_PayrollPayments_EmployeeId_PaymentCategory", "PayrollPayments");
        migrationBuilder.DropIndex("IX_PayrollPayments_LaborBusinessPartnerId", "PayrollPayments");
        migrationBuilder.DropIndex("IX_PayrollPayments_ProjectId", "PayrollPayments");

        migrationBuilder.DropColumn("AttachmentId", "EmployeeOtherPayments");
        migrationBuilder.DropColumn("AttachmentId", "EmployeeWageEntries");
        migrationBuilder.DropColumn("EntryType", "EmployeeWageEntries");
        migrationBuilder.DropColumn("ExcludeFromWageCost", "EmployeeWageEntries");
        migrationBuilder.DropColumn("IsSystemGenerated", "EmployeeWageEntries");
        migrationBuilder.DropColumn("SourcePersonalAdvanceBatchId", "EmployeeWageEntries");
        migrationBuilder.DropColumn("OwnerEmployeeId", "FinancialAccounts");
        migrationBuilder.DropColumn("OwnerName", "FinancialAccounts");
        migrationBuilder.DropColumn("DisbursementType", "PayrollBatches");
        migrationBuilder.DropColumn("FundingSource", "PayrollBatches");
        migrationBuilder.DropColumn("RepaysPersonalAdvanceAccountId", "PayrollBatches");
        migrationBuilder.DropColumn("LaborBusinessPartnerId", "PayrollPayments");
        migrationBuilder.DropColumn("PaymentCategory", "PayrollPayments");
        migrationBuilder.DropColumn("ProjectId", "PayrollPayments");
        migrationBuilder.DropColumn("WageCategory", "PayrollPayments");
    }
}
